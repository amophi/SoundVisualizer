// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Numerics;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 기수 2 Cooley–Tukey FFT(제자리 계산). 모바일 판 <c>AudioPreprocessor.kt</c> 의 <c>fftInPlace</c> 를 곱셈·덧셈 순서까지
    /// 그대로 옮겼다. 로그멜 기준 데이터와의 오차를 Kotlin 과 같게 두려면 계산 순서가 같아야 한다.
    ///
    /// Kotlin 은 단계마다 회전 인자를 w ← w·w_len 점화식으로 블록마다 처음부터 다시 만든다. 여기서는 같은 점화식으로
    /// 생성할 때 한 번만 표를 만든다. 같은 곱셈을 같은 순서로 하므로 블록마다 다시 만든 값과 비트까지 같고,
    /// 호출마다 삼각함수와 점화식 곱셈을 하지 않아도 된다.
    ///
    /// 표는 만든 뒤 읽기만 하므로 여러 스레드가 함께 써도 된다. 버퍼는 부르는 쪽이 준다.
    /// </summary>
    internal sealed class Radix2Fft
    {
        private readonly int[] _bitReversed;

        // 단계(len = 2·half)마다 w_0..w_{half-1} 을 [half - 1, 2·half - 1) 에 둔다. 모두 합쳐 Size - 1 칸이다.
        private readonly double[] _twiddleRe;
        private readonly double[] _twiddleIm;

        public Radix2Fft(int size)
        {
            if (!BitOperations.IsPow2(size))
                throw new ArgumentException("FFT size must be a power of two", nameof(size));

            Size = size;
            _bitReversed = CreateBitReversedIndices(size);
            _twiddleRe = new double[size - 1];
            _twiddleIm = new double[size - 1];

            for (int len = 2; len <= size; len <<= 1)
            {
                double ang = -2.0 * Math.PI / len;
                double wLenRe = Math.Cos(ang);
                double wLenIm = Math.Sin(ang);
                int half = len >> 1;
                double wRe = 1.0;
                double wIm = 0.0;
                for (int j = 0; j < half; j++)
                {
                    _twiddleRe[half - 1 + j] = wRe;
                    _twiddleIm[half - 1 + j] = wIm;
                    double nextWRe = wRe * wLenRe - wIm * wLenIm;
                    double nextWIm = wRe * wLenIm + wIm * wLenRe;
                    wRe = nextWRe;
                    wIm = nextWIm;
                }
            }
        }

        public int Size { get; }

        /// <summary><paramref name="real"/>·<paramref name="imag"/> 를 제자리에서 이산 푸리에 변환한다(지수 부호 −).</summary>
        public void Transform(Span<double> real, Span<double> imag)
        {
            int n = Size;
            if (real.Length != n || imag.Length != n)
                throw new ArgumentException($"FFT buffers must hold exactly {n} values");

            for (int i = 0; i < n; i++)
            {
                int j = _bitReversed[i];
                if (j > i)
                {
                    double tr = real[i]; real[i] = real[j]; real[j] = tr;
                    double ti = imag[i]; imag[i] = imag[j]; imag[j] = ti;
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                int half = len >> 1;
                int twiddleStart = half - 1;
                for (int i = 0; i < n; i += len)
                {
                    for (int j = 0; j < half; j++)
                    {
                        double wRe = _twiddleRe[twiddleStart + j];
                        double wIm = _twiddleIm[twiddleStart + j];
                        int top = i + j;
                        int bottom = top + half;
                        double uRe = real[top];
                        double uIm = imag[top];
                        double vRe = real[bottom] * wRe - imag[bottom] * wIm;
                        double vIm = real[bottom] * wIm + imag[bottom] * wRe;
                        real[top] = uRe + vRe;
                        imag[top] = uIm + vIm;
                        real[bottom] = uRe - vRe;
                        imag[bottom] = uIm - vIm;
                    }
                }
            }
        }

        private static int[] CreateBitReversedIndices(int n)
        {
            int bits = BitOperations.TrailingZeroCount(n);
            var indices = new int[n];
            for (int i = 0; i < n; i++)
            {
                int v = i;
                int r = 0;
                for (int b = 0; b < bits; b++)
                {
                    r = (r << 1) | (v & 1);
                    v >>= 1;
                }
                indices[i] = r;
            }
            return indices;
        }
    }
}
