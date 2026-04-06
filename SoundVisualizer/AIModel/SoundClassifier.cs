using System;
using System.Linq;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SoundVisualizer.AIModel
{
    public class SoundClassifier : IDisposable
    {
        private InferenceSession? _session;
        private string[]? _classNames;
        private float[][]? _melFilterbank;

        // YAMNet 전처리 파라미터 (metadata.yaml 기준: [1, 1, 96, 64])
        private const int TargetSampleRate = 16000;
        private const int WindowSize = 400;    // 25ms @ 16kHz
        private const int HopSize = 160;       // 10ms @ 16kHz
        private const int MelBands = 64;       // 멜 밴드 수
        private const int PatchFrames = 96;    // 한 패치당 프레임 수
        private const int FftSize = 512;       // FFT 윈도우 크기 (2의 거듭제곱)

        public SoundClassifier()
        {
            try
            {
                string modelPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "AIModel", "yamnet.onnx");
                _session = new InferenceSession(modelPath);
                LoadClassNames();
                BuildMelFilterbank();

                // 디버그 로그
                foreach (var input in _session.InputMetadata)
                    Console.WriteLine($"  📥 입력: {input.Key} -> [{string.Join(", ", input.Value.Dimensions)}]");
                foreach (var output in _session.OutputMetadata)
                    Console.WriteLine($"  📤 출력: {output.Key} -> [{string.Join(", ", output.Value.Dimensions)}]");

                Console.WriteLine("🧠 AI 모델(YAMNet) 로딩 완료!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 AI 모델 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 오디오 데이터에서 소리 종류를 추론합니다.
        /// raw 7.1 PCM → 센터채널 추출 → 다운샘플 → 멜 스펙트로그램 → ONNX 추론
        /// </summary>
        public string PredictSoundType(byte[] rawAudioData, int bytesRecorded, int channelCount = 8)
        {
            if (_session == null || _classNames == null || _melFilterbank == null)
                return "AI 비활성";

            try
            {
                // 1. 모노 오디오 추출 (7.1이면 센터, 스테레오면 L+R 믹스)
                float[] monoAudio = ExtractMonoChannel(rawAudioData, bytesRecorded, channelCount);
                if (monoAudio.Length < WindowSize) return "데이터 부족";

                // 2. 다운샘플링 (48kHz → 16kHz)
                float[] resampled = Downsample(monoAudio, 48000, TargetSampleRate);
                if (resampled.Length < WindowSize) return "데이터 부족";

                // 3. 멜 스펙트로그램 계산 → [totalFrames, 64]
                float[,] melSpec = ComputeMelSpectrogram(resampled);
                int totalFrames = melSpec.GetLength(0);

                // 프레임이 96개 미만이면 zero-padding
                if (totalFrames < PatchFrames)
                {
                    var padded = new float[PatchFrames, MelBands];
                    for (int f = 0; f < totalFrames; f++)
                        for (int m = 0; m < MelBands; m++)
                            padded[f, m] = melSpec[f, m];
                    melSpec = padded;
                }

                // 4. [1, 1, 96, 64] 텐서 생성 (첫 번째 패치만 사용)
                var inputData = new float[1 * 1 * PatchFrames * MelBands];
                int idx = 0;
                for (int f = 0; f < PatchFrames; f++)
                    for (int m = 0; m < MelBands; m++)
                        inputData[idx++] = melSpec[f, m];

                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 1, PatchFrames, MelBands });

                string inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "audio";
                var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

                // 5. 추론 실행
                using var results = _session.Run(inputs);
                var output = results.First().AsEnumerable<float>().ToArray();
                int maxIndex = Array.IndexOf(output, output.Max());
                float probability = output[maxIndex] * 100;

                if (probability < 30.0f) return "배경음";
                if (maxIndex >= _classNames.Length) return $"알 수 없음 ({probability:F1}%)";
                return $"{_classNames[maxIndex]} ({probability:F1}%)";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ AI 추론 오류: {ex.Message}");
                return "AI 오류";
            }
        }

        // =============================================
        //  전처리 파이프라인 구현
        // =============================================

        /// <summary>
        /// 다채널 PCM에서 AI 추론에 사용할 단일(모노) 채널을 추출합니다.
        /// 7.1채널이면 센터(2번)를, 스테레오면 L+R을 섞어서 반환합니다.
        /// </summary>
        private float[] ExtractMonoChannel(byte[] rawAudioData, int bytesRecorded, int channelCount)
        {
            int floatCount = bytesRecorded / 4;
            int samplesPerChannel = floatCount / channelCount;
            float[] monoAudio = new float[samplesPerChannel];

            for (int i = 0; i < samplesPerChannel; i++)
            {
                if (channelCount >= 8)
                {
                    // 7.1채널: 센터(인덱스 2) 추출
                    int byteOffset = (i * channelCount + 2) * 4;
                    if (byteOffset + 4 <= bytesRecorded)
                        monoAudio[i] = BitConverter.ToSingle(rawAudioData, byteOffset);
                }
                else if (channelCount == 2)
                {
                    // 스테레오: L(0) + R(1) 믹운드
                    int offsetL = (i * channelCount + 0) * 4;
                    int offsetR = (i * channelCount + 1) * 4;
                    if (offsetR + 4 <= bytesRecorded)
                    {
                        float L = BitConverter.ToSingle(rawAudioData, offsetL);
                        float R = BitConverter.ToSingle(rawAudioData, offsetR);
                        monoAudio[i] = (L + R) / 2.0f;
                    }
                }
            }
            return monoAudio;
        }

        /// <summary>
        /// 단순 정수비 다운샘플링 (예: 48kHz → 16kHz = 3배 간격으로 샘플 추출)
        /// </summary>
        private float[] Downsample(float[] audio, int sourceSampleRate, int targetSampleRate)
        {
            if (sourceSampleRate <= targetSampleRate) return audio;

            int ratio = sourceSampleRate / targetSampleRate; // 48000/16000 = 3
            int newLength = audio.Length / ratio;
            float[] result = new float[newLength];

            for (int i = 0; i < newLength; i++)
                result[i] = audio[i * ratio];

            return result;
        }

        /// <summary>
        /// 멜 스펙트로그램을 계산합니다.
        /// STFT → 파워 스펙트럼 → 멜 필터뱅크 적용 → 로그 스케일
        /// </summary>
        private float[,] ComputeMelSpectrogram(float[] audio)
        {
            // 총 프레임 수 계산
            int totalFrames = Math.Max(0, (audio.Length - WindowSize) / HopSize + 1);
            if (totalFrames == 0) return new float[0, MelBands];

            float[,] melSpec = new float[totalFrames, MelBands];
            float[] window = MakeHannWindow(WindowSize);

            for (int frame = 0; frame < totalFrames; frame++)
            {
                int offset = frame * HopSize;

                // 윈도우 적용 + zero-padding to FftSize
                var fftBuffer = new Complex[FftSize];
                for (int i = 0; i < WindowSize && (offset + i) < audio.Length; i++)
                    fftBuffer[i] = new Complex(audio[offset + i] * window[i], 0);

                // FFT 수행
                FFT(fftBuffer);

                // 파워 스펙트럼 (앞쪽 절반만 사용)
                int specLength = FftSize / 2 + 1;
                float[] powerSpec = new float[specLength];
                for (int i = 0; i < specLength; i++)
                    powerSpec[i] = (float)(fftBuffer[i].Real * fftBuffer[i].Real +
                                          fftBuffer[i].Imaginary * fftBuffer[i].Imaginary);

                // 멜 필터뱅크 적용
                for (int m = 0; m < MelBands; m++)
                {
                    float sum = 0;
                    for (int k = 0; k < specLength && k < _melFilterbank![m].Length; k++)
                        sum += _melFilterbank[m][k] * powerSpec[k];

                    // 로그 스케일 (log(x + epsilon) 으로 -inf 방지)
                    melSpec[frame, m] = (float)Math.Log(Math.Max(sum, 1e-10));
                }
            }

            return melSpec;
        }

        /// <summary>
        /// Hann 윈도우 생성
        /// </summary>
        private float[] MakeHannWindow(int size)
        {
            float[] window = new float[size];
            for (int i = 0; i < size; i++)
                window[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (size - 1)));
            return window;
        }

        // =============================================
        //  FFT 구현 (Radix-2 Cooley-Tukey)
        // =============================================

        private void FFT(Complex[] buffer)
        {
            int n = buffer.Length;
            if (n <= 1) return;

            // 비트 반전 정렬
            int bits = (int)Math.Log2(n);
            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, bits);
                if (j > i)
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }

            // 버터플라이 연산
            for (int len = 2; len <= n; len *= 2)
            {
                double angle = -2.0 * Math.PI / len;
                var wBase = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = buffer[i + j];
                        var v = buffer[i + j + len / 2] * w;
                        buffer[i + j] = u + v;
                        buffer[i + j + len / 2] = u - v;
                        w *= wBase;
                    }
                }
            }
        }

        private int BitReverse(int x, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (x & 1);
                x >>= 1;
            }
            return result;
        }

        // =============================================
        //  멜 필터뱅크 생성
        // =============================================

        /// <summary>
        /// 64개의 삼각형 멜 필터를 생성합니다. (125Hz ~ 7500Hz)
        /// </summary>
        private void BuildMelFilterbank()
        {
            int specLength = FftSize / 2 + 1;
            _melFilterbank = new float[MelBands][];

            float fMin = 125.0f;
            float fMax = 7500.0f;
            float melMin = HzToMel(fMin);
            float melMax = HzToMel(fMax);

            // 멜 스케일에서 균등하게 분포된 경계점 생성
            float[] melPoints = new float[MelBands + 2];
            for (int i = 0; i < MelBands + 2; i++)
                melPoints[i] = melMin + i * (melMax - melMin) / (MelBands + 1);

            // 주파수(Hz)로 변환 후, FFT bin 인덱스로 매핑
            int[] binPoints = new int[MelBands + 2];
            for (int i = 0; i < MelBands + 2; i++)
            {
                float freq = MelToHz(melPoints[i]);
                binPoints[i] = (int)Math.Floor(freq * FftSize / TargetSampleRate);
                binPoints[i] = Math.Min(binPoints[i], specLength - 1);
            }

            // 삼각형 필터 생성
            for (int m = 0; m < MelBands; m++)
            {
                _melFilterbank[m] = new float[specLength];
                int left = binPoints[m];
                int center = binPoints[m + 1];
                int right = binPoints[m + 2];

                for (int k = left; k < center && k < specLength; k++)
                {
                    if (center != left)
                        _melFilterbank[m][k] = (float)(k - left) / (center - left);
                }
                for (int k = center; k <= right && k < specLength; k++)
                {
                    if (right != center)
                        _melFilterbank[m][k] = (float)(right - k) / (right - center);
                }
            }
        }

        private float HzToMel(float hz) => 2595.0f * (float)Math.Log10(1.0 + hz / 700.0);
        private float MelToHz(float mel) => 700.0f * ((float)Math.Pow(10.0, mel / 2595.0) - 1.0f);

        // =============================================
        //  클래스 라벨 로딩 (YAMNet 521개 카테고리)
        // =============================================

        private void LoadClassNames()
        {
            // 프로토타입: 중요한 게임사운드만 매핑 (실제로는 yamnet_class_map.csv를 읽어야 함)
            _classNames = new string[521];
            for (int i = 0; i < 521; i++) _classNames[i] = "알 수 없는 소리";

            // 실제 YAMNet 클래스 인덱스 매핑
            _classNames[0] = "🗣 말소리";
            _classNames[1] = "🗣 말소리";
            _classNames[132] = "🔫 총소리";
            _classNames[133] = "🔫 기관총";
            _classNames[134] = "🔫 기관총";
            _classNames[420] = "💥 폭발음";
            _classNames[421] = "💥 폭발음";
            _classNames[288] = "🚗 자동차";
            _classNames[300] = "🚁 헬리콥터";
            _classNames[310] = "🏍 엔진소리";
            _classNames[315] = "🚨 사이렌";
            _classNames[394] = "👣 발소리";
            _classNames[395] = "👣 발소리";
            _classNames[399] = "🚪 문소리";
            _classNames[316] = "🚨 경적";
            _classNames[427] = "🔫 총소리";
            _classNames[426] = "💥 폭발음";
            _classNames[322] = "👣 발소리";
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}