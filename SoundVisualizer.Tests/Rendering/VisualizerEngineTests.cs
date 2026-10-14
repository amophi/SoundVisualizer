// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Rendering;
using static SoundVisualizer.Tests.Rendering.EngineHarness;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>오버레이 엔진의 스무딩·깊이(Android VisualizerEngineTest 의 해당 부분을 PC 값으로 옮겼다).</summary>
public class VisualizerEngineTests
{
    // 캡처 채널 번호
    private const int FL = 0, FR = 1, FC = 2, BL = 3, BR = 4, SL = 5, SR = 6;

    // ---------------------------------------------------------------
    // 피크 유지와 감쇠
    // ---------------------------------------------------------------

    [Fact]
    public void 새_버퍼가_오면_전체_크기가_올라간다()
    {
        var run = new EngineHarness();
        run.All(0.9f);
        run.Advance(30);
        Assert.True(run.Engine.SmoothTotal > 0);
    }

    [Fact]
    public void 버퍼가_끊기면_전체_크기가_줄어든다()
    {
        var run = new EngineHarness();
        run.All(0.9f);
        run.Advance(30);
        double loud = run.Engine.SmoothTotal;

        run.Buffers = false;
        run.Advance(60);
        Assert.True(run.Engine.SmoothTotal < loud, $"{run.Engine.SmoothTotal} < {loud}");
    }

    // ---------------------------------------------------------------
    // 시간 정규화: 프레임 간격이 달라도 같은 시간에 같은 결과
    // ---------------------------------------------------------------

    [Fact]
    public void 프레임_간격이_두_배여도_같은_시간_뒤_스무딩_결과가_같다()
    {
        var settings = new VisualModeSettings { Sensitivity = 10, PositionSpeed = 30 };

        var fast = new EngineHarness { Settings = settings };
        fast.Peaks[FL] = 0.8f;
        fast.Peaks[FR] = 0.4f;
        fast.Peaks[SL] = 0.3f;
        // 첫 프레임은 둘 다 1 프레임이다. 그 뒤로 fast 는 1 프레임씩 20번, slow 는 2 프레임씩 10번이라
        // 둘 다 60fps 기준 21 프레임 분량이다.
        fast.Advance(21, Frame60);

        var slow = new EngineHarness { Settings = settings };
        fast.Peaks.CopyTo(slow.Peaks, 0);
        slow.Advance(11, Frame60 * 2);

        double a = fast.Engine.SmoothTotal, b = slow.Engine.SmoothTotal;
        Assert.True(a > 0, "스무딩이 진행되지 않았다");
        Assert.Equal(a, b, 9);
        for (int i = 0; i < VisualizerEngine.Channels; i++)
        {
            Assert.Equal(fast.Engine.Distribution[i], slow.Engine.Distribution[i], 9);
        }
        for (int p = 0; p < VisualizerEngine.Positions; p++)
        {
            Assert.Equal(fast.Engine.Depths[p], slow.Engine.Depths[p], 6);
        }
    }

    [Fact]
    public void 버퍼가_끊긴_뒤_줄어드는_빠르기는_최대_프레임과_상관없다()
    {
        float Remaining(double fps)
        {
            var run = new EngineHarness();
            run.All(0.5f);
            run.Advance(10, 1000.0 / fps);
            run.Buffers = false;
            run.Advance((int)fps, 1000.0 / fps); // 1초
            return run.Engine.TotalTarget;
        }

        // 1초 = 60fps 60 프레임 → 0.87^60. 채널 값이 float 라 곱할 때마다 생기는 반올림만큼은 봐준다.
        double expected = 8 * 0.5 * Math.Pow(0.87, 60);
        foreach (double fps in new[] { 30.0, 60.0, 144.0, 240.0 })
        {
            Assert.InRange(Remaining(fps), expected * (1 - 1e-4), expected * (1 + 1e-4));
        }
    }

