// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Audio;

namespace SoundVisualizer.Tests.Audio;

/// <summary>
/// 개발자 모드 HUD·결과 기록의 소리 크기(모바일 AudioEngine.currentLevel). 이 값이 틀리면 채점할 때 "조용해서 안 그려졌다" 와
/// "AI 가 못 잡았다" 를 거꾸로 읽는다.
/// </summary>
public class CaptureLevelTests
{
    [Fact]
    public void 버퍼를_받기_전에는_0이다()
    {
        Assert.Equal(0f, new CaptureLevel().Read(nowMs: 1_000L));
    }

    [Fact]
    public void 가장_최근_버퍼의_채널_가운데_가장_큰_피크를_읽는다()
    {
        var level = new CaptureLevel();
        level.Offer([0.1f, 0.7f, 0.2f, 0f, 0f, 0f, 0f, 0f], nowMs: 1_000L);
        Assert.Equal(0.7f, level.Read(1_000L));

        // LFE 만 울려도 AI 무음 게이트는 소리가 있다고 본다. 그리는 쪽과 달리 이 값도 그 채널을 센다.
        level.Offer([0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.4f], nowMs: 1_010L);
        Assert.Equal(0.4f, level.Read(1_010L));
    }

    [Fact]
    public void 앞_버퍼의_큰_값을_모으지_않는다()
    {
        // PeakAccumulator 와 다른 점이다. 지금 순간의 크기라 총성 뒤의 조용한 버퍼가 오면 바로 작아진다.
        var level = new CaptureLevel();
        level.Offer([0.9f, 0.9f], nowMs: 1_000L);
        level.Offer([0.05f, 0.02f], nowMs: 1_010L);

        Assert.Equal(0.05f, level.Read(1_010L));
    }

    [Fact]
    public void 읽어도_비우지_않는다()
    {
        var level = new CaptureLevel();
        level.Offer([0.3f, 0.6f], nowMs: 1_000L);

        Assert.Equal(0.6f, level.Read(1_050L));
        Assert.Equal(0.6f, level.Read(1_100L));
    }

    [Fact]
    public void NaN_인_채널은_건너뛴다()
    {
        var level = new CaptureLevel();
        level.Offer([float.NaN, 0.25f, float.NaN], nowMs: 1_000L);
        Assert.Equal(0.25f, level.Read(1_000L));

        level.Offer([float.NaN, float.NaN], nowMs: 1_010L);
        Assert.Equal(0f, level.Read(1_010L));
    }

    [Fact]
    public void 버퍼가_끊기면_정한_시간이_지난_뒤부터_0으로_읽는다()
    {
        // WASAPI 루프백은 아무것도 재생하지 않으면 버퍼를 보내지 않는다. 마지막 값이 남으면 조용한데도 소리가 나는 것처럼 보인다.
        var level = new CaptureLevel();
        level.Offer([0.5f, 0.5f], nowMs: 10_000L);

        Assert.Equal(0.5f, level.Read(10_000L + CaptureLevel.StaleAfterMs));
        Assert.Equal(0f, level.Read(10_000L + CaptureLevel.StaleAfterMs + 1));

        // 다시 소리가 나면 바로 읽힌다.
        level.Offer([0.2f, 0.1f], nowMs: 20_000L);
        Assert.Equal(0.2f, level.Read(20_000L));
    }
}
