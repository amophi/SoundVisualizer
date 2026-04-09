using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SoundVisualizer.AIModel
{
    public class SoundClassifier : IDisposable
    {
        private InferenceSession _session;
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
        private const float LogEps = 1e-6f;

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

        public string PredictSoundType(byte[] rawAudioData, int bytesRecorded, int channels)
        {
            if (_session == null) return "AI 꺼짐";

            // 2. 전처리 (Pre-processing): AI는 다채널을 못 먹습니다. 
            // 센터 채널을 뽑아오도록 수정
            float[] monoAudio = ExtractCenterChannel(rawAudioData, bytesRecorded, channels);

            InferenceResult r = PredictFromMono16k(monoAudio, 0.3f);
            if (r.YamnetClassIndex < 0)
                return "AI 에러";
            if (r.Confidence < 0.3f)
                return "배경음";

            return $"{r.YamnetDisplayName} ({r.Confidence * 100f:F1}%)";
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
            LogLogMelTensorStats(logMel);

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
            string coarse = YamnetThreeClassMapper.MapDisplayNameToCoarse(display);
            bool ok = conf >= confidenceThreshold;

            return new InferenceResult(maxIndex, display, conf, coarse, ok, inferMs);
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

        private float[] ExtractCenterChannel(byte[] rawAudioData, int bytesRecorded, int channels)
        {
            int floatCount = bytesRecorded / 4;
            // 지정된 채널 수에 따라 분할
            float[] centerChannel = new float[floatCount / channels];

            int centerIndex = 0;
            for (int i = 0; i < floatCount - (channels - 1); i += channels)
            {
                // 채널 2번이 센터 채널이라고 가정합니다. 최소 2개의 채널이 있어야 합니다.
                int cIndex = channels > 2 ? 2 : 0;
                centerChannel[centerIndex++] = BitConverter.ToSingle(rawAudioData, (i + cIndex) * 4);
            }
            return centerChannel;
        }

        /// <summary>
        /// log-mel을 [time, mel] 순으로 평탄화: 인덱스 time * MelBins + mel → DenseTensor [1,1,TimeFrames,MelBins].
        /// </summary>
        private float[] ComputeLogMelSpectrogram(float[] monoAudio)
        {
            int requiredSamples = WindowLength + HopLength * (TimeFrames - 1);

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
                // Hann window: 0.5 - 0.5*cos(2*pi*n/(N-1))
                window[n] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * n / (length - 1)));
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
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("index,", StringComparison.OrdinalIgnoreCase))
                    continue;

                int c1 = line.IndexOf(',');
                if (c1 < 0) continue;
                int c2 = line.IndexOf(',', c1 + 1);
                if (c2 < 0) continue;

                if (!int.TryParse(line.AsSpan(0, c1), out int index) || index < 0 || index >= 521)
                    continue;

                string name = line.Substring(c2 + 1).Trim();
                if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
                    name = name.Substring(1, name.Length - 2).Replace("\"\"", "\"", StringComparison.Ordinal);

                _classNames[index] = name;
            }
        }

        // 메모리 누수 방지용
        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}