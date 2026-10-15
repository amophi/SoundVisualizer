// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;
using static SoundVisualizer.Tests.Rendering.EngineHarness;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>사운드 모드 2.0 의 Mid/Side 펼치기와 뒤쪽 지연(Android VisualizerEngine 의 upmix·공간 리플).</summary>
public class StereoUpmixTests
{
    // 둘레 자리
    private const int TopCenter = 0, TopRight = 1, RightCenter = 2, BottomRight = 3, BottomCenter = 4, BottomLeft = 5, LeftCenter = 6, TopLeft = 7;

    private static EngineHarness Stereo2(VisualModeSettings? settings = null) =>
        new() { SoundMode = 0, Settings = settings ?? new VisualModeSettings { Sensitivity = 15 } };

    // ---------------------------------------------------------------
    // 펼치기
    // ---------------------------------------------------------------

    [Fact]
    public void 가운데_소리는_앞에_서고_옆은_비운다()
    {
        var p = new float[8];
        float total = StereoUpmix.Spread(0.5f, 0.5f, 0.5f, 0.5f, p);

        Assert.Equal(0.6f, p[TopCenter], 6);
        Assert.Equal(0.45f, p[TopRight], 6);
        Assert.Equal(0.45f, p[TopLeft], 6);
        Assert.Equal(0f, p[RightCenter]);
        Assert.Equal(0f, p[LeftCenter]);
        Assert.Equal(0f, p[BottomRight]);
        Assert.Equal(0f, p[BottomLeft]);
        Assert.Equal(0.4f, p[BottomCenter], 6);
        Assert.Equal(1.9f, total, 5);
    }

    [Fact]
    public void 한쪽에서만_나는_소리는_그쪽_옆으로만_펼친다()
    {
        var p = new float[8];
        float total = StereoUpmix.Spread(1f, 0f, 1f, 0f, p);

        Assert.Equal(0.9f, p[TopLeft], 6);
        Assert.Equal(1.5f, p[LeftCenter], 6);
        Assert.Equal(1.2f, p[BottomLeft], 6);
        foreach (int i in new[] { TopCenter, TopRight, RightCenter, BottomRight, BottomCenter }) Assert.Equal(0f, p[i]);
        Assert.Equal(3.6f, total, 5);
    }

    [Fact]
    public void 뒤쪽_자리는_늦은_좌우로_그린다()
    {
        var p = new float[8];
        StereoUpmix.Spread(0.8f, 0.2f, 0f, 0f, p);
        Assert.True(p[LeftCenter] > 0);
        Assert.Equal(0f, p[BottomLeft]);
        Assert.Equal(0f, p[BottomCenter]);
        Assert.Equal(0f, p[BottomRight]);
    }

    // ---------------------------------------------------------------
    // 뒤쪽 지연
    // ---------------------------------------------------------------

    [Fact]
    public void 뒤쪽은_60fps_네_프레임_전의_좌우를_쓴다()
    {
        // Android 는 5칸 히스토리의 가장 오래된 값, 곧 네 틱 전 값을 쓴다. 60fps 에서는 같아야 한다.
        var upmix = new StereoUpmix();
        for (int n = 0; n < 12; n++)
        {
            upmix.Push(n * Frame60, n, -n, out float rearL, out float rearR);
            float expected = n >= 4 ? n - 4 : 0;
            Assert.Equal(expected, rearL);
            Assert.Equal(-expected, rearR);
        }
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(60.0)]
    [InlineData(144.0)]
    [InlineData(240.0)]
    [InlineData(360.0)]
    public void 뒤쪽_지연은_최대_프레임과_상관없이_약_67ms_다(double fps)
    {
        double interval = 1000.0 / fps;
        var upmix = new StereoUpmix();
        double soundAt = 10 * interval;
        double rearAt = double.NaN;
        for (int n = 0; n < 200 && double.IsNaN(rearAt); n++)
        {
            double t = n * interval;
            float value = t >= soundAt ? 1f : 0f;
            upmix.Push(t, value, value, out float rearL, out _);
            if (rearL > 0) rearAt = t;
        }
        double delay = rearAt - soundAt;
        Assert.InRange(delay, StereoUpmix.RearDelayMs - 1.01, StereoUpmix.RearDelayMs + interval);
    }

    [Fact]
    public void 지연만큼_오래된_프레임이_없으면_뒤쪽은_0이다()
    {
        var upmix = new StereoUpmix();
        upmix.Push(0, 0.9f, 0.9f, out float l, out float r);
        Assert.Equal(0f, l);
        Assert.Equal(0f, r);
        upmix.Push(30, 0.9f, 0.9f, out l, out r);
        Assert.Equal(0f, l);

        upmix.Push(70, 0.5f, 0.5f, out l, out r);
        Assert.Equal(0.9f, l); // 70ms 전의 값

        upmix.Clear();
        upmix.Push(5000, 0.5f, 0.5f, out l, out r);
        Assert.Equal(0f, l); // 비운 뒤로는 지난 프레임이 없다
    }

    // ---------------------------------------------------------------
    // 엔진의 2.0 모드
    // ---------------------------------------------------------------

    [Fact]
    public void 왼쪽에서_소리가_나면_왼쪽이_더_깊다()
    {
        var run = Stereo2();
        run.Stereo(1f, 0.05f);
        run.Advance(300);

        var d = run.Engine.Depths;
        Assert.True(d[LeftCenter] > d[RightCenter], $"좌측({d[LeftCenter]}) 이 우측({d[RightCenter]}) 보다 깊어야 한다");
        Assert.True(d[TopLeft] > d[TopRight]);
        Assert.True(d[BottomLeft] > d[BottomRight]);
    }