    [Fact]
    public void 패드와_원형은_최대_프레임과_상관없이_같은_시간에_같은_크기가_된다()
    {
        // 민감도·속도를 100 으로 두어 깊이가 첫 프레임부터 고정되게 하고, 패드·원형의 따라붙기만 본다.
        var settings = new VisualModeSettings { Sensitivity = 100, PositionSpeed = 100 };
        foreach (int mode in new[] { VisualizerEngine.ModePad, VisualizerEngine.ModeCircle })
        {
            EngineHarness RunFor(double fps, int ticks)
            {
                var run = new EngineHarness { Settings = settings, VisualMode = mode };
                run.Peaks[FL] = 0.6f;
                run.Peaks[SL] = 0.3f;
                run.Peaks[FC] = 0.1f;
                run.Advance(ticks, 1000.0 / fps);
                return run;
            }

            // 첫 프레임 + 0.5초: 30fps 는 1 + 15×2, 144fps 는 1 + 72×(60/144), 둘 다 31 프레임 분량
            var slow = RunFor(30, 16);
            var fast = RunFor(144, 73);
            var slowValues = mode == VisualizerEngine.ModePad ? slow.Engine.PadThickness : slow.Engine.CircleDepths;
            var fastValues = mode == VisualizerEngine.ModePad ? fast.Engine.PadThickness : fast.Engine.CircleDepths;
            Assert.True(slowValues[7] > 1, $"모드 {mode}: 움직이지 않았다");
            for (int p = 0; p < VisualizerEngine.Positions; p++)
            {
                Assert.Equal(slowValues[p], fastValues[p], 6);
            }
        }
    }

    [Fact]
    public void 애니메이션_시각은_흐른_시간만큼_간다()
    {
        // 첫 프레임 + 1초 = 61 프레임 분량. 보이는 것 없이 1초가 지나면 쉬므로 소리를 넣어 둔다.
        var slow = new EngineHarness();
        slow.All(0.5f);
        slow.Advance(31, 1000.0 / 30);
        var fast = new EngineHarness();
        fast.All(0.5f);
        fast.Advance(121, 1000.0 / 120);

        Assert.Equal(VisualizerEngine.AnimationStep * 61, slow.Engine.AnimationTime, 9);
        Assert.Equal(VisualizerEngine.AnimationStep * 61, fast.Engine.AnimationTime, 9);
    }

    [Fact]
    public void 화면이_60fps_면_예전처럼_프레임마다_계수를_그대로_곱한다()
    {
        // 시간 정규화 전 MainWindow 의 계산을 그대로 옮긴 것과 견준다. 60fps 화면에서 보이는 모양이 바뀌면 안 된다.
        var settings = new VisualModeSettings { Sensitivity = 3.75, PositionSpeed = 20 };
        var run = new EngineHarness { Settings = settings, KeepAwake = true }; // 소리가 그친 뒤의 계산도 견주게 쉬지 않는다
        var legacy = new LegacyFrameLoop();
        var rnd = new Random(60);

        for (int frame = 0; frame < 240; frame++)
        {
            bool buffer = frame < 90 || rnd.Next(3) == 0;
            if (buffer)
            {
                for (int i = 0; i < VisualizerEngine.Channels; i++) run.Peaks[i] = frame < 90 ? (float)rnd.NextDouble() : 0;
            }
            run.Buffers = buffer;
            run.Tick();
            legacy.Frame(buffer ? run.Peaks : null, settings);

            Assert.Equal(legacy.SmoothTotal, run.Engine.SmoothTotal, 9);
            for (int i = 0; i < VisualizerEngine.Channels; i++) Assert.Equal(legacy.Dist[i], run.Engine.Distribution[i], 9);
        }
    }

    /// <summary>시간 정규화 전 MainWindow.RenderFrame 의 스무딩. 버퍼가 오면 바꾸고, 그린 뒤 프레임마다 0.87 을 곱했다.</summary>
    private sealed class LegacyFrameLoop
    {
        private readonly float[] _targets = new float[8];
        public double SmoothTotal;
        public readonly double[] Dist = { 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125 };

        public void Frame(float[]? peaks, VisualModeSettings s)
        {
            if (peaks != null) peaks.CopyTo(_targets, 0);
            float total = 0;
            for (int i = 0; i < 8; i++) total += _targets[i];
            float sfTremor = (float)(Math.Max(0.1, s.Sensitivity) / 100.0);
            if (sfTremor > 1.0f) sfTremor = 1.0f;
            SmoothTotal += (total - SmoothTotal) * sfTremor;
            float sfPosition = (float)(Math.Max(0.1, s.PositionSpeed) / 100.0);
            if (sfPosition > 1.0f) sfPosition = 1.0f;
            if (total > 0.0001f)
            {
                for (int i = 0; i < 8; i++) Dist[i] += ((_targets[i] / total) - Dist[i]) * sfPosition;
            }
            for (int i = 0; i < 8; i++) _targets[i] *= 0.87f;
        }
    }

    // ---------------------------------------------------------------
    // 깊이
    // ---------------------------------------------------------------

