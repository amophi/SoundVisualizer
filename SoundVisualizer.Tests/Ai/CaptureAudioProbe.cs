// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Buffers.Binary;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>AI 입력 경로 테스트가 함께 쓰는 기준 데이터 읽기와 측정.</summary>
internal static class CaptureAudioProbe
{
    /// <summary>테스트 출력의 Ai/Reference 에 복사된 리틀엔디언 float32 배열을 읽는다.</summary>
    public static float[] LoadReference(string fileName)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Ai", "Reference", fileName));
        var values = new float[bytes.Length / sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)));
        }
        return values;
    }

    public static float MaxAbs(float[] actual, float[] expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        float maximum = 0f;
        for (int i = 0; i < actual.Length; i++)
        {
            maximum = Math.Max(maximum, Math.Abs(actual[i] - expected[i]));
        }
        return maximum;
    }

    /// <summary>
    /// 캡처 레이트의 코사인을 16kHz 로 바꾼 뒤, 접혀 들어올 주파수 성분의 크기를 dB 로 잰다(Android 테스트의 toneGainDb).
    /// 출력 4,096개 가운데 양 끝 128개는 창 가장자리라 빼고, 코사인·사인 두 성분을 최소제곱으로 맞춘다.
    /// </summary>
    public static double ToneGainDb(int sampleRate, double inputFrequencyHz)
    {
        const int destinationLength = 4096;
        int sourceLength = (int)Math.Ceiling(destinationLength * (double)sampleRate / CaptureAudioMath.TargetSampleRate) + 96;
        var source = new float[sourceLength];
        for (int index = 0; index < sourceLength; index++)
        {
            source[index] = (float)Math.Cos(2.0 * Math.PI * inputFrequencyHz * index / sampleRate);
        }
        var destination = new float[destinationLength];
        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, sampleRate, destination);

        double aliasFrequency = inputFrequencyHz % CaptureAudioMath.TargetSampleRate;
        if (aliasFrequency > CaptureAudioMath.TargetSampleRate / 2.0)
        {
            aliasFrequency = CaptureAudioMath.TargetSampleRate - aliasFrequency;
        }
        double yc = 0.0, ys = 0.0, cc = 0.0, ss = 0.0;
        for (int index = 128; index < destinationLength - 128; index++)
        {
            double phase = 2.0 * Math.PI * aliasFrequency * index / CaptureAudioMath.TargetSampleRate;
            double cosine = Math.Cos(phase);
            double sine = Math.Sin(phase);
            double value = destination[index];
            yc += value * cosine;
            ys += value * sine;
            cc += cosine * cosine;
            ss += sine * sine;
        }
        double cosineCoefficient = yc / cc;
        double sineCoefficient = ss < 1e-12 ? 0.0 : ys / ss;
        double amplitude = Math.Sqrt(cosineCoefficient * cosineCoefficient + sineCoefficient * sineCoefficient);
        return 20.0 * Math.Log10(Math.Max(amplitude, 1e-300));
    }

    public static void AssertGainAtLeast(int sampleRate, double frequencyHz, double minimumDb)
    {
        double gain = ToneGainDb(sampleRate, frequencyHz);
        Assert.True(gain >= minimumDb, $"{sampleRate} Hz, {frequencyHz} Hz gain {gain} dB must be >= {minimumDb} dB");
    }

    public static void AssertGainAtMost(int sampleRate, double frequencyHz, double maximumDb)
    {
        double gain = ToneGainDb(sampleRate, frequencyHz);
        Assert.True(gain <= maximumDb, $"{sampleRate} Hz, {frequencyHz} Hz gain {gain} dB must be <= {maximumDb} dB");
    }

    /// <summary>Android 가 44.1·48kHz 에 거는 통과·차단 요건. PC 고레이트에도 그대로 건다.</summary>
    public static void AssertFrequencyAcceptance(int sampleRate)
    {
        AssertGainAtLeast(sampleRate, 7000.0, -1.5);
        AssertGainAtLeast(sampleRate, 7500.0, -3.0);
        AssertGainAtMost(sampleRate, 8500.0, -20.0);
        AssertGainAtMost(sampleRate, 9000.0, -45.0);
        AssertGainAtMost(sampleRate, 10000.0, -45.0);
        AssertGainAtMost(sampleRate, 15000.0, -60.0);
    }

    /// <summary>한 창 길이의 상수 1 을 바꾸면 끝까지 유한하고, 가장자리도 0.5~1.2 안이고, 안쪽은 1 이고, 마지막 샘플도 채워진다.</summary>
    public static void AssertConstantWindowResponse(int sampleRate)
    {
        int sourceLength = CaptureAudioMath.CaptureSamplesForOneYamnetWindow(sampleRate);
        var source = new float[sourceLength];
        Array.Fill(source, 1f);
        var destination = new float[CaptureAudioMath.RequiredMono16kSamples];

        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, sampleRate, destination);

        Assert.All(destination, value => Assert.True(float.IsFinite(value), $"{sampleRate} output must stay finite"));
        Assert.All(destination, value => Assert.InRange(value, 0.5f, 1.2f));
        for (int index = 16; index < destination.Length - 16; index++)
        {
            Assert.Equal(1f, destination[index], 1e-3f);
        }
        Assert.NotEqual(0f, destination[^1]);
    }
}
