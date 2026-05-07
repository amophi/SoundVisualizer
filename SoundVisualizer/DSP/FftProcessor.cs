using System;
using NAudio.Dsp;

namespace SoundVisualizer.DSP
{
    /// <summary>
    /// WASAPI에서 캡처한 raw byte[] 데이터를 채널별로 분리 후 FFT를 수행하여
    /// 주파수 대역별 크기(magnitude) 배열을 반환하는 DSP 모듈.
    /// </summary>
    public class FftProcessor
    {
        // FFT 크기: 2의 거듭제곱이어야 함 (1024 = 약 23ms @44100Hz, 약 21ms @48000Hz)
        private readonly int _fftSize;
        private readonly int _fftExponent; // log2(_fftSize)

        public int FftSize => _fftSize;

        public FftProcessor(int fftSize = 1024)
        {
            if ((fftSize & (fftSize - 1)) != 0 || fftSize < 64)
                throw new ArgumentException("fftSize는 64 이상의 2의 거듭제곱이어야 합니다.", nameof(fftSize));

            _fftSize = fftSize;
            _fftExponent = (int)Math.Log2(fftSize);
        }

        /// <summary>
        /// raw byte[]에서 특정 채널의 데이터만 추출하여 FFT를 수행합니다.
        /// </summary>
        /// <param name="rawAudioData">WASAPI에서 캡처한 원본 byte[] (32-bit IEEE Float)</param>
        /// <param name="bytesRecorded">실제 녹음된 바이트 수</param>
        /// <param name="channelCount">총 채널 수 (예: 8 = 7.1ch, 2 = 스테레오)</param>
        /// <param name="targetChannel">추출할 채널 인덱스 (0~channelCount-1)</param>
        /// <returns>주파수 대역별 크기(magnitude) 배열 (길이: fftSize/2)</returns>
        public float[] ProcessChannel(byte[] rawAudioData, int bytesRecorded, int channelCount, int targetChannel)
        {
            if (targetChannel < 0 || targetChannel >= channelCount)
                throw new ArgumentOutOfRangeException(nameof(targetChannel));

            // 1. byte[] → float[] 전체 변환
            int totalFloats = bytesRecorded / 4;
            float[] allSamples = new float[totalFloats];
            Buffer.BlockCopy(rawAudioData, 0, allSamples, 0, bytesRecorded);

            // 2. 인터리브된 데이터에서 대상 채널만 추출
            int samplesPerChannel = totalFloats / channelCount;
            float[] channelSamples = new float[samplesPerChannel];

            for (int i = 0; i < samplesPerChannel; i++)
            {
                channelSamples[i] = allSamples[i * channelCount + targetChannel];
            }

            // 3. FFT 수행
            return ComputeFft(channelSamples);
        }

        /// <summary>
        /// 모든 채널을 한 번에 FFT 처리하여 채널별 magnitude 배열을 반환합니다.
        /// </summary>
        /// <returns>float[channelCount][fftSize/2] 형태의 결과</returns>
        public float[][] ProcessAllChannels(byte[] rawAudioData, int bytesRecorded, int channelCount)
        {
            int totalFloats = bytesRecorded / 4;
            float[] allSamples = new float[totalFloats];
            Buffer.BlockCopy(rawAudioData, 0, allSamples, 0, bytesRecorded);

            int samplesPerChannel = totalFloats / channelCount;
            float[][] results = new float[channelCount][];

            for (int ch = 0; ch < channelCount; ch++)
            {
                float[] channelSamples = new float[samplesPerChannel];
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    channelSamples[i] = allSamples[i * channelCount + ch];
                }
                results[ch] = ComputeFft(channelSamples);
            }

            return results;
        }

        /// <summary>
        /// float[] 샘플에 대해 FFT를 수행하고 magnitude 배열을 반환합니다.
        /// </summary>
        private float[] ComputeFft(float[] samples)
        {
            // FFT 입력 버퍼 (fftSize만큼만 사용, 부족하면 0-padding)
            int length = Math.Min(samples.Length, _fftSize);
            Complex[] fftBuffer = new Complex[_fftSize];

            for (int i = 0; i < length; i++)
            {
                // Hann 윈도우 적용 (스펙트럼 누출 방지)
                double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (length - 1)));
                fftBuffer[i].X = (float)(samples[i] * window);
                fftBuffer[i].Y = 0;
            }

            // NAudio 내장 FFT 수행
            FastFourierTransform.FFT(true, _fftExponent, fftBuffer);

            // magnitude 계산 (나이퀴스트 절반만 유효)
            int halfSize = _fftSize / 2;
            float[] magnitudes = new float[halfSize];

            for (int i = 0; i < halfSize; i++)
            {
                float re = fftBuffer[i].X;
                float im = fftBuffer[i].Y;
                magnitudes[i] = MathF.Sqrt(re * re + im * im);
            }

            return magnitudes;
        }
    }
}
