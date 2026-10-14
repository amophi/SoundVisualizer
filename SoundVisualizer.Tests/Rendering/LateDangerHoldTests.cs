// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tests.Rendering;

public class LateDangerHoldTests
{
    private const float Threshold = 0.01f;
    private const int Newest = LateDangerHold.Slots - 1; // 링에서 가장 최근 칸
    private const int Current = LateDangerHold.Slots;    // 채우는 중인 칸

    private static float[] Frame(float fl, float fr, float sl = 0) => new[] { fl, fr, 0f, 0f, 0f, sl, 0f, 0f };

    [Fact]
    public void 문턱_이하의_소리는_보관하지_않는다()
    {
        var hold = new LateDangerHold();
        hold.Keep(Frame(0.01f, 0.005f), level: 0.01f, Threshold);
        Assert.False(hold.HasData);
    }

    [Fact]
    public void 칸마다_가장_큰_프레임_하나를_통째로_남긴다()
    {
        // 채널마다 최댓값을 따로 남기면 왼쪽 소리와 오른쪽 소리가 섞여 방향이 흐려진다.
        var hold = new LateDangerHold();
        hold.Keep(Frame(0.5f, 0.1f), level: 0.5f, Threshold);
        hold.Keep(Frame(0.05f, 0.6f), level: 0.6f, Threshold);
        hold.Keep(Frame(0.3f, 0.3f), level: 0.3f, Threshold);

        Assert.Equal(Frame(0.05f, 0.6f), hold.Slot(Current).ToArray());
        Assert.Equal(0.6f, hold.SlotLevel(Current));

        hold.Advance(LateDangerHold.SlotFrames);
        Assert.Equal(Frame(0.05f, 0.6f), hold.Slot(Newest).ToArray());
        Assert.All(hold.Slot(Current).ToArray(), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void 오래된_칸부터_읽는다()
    {
        var hold = new LateDangerHold();
        hold.Keep(Frame(0.2f, 0f), 0.2f, Threshold);
        hold.Advance(2);
        hold.Keep(Frame(0f, 0.4f), 0.4f, Threshold);
        hold.Advance(2);

        Assert.Equal(Frame(0.2f, 0f), hold.Slot(Newest - 1).ToArray());
        Assert.Equal(Frame(0f, 0.4f), hold.Slot(Newest).ToArray());
        Assert.Equal(0.2f, hold.SlotLevel(Newest - 1));
        Assert.Equal(0.4f, hold.SlotLevel(Newest));
    }

    [Fact]
    public void 시간을_조금씩_흘려도_칸_길이마다_넘어간다()
    {
        var hold = new LateDangerHold();
        hold.Keep(Frame(0.5f, 0.5f), 0.5f, Threshold);
        hold.Advance(0.5);
        hold.Advance(0.5);
        hold.Advance(0.5);
        Assert.Equal(0.5f, hold.SlotLevel(Current)); // 1.5 프레임: 아직 지금 칸
        hold.Advance(0.5);
        Assert.Equal(0.5f, hold.SlotLevel(Newest));  // 2 프레임: 링으로
    }

    [Fact]
    public void 보관한_소리는_약_1초_뒤_모두_밀려난다()
    {
        // 지금 칸 + 링 한 바퀴(30칸) = 31칸 × 2프레임 ≈ 1.03초
        var hold = new LateDangerHold();
        hold.Keep(Frame(0.9f, 0.9f), 0.9f, Threshold);
        for (int i = 0; i < LateDangerHold.Slots; i++) hold.Advance(LateDangerHold.SlotFrames);
        Assert.True(hold.HasData);
        Assert.Equal(0.9f, hold.SlotLevel(0)); // 가장 오래된 칸에 남아 있다

        hold.Advance(LateDangerHold.SlotFrames);
        Assert.False(hold.HasData);
        for (int n = 0; n <= LateDangerHold.Slots; n++) Assert.Equal(0f, hold.SlotLevel(n));
    }

    [Fact]
    public void 비우면_다시_보관할_수_있다()
    {
        var hold = new LateDangerHold();
        hold.Keep(Frame(0.9f, 0.9f), 0.9f, Threshold);
        hold.Advance(5);
        hold.Clear();
        Assert.False(hold.HasData);
        for (int n = 0; n <= LateDangerHold.Slots; n++) Assert.Equal(0f, hold.SlotLevel(n));

        hold.Keep(Frame(0.3f, 0f), 0.3f, Threshold);
        Assert.True(hold.HasData);
        Assert.Equal(0.3f, hold.SlotLevel(Current));
    }
}
