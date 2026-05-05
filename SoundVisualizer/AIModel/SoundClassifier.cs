using System;
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

        /// <summary>멜 스펙트럼 한 윈도우에 필요한 16kHz 모노 샘플 수(≈0.975s). 콜백 한 번 분량만 넣으면 대부분 0 패딩이라 신뢰도가 붕괴합니다.</summary>
        private static readonly int RequiredMono16kSamples = WindowLength + HopLength * (TimeFrames - 1);

        private const int RingSeconds = 2;

        private readonly object _captureRingLock = new();
        private readonly List<float> _monoAtCaptureRateRing = new();

#if DEBUG
        private static long _lastClassifyDebugTickMs;
        private const int ClassifyDebugMinIntervalMs = 200;
#endif

        // coarse 히스테리시스: 연속 N프레임 동일해야 전환 (값이 클수록 안정·느린 반응)
        private const int CoarseHysteresisThreshold = 6;
        private string _confirmedCoarse = "ambient";
        private string _confirmedDisplay = "";
        private float _confirmedConfidence;
        private string _candidateCoarse = "";
        private int _candidateStreak;

        /// <summary>
        /// WASAPI 루프백이 흔히 48kHz float이므로, 호출부에서 샘플레이트를 넘기지 않을 때의 기본값입니다.
        /// 44.1kHz 환경이면 <see cref="PredictSoundType(byte[], int, int, int)"/> 네 번째 인자로 44100을 넘기세요.
        /// </summary>
        public const int DefaultCaptureSampleRate = 48000;

        // Precomputed preprocessing artifacts
        private readonly float[] _hannWindow;
        private readonly float[,] _melFilterBank; // [MelBins, FftSize/2+1]
        private readonly int[] _bitReversed;       // [FftSize]

        public SoundClassifier()
        {
            // 최소 전처리(로그-멜 생성)에 필요한 파라미터를 항상 초기화
            _hannWindow = CreateHannWindow(WindowLength);
            _melFilterBank = CreateMelFilterBank(MelBins, FftSize, SampleRate, MelFMin, MelFMax);
            _bitReversed = CreateBitReversedIndices(FftSize);

            // 1. AI 뇌(ONNX 모델) 로드
            // (주의: yamnet.onnx 파일이 실행 파일과 같은 폴더에 있어야 합니다!)
            try
            {
                // csproj에서 AIModel 폴더 안의 yamnet.onnx가 그대로 출력 디렉터리의 AIModel 하위 폴더로 복사되도록 설정되었으므로 경로를 수정합니다.
                string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "yamnet.onnx");
                _session = new InferenceSession(modelPath);
                LoadClassNames(); // 실제로는 csv 파일에서 "총소리", "폭발음" 목록을 불러옴
                TryLoadDistilledCoarseHead();

                // 입력명 확인(디버깅용). metadata.yaml이 말하는 input name이 실제로도 같은지 체크합니다.
                foreach (var kv in _session.InputMetadata)
                {
                    Console.WriteLine($"[YAMNet] Input: {kv.Key} | {kv.Value}");
                }
                Console.WriteLine("🧠 AI 모델(YAMNet) 로딩 완료!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 AI 모델 로드 실패: {ex.Message}");
            }
        }

        public string PredictSoundType(byte[] rawAudioData, int bytesRecorded, int channels, int captureSampleRate = DefaultCaptureSampleRate)
        {
            if (_session == null) return "AI 꺼짐";

            if (bytesRecorded <= 0)
                return "오디오 대기… | ambient | —";

            // 2. 전처리 (Pre-processing): AI는 다채널을 못 먹습니다. 
            // 전방향(모든 채널) 소리를 모노로 다운믹스하여 모든 방향의 소리를 인식할 수 있게 수정
            float[] monoAudio = DownmixToMono(rawAudioData, bytesRecorded, channels);

            const float threshold = 0.25f;
            float[]? tail = null;
            int tailRate = captureSampleRate;
            int ringCount;

            lock (_captureRingLock)
            {
                AppendMonoCaptureRing(monoAudio, captureSampleRate);
                ringCount = _monoAtCaptureRateRing.Count;
                int resampleFrom = captureSampleRate == DefaultCaptureSampleRate ? 48000 : captureSampleRate;
                int n = CaptureSamplesForOneYamnetWindow(resampleFrom);
                tail = CopyRingTailRightPadded(n);
                tailRate = resampleFrom;
            }

            int minRingSamples = CaptureSamplesForOneYamnetWindow(tailRate);
            if (ringCount < minRingSamples)
                return "오디오 축적 중… | ambient | —";

            InferenceResult r = PredictFromMono16k(
                ResampleMonoFloatTo16k(tail ?? Array.Empty<float>(), tailRate), threshold);

            if (r.YamnetClassIndex < 0)
                return "AI 에러";

#if DEBUG
            LogClassificationDebugThrottled(in r, threshold);
#endif

            ApplyCoarseHysteresis(in r);

            string translatedName = YamnetThreeClassMapper.TranslateToKorean(_confirmedDisplay);

            if (_confirmedConfidence < threshold)
                return $"{translatedName} | {_confirmedCoarse} | {_confirmedConfidence * 100f:F1}% (저신뢰)";

            return $"{translatedName} | {_confirmedCoarse} | {_confirmedConfidence * 100f:F1}%";
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

            if (newCoarse == _candidateCoarse)
            {
                _candidateStreak++;
            }
            else
            {
                _candidateCoarse = newCoarse;
                _candidateStreak = 1;
            }

            if (_candidateStreak >= CoarseHysteresisThreshold)
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

        private void AppendMonoCaptureRing(float[] monoChunk, int captureSampleRate)
        {
            if (monoChunk.Length == 0)
                return;
            if (captureSampleRate <= 0)
                captureSampleRate = DefaultCaptureSampleRate;

            for (int i = 0; i < monoChunk.Length; i++)
                _monoAtCaptureRateRing.Add(monoChunk[i]);

            int cap = Math.Max(DefaultCaptureSampleRate * RingSeconds, captureSampleRate * RingSeconds);
            while (_monoAtCaptureRateRing.Count > cap)
                _monoAtCaptureRateRing.RemoveAt(0);
        }

        /// <summary>맨 끝(가장 최근) <paramref name="length"/>샘플. 부족하면 앞을 0으로 패딩.</summary>
        private float[] CopyRingTailRightPadded(int length)
        {
            var buf = new float[length];
            int n = _monoAtCaptureRateRing.Count;
            int take = Math.Min(length, n);
            if (take <= 0)
                return buf;
            int dst = length - take;
            for (int i = 0; i < take; i++)
                buf[dst + i] = _monoAtCaptureRateRing[n - take + i];
            return buf;
        }

        // InferLoopbackPickBestFromTails 제거됨 — 48k 단일 리샘플만 사용

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
                Console.WriteLine($"🚨 [YAMNet] Inference failed: {ex}");
                return new InferenceResult(-1, "AI 에러", 0f, "ambient", false, 0);
            }
            finally
            {
                sw.Stop();
            }

            inferMs = sw.Elapsed.TotalMilliseconds;

            float[] probs = Softmax(logits);
            int maxIndex = Array.IndexOf(probs, probs.Max());
            float conf = probs[maxIndex];
            string display = _classNames[maxIndex];

            PreferDangerWhenTopIsGenericSoundEffect(probs, ref maxIndex, ref conf, ref display);

            string coarse = VoteCoarseFromTopK(probs, 3);
            float coarseConf = conf;
            if (TryPredictCoarseFromDistilledHead(probs, out string headCoarse, out float headConf))
            {
                // 헤드가 비정상적으로 포화(1.0 근처 고정)되면 기존 규칙 기반으로 폴백
                // 정상 범위에서만 헤드 결과를 채택하여 붕괴 리스크를 줄입니다.
                const float HeadAdoptMin = 0.35f;
                const float HeadAdoptMax = 0.99f;
                if (headConf >= HeadAdoptMin && headConf <= HeadAdoptMax)
                {
                    coarse = headCoarse;
                    coarseConf = headConf;
                }
            }

            bool ok = coarseConf >= confidenceThreshold;
            string? topK = FormatTopKSoftmaxLabels(probs, 3);

            return new InferenceResult(maxIndex, display, coarseConf, coarse, ok, inferMs, topK);
        }

        private void TryLoadDistilledCoarseHead()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "three_class_score_head.onnx");
                if (!File.Exists(path))
                {
                    Console.WriteLine($"[3ClassHead] 파일 없음, 기존 규칙 경로 사용: {path}");
                    return;
                }

                _coarseHeadSession = new InferenceSession(path);
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

        private bool TryPredictCoarseFromDistilledHead(float[] yamnetProbs, out string coarse, out float confidence)
        {
            coarse = "ambient";
            confidence = 0f;
            if (_coarseHeadSession == null || string.IsNullOrEmpty(_coarseHeadInputName))
                return false;
            if (yamnetProbs.Length < 521)
                return false;

            try
            {
                var input = new DenseTensor<float>(new[] { 1, 521 });
                for (int i = 0; i < 521; i++)
                    input[0, i] = yamnetProbs[i];

                var inputs = new[] { NamedOnnxValue.CreateFromTensor(_coarseHeadInputName, input) };
                using var results = _coarseHeadSession.Run(inputs);
                var outTensor = string.IsNullOrEmpty(_coarseHeadOutputName)
                    ? results.First().AsEnumerable<float>().ToArray()
                    : results.First(r => r.Name == _coarseHeadOutputName).AsEnumerable<float>().ToArray();
                if (outTensor.Length < 3)
                    return false;

                int maxIdx = 0;
                float maxVal = outTensor[0];
                for (int i = 1; i < 3; i++)
                {
                    if (outTensor[i] > maxVal)
                    {
                        maxVal = outTensor[i];
                        maxIdx = i;
                    }
                }

                coarse = maxIdx == 0 ? "danger" : maxIdx == 1 ? "speech" : "ambient";
                confidence = maxVal;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// top-k 클래스의 확률을 coarse별로 합산하여 가장 높은 coarse를 반환합니다.
        /// 예: top-3가 Plop 32% > Siren 30% > Alarm 25%이면 danger(55%) > ambient(32%) → danger.
        /// </summary>
        private string VoteCoarseFromTopK(float[] probs, int k)
        {
            int n = Math.Min(probs.Length, _classNames.Length);
            if (n == 0) return "ambient";

            var topIndices = Enumerable.Range(0, n).OrderByDescending(i => probs[i]).Take(k);

            float dangerSum = 0f, speechSum = 0f, ambientSum = 0f;
            foreach (int i in topIndices)
            {
                string c = YamnetThreeClassMapper.MapDisplayNameToCoarse(_classNames[i]);
                float p = probs[i];
                if (c == "danger") dangerSum += p;
                else if (c == "speech") speechSum += p;
                else ambientSum += p;
            }

            if (dangerSum >= speechSum && dangerSum >= ambientSum) return "danger";
            if (speechSum >= ambientSum) return "speech";
            return "ambient";
        }

        private string FormatTopKSoftmaxLabels(float[] probs, int k)
        {
            if (probs.Length == 0 || k <= 0 || _classNames.Length != probs.Length)
                return string.Empty;

            int n = Math.Min(probs.Length, _classNames.Length);
            var order = Enumerable.Range(0, n).OrderByDescending(i => probs[i]).Take(k);
            return string.Join(" > ", order.Select(i => $"{_classNames[i]} {probs[i] * 100f:F1}%"));
        }

        /// <summary>
        /// top-1이 “Sound effect”일 때, 총·폭발 등 danger 세부 클래스가 상위 softmax에 있으면 그쪽을 채택합니다.
        /// (게임/영상 총소리가 Gunshot 대신 Sound effect로만 나오는 현상 완화)
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

        /// <summary>log-mel 텐서 통계(FFT/멜 실험 시 비교용). Visual Studio 출력 창에서 확인.</summary>
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

        private static float[] Softmax(float[] logits)
        {
            if (logits.Length == 0)
                return Array.Empty<float>();

            float max = logits.Max();
            var exp = new float[logits.Length];
            double sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                exp[i] = MathF.Exp(logits[i] - max);
                sum += exp[i];
            }

            if (sum <= 0 || double.IsNaN(sum))
            {
                float inv = 1f / logits.Length;
                for (int i = 0; i < logits.Length; i++)
                    exp[i] = inv;
                return exp;
            }

            for (int i = 0; i < logits.Length; i++)
                exp[i] = (float)(exp[i] / sum);

            return exp;
        }

        /// <summary>
        /// UI 방향 표시는 다채널 그대로 두고, 분류용 모노만 냅니다.
        /// 7.1에서 유튜브·스테레오 믹스는 FL/FR에만 실리고 FC가 거의 0인 경우가 많아, FC만 쓰면 침묵처럼 보입니다.
        /// 샘플마다 FC와 (FL+FR)/2의 제곱 에너지 비율로 블렌딩합니다.
        /// 채널 순서: FL,FR,FC,LFE,SL,SR,BL,BR.
        /// </summary>
        private static float[] DownmixToMono(byte[] rawAudioData, int bytesRecorded, int channels)
        {
            int floatCount = bytesRecorded / 4;
            if (channels <= 0 || floatCount < channels)
                return Array.Empty<float>();

            int frames = floatCount / channels;
            float[] monoAudio = new float[frames];

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
        /// 루프백 등 실시간 모노 float을 YAMNet이 기대하는 16kHz로 맞춥니다. (WAV 테스트 경로는 <see cref="WavAudioLoader"/>와 동일하게 WDL 리샘플)
        /// </summary>
        private static float[] ResampleMonoFloatTo16k(float[] mono, int sourceSampleRate)
        {
            if (mono.Length == 0)
                return Array.Empty<float>();

            if (sourceSampleRate <= 0)
                sourceSampleRate = DefaultCaptureSampleRate;

            if (sourceSampleRate == SampleRate)
                return mono;

            ISampleProvider provider = new FloatArraySampleProvider(mono, sourceSampleRate);
            provider = new WdlResamplingSampleProvider(provider, SampleRate);

            var chunk = new float[4096];
            var list = new List<float>(mono.Length * SampleRate / sourceSampleRate + 64);
            int read;
            while ((read = provider.Read(chunk, 0, chunk.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                    list.Add(chunk[i]);
            }

            return list.ToArray();
        }

        private sealed class FloatArraySampleProvider : ISampleProvider
        {
            private readonly float[] _data;
            private int _position;

            public FloatArraySampleProvider(float[] data, int sampleRate)
            {
                _data = data;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int available = _data.Length - _position;
                int toRead = Math.Min(count, available);
                for (int i = 0; i < toRead; i++)
                    buffer[offset + i] = _data[_position++];
                return toRead;
            }
        }

        /// <summary>
        /// log-mel을 [time, mel] 순으로 평탄화: 인덱스 time * MelBins + mel → DenseTensor [1,1,TimeFrames,MelBins].
        /// </summary>
        private float[] ComputeLogMelSpectrogram(float[] monoAudio)
        {
            int requiredSamples = RequiredMono16kSamples;

            // 고정 길이로 패딩/트렁케이션(실시간 입력 길이가 매번 달라질 수 있으므로)
            float[] audio = new float[requiredSamples];
            int copyLen = Math.Min(monoAudio.Length, requiredSamples);
            if (copyLen > 0)
                Array.Copy(monoAudio, 0, audio, 0, copyLen);

            int freqBins = (FftSize / 2) + 1;
            float[] power = new float[freqBins];
            float[] logMel = new float[TimeFrames * MelBins];

            // FFT 버퍼(복사 최소화 목적)
            Complex[] fftBuffer = new Complex[FftSize];

            for (int t = 0; t < TimeFrames; t++)
            {
                int start = t * HopLength;

                // 1) windowing + zero-padding -> complex buffer 준비
                for (int i = 0; i < FftSize; i++)
                {
                    float sample = (i < WindowLength) ? audio[start + i] * _hannWindow[i] : 0f;
                    fftBuffer[i] = new Complex(sample, 0f);
                }

                // 2) FFT
                FFTInPlace(fftBuffer, _bitReversed);

                // 3) power spectrum (magnitude^2)
                for (int k = 0; k < freqBins; k++)
                {
                    var c = fftBuffer[k];
                    power[k] = (float)(c.Real * c.Real + c.Imaginary * c.Imaginary);
                }

                // 4) mel filterbank -> log
                for (int mel = 0; mel < MelBins; mel++)
                {
                    double melSum = 0.0;
                    for (int k = 0; k < freqBins; k++)
                    {
                        float w = _melFilterBank[mel, k];
                        if (w != 0f) melSum += w * power[k];
                    }

                    float value = (float)Math.Log(melSum + LogEps);
                    logMel[t * MelBins + mel] = value;
                }
            }

            return logMel;
        }

        private static float[] CreateHannWindow(int length)
        {
            var window = new float[length];
            if (length <= 1) return window;
            for (int n = 0; n < length; n++)
            {
                // Periodic Hann window: 0.5 - 0.5*cos(2*pi*n/N)
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

            // Bit-reversal permutation
            for (int i = 0; i < n; i++)
            {
                int j = bitReversed[i];
                if (j > i)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            // Iterative Cooley-Tukey radix-2 FFT
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

            // melBins + 2 points (edges)
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

            // Triangular mel filters
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
                Console.WriteLine($"⚠️ yamnet_class_map.csv 없음: {path}");
                return;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // yamnet_class_map.csv 형식 차이(2열/3열)를 모두 수용:
                // - 2열: index,display_name
                // - 3열: index,mid,display_name
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
            lock (_captureRingLock)
                _monoAtCaptureRateRing.Clear();
            _session?.Dispose();
            _coarseHeadSession?.Dispose();
        }
    }
}