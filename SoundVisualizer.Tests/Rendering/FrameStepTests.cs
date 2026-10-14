// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tests.Rendering;

public class FrameStepTests
{
    [Fact]
    public void 프레임_사이_시간을_60fps_프레임_수로_잰다()
    {
        Assert.Equal(1.0, FrameStep.FromElapsed(FrameStep.ReferenceFrameMs));
        Assert.Equal(2.0, FrameStep.FromElapsed(1000.0 / 30.0), 9);
        Assert.Equal(60.0 / 144.0, FrameStep.FromElapsed(1000.0 / 144.0), 9);
    }

    [Fact]
    public void 멈췄다_돌아온_프레임은_4프레임까지만_진행한다()
    {
        Assert.Equal(FrameStep.MaxStep, FrameStep.FromElapsed(1000));
        Assert.Equal(4.0, FrameStep.MaxStep);
    }

    [Fact]
    public void 주사율이_240Hz_를_넘는_모니터도_자르지_않는다()
    {
        // PC 는 최대 프레임을 모니터 주사율까지 올린다. Android 처럼 0.25 에서 자르면 360Hz 에서 1.5배 빨라진다.
        Assert.Equal(1.0 / 6.0, FrameStep.FromElapsed(1000.0 / 360.0), 9);
    }

    [Fact]
    public void 시간이_흐르지_않았으면_진행하지_않는다()
    {
        Assert.Equal(0.0, FrameStep.FromElapsed(0));
        Assert.Equal(0.0, FrameStep.FromElapsed(-3));
        Assert.Equal(0.0, FrameStep.FromElapsed(double.NaN));
        Assert.Equal(1f, FrameStep.Decay(0.87f, 0));
        Assert.Equal(0.0, FrameStep.Approach(0.3, 0));
    }

    [Fact]
    public void 한_프레임이면_예전_계수를_그대로_쓴다()
    {
        Assert.Equal(0.87f, FrameStep.Decay(0.87f, 1.0));
        Assert.Equal(0.3, FrameStep.Approach(0.3, 1.0));
    }

    [Fact]
    public void 감쇠는_나눠_가도_한_번에_가도_같다()
    {
        float twoSteps = FrameStep.Decay(0.87f, 0.4) * FrameStep.Decay(0.87f, 1.6);
        Assert.Equal(FrameStep.Decay(0.87f, 2.0), twoSteps, 6);
        Assert.Equal(0.87f * 0.87f, FrameStep.Decay(0.87f, 2.0), 6);
    }

    [Fact]
    public void 따라붙기는_나눠_가도_한_번에_가도_같다()
    {
        // 0 에서 1 로 따라붙는 값을 0.4 + 1.6 프레임과 2 프레임으로 나눠 본다.
        double split = 0;
        split += (1 - split) * FrameStep.Approach(0.25, 0.4);
        split += (1 - split) * FrameStep.Approach(0.25, 1.6);

        double once = (1 - 0.0) * FrameStep.Approach(0.25, 2.0);

        double perFrame = 0;
        perFrame += (1 - perFrame) * 0.25;
        perFrame += (1 - perFrame) * 0.25;

        Assert.Equal(once, split, 12);
        Assert.Equal(perFrame, once, 12);
    }

    [Fact]
    public void 계수가_1이면_바로_붙고_0이면_움직이지_않는다()
    {
        Assert.Equal(1.0, FrameStep.Approach(1.0, 0.5));
        Assert.Equal(0.0, FrameStep.Approach(0.0, 3.0));
    }

    [Fact]
    public void 시계는_첫_프레임과_다시_시작한_프레임을_한_프레임으로_센다()
    {
        var clock = new FrameClock();
        Assert.Equal(1.0, clock.Next(5_000)); // 처음 시각이 얼마든 한 프레임
        Assert.Equal(2.0, clock.Next(5_000 + 1000.0 / 30.0), 9);
        Assert.Equal(FrameStep.MaxStep, clock.Next(9_000)); // 4초 멈춤

        clock.Reset();
        Assert.Equal(1.0, clock.Next(60_000)); // 쉬다 깨어난 첫 프레임은 쉰 시간만큼 건너뛰지 않는다
    }
}