    [Fact]
    public void 크기_100이면_baseDepth_가_화면_중앙_한계선까지_닿는다()
    {
        var run = new EngineHarness { Settings = new VisualModeSettings { Intensity = 100 } };
        run.Tick();
        Assert.Equal(MaxBase, run.Engine.BaseDepth, 9);
    }

    [Fact]
    public void baseDepth_는_크기_설정에_비례한다()
    {
        var run = new EngineHarness { Settings = new VisualModeSettings { Intensity = 50 } };
        run.Tick();
        Assert.Equal(MaxBase / 2, run.Engine.BaseDepth, 9);
    }

    [Fact]
    public void 크기_고정_모드는_고정_크기의_절반을_쓴다()
    {
        var run = new EngineHarness
        {
            Settings = new VisualModeSettings { IntensityAsOpacity = true, OpacityFixedSize = 30, Intensity = 100 },
        };
        run.Tick();
        Assert.Equal(MaxBase * 0.15, run.Engine.BaseDepth, 9);
    }

    [Fact]
    public void 깊이는_baseDepth_를_넘지_않는다()
    {
        // 아주 큰 소리를 길게 넣어 스무딩이 포화되게 만든다. 민감도 25 는 편집 패널의 100 이다.
        foreach (int soundMode in new[] { 0, 1, 2 })
        {
            var run = new EngineHarness { Settings = new VisualModeSettings { Intensity = 100, Sensitivity = 25 }, SoundMode = soundMode };
            run.All(1f);
            run.Advance(600);
            foreach (double d in run.Engine.Depths)
            {
                Assert.InRange(d, 0, run.Engine.BaseDepth + 0.001);
            }
        }
    }

    [Fact]
    public void 무음에서는_깊이가_없다()
    {
        var run = new EngineHarness();
        run.Advance(10);
        Assert.All(run.Engine.Depths.ToArray(), d => Assert.Equal(0, d));
    }

    [Fact]
    public void 민감도가_높으면_더_빨리_반응한다()
    {
        double SmoothAfter(double sensitivity)
        {
            var run = new EngineHarness { Settings = new VisualModeSettings { Sensitivity = sensitivity } };
            run.All(1f);
            run.Advance(10);
            return run.Engine.SmoothTotal;
        }
        Assert.True(SmoothAfter(20) > SmoothAfter(1.25));
    }

    [Fact]
    public void 서라운드에서_왼쪽_채널이_크면_왼쪽_자리가_더_깊다()
    {
        var run = new EngineHarness { Settings = new VisualModeSettings { Sensitivity = 15 } };
        run.Peaks[FL] = 1f;
        run.Peaks[SL] = 1f;
        run.Peaks[BL] = 1f;
        run.Peaks[FR] = 0.05f;
        run.Peaks[SR] = 0.05f;
        run.Peaks[BR] = 0.05f;
        run.Advance(300);

        var d = run.Engine.Depths;
        Assert.True(d[7] > d[1], $"좌상단 {d[7]} > 우상단 {d[1]}");
        Assert.True(d[6] > d[2], $"좌측 {d[6]} > 우측 {d[2]}");
        Assert.True(d[5] > d[3], $"좌하단 {d[5]} > 우하단 {d[3]}");
    }

    [Fact]
    public void 패드와_원형은_그리는_모드에서만_움직인다()
    {
        var run = new EngineHarness { VisualMode = VisualizerEngine.ModeWave };
        run.All(1f);
        run.Advance(60);
        Assert.All(run.Engine.PadThickness.ToArray(), t => Assert.Equal(0, t));
        Assert.All(run.Engine.CircleDepths.ToArray(), t => Assert.Equal(0, t));

        run.VisualMode = VisualizerEngine.ModePad;
        run.Advance(60);
        Assert.Contains(run.Engine.PadThickness.ToArray(), t => t > 0.5);
        Assert.All(run.Engine.CircleDepths.ToArray(), t => Assert.Equal(0, t));
    }

    [Fact]
    public void 패드_두께는_55에서_자른다()
    {
        // 크기 100 의 상단 가운데 깊이는 530 이라 그 0.25 배(132.5)가 한도를 넘는다.
        var pad = new EngineHarness
        {
            Settings = new VisualModeSettings { Intensity = 100, Sensitivity = 100, PositionSpeed = 100 },
            VisualMode = VisualizerEngine.ModePad,
        };
        pad.Peaks[FC] = 1f;
        pad.Advance(300);
        Assert.Equal(MaxBase, pad.Engine.Depths[0], 6);
        Assert.Equal(VisualizerEngine.PadMaxThickness, pad.Engine.PadThickness[0], 6);
    }
}
