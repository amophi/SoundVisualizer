using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundVisualizer.AIModel
{
    public class SoundClassifier : IDisposable
    {
        private InferenceSession _session;
        private InferenceSession? _coarseHeadSession;
        private string? _coarseHeadInputName;
        private string? _coarseHeadOutputName;
        private InferenceSession? _gunshotBoosterSession;
        private string? _gunshotBoosterInputName;
        private string? _gunshotBoosterOutputName;
        private string[] _classNames; // YAMNet의 521개 소리 이름 목록

        // YAMNet: 16kHz mono → log-mel, 일반적으로 time × mel = 96 × 64
        // ONNX 입력 shape [1, 1, 96, 64] = [batch, ch, time_frames, mel_bins]
        private const int SampleRate = 16000;
        private const int MelBins = 64;
        private const int TimeFrames = 96;
        private const int WindowLength = 400; // 25ms @ 16kHz
        private const int HopLength = 160;    // 10ms  @ 16kHz
        private const int FftSize = 512;       // TF YAMNet 계열과 맞추기 위한 실험 (>= WindowLength, 2의 거듭제곱)
        private const float MelFMin = 125f;
        private const float MelFMax = 7500f;
        private const float LogEps = 0.001f;

        /// <summary>멜 스펙트럼 한 윈도우에 필요한 16kHz 모노 샘플 수(≈0.975s).</summary>
        private static readonly int RequiredMono16kSamples = WindowLength + HopLength * (TimeFrames - 1);

        private const int RingSeconds = 2;

        private readonly object _inferenceLock = new();
        private bool _isDisposed;

        private readonly object _captureRingLock = new();
        
        // 고정 크기 순환 링 버퍼 필드
        private float[]? _ringBuffer;
        private int _ringHead;
        private int _ringTail;
        private int _ringCount;
        private const int MaxRingSize = 131072; // 2의 거듭제곱 (비트마스킹 & 131071 가속)

        // GC Free 전처리 및 추론용 캐시 버퍼
        private float[]? _preprocessedAudio; // 크기: RequiredMono16kSamples
        private Complex[]? _fftComplexBuffer; // 크기: FftSize (512)
        private float[]? _fftPowerBuffer; // 크기: FftSize / 2 + 1 (257)
        private float[]? _logMelSpectrogramBuffer; // 크기: TimeFrames * MelBins (6144)
        private float[]? _softmaxBuffer; // 크기: 521
        private float[]? _captureTailBuffer; // 16kHz 리샘플 이전 원시 오디오 복사용 임시 버퍼

        // Top-K 계산용 캐시 버퍼 (가비지 프리)
        private int[]? _topKIndices; // 크기: 5
        private float[]? _topKProbs; // 크기: 5

        private string _lastPredictResult = "오디오 대기… | ambient | —";

#if DEBUG
        private static long _lastClassifyDebugTickMs;
        private const int ClassifyDebugMinIntervalMs = 200;
#endif

        // coarse 히스테리시스: 연속 N프레임 동일해야 전환
        private const int CoarseHysteresisThreshold = 2;
        private const int DangerHysteresisThreshold = 1;
        private const float DangerImmediateSwitchConfidence = 0.28f;
        private string _confirmedCoarse = "ambient";
        private string _confirmedDisplay = "";
        private float _confirmedConfidence;
        private string _candidateCoarse = "";
        private int _candidateStreak;

        /// <summary>
        /// WASAPI 루프백이 흔히 48kHz float이므로, 호출부에서 샘플레이트를 넘기지 않을 때의 기본값입니다.
        /// </summary>
        public const int DefaultCaptureSampleRate = 48000;

        // Precomputed preprocessing artifacts
        private readonly float[] _hannWindow;
        private readonly float[,] _melFilterBank; // [MelBins, FftSize/2+1]
        private readonly int[] _bitReversed;       // [FftSize]

        public SoundClassifier()
        {
            _hannWindow = CreateHannWindow(WindowLength);
            _melFilterBank = CreateMelFilterBank(MelBins, FftSize, SampleRate, MelFMin, MelFMax);
            _bitReversed = CreateBitReversedIndices(FftSize);

            // 링 버퍼 및 사전 캐싱 버퍼들 할당
            _ringBuffer = new float[MaxRingSize];
            _preprocessedAudio = new float[RequiredMono16kSamples];
            _fftComplexBuffer = new Complex[FftSize];
            _fftPowerBuffer = new float[(FftSize / 2) + 1];
            _logMelSpectrogramBuffer = new float[TimeFrames * MelBins];
            _softmaxBuffer = new float[521];
            _topKIndices = new int[5];
            _topKProbs = new float[5];

            // 1. AI 뇌(ONNX 모델) 로드 (SessionOptions 세팅으로 스레드 1개 제한)
            try
            {
                using var options = new SessionOptions();
                options.IntraOpNumThreads = 1;
                options.InterOpNumThreads = 1;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

                string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "yamnet.onnx");
                _session = new InferenceSession(modelPath, options);
                LoadClassNames();
                TryLoadGunshotBooster(options);

                foreach (var kv in _session.InputMetadata)
                {
                    Console.WriteLine($"[YAMNet] Input: {kv.Key} | {kv.Value}");
                }
                Console.WriteLine("AI 모델(YAMNet) 로딩 완료 (스레딩 최적화 적용)!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI 모델 로드 실패: {ex.Message}");
            }
        }

        public string PredictSoundType(byte[] rawAudioData, int bytesRecorded, int channels, int captureSampleRate = DefaultCaptureSampleRate)
        {
            IngestAudio(rawAudioData, bytesRecorded, channels, captureSampleRate);
            return PredictSoundType(captureSampleRate);
        }

        /// <summary>
        /// 실시간으로 캡처되는 원시 오디오 데이터를 다운믹싱하여 링 버퍼에 적재하기만 합니다.
        /// </summary>
        public void IngestAudio(byte[] rawAudioData, int bytesRecorded, int channels, int captureSampleRate = DefaultCaptureSampleRate)
        {
            if (_isDisposed || bytesRecorded <= 0) return;

            float[] monoAudio = DownmixToMono(rawAudioData, bytesRecorded, channels, out int actualFrames);
            try
            {
                lock (_captureRingLock)
                {
                    AppendMonoCaptureRing(monoAudio, actualFrames, captureSampleRate);
                }
            }
            finally
            {
                if (monoAudio != Array.Empty<float>())
                {
                    ArrayPool<float>.Shared.Return(monoAudio);
                }
            }
        }

        /// <summary>
        /// 링 버퍼에 누적된 최신 오디오 데이터를 기반으로 YAMNet ONNX 추론을 수행하고 결과를 캐시합니다.
        /// </summary>
        public string PredictSoundType(int captureSampleRate = DefaultCaptureSampleRate)
        {
            lock (_inferenceLock)
            {
                if (_isDisposed || _session == null)
                {
                    _lastPredictResult = "AI 꺼짐";
                    return _lastPredictResult;
                }

                const float threshold = 0.25f;
                int resampleFrom = captureSampleRate == DefaultCaptureSampleRate ? 48000 : captureSampleRate;
                int n = CaptureSamplesForOneYamnetWindow(resampleFrom);
                int ringCount;

                lock (_captureRingLock)
                {
                    ringCount = _ringCount;
                    if (_captureTailBuffer == null || _captureTailBuffer.Length != n)
                    {
                        _captureTailBuffer = new float[n];
                    }
                    CopyRingTailRightPadded(_captureTailBuffer, n);
                }

                int minRingSamples = CaptureSamplesForOneYamnetWindow(resampleFrom);
                if (ringCount < minRingSamples)
                {
                    _lastPredictResult = "오디오 축적 중… | ambient | —";
                    return _lastPredictResult;
                }

                if (_preprocessedAudio == null)
                {
                    _preprocessedAudio = new float[RequiredMono16kSamples];
                }
                ResampleMonoFloatTo16kCustom(_captureTailBuffer, n, resampleFrom, _preprocessedAudio);

                InferenceResult r = PredictFromMono16k(_preprocessedAudio, threshold);

                if (r.YamnetClassIndex < 0)
                {
                    _lastPredictResult = "AI 에러";
                    return _lastPredictResult;
                }

#if DEBUG
                LogClassificationDebugThrottled(in r, threshold);
#endif

                ApplyCoarseHysteresis(in r);

                string translatedName = YamnetThreeClassMapper.TranslateToKorean(_confirmedDisplay);
                string resultText;

                if (_confirmedConfidence < threshold)
                    resultText = $"{translatedName} | {_confirmedCoarse} | {_confirmedConfidence * 100f:F1}% (저신뢰)";
                else
                    resultText = $"{translatedName} | {_confirmedCoarse} | {_confirmedConfidence * 100f:F1}%";

                _lastPredictResult = resultText;
                return resultText;
            }
        }

        /// <summary>
        /// 마지막으로 갱신된 YAMNet 예측 결과를 락 경합 없이 O(1)로 빠르게 반환합니다.
        /// </summary>
        public string GetLastPredictResult()
        {
            return _lastPredictResult;
        }

        private void ApplyCoarseHysteresis(in InferenceResult r)
        {
            // 저신뢰 프레임은 확정 상태·후보 스트릭을 바꾸지 않음(오탐/깜빡임 억제)
            if (!r.MeetsThreshold)
                return;

            string newCoarse = r.CoarseClass;

            if (newCoarse == _confirmedCoarse)
            {
                _candidateStreak = 0;
                _candidateCoarse = "";
                _confirmedDisplay = r.YamnetDisplayName;
                _confirmedConfidence = r.Confidence;
                return;
            }

            // 총성/폭발처럼 짧은 transient는 즉시 반영해 미검출을 줄입니다.
            if (newCoarse == "danger" &&
                (r.Confidence >= DangerImmediateSwitchConfidence || IsCriticalDangerEvent(in r)))
            {
                _confirmedCoarse = newCoarse;
                _confirmedDisplay = r.YamnetDisplayName;
                _confirmedConfidence = r.Confidence;
                _candidateStreak = 0;
                _candidateCoarse = "";
                return;
            }

            if (newCoarse == _candidateCoarse)
            {
                _candidateStreak++;
            }
            else
            {
                _candidateCoarse = newCoarse;
                _candidateStreak = 1;
            }

            int requiredStreak = newCoarse == "danger" ? DangerHysteresisThreshold : CoarseHysteresisThreshold;
            if (_candidateStreak >= requiredStreak)
            {
                _confirmedCoarse = newCoarse;
                _confirmedDisplay = r.YamnetDisplayName;
                _confirmedConfidence = r.Confidence;
                _candidateStreak = 0;
                _candidateCoarse = "";
            }
        }

#if DEBUG
        /// <summary>출력 창에서 UI보다 읽기 쉽게 분류 결과를 확인하기 위한 로그(스로틀).</summary>
        private static void LogClassificationDebugThrottled(in InferenceResult r, float threshold)
        {
            long now = Environment.TickCount64;
            if (now - _lastClassifyDebugTickMs < ClassifyDebugMinIntervalMs)
                return;
            _lastClassifyDebugTickMs = now;

            string topK = string.IsNullOrEmpty(r.TopKSummary) ? "" : $" | top3: {r.TopKSummary}";
            string lo = r.MeetsThreshold ? "OK" : "저신뢰";
            Debug.WriteLine(
                $"[YAMNet 분류] {r.YamnetDisplayName} | coarse={r.CoarseClass} | p={r.Confidence * 100f:F1}% (임계 {threshold * 100f:F0}% {lo}) | {r.InferenceTimeMs:F1}ms{topK}");
        }
#endif

        private static int CaptureSamplesForOneYamnetWindow(int captureSampleRate) =>
            (int)Math.Ceiling(RequiredMono16kSamples * (double)captureSampleRate / SampleRate);

        private void AppendMonoCaptureRing(float[] monoChunk, int length, int captureSampleRate)
        {
            if (length <= 0)
                return;
            if (captureSampleRate <= 0)
                captureSampleRate = DefaultCaptureSampleRate;

            int cap = Math.Max(DefaultCaptureSampleRate * RingSeconds, captureSampleRate * RingSeconds);
            if (_ringBuffer == null)
            {
                _ringBuffer = new float[MaxRingSize];
            }

            for (int i = 0; i < length; i++)
            {
                _ringBuffer[_ringTail] = monoChunk[i];
                _ringTail = (_ringTail + 1) & (MaxRingSize - 1);

                if (_ringCount < MaxRingSize)
                {
                    _ringCount++;
                }
                else
                {
                    _ringHead = (_ringHead + 1) & (MaxRingSize - 1);
                }
            }

            while (_ringCount > cap)
            {
                _ringHead = (_ringHead + 1) & (MaxRingSize - 1);
                _ringCount--;
            }
        }

        private void CopyRingTailRightPadded(float[] destination, int count)
        {
            if (destination == null || count <= 0)
                return;

            int take = Math.Min(_ringCount, count);
            int dstStart = count - take;

            // 앞부분(왼쪽)을 0f로 패딩 (오른쪽 정렬을 위함)
            for (int i = 0; i < dstStart; i++)
            {
                destination[i] = 0f;
            }

            if (take > 0)
            {
                int ringStart = (_ringTail - take) & (MaxRingSize - 1);
                for (int i = 0; i < take; i++)
                {
                    destination[dstStart + i] = _ringBuffer[(ringStart + i) & (MaxRingSize - 1)];
                }
            }
        }

        /// <summary>
        /// 모노 PCM(float)을 16kHz로 맞춘 뒤 전처리·추론합니다. WAV 파일 테스트 등에 사용합니다.
        /// </summary>
        /// <param name="confidenceThreshold">상위 클래스 확률이 이 값 미만이면 MeetsThreshold=false.</param>
        public InferenceResult PredictFromMono16k(float[] monoAudio, float confidenceThreshold)
        {
            if (_session == null)
                return new InferenceResult(-1, "AI 꺼짐", 0f, "ambient", false, 0);

            float[] logMel = ComputeLogMelSpectrogram(monoAudio);

            // [1,1,96,64] = time(96) × mel(64), row-major에서 mel이 마지막 축
            var inputTensor = new DenseTensor<float>(logMel, new[] { 1, 1, TimeFrames, MelBins });
            var inputs = new[] { NamedOnnxValue.CreateFromTensor("audio", inputTensor) };

            float[] logits;
            double inferMs;
            var sw = Stopwatch.StartNew();
            try
            {
                using var results = _session.Run(inputs);
                logits = results.First().AsEnumerable<float>().ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YAMNet] Inference failed: {ex}");
                return new InferenceResult(-1, "AI 에러", 0f, "ambient", false, 0);
            }
            finally
            {
                sw.Stop();
            }

            inferMs = sw.Elapsed.TotalMilliseconds;

            float[] probs = Softmax(logits);
            
            if (_topKIndices == null) _topKIndices = new int[5];
            if (_topKProbs == null) _topKProbs = new float[5];
            ComputeTop5(probs, _topKIndices, _topKProbs);

            int maxIndex = _topKIndices[0];
            float conf = _topKProbs[0];
            string display = _classNames[maxIndex];

            PreferDangerWhenTopIsGenericSoundEffect(probs, ref maxIndex, ref conf, ref display);

            string coarse = VoteCoarseFromTop5(_topKIndices, _topKProbs, 3);
            float coarseConf = conf;
            float dangerEvidence = SumCoarseProbabilityFromTop5(_topKIndices, _topKProbs, 5, "danger");
            bool hasStrongDangerCue = HasStrongDangerCueInTop5(_topKIndices, 5);
            bool hasCriticalDangerCue = HasCriticalDangerCueInTop5(_topKIndices, 5);

            if (TryPredictGunshotBoosterScore(probs, out float gunshotScore))
            {
                float gunshotEvidence = SumGunshotProbabilityFromTop5(_topKIndices, _topKProbs, 5);
                bool hasGunshotCue = HasGunshotCueInTop5(_topKIndices, 5);

                bool adoptGunshotDanger = false;
                if (hasGunshotCue)
                {
                    adoptGunshotDanger = gunshotScore >= 0.20f && gunshotEvidence >= 0.05f;
                }
                else
                {
                    adoptGunshotDanger = gunshotScore >= 0.45f && gunshotEvidence >= 0.10f;
                }

                if (adoptGunshotDanger)
                {
                    coarse = "danger";
                    coarseConf = MathF.Max(coarseConf, MathF.Max(gunshotScore, gunshotEvidence));
                }
            }

            // A 모드: 3클래스 헤드(danger 재판정) 경로 비활성

            // danger는 강한 단서(top-k)에서 임계를 낮춰 미검출을 줄이고,
            // speech/ambient는 기본 임계를 그대로 유지합니다.
            float effectiveThreshold = confidenceThreshold;
            if (coarse == "danger" && (hasStrongDangerCue || hasCriticalDangerCue))
            {
                effectiveThreshold = MathF.Min(effectiveThreshold, 0.20f);
            }
            else if (coarse == "speech")
            {
                // speech 오탐을 줄이기 위해 최소 임계를 상향합니다.
                effectiveThreshold = MathF.Max(effectiveThreshold, 0.25f);
            }

            bool ok = coarseConf >= effectiveThreshold;
            string? topK = FormatTop5SoftmaxLabels(_topKIndices, _topKProbs, 3);

            return new InferenceResult(maxIndex, display, coarseConf, coarse, ok, inferMs, topK);
        }

        private void TryLoadDistilledCoarseHead(SessionOptions options)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "three_class_score_head.onnx");
                if (!File.Exists(path))
                {
                    Console.WriteLine($"[3ClassHead] 파일 없음, 기존 규칙 경로 사용: {path}");
                    return;
                }

                _coarseHeadSession = new InferenceSession(path, options);
                _coarseHeadInputName = _coarseHeadSession.InputMetadata.Keys.FirstOrDefault();
                _coarseHeadOutputName = _coarseHeadSession.OutputMetadata.Keys.FirstOrDefault();
                Console.WriteLine($"[3ClassHead] 로드 완료: {path}");
            }
            catch (Exception ex)
            {
                _coarseHeadSession = null;
                _coarseHeadInputName = null;
                _coarseHeadOutputName = null;
                Console.WriteLine($"[3ClassHead] 로드 실패, 기존 규칙 경로 사용: {ex.Message}");
            }
        }

        private void TryLoadGunshotBooster(SessionOptions options)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "gunshot_booster.onnx");
                if (!File.Exists(path))
                {
                    Console.WriteLine($"[GunshotBooster] 파일 없음, 부스터 비활성: {path}");
                    return;
                }

                _gunshotBoosterSession = new InferenceSession(path, options);
                _gunshotBoosterInputName = _gunshotBoosterSession.InputMetadata.Keys.FirstOrDefault();
                _gunshotBoosterOutputName = _gunshotBoosterSession.OutputMetadata.Keys.FirstOrDefault();
                Console.WriteLine($"[GunshotBooster] 로드 완료: {path}");
            }
            catch (Exception ex)
            {
                _gunshotBoosterSession = null;
                _gunshotBoosterInputName = null;
                _gunshotBoosterOutputName = null;
                Console.WriteLine($"[GunshotBooster] 로드 실패, 부스터 비활성: {ex.Message}");
            }
        }

        private float[]? _boosterInputTensorBuffer;
        private DenseTensor<float>? _boosterInputTensor;

        private bool TryPredictGunshotBoosterScore(float[] yamnetProbs, out float gunshotScore)
        {
            gunshotScore = 0f;
            if (_gunshotBoosterSession == null || string.IsNullOrEmpty(_gunshotBoosterInputName))
                return false;
            if (yamnetProbs.Length < 521)
                return false;

            try
            {
                if (_boosterInputTensorBuffer == null)
                {
                    _boosterInputTensorBuffer = new float[521];
                    _boosterInputTensor = new DenseTensor<float>(_boosterInputTensorBuffer, new[] { 1, 521 });
                }

                for (int i = 0; i < 521; i++)
                    _boosterInputTensorBuffer[i] = yamnetProbs[i];

                var inputs = new[] { NamedOnnxValue.CreateFromTensor(_gunshotBoosterInputName, _boosterInputTensor) };
                using var results = _gunshotBoosterSession.Run(inputs);
                var outTensor = string.IsNullOrEmpty(_gunshotBoosterOutputName)
                    ? results.First().AsTensor<float>()
                    : results.First(r => r.Name == _gunshotBoosterOutputName).AsTensor<float>();
                if (outTensor.Length < 1)
                    return false;

                gunshotScore = outTensor.GetValue(0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private float[]? _coarseInputTensorBuffer;
        private DenseTensor<float>? _coarseInputTensor;

        private bool TryPredictDangerScoreFromDistilledHead(float[] yamnetProbs, out float dangerScore)
        {
            dangerScore = 0f;
            if (_coarseHeadSession == null || string.IsNullOrEmpty(_coarseHeadInputName))
                return false;
            if (yamnetProbs.Length < 521)
                return false;

            try
            {
                if (_coarseInputTensorBuffer == null)
                {
                    _coarseInputTensorBuffer = new float[521];
                    _coarseInputTensor = new DenseTensor<float>(_coarseInputTensorBuffer, new[] { 1, 521 });
                }

                for (int i = 0; i < 521; i++)
                    _coarseInputTensorBuffer[i] = yamnetProbs[i];

                var inputs = new[] { NamedOnnxValue.CreateFromTensor(_coarseHeadInputName, _coarseInputTensor) };
                using var results = _coarseHeadSession.Run(inputs);
                var outTensor = string.IsNullOrEmpty(_coarseHeadOutputName)
                    ? results.First().AsTensor<float>()
                    : results.First(r => r.Name == _coarseHeadOutputName).AsTensor<float>();
                if (outTensor.Length < 3)
                    return false;

                dangerScore = outTensor.GetValue(0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Top-5 데이터를 한 번의 패스로 계산하는 최적화 메서드 (0-allocation)
        private void ComputeTop5(float[] probs, int[] outIndices, float[] outProbs)
        {
            for (int i = 0; i < 5; i++)
            {
                outIndices[i] = -1;
                outProbs[i] = -1f;
            }

            int n = Math.Min(probs.Length, _classNames.Length);
            for (int i = 0; i < n; i++)
            {
                float p = probs[i];
                for (int j = 0; j < 5; j++)
                {
                    if (p > outProbs[j])
                    {
                        for (int k = 4; k > j; k--)
                        {
                            outProbs[k] = outProbs[k - 1];
                            outIndices[k] = outIndices[k - 1];
                        }
                        outProbs[j] = p;
                        outIndices[j] = i;
                        break;
                    }
                }
            }
        }

        private string VoteCoarseFromTop5(int[] topIndices, float[] topProbs, int k)
        {
            float dangerSum = 0f, speechSum = 0f, ambientSum = 0f;
            int limit = Math.Min(5, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                string c = YamnetThreeClassMapper.MapDisplayNameToCoarse(_classNames[i]);
                float p = topProbs[idx];
                if (c == "danger") dangerSum += p;
                else if (c == "speech") speechSum += p;
                else ambientSum += p;
            }

            if (dangerSum >= speechSum && dangerSum >= ambientSum) return "danger";
            if (speechSum >= ambientSum) return "speech";
            return "ambient";
        }

        private float SumCoarseProbabilityFromTop5(int[] topIndices, float[] topProbs, int k, string targetCoarse)
        {
            float sum = 0f;
            int limit = Math.Min(5, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                if (YamnetThreeClassMapper.MapDisplayNameToCoarse(_classNames[i]) == targetCoarse)
                    sum += topProbs[idx];
            }
            return sum;
        }

        private float SumGunshotProbabilityFromTop5(int[] topIndices, float[] topProbs, int k)
        {
            float sum = 0f;
            int limit = Math.Min(5, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                string name = _classNames[i];
                if (IsGunshotKeyword(name))
                {
                    sum += topProbs[idx];
                }
            }
            return sum;
        }

        private bool HasGunshotCueInTop5(int[] topIndices, int k)
        {
            int limit = Math.Min(5, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                string name = _classNames[i];
                if (IsGunshotKeyword(name))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasStrongDangerCueInTop5(int[] topIndices, int k)
        {
            int limit = Math.Min(5, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                string name = _classNames[i];
                if (IsStrongDangerKeyword(name))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasCriticalDangerCueInTop5(int[] topIndices, int k)
        {
            int limit = Math.Min(5, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                string name = _classNames[i];
                if (IsCriticalDangerKeyword(name))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsGunshotKeyword(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("gunshot", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("gunfire", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("machine gun", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("artillery", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("fusillade", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("cap gun", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStrongDangerKeyword(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return IsGunshotKeyword(name) ||
                   name.Contains("explosion", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("fireworks", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("firecracker", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("siren", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("alarm", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCriticalDangerKeyword(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return IsGunshotKeyword(name) ||
                   name.Contains("explosion", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("fireworks", StringComparison.OrdinalIgnoreCase) || 
                   name.Contains("firecracker", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCriticalDangerEvent(in InferenceResult r)
        {
            string display = r.YamnetDisplayName ?? "";
            string top = r.TopKSummary ?? "";
            return IsCriticalDangerKeyword(display) || IsCriticalDangerKeyword(top);
        }

        private string FormatTop5SoftmaxLabels(int[] topIndices, float[] topProbs, int k)
        {
            int limit = Math.Min(5, k);
            var sb = new System.Text.StringBuilder();
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                if (sb.Length > 0) sb.Append(" > ");
                sb.Append(_classNames[i]).Append(' ').Append((topProbs[idx] * 100f).ToString("F1")).Append('%');
            }
            return sb.ToString();
        }

        /// <summary>
        /// top-1이 “Sound effect”일 때, 총·폭발 등 danger 세부 클래스가 상위 softmax에 있으면 그쪽을 채택합니다.
        /// </summary>
        private void PreferDangerWhenTopIsGenericSoundEffect(float[] probs, ref int maxIndex, ref float conf, ref string display)
        {
            if (maxIndex < 0 || maxIndex >= _classNames.Length || probs.Length != _classNames.Length)
                return;
            if (!YamnetThreeClassMapper.IsGenericSoundEffectLabel(display))
                return;

            int bestDangerIdx = -1;
            float bestDangerProb = 0f;
            int n = Math.Min(probs.Length, _classNames.Length);
            for (int i = 0; i < n; i++)
            {
                if (YamnetThreeClassMapper.MapDisplayNameToCoarse(_classNames[i]) != "danger")
                    continue;
                if (probs[i] > bestDangerProb)
                {
                    bestDangerProb = probs[i];
                    bestDangerIdx = i;
                }
            }

            if (bestDangerIdx < 0)
                return;

            float topProb = probs[maxIndex];
            float bar = Math.Max(0.06f, topProb * 0.22f);
            if (bestDangerProb < bar)
                return;

            maxIndex = bestDangerIdx;
            conf = bestDangerProb;
            display = _classNames[bestDangerIdx];
        }

        /// <summary>log-mel 텐서 통계(FFT/멜 실험 시 비교용).</summary>
        private static void LogLogMelTensorStats(float[] logMel)
        {
            if (logMel.Length == 0) return;

            float min = logMel[0], max = logMel[0];
            double sum = 0;
            foreach (var v in logMel)
            {
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }

            float mean = (float)(sum / logMel.Length);
            double varAcc = 0;
            foreach (var v in logMel)
            {
                double d = v - mean;
                varAcc += d * d;
            }

            float std = (float)Math.Sqrt(varAcc / logMel.Length);
            if (std < 1e-4f)
                return;

            Debug.WriteLine(
                $"[YAMNet logMel] shape=[1,1,{TimeFrames},{MelBins}] (time×mel) n={logMel.Length} min={min:F6} max={max:F6} mean={mean:F6} std={std:F6}");
        }

        private float[] Softmax(float[] logits)
        {
            if (logits.Length == 0)
                return Array.Empty<float>();

            if (_softmaxBuffer == null) _softmaxBuffer = new float[logits.Length];

            float max = logits.Max();
            double sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                _softmaxBuffer[i] = MathF.Exp(logits[i] - max);
                sum += _softmaxBuffer[i];
            }

            if (sum <= 0 || double.IsNaN(sum))
            {
                float inv = 1f / logits.Length;
                for (int i = 0; i < logits.Length; i++)
                    _softmaxBuffer[i] = inv;
                return _softmaxBuffer;
            }

            for (int i = 0; i < logits.Length; i++)
                _softmaxBuffer[i] = (float)(_softmaxBuffer[i] / sum);

            return _softmaxBuffer;
        }

        /// <summary>
        /// rawAudioData(멀티채널 float)를 모노 float으로 다운믹스합니다.
        /// 반환된 배열은 ArrayPool에서 빌린 것이므로 사용 후 반드시 ArrayPool<float>.Shared.Return으로 반환해야 합니다.
        /// </summary>
        private static float[] DownmixToMono(byte[] rawAudioData, int bytesRecorded, int channels, out int actualFrames)
        {
            int floatCount = bytesRecorded / 4;
            if (channels <= 0 || floatCount < channels)
            {
                actualFrames = 0;
                return Array.Empty<float>();
            }

            int frames = floatCount / channels;
            actualFrames = frames;
            float[] monoAudio = ArrayPool<float>.Shared.Rent(frames);

            if (channels == 8)
            {
                int bytesPerFrame = channels * 4;
                const float eps = 1e-12f;

                for (int f = 0; f < frames; f++)
                {
                    int o = f * bytesPerFrame;
                    float fl = BitConverter.ToSingle(rawAudioData, o + 0);
                    float fr = BitConverter.ToSingle(rawAudioData, o + 4);
                    float fc = BitConverter.ToSingle(rawAudioData, o + 8);
                    float lr = 0.5f * (fl + fr);
                    float eFc = fc * fc;
                    float eLr = lr * lr;
                    float w = eFc / (eFc + eLr + eps);
                    monoAudio[f] = w * fc + (1f - w) * lr;
                }

                return monoAudio;
            }

            int frameIndex = 0;
            for (int i = 0; i < floatCount - (channels - 1); i += channels)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += BitConverter.ToSingle(rawAudioData, (i + c) * 4);
                monoAudio[frameIndex++] = sum / channels;
            }

            return monoAudio;
        }

        /// <summary>
        /// 고속 0-allocation 선형 보간 리샘플러
        /// </summary>
        private void ResampleMonoFloatTo16kCustom(float[] source, int sourceLength, int sourceSampleRate, float[] destination)
        {
            int destLength = destination.Length;
            if (sourceSampleRate == SampleRate)
            {
                int copyLen = Math.Min(sourceLength, destLength);
                Array.Copy(source, 0, destination, 0, copyLen);
                if (copyLen < destLength)
                {
                    Array.Clear(destination, copyLen, destLength - copyLen);
                }
                return;
            }

            double factor = (double)sourceSampleRate / SampleRate;
            for (int i = 0; i < destLength; i++)
            {
                double srcPos = i * factor;
                int index1 = (int)srcPos;
                int index2 = index1 + 1;
                float alpha = (float)(srcPos - index1);

                if (index1 >= sourceLength)
                {
                    destination[i] = 0f;
                }
                else
                {
                    float val1 = source[index1];
                    float val2 = (index2 < sourceLength) ? source[index2] : val1;
                    destination[i] = (1f - alpha) * val1 + alpha * val2;
                }
            }
        }

        /// <summary>
        /// log-mel을 [time, mel] 순으로 평탄화: 인덱스 time * MelBins + mel → DenseTensor [1,1,TimeFrames,MelBins].
        /// </summary>
        private float[] ComputeLogMelSpectrogram(float[] monoAudio)
        {
            int requiredSamples = RequiredMono16kSamples;

            if (_preprocessedAudio == null) _preprocessedAudio = new float[requiredSamples];
            int copyLen = Math.Min(monoAudio.Length, requiredSamples);
            if (copyLen > 0)
                Array.Copy(monoAudio, 0, _preprocessedAudio, 0, copyLen);
            if (copyLen < requiredSamples)
                Array.Clear(_preprocessedAudio, copyLen, requiredSamples - copyLen);

            int freqBins = (FftSize / 2) + 1;
            if (_fftPowerBuffer == null) _fftPowerBuffer = new float[freqBins];
            if (_logMelSpectrogramBuffer == null) _logMelSpectrogramBuffer = new float[TimeFrames * MelBins];
            if (_fftComplexBuffer == null) _fftComplexBuffer = new Complex[FftSize];

            for (int t = 0; t < TimeFrames; t++)
            {
                int start = t * HopLength;

                for (int i = 0; i < FftSize; i++)
                {
                    float sample = (i < WindowLength) ? _preprocessedAudio[start + i] * _hannWindow[i] : 0f;
                    _fftComplexBuffer[i] = new Complex(sample, 0f);
                }

                FFTInPlace(_fftComplexBuffer, _bitReversed);

                for (int k = 0; k < freqBins; k++)
                {
                    var c = _fftComplexBuffer[k];
                    _fftPowerBuffer[k] = (float)(c.Real * c.Real + c.Imaginary * c.Imaginary);
                }

                for (int mel = 0; mel < MelBins; mel++)
                {
                    double melSum = 0.0;
                    for (int k = 0; k < freqBins; k++)
                    {
                        float w = _melFilterBank[mel, k];
                        if (w != 0f) melSum += w * _fftPowerBuffer[k];
                    }

                    float value = (float)Math.Log(melSum + LogEps);
                    _logMelSpectrogramBuffer[t * MelBins + mel] = value;
                }
            }

            return _logMelSpectrogramBuffer;
        }

        private static float[] CreateHannWindow(int length)
        {
            var window = new float[length];
            if (length <= 1) return window;
            for (int n = 0; n < length; n++)
            {
                window[n] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * n / length));
            }
            return window;
        }

        private static int[] CreateBitReversedIndices(int n)
        {
            int bits = (int)Math.Log2(n);
            var arr = new int[n];
            for (int i = 0; i < n; i++)
            {
                int v = i;
                int r = 0;
                for (int b = 0; b < bits; b++)
                {
                    r = (r << 1) | (v & 1);
                    v >>= 1;
                }
                arr[i] = r;
            }
            return arr;
        }

        private static void FFTInPlace(Complex[] buffer, int[] bitReversed)
        {
            int n = buffer.Length;

            for (int i = 0; i < n; i++)
            {
                int j = bitReversed[i];
                if (j > i)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                Complex wLen = new Complex(Math.Cos(ang), Math.Sin(ang));

                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    int half = len >> 1;
                    for (int j = 0; j < half; j++)
                    {
                        Complex u = buffer[i + j];
                        Complex v = buffer[i + j + half] * w;
                        buffer[i + j] = u + v;
                        buffer[i + j + half] = u - v;
                        w *= wLen;
                    }
                }
            }
        }

        private static float[,] CreateMelFilterBank(int melBins, int fftSize, int sampleRate, float fMin, float fMax)
        {
            int freqBins = (fftSize / 2) + 1;
            var weights = new float[melBins, freqBins];

            float hzToMel(float hz) => 2595f * (float)Math.Log10(1f + hz / 700f);
            float melToHz(float mel) => 700f * (float)(Math.Pow(10d, mel / 2595f) - 1d);

            float melMin = hzToMel(fMin);
            float melMax = hzToMel(fMax);

            float[] melPoints = new float[melBins + 2];
            for (int i = 0; i < melPoints.Length; i++)
            {
                melPoints[i] = melMin + (melMax - melMin) * i / (melBins + 1);
            }

            int[] bin = new int[melBins + 2];
            for (int i = 0; i < melPoints.Length; i++)
            {
                float hz = melToHz(melPoints[i]);
                int b = (int)Math.Floor((fftSize + 1) * hz / sampleRate);
                b = Math.Max(0, Math.Min(freqBins - 1, b));
                bin[i] = b;
            }

            for (int m = 0; m < melBins; m++)
            {
                int f0 = bin[m];
                int f1 = bin[m + 1];
                int f2 = bin[m + 2];

                if (f1 <= f0 || f2 <= f1) continue;

                for (int k = f0; k < f1; k++)
                {
                    weights[m, k] = (float)(k - f0) / (f1 - f0);
                }
                for (int k = f1; k < f2; k++)
                {
                    weights[m, k] = (float)(f2 - k) / (f2 - f1);
                }
            }

            return weights;
        }

        private void LoadClassNames()
        {
            _classNames = new string[521];
            for (int i = 0; i < 521; i++)
                _classNames[i] = $"class_{i}";

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "yamnet_class_map.csv");
            if (!File.Exists(path))
            {
                Console.WriteLine($"yamnet_class_map.csv 없음: {path}");
                return;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',', 3);
                if (parts.Length < 2)
                    continue;

                string indexToken = parts[0].Trim().Trim('"');
                if (!int.TryParse(indexToken, out int index) || index < 0 || index >= 521)
                    continue;

                string name = (parts.Length == 2 ? parts[1] : parts[2]).Trim();
                if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
                    name = name.Substring(1, name.Length - 2).Replace("\"\"", "\"", StringComparison.Ordinal);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                _classNames[index] = name;
            }
        }

        // 메모리 누수 방지용
        public void Dispose()
        {
            lock (_inferenceLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                lock (_captureRingLock)
                {
                    _ringBuffer = null;
                    _ringHead = 0;
                    _ringTail = 0;
                    _ringCount = 0;
                }

                _session?.Dispose();
                _coarseHeadSession?.Dispose();
                _gunshotBoosterSession?.Dispose();
            }
        }
    }
}
