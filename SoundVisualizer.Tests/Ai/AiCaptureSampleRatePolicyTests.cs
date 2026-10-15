// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// Android 의 AiCaptureSampleRatePolicyTest.kt 를 옮겼다. Android 가 "AI 가 못 받는 레이트" 의 예로 쓰는 88.2~192kHz 를
/// PC 는 받으므로, 그 자리에는 PC 에서도 여전히 못 받는 레이트를 넣었다. PC 고레이트 자체의 확인은 뒤쪽에 따로 둔다.
/// </summary>
public class AiCaptureSampleRatePolicyTests
{
    public static TheoryData<int> UnsupportedRates => new() { 8000, 11025, 22050, 24000, 32000, 352800, 384000 };

    public static TheoryData<int> PcHighRates => new() { 88200, 96000, 176400, 192000 };

    [Theory]
    [MemberData(nameof(UnsupportedRates))]
    public void AI가_못_받는_레이트가_보고되면_받을_수_있는_레이트를_먼저_고른다(int reported)
    {
        Assert.Equal(new[] { 48000, 44100, reported }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(reported));

        int selected = AiCaptureSampleRatePolicy.SelectCaptureRate(reported, rate => rate == reported || rate == 48000);

        Assert.Equal(48000, selected);
        Assert.True(AiCaptureSampleRatePolicy.IsSupportedForAi(selected));
    }

    [Theory]
    [MemberData(nameof(UnsupportedRates))]
    public void 받을_수_있는_레이트가_없으면_보고된_레이트로_화면만_돈다(int reported)
    {
        int selected = AiCaptureSampleRatePolicy.SelectCaptureRate(reported, rate => rate == reported);

        Assert.Equal(reported, selected);
        Assert.False(AiCaptureSampleRatePolicy.IsSupportedForAi(selected));
    }

    [Fact]
    public void AI가_받는_레이트가_보고되면_그_레이트를_먼저_쓴다()
    {
        Assert.Equal(new[] { 44100, 48000 }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(44100));
        Assert.Equal(new[] { 48000, 44100 }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(48000));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void 안드로이드가_받는_레이트는_고정_크기_링에_들어간다(int rate)
    {
        Assert.True(AiCaptureSampleRatePolicy.IsSupportedForAi(rate));
        Assert.True(CaptureAudioMath.CaptureSamplesForOneYamnetWindow(rate) <= AiAudioBuffer.MaxRingSize);

        var buffer = new AiAudioBuffer(captureSampleRate: rate, channels: 2);

        Assert.Equal(AiAudioBuffer.MaxRingSize, buffer.RingCapacity);
    }

    [Fact]
    public void 고정_크기_링은_176_4kHz와_192kHz의_한_창도_담지_못한다()
    {
        // Android 는 그래서 이 레이트를 막았다. PC 는 링을 늘려 받는다(아래 PC 고레이트 테스트).
        Assert.True(CaptureAudioMath.CaptureSamplesForOneYamnetWindow(176400) > AiAudioBuffer.MaxRingSize);
        Assert.True(CaptureAudioMath.CaptureSamplesForOneYamnetWindow(192000) > AiAudioBuffer.MaxRingSize);
    }

    [Theory]
    [MemberData(nameof(UnsupportedRates))]
    public void 검증하지_않은_레이트는_AI_링에_들어오기_전에_거절한다(int rate)
    {
        Assert.False(AiCaptureSampleRatePolicy.IsSupportedForAi(rate));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiAudioBuffer(captureSampleRate: rate, channels: 2));
    }

    [Fact]
    public void 보고된_레이트가_없으면_48kHz와_44_1kHz만_시도하고_아무것도_안_되면_48kHz다()
    {
        Assert.Equal(new[] { 48000, 44100 }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(null));
        Assert.Equal(new[] { 48000, 44100 }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(0));
        Assert.Equal(new[] { 48000, 44100 }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(-1));
        Assert.Equal(AiCaptureSampleRatePolicy.DefaultCaptureSampleRate, AiCaptureSampleRatePolicy.SelectCaptureRate(null, _ => false));
    }

    // PC 에서 더한 것: 88.2·96·176.4·192kHz 도 AI 가 받는다.

    [Theory]
    [MemberData(nameof(PcHighRates))]
    public void PC_고레이트는_AI가_받고_보고된_레이트를_먼저_쓴다(int reported)
    {
        Assert.True(AiCaptureSampleRatePolicy.IsSupportedForAi(reported));
        Assert.Equal(new[] { reported, 48000, 44100 }, AiCaptureSampleRatePolicy.OrderedCaptureCandidates(reported));
        Assert.Equal(reported, AiCaptureSampleRatePolicy.SelectCaptureRate(reported, _ => true));
    }

    [Theory]
    [InlineData(88200, 262144)]
    [InlineData(96000, 262144)]
    [InlineData(176400, 524288)]
    [InlineData(192000, 524288)]
    public void PC_고레이트는_링을_늘려_한_창과_2초를_담는다(int rate, int expectedCapacity)
    {
        var buffer = new AiAudioBuffer(captureSampleRate: rate, channels: 2);

        Assert.Equal(expectedCapacity, buffer.RingCapacity);
        Assert.True(CaptureAudioMath.CaptureSamplesForOneYamnetWindow(rate) <= buffer.RingCapacity);
        Assert.True(rate * AiAudioBuffer.RingSeconds <= buffer.RingCapacity);
    }

    [Fact]
    public void PC_192kHz_링은_2초만_남기고_최근_한_창을_순서대로_돌려준다()
    {
        const int rate = 192000;
        var buffer = new AiAudioBuffer(captureSampleRate: rate, channels: 1);
        int total = rate * 3; // 링(524,288)을 한 바퀴 넘게 돈다
        var chunk = new float[rate / 100]; // 10ms 씩
        for (int written = 0; written < total; written += chunk.Length)
        {
            for (int i = 0; i < chunk.Length; i++) chunk[i] = written + i;
            buffer.IngestInterleaved(chunk);
        }

        Assert.Equal(rate * AiAudioBuffer.RingSeconds, buffer.AvailableSamples);
        Assert.True(buffer.HasEnoughForYamnetWindow());

        var window = new float[CaptureAudioMath.CaptureSamplesForOneYamnetWindow(rate)];
        int available = buffer.CopyTailRightPadded(window);

        Assert.Equal(rate * AiAudioBuffer.RingSeconds, available);
        for (int i = 0; i < window.Length; i++)
        {
            Assert.Equal((float)(total - window.Length + i), window[i]);
        }
    }
}
