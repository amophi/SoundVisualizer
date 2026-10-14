// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>기준 데이터와의 비교(<see cref="QualcommSourceAudioPreprocessorGoldenTests"/>) 밖의 약속: 입력 길이 맞추기와 버퍼 재사용.</summary>
public class QualcommSourceAudioPreprocessorTests
{
    private const int Required = QualcommSourceAudioPreprocessor.RequiredMono16kSamples;
    private const int LogMelSize = QualcommSourceAudioPreprocessor.LogMelSize;

    /// <summary>고정 시드 잡음. 같은 입력을 두 길로 넣어 결과가 비트까지 같은지만 보므로 값 자체는 상관없다.</summary>
    private static float[] Noise(int length, int seed)
    {
        var random = new Random(seed);
        var samples = new float[length];
        for (int index = 0; index < length; index++)
        {
            samples[index] = (float)(random.NextDouble() - 0.5);
        }
        return samples;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10_000)]
    [InlineData(Required - 1)]
    public void 짧은_입력은_뒤를_0_으로_채운_것과_같다(int length)
    {
        float[] shortInput = Noise(length, seed: 1);
        var padded = new float[Required];
        shortInput.CopyTo(padded, 0);
        var preprocessor = new QualcommSourceAudioPreprocessor();

        Assert.Equal(preprocessor.ComputeLogMelSpectrogram(padded), preprocessor.ComputeLogMelSpectrogram(shortInput));
    }

    [Fact]
    public void 긴_입력은_앞_15600샘플만_쓴다()
    {
        float[] longInput = Noise(Required + 1_600, seed: 2);
        var preprocessor = new QualcommSourceAudioPreprocessor();

        Assert.Equal(
            preprocessor.ComputeLogMelSpectrogram(longInput.AsSpan(0, Required)),
            preprocessor.ComputeLogMelSpectrogram(longInput));
    }

    [Fact]
    public void 같은_인스턴스와_버퍼를_다시_써도_앞_호출이_섞이지_않는다()
    {
        // 짧은 입력 뒤에 긴 입력을 넣는다. 작업 버퍼에 앞 호출의 샘플이 남으면 결과가 달라진다.
        float[] first = Noise(Required, seed: 3);
        float[] expected = new QualcommSourceAudioPreprocessor().ComputeLogMelSpectrogram(first);
        var preprocessor = new QualcommSourceAudioPreprocessor();
        var buffer = new float[LogMelSize];

        preprocessor.ComputeLogMelSpectrogram(Noise(7_000, seed: 4), buffer);
        preprocessor.ComputeLogMelSpectrogram(first, buffer);

        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void 버퍼를_받는_쪽은_메모리를_새로_잡지_않는다()
    {
        // 추론은 250ms 마다 돈다. 호출마다 배열을 만들면 GC 가 캡처·렌더 스레드를 멈출 수 있다.
        var preprocessor = new QualcommSourceAudioPreprocessor();
        float[] input = Noise(Required, seed: 5);
        var buffer = new float[LogMelSize];
        preprocessor.ComputeLogMelSpectrogram(input, buffer); // 첫 호출의 준비 비용은 뺀다

        long before = GC.GetAllocatedBytesForCurrentThread();
        preprocessor.ComputeLogMelSpectrogram(input, buffer);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void 받을_자리가_모자라면_거절한다()
    {
        var preprocessor = new QualcommSourceAudioPreprocessor();

        Assert.Throws<ArgumentException>(() =>
            preprocessor.ComputeLogMelSpectrogram(new float[Required], new float[LogMelSize - 1]));
    }
}
