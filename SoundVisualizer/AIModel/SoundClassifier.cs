using System;
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

        // YAMNet ONNX 입력 규격(metadata.yaml 기준)
        // input: [1, 1, 96, 64]
        private const int SampleRate = 16000;
        private const int MelBins = 96;
        private const int Frames = 64;
        private const int WindowLength = 400; // 25ms @ 16kHz
        private const int HopLength = 160;    // 10ms  @ 16kHz
        private const int FftSize = 1024;      // FFT size (>= WindowLength)
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

        // 🚀 가람 님이 분석할 때, 성진 님은 이 함수를 동시에 실행합니다.
        public string PredictSoundType(byte[] rawAudioData, int bytesRecorded)
        {
            if (_session == null) return "AI 꺼짐";

            // 2. 전처리 (Pre-processing): AI는 8채널을 못 먹습니다. 
            // 가장 대사와 효과음이 선명한 '센터 채널(Index 2)'만 쏙 빼서 1채널(Mono)로 만듭니다.
            float[] monoAudio = ExtractCenterChannel(rawAudioData, bytesRecorded);

            // 3. YAMNet 입력 텐서 [1, 1, 96, 64] 만들기
            // 최소 버전: mono audio -> (윈도우/호핑/FFT) -> log-mel spectrogram -> 고정 shape로 패딩/트렁케이션
            float[] logMel = ComputeLogMelSpectrogram(monoAudio);
            var inputTensor = new DenseTensor<float>(logMel, new[] { 1, 1, MelBins, Frames });
            var inputs = new[] { NamedOnnxValue.CreateFromTensor("audio", inputTensor) };

            // 4. 추론 직전 검증/로깅 (YAMNet 기대 입력과 일치 확인)
            try
            {
                var dims = inputTensor.Dimensions;
                string dimsStr = string.Join(", ", dims.Select(d => d.ToString()));
                Console.WriteLine($"[YAMNet] inputTensor shape=[{dimsStr}] rank={inputTensor.Rank}");

                bool hasAudioInput = _session.InputMetadata.Keys.Contains("audio");
                Console.WriteLine($"[YAMNet] model has input named 'audio': {hasAudioInput}");

                // 기대 shape: [1,1,96,64]
                bool shapeOk = dims.Length == 4 && dims[0] == 1 && dims[1] == 1 && dims[2] == MelBins && dims[3] == Frames;
                if (!shapeOk)
                    Console.WriteLine($"[YAMNet] WARNING: expected [1,1,{MelBins},{Frames}] but got [{dimsStr}]");
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"[YAMNet] input logging failed: {logEx.Message}");
            }

            // 5. 추론 (Inference) 시작!
            try
            {
                using var results = _session.Run(inputs);

                // 6. 결과 분석: 521개의 소리 중 가장 확률이 높은(Max) 인덱스 찾기
                var output = results.First().AsEnumerable<float>().ToArray();
                int maxIndex = Array.IndexOf(output, output.Max());
                float probability = output[maxIndex] * 100;

                // 너무 확신이 없는(확률이 낮은) 소리는 무시
                if (probability < 30.0f) return "배경음";

                return $"{_classNames[maxIndex]} ({probability:F1}%)";
            }
            catch (Exception ex)
            {
                // shape/runtime 오류가 나면 여기로 떨어집니다(검증 목적).
                Console.WriteLine($"🚨 [YAMNet] Inference failed: {ex}");
                return "AI 에러";
            }
        }

        private float[] ExtractCenterChannel(byte[] rawAudioData, int bytesRecorded)
        {
            int floatCount = bytesRecorded / 4;
            // 8채널 중 센터(1가닥)만 뽑으니까 길이는 1/8
            float[] centerChannel = new float[floatCount / 8];

            int centerIndex = 0;
            for (int i = 0; i < floatCount - 7; i += 8)
            {
                // 인덱스 2번이 센터(Center) 스피커 채널입니다.
                // byte 배열을 float로 변환했다고 가정하고 값을 빼옵니다.
                centerChannel[centerIndex++] = BitConverter.ToSingle(rawAudioData, (i + 2) * 4);
            }
            return centerChannel;
        }

        private float[] ComputeLogMelSpectrogram(float[] monoAudio)
        {
            int requiredSamples = WindowLength + HopLength * (Frames - 1);

            // 고정 길이로 패딩/트렁케이션(실시간 입력 길이가 매번 달라질 수 있으므로)
            float[] audio = new float[requiredSamples];
            int copyLen = Math.Min(monoAudio.Length, requiredSamples);
            if (copyLen > 0)
                Array.Copy(monoAudio, 0, audio, 0, copyLen);

            int freqBins = (FftSize / 2) + 1;
            float[] power = new float[freqBins];
            float[] logMel = new float[MelBins * Frames]; // [melBin, frame] (frame이 마지막 차원)

            // FFT 버퍼(복사 최소화 목적)
            Complex[] fftBuffer = new Complex[FftSize];

            for (int frame = 0; frame < Frames; frame++)
            {
                int start = frame * HopLength;

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
                    logMel[mel * Frames + frame] = value; // mel major, frame minor
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
            // 프로토타입용 가짜 데이터 (실제로는 YAMNet class map CSV를 읽어야 함)
            _classNames = new string[521];
            for (int i = 0; i < 521; i++) _classNames[i] = "알 수 없는 소리";
            _classNames[426] = "💥 폭발음";
            _classNames[427] = "🔫 총소리";
            _classNames[322] = "👣 발소리";
        }

        // 메모리 누수 방지용
        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}