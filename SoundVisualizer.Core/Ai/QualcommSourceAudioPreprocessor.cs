// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// Qualcomm 의 YAMNet 레시피가 쓰는 원본 전처리를 옮겨, 16kHz 모노 15,600샘플(0.975초)을 YAMNet 입력
    /// [1, 1, 96, 64] 로그멜로 바꾼다. 결과는 시간 우선(<c>time * 64 + mel</c>)으로 편 6,144칸이다.
    ///
    /// 출처(고정한 버전):
    /// - qai-hub-models commit 7925bdd04586a87b09914164903f51b3f889c4aa
    ///   - yamnet/app.py::preprocessing_yamnet_from_source
    ///   - yamnet/model.py 가 torch_audioset commit e8852c5 를 고정한다
    /// - w-hc/torch_audioset commit e8852c53becef811784754a2de9c4617d8db2156
    ///   - torch_input_processing.py::WaveformToInput
    ///   - torch_input_processing.py::VGGishLogMelSpectrogram
    ///
    /// torch.stft(center=True, pad_mode="reflect") 처럼 양쪽을 256샘플씩 반사로 늘려 98프레임을 만들고 앞 96프레임만 쓴다.
    /// 400점 periodic Hann 창을 512점 FFT 가운데에 놓고, 파워가 아닌 크기(magnitude)에 torchaudio 기본 HTK 멜 필터
    /// (연속 주파수 위의 삼각형, 125~7,500Hz, 64대역, 정규화 없음)를 곱한 뒤 ln(x + 0.001) 을 취한다.
    ///
    /// 모바일 판 <c>QualcommSourceAudioPreprocessor.kt</c> 를 계산 순서와 자료형(double)까지 그대로 옮겼다. Python 기준
    /// 데이터와의 오차를 Kotlin 과 같은 한도(최대 1e-5, 평균 1e-7) 안에 두려면 순서를 바꾸거나 float 로 줄이면 안 된다.
    /// 다만 창과 멜 경계를 만드는 cos·expm1 은 플랫폼마다 마지막 자리(1ulp)가 달라 표가 Kotlin 과 비트까지 같지는 않다.
    /// 그 차이는 1e-15 수준이라 float 로 줄인 출력에는 드물게 1ulp 로만 드러날 수 있다.
    ///
    /// 창·멜 필터·FFT 표는 생성할 때 한 번만 만든다. 작업 버퍼를 인스턴스가 들고 있으므로 스레드 안전하지 않다.
    /// 추론 스레드 하나가 인스턴스 하나를 쓴다.
    /// </summary>
    public sealed class QualcommSourceAudioPreprocessor
    {
        public const int SampleRate = 16000;
        public const int MelBins = 64;
        public const int TimeFrames = 96;
        public const int WindowLength = 400;
        public const int HopLength = 160;
        public const int FftSize = 512;
        public const float MelFMin = 125f;
        public const float MelFMax = 7500f;

        /// <summary>
        /// 로그 앞에 더하는 값. Kotlin 은 0.001f 를 double 로 넓혀(0.0010000000474974513) 더하므로 float 로 둔다.
        /// 0.001(double) 로 바꾸면 기준 데이터와의 오차가 Kotlin 과 달라진다.
        /// </summary>
        public const float LogEps = 0.001f;

        /// <summary>한 번에 쓰는 입력 길이. 400 + 160 × 95 = 15,600샘플.</summary>
        public const int RequiredMono16kSamples = WindowLength + HopLength * (TimeFrames - 1);

        /// <summary>출력 길이. 96 × 64 = 6,144칸.</summary>
        public const int LogMelSize = TimeFrames * MelBins;

        private const int FreqBins = FftSize / 2 + 1;
        private const int ReflectPadding = FftSize / 2;
        private const int WindowOffset = (FftSize - WindowLength) / 2;

        private readonly double[] _hannWindow = CreateHannWindow(WindowLength);
        private readonly double[][] _melFilterBank =
            CreateTorchaudioHtkMelFilterBank(MelBins, FftSize, SampleRate, MelFMin, MelFMax);
        private readonly int[] _melStartBins = new int[MelBins];
        private readonly int[] _melEndBins = new int[MelBins];
        private readonly Radix2Fft _fft = new(FftSize);

        private readonly float[] _fitted = new float[RequiredMono16kSamples];
        private readonly double[] _fftReal = new double[FftSize];
        private readonly double[] _fftImag = new double[FftSize];
        private readonly double[] _magnitude = new double[FreqBins];

        public QualcommSourceAudioPreprocessor()
        {
            // 필터마다 0 이 아닌 구간만 더한다. 0 을 더해도 합은 그대로이므로 결과는 같고 계산만 준다.
            for (int mel = 0; mel < MelBins; mel++)
            {
                double[] weights = _melFilterBank[mel];
                int start = 0;
                while (start < FreqBins && weights[start] == 0.0) start++;
                int last = FreqBins - 1;
                while (last >= start && weights[last] == 0.0) last--;
                _melStartBins[mel] = start;
                _melEndBins[mel] = Math.Min(FreqBins, last + 1);
            }
        }

        /// <summary>
        /// <paramref name="mono16k"/> 의 로그멜을 새 배열에 담아 돌려준다(Kotlin 과 같은 꼴). 실시간 경로에서는 매번 새 배열을
        /// 만들지 않도록 버퍼를 받는 쪽을 쓴다.
        /// </summary>
        public float[] ComputeLogMelSpectrogram(ReadOnlySpan<float> mono16k)
        {
            var logMel = new float[LogMelSize];
            ComputeLogMelSpectrogram(mono16k, logMel);
            return logMel;
        }

        /// <summary>
        /// 16kHz 모노 소리의 [1, 1, 96, 64] 로그멜을 <paramref name="logMel"/> 앞쪽 6,144칸에 시간 우선으로 쓴다.
        /// 입력이 15,600샘플보다 길면 앞 15,600샘플만 쓰고, 짧으면 뒤를 0 으로 채운 다음 반사 패딩을 한다(고정한 원본과 같다).
        /// 메모리를 새로 잡지 않는다.
        /// </summary>
        public void ComputeLogMelSpectrogram(ReadOnlySpan<float> mono16k, Span<float> logMel)
        {
            if (logMel.Length < LogMelSize)
                throw new ArgumentException($"logMel must hold {LogMelSize} values", nameof(logMel));

            int copyLength = Math.Min(mono16k.Length, RequiredMono16kSamples);
            mono16k[..copyLength].CopyTo(_fitted);
            Array.Clear(_fitted, copyLength, RequiredMono16kSamples - copyLength);

            for (int time = 0; time < TimeFrames; time++)
            {
                Array.Clear(_fftReal);
                Array.Clear(_fftImag);

                // torch.stft(center=True, pad_mode="reflect") 는 양쪽을 256샘플씩 늘린 뒤,
                // 400점 창을 512점 FFT 가운데(56번째 칸부터)에 놓는다.
                int unpaddedFrameStart = time * HopLength - ReflectPadding;
                for (int windowIndex = 0; windowIndex < WindowLength; windowIndex++)
                {
                    int fftIndex = WindowOffset + windowIndex;
                    int sourceIndex = ReflectIndex(unpaddedFrameStart + fftIndex, RequiredMono16kSamples);
                    _fftReal[fftIndex] = (double)_fitted[sourceIndex] * _hannWindow[windowIndex];
                }

                _fft.Transform(_fftReal, _fftImag);
                for (int frequency = 0; frequency < FreqBins; frequency++)
                {
                    double real = _fftReal[frequency];
                    double imaginary = _fftImag[frequency];
                    _magnitude[frequency] = Math.Sqrt(real * real + imaginary * imaginary);
                }

                for (int mel = 0; mel < MelBins; mel++)
                {
                    double sum = 0.0;
                    double[] weights = _melFilterBank[mel];
                    for (int frequency = _melStartBins[mel]; frequency < _melEndBins[mel]; frequency++)
                    {
                        sum += weights[frequency] * _magnitude[frequency];
                    }
                    logMel[time * MelBins + mel] = (float)Math.Log(sum + (double)LogEps);
                }
            }
        }

        /// <summary>numpy·torch 의 reflect: 가장자리 샘플을 되풀이하지 않고 접는다(-1 → 1, length → length - 2).</summary>
        private static int ReflectIndex(int index, int length)
        {
            System.Diagnostics.Debug.Assert(length > 1, "reflect padding requires at least two samples");
            int reflected = index;
            while (reflected < 0 || reflected >= length)
            {
                reflected = reflected < 0 ? -reflected : 2 * length - 2 - reflected;
            }
            return reflected;
        }

        /// <summary>periodic Hann(torch.hann_window 기본값). 분모가 length - 1 이 아니라 length 다.</summary>
        private static double[] CreateHannWindow(int length)
        {
            var window = new double[length];
            for (int index = 0; index < length; index++)
            {
                window[index] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * index / length);
            }
            return window;
        }

        /// <summary>
        /// torchaudio.functional.melscale_fbanks 의 기본값(HTK 척도, norm=None). 삼각형을 FFT 빈 번호로 반올림하지 않고
        /// 각 빈의 연속 주파수 위에서 잰다. 결과는 [멜 대역][FFT 빈] 이다.
        /// </summary>
        private static double[][] CreateTorchaudioHtkMelFilterBank(
            int melBins,
            int fftSize,
            int sampleRate,
            double minimumFrequency,
            double maximumFrequency)
        {
            int frequencyBins = fftSize / 2 + 1;
            double minimumMel = HertzToHtkMel(minimumFrequency);
            double maximumMel = HertzToHtkMel(maximumFrequency);
            var frequencyEdges = new double[melBins + 2];
            for (int edge = 0; edge < frequencyEdges.Length; edge++)
            {
                double mel = minimumMel + (maximumMel - minimumMel) * edge / (melBins + 1.0);
                frequencyEdges[edge] = HtkMelToHertz(mel);
            }

            var weights = new double[melBins][];
            for (int mel = 0; mel < melBins; mel++)
            {
                weights[mel] = new double[frequencyBins];
                double lower = frequencyEdges[mel];
                double center = frequencyEdges[mel + 1];
                double upper = frequencyEdges[mel + 2];
                for (int frequencyBin = 0; frequencyBin < frequencyBins; frequencyBin++)
                {
                    double frequency = frequencyBin * (sampleRate / 2.0) / (frequencyBins - 1);
                    double downSlope = (frequency - lower) / (center - lower);
                    double upSlope = (upper - frequency) / (upper - center);
                    weights[mel][frequencyBin] = Math.Max(0.0, Math.Min(downSlope, upSlope));
                }
            }
            return weights;
        }

        private static double HertzToHtkMel(double hertz) => 1127.0 * Log1P(hertz / 700.0);

        private static double HtkMelToHertz(double mel) => 700.0 * ExpM1(mel / 1127.0);

        // double.LogP1·ExpM1 은 Log(x + 1)·Exp(x) − 1 을 그대로 계산해 x 가 0 에 가까우면 자릿수를 잃는다.
        // Kotlin 의 ln1p·expm1(fdlibm) 처럼 어디서나 정확한 값을 내도록 Kahan 의 보정을 쓴다.
        private static double Log1P(double x)
        {
            double u = 1.0 + x;
            if (u == 1.0) return x;
            if (double.IsPositiveInfinity(u)) return u;
            return Math.Log(u) * x / (u - 1.0);
        }

        private static double ExpM1(double x)
        {
            double u = Math.Exp(x);
            if (u == 1.0) return x;
            if (double.IsPositiveInfinity(u)) return u;
            double uMinusOne = u - 1.0;
            if (uMinusOne == -1.0) return -1.0;
            return uMinusOne * x / Math.Log(u);
        }
    }
}