    [Fact]
    public void 좌우가_같으면_좌우가_대칭이다()
    {
        var run = Stereo2();
        run.Stereo(0.7f, 0.7f);
        run.Advance(300);

        var d = run.Engine.Depths;
        Assert.Equal(d[LeftCenter], d[RightCenter], 9);
        Assert.Equal(d[TopLeft], d[TopRight], 9);
        Assert.Equal(d[BottomLeft], d[BottomRight], 9);
    }

    [Fact]
    public void 가운데_소리는_앞_가운데가_가장_깊고_양옆에는_그리지_않는다()
    {
        // 예전 2.0 은 좌우를 옆으로 복사해 가운데 소리가 양옆에서 똑같이 그려졌다. 양쪽에서 나는 소리와 구별되지 않았다.
        var run = Stereo2();
        run.Stereo(0.6f, 0.6f);
        run.Advance(300);

        var d = run.Engine.Depths.ToArray();
        Assert.Equal(d.Max(), d[TopCenter]);
        Assert.True(d[TopCenter] > 3);
        Assert.True(d[LeftCenter] < 0.01, $"좌측 {d[LeftCenter]}");
        Assert.True(d[RightCenter] < 0.01, $"우측 {d[RightCenter]}");
    }

    [Fact]
    public void 한쪽으로_치우친_소리는_반대쪽_옆을_비운다()
    {
        var run = Stereo2();
        run.Stereo(0.6f, 0.2f);
        run.Advance(300);

        var d = run.Engine.Depths;
        Assert.True(d[LeftCenter] > 3);
        Assert.True(d[RightCenter] < 0.01, $"우측 {d[RightCenter]}");
        Assert.True(d[TopLeft] > d[TopRight] * 2.9, $"좌상단 {d[TopLeft]} / 우상단 {d[TopRight]}");
    }

    [Fact]
    public void 뒤쪽은_앞보다_조금_늦게_따라온다()
    {
        var run = Stereo2(new VisualModeSettings { Sensitivity = 100, PositionSpeed = 100 });
        run.Stereo(0.5f, 0.5f);
        run.Advance(2); // 33ms
        Assert.True(run.Engine.Depths[TopCenter] > 0);
        Assert.Equal(0, run.Engine.Depths[BottomCenter]);

        run.Advance(4); // 100ms
        Assert.True(run.Engine.Depths[BottomCenter] > 0);
    }

    [Fact]
    public void 앞_좌우_말고는_보지_않는다()
    {
        // 2.0 에서 그리는 것은 앞 좌우뿐이다. 다른 채널만 울리면 그릴 것이 없고 쉰다.
        var run = Stereo2();
        run.Peaks[2] = 0.9f; // FC
        run.Peaks[5] = 0.9f; // SL
        run.Peaks[7] = 0.9f; // LFE
        run.Advance(30);
        Assert.All(run.Engine.Depths.ToArray(), v => Assert.Equal(0, v));
        Assert.False(run.Engine.Visible);
        run.TickUntilIdle();
    }

    [Fact]
    public void 사운드_모드를_바꾸면_방향_분포를_고르게_놓고_새로_따라간다()
    {
        // 5.1·7.1 의 분포는 채널 순서, 2.0 의 분포는 둘레 자리 순서라 그대로 이어 쓰면 바꾼 직후 엉뚱한 자리가 깊어진다.
        var run = new EngineHarness { SoundMode = 2, Settings = new VisualModeSettings { Sensitivity = 15 } };
        run.Peaks[0] = 1f; // FL
        run.Advance(300);
        Assert.True(run.Engine.Distribution[0] > 0.9); // 채널 순서의 FL

        run.SoundMode = 0;
        run.Stereo(1f, 0f);
        run.Tick();
        var d = run.Engine.Depths;
        Assert.True(d[LeftCenter] > d[TopCenter], $"바꾼 직후 위 가운데({d[TopCenter]})가 왼쪽({d[LeftCenter]}) 보다 깊다");
    }

    [Fact]
    public void 쉬다가_깨어나면_뒤쪽_지연을_새로_시작한다()
    {
        // 숨긴 종류의 소리가 나는 채로 쉬면 지연 히스토리에 그 소리가 남는다. 표시를 켜 깨어난 첫 프레임에 1초 넘게 지난
        // 그 소리로 뒤쪽을 그리면 안 된다.
        var run = Stereo2(new VisualModeSettings { Sensitivity = 100, PositionSpeed = 100 });
        run.Shown = false;
        run.Stereo(0.5f, 0.5f);
        run.TickUntilIdle();

        run.Shown = true;
        Assert.True(run.Poll());
        run.Tick();
        Assert.Equal(0, run.Engine.Depths[BottomCenter]);
        Assert.True(run.Engine.Depths[TopCenter] > 0);

        run.Advance(5); // 지연(약 67ms)이 지나면 뒤쪽도 그린다
        Assert.True(run.Engine.Depths[BottomCenter] > 0);
    }

    [Fact]
    public void 늦게_온_위협음_판정에_제때_표시했을_크기로_보인다()
    {
        // Android 와 같은 소리(좌우 0.9 한 번, 잔향 0.03)로, 2.0 의 펼치기를 거친 크기를 견준다.
        var run = new EngineHarness { SoundMode = 0, Label = SoundType.Ambient, Shown = false };
        run.TickUntilIdle();
        run.Stereo(0.9f, 0.9f);
        run.Poll();
        run.Stereo(0.03f, 0.03f);
        run.Polls(10);

        run.BecomeDanger();
        Assert.True(run.Poll());
        double onTime = OnTimePeakSmoothTotal(0.9f, 2, 0.03f, 20, soundMode: 0);
        Assert.InRange(run.Engine.SmoothTotal, onTime * 0.8, onTime * 1.2);

        run.Advance(3);
        Assert.True(run.Engine.Visible);
    }
}
