// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 캡처한 소리를 YAMNet 입력(16kHz 모노)으로 바꾸는 계산. Android 의 CaptureAudioMath.kt 를 옮겼다.
    ///
    /// 1·2채널과 16·44.1·48kHz 는 Android 와 같은 순서로 같은 float·double 계산을 한다. Python 기준 구현과 1e-5 안으로
    /// 맞추는 기준 데이터 테스트가 두 앱을 함께 묶는다.
    /// PC 에서 더한 것은 두 가지다. 5.1·7.1 채널을 섞는 법(<see cref="DownmixInterleavedToMono"/>)과
    /// 88.2·96·176.4·192kHz 에서 FIR 을 레이트에 비례해 늘리는 것(<see cref="FirTapsFor"/>)이다.
    /// </summary>
    public static class CaptureAudioMath
    {
        public const int TargetSampleRate = 16000;

        /// <summary>YAMNet 한 창(0.975초)의 16kHz 샘플 수. log-mel 의 창 400 + 홉 160 × (96프레임 − 1) = 15,600 이다.</summary>
        public const int RequiredMono16kSamples = 400 + 160 * (96 - 1);

        // 44.1·48kHz 설계(차단 7.8kHz, β 8.6). YAMNet 멜 대역의 끝(7,500Hz)은 −3dB 안으로 남는다. 8~8.5kHz 는 덜 막히지만
        // 16kHz 로 줄이면 7.5~8kHz 로 접혀 멜 대역 밖에 떨어지고, 9kHz 부터는 45dB 넘게 막힌다.
        private const int BaseFirTaps = 96;
        private const double FirCutoffHz = 7800.0;
        private const double FirKaiserBeta = 8.6;
        private const int MaxCachedPhases = 1024;

        // 5.1·7.1 → 스테레오 계수. 스피커로 다시 내보낼 때 쓰는 ChannelDownmix 와 같은 ITU-R BS.775 값이다.
        // 둘이 어긋나지 않는지는 테스트가 ChannelDownmix 의 결과와 비교해 지킨다.
        private const float KCenter = 0.707f; // -3dB
        private const float KSide = 0.707f;   // -3dB
        private const float KBack = 0.500f;   // -6dB

        /// <summary>레이트마다 한 번 만들어 250ms 마다 다시 쓴다. 만든 뒤에는 바뀌지 않아 잠금 밖에서 읽어도 된다.</summary>
        private sealed class FirTable
        {
            public FirTable(int taps, int phaseCount, float[] coefficients)
            {
                Taps = taps;
                PhaseCount = phaseCount;
                Coefficients = coefficients;
            }

            public int Taps { get; }

            public int PhaseCount { get; }

            /// <summary>위상 p 의 계수는 [p × Taps, (p + 1) × Taps) 에 있다.</summary>
            public float[] Coefficients { get; }
        }

        private static readonly object FirTablesLock = new();
        private static readonly Dictionary<int, FirTable> FirTables = new();

        /// <summary>16kHz 15,600샘플을 만드는 데 필요한 캡처 레이트의 샘플 수(올림).</summary>
        public static int CaptureSamplesForOneYamnetWindow(int captureSampleRate)
        {
            return (int)Math.Ceiling(RequiredMono16kSamples * (double)captureSampleRate / TargetSampleRate);
        }

        /// <summary>
        /// 섞기 전 캡처 PCM 의 가장 큰 절댓값. 무음 게이트가 쓴다.
        /// 섞은 뒤에 재면 좌우 위상이 반대인 짧은 소리가 서로 지워져 게이트가 열리지 않는다.
        /// PC 의 5.1·7.1 도 LFE 를 포함한 모든 채널을 그대로 본다. NaN 은 건너뛴다.
        /// </summary>
        public static float MaxAbsoluteInterleavedPeak(ReadOnlySpan<float> interleaved)
        {
            float peak = 0f;
            foreach (float value in interleaved)
            {
                float sample = Math.Abs(value);
                if (sample > peak) peak = sample;
            }
            return peak;
        }

        /// <summary>
        /// 인터리브 PCM 을 모노 프레임으로 섞어 <paramref name="destination"/> 앞쪽에 쓰고 프레임 수를 돌려준다.
        /// 채널 수로 나눠 떨어지지 않는 끝의 샘플은 버린다.
        ///
        /// 1채널은 그대로, 2채널은 (L+R)/2 로 Android 와 같다.
        /// PC 에서 더한 것: 6채널(FL FR FC LFE SL SR)과 8채널(FL FR FC LFE BL BR SL SR)은 ChannelDownmix 와 같은
        /// ITU-R BS.775 계수로 스테레오를 만든 뒤(LFE 는 뺀다) (L+R)/2 를 한다. 예전 PC 코드는 FL·FR·FC 만 AI 에 넣어
        /// 뒤·옆 스피커로만 나는 소리를 AI 가 듣지 못했다. 스피커용과 달리 ±1 로 자르지 않는다. 자르면 큰 소리의 파형이
        /// 바뀌어 AI 가 다른 소리로 듣는다(AI 입력은 소리로 내보내지 않으므로 1 을 넘어도 된다).
        /// 그 밖의 채널 수는 Android 처럼 모든 채널의 평균이다.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="destination"/> 이 프레임 수보다 짧다.</exception>
        public static int DownmixInterleavedToMono(ReadOnlySpan<float> interleaved, int channels, Span<float> destination)
        {
            if (interleaved.Length <= 0 || channels <= 0) return 0;
            int frames = interleaved.Length / channels;
            if (frames <= 0) return 0;
            if (frames > destination.Length)
                throw new ArgumentException($"destination must hold {frames} frames", nameof(destination));

            switch (channels)
            {
                case 1:
                    interleaved[..frames].CopyTo(destination);
                    return frames;
                case 6:
                    Downmix51ToMono(interleaved, frames, destination);
                    return frames;
                case 8:
                    Downmix71ToMono(interleaved, frames, destination);
                    return frames;
            }

            int si = 0;
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += interleaved[si++];
                }
                destination[f] = sum / channels;
            }
            return frames;
        }

        /// <summary>L = FL + 0.707·FC + 0.707·SL, R 도 같은 꼴(ChannelDownmix 와 같은 식). LFE 는 뺀다.</summary>
        private static void Downmix51ToMono(ReadOnlySpan<float> src, int frames, Span<float> dst)
        {
            for (int f = 0; f < frames; f++)
            {
                int o = f * 6;
                float fl = src[o + 0];
                float fr = src[o + 1];
                float fc = src[o + 2];
                // o + 3 은 LFE
                float sl = src[o + 4];
                float sr = src[o + 5];

                float left = fl + KCenter * fc + KSide * sl;
                float right = fr + KCenter * fc + KSide * sr;
                dst[f] = (left + right) / 2f;
            }
        }

        /// <summary>L = FL + 0.707·FC + 0.707·SL + 0.5·BL, R 도 같은 꼴(ChannelDownmix 와 같은 식). LFE 는 뺀다.</summary>
        private static void Downmix71ToMono(ReadOnlySpan<float> src, int frames, Span<float> dst)
        {
            for (int f = 0; f < frames; f++)
            {
                int o = f * 8;
                float fl = src[o + 0];
                float fr = src[o + 1];
                float fc = src[o + 2];
                // o + 3 은 LFE
                float bl = src[o + 4];
                float br = src[o + 5];
                float sl = src[o + 6];
                float sr = src[o + 7];

                float left = fl + KCenter * fc + KSide * sl + KBack * bl;
                float right = fr + KCenter * fc + KSide * sr + KBack * br;
                dst[f] = (left + right) / 2f;
            }
        }

        /// <summary>
        /// 이 레이트에서 쓰는 FIR 탭 수. Android 가 받는 44.1·48kHz 와 그 밖의 레이트는 96탭이다.
        ///
        /// PC 에서 더한 것: 88.2·176.4kHz 는 96 × 레이트 ÷ 44,100, 96·192kHz 는 96 × 레이트 ÷ 48,000 (192·384탭)으로 늘린다.
        /// Kaiser 창 FIR 의 전이 대역 폭(Hz)은 레이트 ÷ 탭 수에 비례하므로 이렇게 해야 7.8kHz 언저리의 통과·차단이
        /// 44.1·48kHz 와 같다. 96탭 그대로면 96kHz 에서 전이 대역이 두 배로 넓어져 7kHz 가 깎이고 9kHz 가 덜 막힌다.
        /// </summary>
        public static int FirTapsFor(int sourceSampleRate) => sourceSampleRate switch
        {
            88200 or 176400 => BaseFirTaps * sourceSampleRate / 44100,
            96000 or 192000 => BaseFirTaps * sourceSampleRate / 48000,
            _ => BaseFirTaps,
        };

        /// <summary>
        /// 모노 캡처를 16kHz 로 바꿔 <paramref name="destination"/> 전체를 채운다(보통 <see cref="RequiredMono16kSamples"/> 개).
        ///
        /// 16kHz 는 그대로 복사한다. 16kHz 보다 낮으면 예전 선형 보간을 그대로 둔다(AI 는 이 레이트를 받지 않는다).
        /// 더 높으면 Kaiser 창(β 8.6) sinc 저역통과 FIR(차단 7.8kHz)을 출력 샘플마다 원본 위치에서 계산한다.
        /// 위치는 정수(i × 레이트 ÷ 16,000 의 몫과 나머지)로 세어 15,600샘플 동안 어긋나지 않고, 나머지로 고르는 위상별
        /// 계수 표는 레이트마다 한 번만 만든다(위상 수: 44.1kHz 160개, 88.2kHz 80개, 176.4kHz 40개, 48·96·192kHz 1개).
        /// 원본 범위 밖은 0 으로 보므로 창 가장자리는 FIR 의 일부만 더한다. 곱과 합은 double, 계수와 결과는 float 로 Android 와 같다.
        /// </summary>
        public static void ResampleMonoFloatTo16kCustom(ReadOnlySpan<float> source, int sourceSampleRate, Span<float> destination)
        {
            destination.Clear();
            if (source.Length <= 0) return;

            if (sourceSampleRate == TargetSampleRate)
            {
                int copyLength = Math.Min(source.Length, destination.Length);
                source[..copyLength].CopyTo(destination);
                return;
            }

            if (sourceSampleRate < TargetSampleRate)
            {
                LinearResample(source, sourceSampleRate, destination);
                return;
            }

            int sourceAvailable = source.Length;
            FirTable table = FirTableFor(sourceSampleRate);
            int taps = table.Taps;
            int halfTaps = taps / 2;
            float[] coefficients = table.Coefficients;
            for (int i = 0; i < destination.Length; i++)
            {
                long positionNumerator = (long)i * sourceSampleRate;
                int center = (int)(positionNumerator / TargetSampleRate);
                int phase = PhaseIndex(positionNumerator % TargetSampleRate, table.PhaseCount);
                int phaseStart = phase * taps;
                int @base = center - halfTaps + 1;
                int firstTap = Math.Max(0, -@base);
                int lastTapExclusive = Math.Min(taps, sourceAvailable - @base);
                double sum = 0.0;
                for (int tap = firstTap; tap < lastTapExclusive; tap++)
                {
                    sum += (double)coefficients[phaseStart + tap] * source[@base + tap];
                }
                destination[i] = (float)sum;
            }
        }

        /// <summary>16kHz 보다 낮은 레이트(AI 는 받지 않는다)에서 예전 보간을 그대로 둔다.</summary>
        private static void LinearResample(ReadOnlySpan<float> source, int sourceSampleRate, Span<float> destination)
        {
            int sourceAvailable = source.Length;
            double factor = (double)sourceSampleRate / TargetSampleRate;
            for (int i = 0; i < destination.Length; i++)
            {
                double sourcePosition = i * factor;
                int index1 = (int)sourcePosition;
                int index2 = index1 + 1;
                if (index1 >= sourceAvailable) continue;
                float alpha = (float)(sourcePosition - index1);
                float value1 = source[index1];
                float value2 = index2 < sourceAvailable ? source[index2] : value1;
                destination[i] = (1f - alpha) * value1 + alpha * value2;
            }
        }

        private static FirTable FirTableFor(int sourceSampleRate)
        {
            if (sourceSampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceSampleRate), sourceSampleRate, "sample rate must be positive");

            lock (FirTablesLock)
            {
                if (FirTables.TryGetValue(sourceSampleRate, out FirTable? cached)) return cached;

                int taps = FirTapsFor(sourceSampleRate);
                int exactPhaseCount = TargetSampleRate / GreatestCommonDivisor(sourceSampleRate, TargetSampleRate);
                int phaseCount = Math.Min(exactPhaseCount, MaxCachedPhases);
                var coefficients = new float[phaseCount * taps];
                for (int phase = 0; phase < phaseCount; phase++)
                {
                    CreateFirCoefficients(
                        sourceSampleRate,
                        (double)phase / phaseCount,
                        coefficients.AsSpan(phase * taps, taps));
                }

                var table = new FirTable(taps, phaseCount, coefficients);
                FirTables[sourceSampleRate] = table;
                return table;
            }
        }

        private static int PhaseIndex(long remainder, int phaseCount)
        {
            if (phaseCount == 1) return 0;
            long rounded = (remainder * phaseCount + TargetSampleRate / 2) / TargetSampleRate;
            return (int)(rounded % phaseCount);
        }

        /// <summary>
        /// 원본 위치가 정수에서 <paramref name="fractionalPosition"/> 만큼 지난 출력 샘플의 계수. 탭 수는 넘겨받은 칸 수이고,
        /// 합이 1 이 되게 맞춰 상수 입력이 그대로 나온다. 계수를 float 로 한 번 줄인 뒤 double 합으로 나누는 순서까지 Android 와 같다.
        /// </summary>
        private static void CreateFirCoefficients(int sourceSampleRate, double fractionalPosition, Span<float> coefficients)
        {
            int taps = coefficients.Length;
            int halfTaps = taps / 2;
            double windowScale = BesselI0(FirKaiserBeta);
            double normalizedCutoff = 2.0 * FirCutoffHz / sourceSampleRate;
            double sum = 0.0;
            for (int tap = 0; tap < taps; tap++)
            {
                int sampleOffset = tap - halfTaps + 1;
                double time = sampleOffset - fractionalPosition;
                double normalizedRadius = Math.Min(1.0, Math.Abs(time) / halfTaps);
                double window = BesselI0(FirKaiserBeta * Math.Sqrt(1.0 - normalizedRadius * normalizedRadius)) / windowScale;
                double value = normalizedCutoff * Sinc(normalizedCutoff * time) * window;
                coefficients[tap] = (float)value;
                sum += value;
            }
            for (int tap = 0; tap < taps; tap++)
            {
                coefficients[tap] = (float)(coefficients[tap] / sum);
            }
        }

        private static double Sinc(double value)
        {
            return Math.Abs(value) < 1e-12 ? 1.0 : Math.Sin(Math.PI * value) / (Math.PI * value);
        }

        private static double BesselI0(double value)
        {
            double term = 1.0;
            double sum = 1.0;
            int k = 1;
            while (true)
            {
                term *= (value * value / 4.0) / ((double)k * k);
                sum += term;
                if (term < sum * 1e-15) return sum;
                k++;
            }
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            int x = Math.Abs(a);
            int y = Math.Abs(b);
            while (y != 0)
            {
                int remainder = x % y;
                x = y;
                y = remainder;
            }
            return x;
        }
    }
}
