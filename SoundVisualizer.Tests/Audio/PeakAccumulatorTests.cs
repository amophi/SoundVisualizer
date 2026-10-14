// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Audio;

namespace SoundVisualizer.Tests.Audio;

public class PeakAccumulatorTests
{
    private readonly float[] _frame = new float[PeakAccumulator.ChannelCount];

    [Fact]
    public void 프레임_사이의_버퍼들은_채널마다_가장_큰_값만_남는다()
    {
        var acc = new PeakAccumulator();
        acc.Offer(0.9f, 0.1f, 0f, 0f, 0f, 0f, 0f, 0f); // 총성
        acc.Offer(0.05f, 0.3f, 0f, 0f, 0f, 0f, 0f, 0.2f); // 바로 뒤의 조용한 버퍼

        Assert.True(acc.TryTake(_frame));
        Assert.Equal(new[] { 0.9f, 0.3f, 0f, 0f, 0f, 0f, 0f, 0.2f }, _frame);
    }

    [Fact]
    public void 버퍼가_오지_않았으면_가져갈_것이_없고_이전_값을_건드리지_않는다()
    {
        var acc = new PeakAccumulator();
        Array.Fill(_frame, 0.5f);

        Assert.False(acc.TryTake(_frame));
        Assert.All(_frame, v => Assert.Equal(0.5f, v));
    }

    [Fact]
    public void 가져가면_비워진다()
    {
        var acc = new PeakAccumulator();
        acc.Offer(0.4f, 0.4f, 0.4f, 0.4f, 0.4f, 0.4f, 0.4f, 0.4f);
        Assert.True(acc.TryTake(_frame));

        Assert.False(acc.TryTake(_frame));
        acc.Offer(0.1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.True(acc.TryTake(_frame));
        Assert.Equal(0.1f, _frame[0]);
        Assert.Equal(0f, _frame[1]);
    }

    [Fact]
    public void 조용한_버퍼도_새_값으로_가져간다()
    {
        // 소리가 멈추면 0 이 바로 들어와야 그래픽이 줄어든다. "버퍼 없음" 과 구분한다.
        var acc = new PeakAccumulator();
        acc.Offer(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        Array.Fill(_frame, 0.7f);

        Assert.True(acc.TryTake(_frame));
        Assert.All(_frame, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Clear_는_모인_값을_버린다()
    {
        var acc = new PeakAccumulator();
        acc.Offer(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
        acc.Clear();
        Assert.False(acc.TryTake(_frame));
    }

    [Fact]
    public void NaN_은_버린다()
    {
        var acc = new PeakAccumulator();
        acc.Offer(float.NaN, 0.2f, 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.True(acc.TryTake(_frame));
        Assert.Equal(0f, _frame[0]);
        Assert.Equal(0.2f, _frame[1]);
    }

    [Fact]
    public void 여러_스레드가_동시에_넣어도_가장_큰_값이_남는다()
    {
        var acc = new PeakAccumulator();
        Parallel.For(0, 10_000, i =>
        {
            float v = i / 10_000f;
            acc.Offer(v, v, v, v, v, v, v, v);
        });

        Assert.True(acc.TryTake(_frame));
        Assert.All(_frame, v => Assert.Equal(9_999 / 10_000f, v));
    }

    [Fact]
    public void 받을_자리가_모자라면_거절한다()
    {
        var acc = new PeakAccumulator();
        Assert.Throws<ArgumentException>(() => acc.TryTake(new float[4]));
    }
}
