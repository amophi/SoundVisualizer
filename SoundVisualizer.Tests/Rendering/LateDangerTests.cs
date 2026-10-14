// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;
using static SoundVisualizer.Tests.Rendering.EngineHarness;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>
/// 늦게 온 위협음 판정: 숨긴 라벨로 먼저 들어온 짧은 소리(Android VisualizerEngineTest 의 같은 묶음).
/// 깨어 있는 엔진의 경우다.
/// </summary>
public class LateDangerTests
{
    // 캡처 채널 번호
    private const int FL = 0, FR = 1, BL = 3, BR = 4, SL = 5, SR = 6;

    /// <summary>파도·외곽선이 보이기 시작하는 깊이(WaveVisualizer 의 문턱).</summary>
    private const double WaveActiveDepth = 3;

    /// <summary>환경음을 숨기고 위협음만 보이게 한 엔진. 조용한 프레임 10개를 흘려 둔다.</summary>
    private static EngineHarness HiddenAmbient()
    {
        var run = new EngineHarness { Label = SoundType.Ambient, Shown = false };
        run.Advance(10);
        return run;
    }

    private static void Stereo(EngineHarness run, float left, float right)
    {
        run.Peaks[FL] = left;
        run.Peaks[FR] = right;
    }

    private static double DeepestDepth(EngineHarness run) => run.Engine.Depths.ToArray().Max();

    /// <summary>
    /// 위협음을 표시 중인 엔진이 같은 소리를 제때 받았을 때 평소 경로가 도달하는 가장 큰 전체 크기.
    /// <paramref name="loudFrames"/> 프레임 동안 <paramref name="loud"/>, 이어서 <paramref name="tailFrames"/> 프레임 동안 <paramref name="tail"/>.
    /// </summary>
    private static double OnTimePeakSmoothTotal(float loud, int loudFrames, float tail, int tailFrames)
    {
        var run = new EngineHarness { Label = SoundType.Danger, Shown = true };
        double peak = 0;
        for (int n = 0; n < loudFrames + tailFrames; n++)
        {
            float v = n < loudFrames ? loud : tail;
            Stereo(run, v, v);
            run.Tick();
            peak = Math.Max(peak, run.Engine.SmoothTotal);
        }
        return peak;
    }

    [Fact]
    public void 숨긴_라벨로_지나간_짧은_소리는_늦게_온_위협음_판정에_제때_표시했을_크기로_커진다()
    {
        // 총성 두 프레임 뒤로는 약한 잔향만 남는다. 아직 라벨은 숨긴 환경음이다.
        var run = HiddenAmbient();
        Stereo(run, 0.9f, 0.9f);
        run.Advance(2);
        Stereo(run, 0.03f, 0.03f);
        run.Advance(20);
        double before = run.Engine.SmoothTotal;

        // 약 0.36초 뒤 위협음 판정이 온다.
        run.BecomeDanger();
        run.Tick();

        // 위협음을 제때 표시했다면 그렸을 크기로 다시 커진다. 보관한 0.9 를 그대로 전체 크기로 쓰면 소리가 1초 넘게
        // 이어질 때의 크기라 짧은 총성마다 크기 한도까지 뛴다.
        double onTime = OnTimePeakSmoothTotal(0.9f, 2, 0.03f, 20);
        double seeded = run.Engine.SmoothTotal;
        Assert.True(seeded > before * 1.2, $"보관한 크기로 커지지 않았다 ({before} -> {seeded})");
        Assert.InRange(seeded, onTime * 0.8, onTime * 1.2);
    }

    [Fact]
    public void 제때_표시해도_안_보일_작은_위협음도_늦게_온_판정에서는_보이게_그린다()
    {
        // 전제: 0.1 짜리 짧은 소리는 위협음을 표시 중이어도 파도가 보이는 깊이(3)에 못 미친다.
        var onTime = new EngineHarness { Label = SoundType.Danger, Shown = true };
        double onTimeDeepest = 0;
        for (int n = 0; n < 32; n++)
        {
            float v = n < 2 ? 0.1f : 0f;
            Stereo(onTime, v, v);
            onTime.Tick();
            onTimeDeepest = Math.Max(onTimeDeepest, DeepestDepth(onTime));
        }
        Assert.True(onTimeDeepest < WaveActiveDepth, $"전제가 틀렸다: 제때 표시해도 보이는 크기다 ({onTimeDeepest})");

        // 판정까지 난 위협음은 안 보이면 안 되므로, 늦게 온 판정에서는 보이는 깊이까지 올린다.
        var run = HiddenAmbient();
        Stereo(run, 0.1f, 0.1f);
        run.Advance(2);
        Stereo(run, 0f, 0f);
        run.Advance(10);

        run.BecomeDanger();
        run.Advance(4);
        Assert.True(DeepestDepth(run) > WaveActiveDepth, $"판정까지 난 작은 위협음이 보이지 않는다 ({DeepestDepth(run)})");
    }

    [Fact]
    public void 크기_고정_모드는_전체_크기를_최소_깊이까지_올리지_않는다()
    {
        // 크기 고정 모드에서 전체 크기는 깊이가 아니라 진하기다. 보관한 소리가 제때 그렸을 크기로만 되돌린다.
        var settings = new VisualModeSettings { IntensityAsOpacity = true };
        var run = new EngineHarness { Settings = settings, Label = SoundType.Ambient, Shown = false };
        run.Advance(10);
        Stereo(run, 0.1f, 0.1f);
        run.Advance(2);
        Stereo(run, 0f, 0f);
        run.Advance(10);
        run.BecomeDanger();
        run.Tick();

        double onTime = OnTimePeakSmoothTotal(0.1f, 2, 0f, 10);
        Assert.InRange(run.Engine.SmoothTotal, onTime * 0.8, onTime * 1.2);
    }

    [Fact]
    public void 위협음_판정이_소리가_끝난_뒤에_와도_보관한_방향으로_보인다()
    {
        // 왼쪽에서 난 짧은 소리. 판정이 올 때는 완전히 조용하다.
        var run = HiddenAmbient();
        run.Peaks[FL] = 0.6f; run.Peaks[SL] = 0.6f; run.Peaks[BL] = 0.6f;
        run.Peaks[FR] = 0.2f; run.Peaks[SR] = 0.2f; run.Peaks[BR] = 0.2f;
        run.Tick();
        run.Silence();
        run.Advance(30);

        run.BecomeDanger();
        run.Advance(3);
        var d = run.Engine.Depths;
        Assert.True(d[6] > WaveActiveDepth, $"좌측이 보이지 않는다 ({d[6]})");
        Assert.True(d[6] > d[2], $"좌측({d[6]}) 이 우측({d[2]}) 보다 깊어야 한다");
    }

    [Fact]
    public void 위협음_라벨이어도_숨겨서_놓친_소리가_없으면_보통처럼_서서히_커진다()
    {
        var run = new EngineHarness { Label = SoundType.Danger, Shown = true };
        run.Advance(10);
        Stereo(run, 0.9f, 0.9f);
        run.Tick();

        var fresh = new EngineHarness { Label = SoundType.Danger, Shown = true };
        Stereo(fresh, 0.9f, 0.9f);
        fresh.Tick();

        Assert.Equal(fresh.Engine.SmoothTotal, run.Engine.SmoothTotal, 9);
        Assert.True(run.Engine.SmoothTotal < 0.5, $"보통의 소리가 한 번에 커졌다 ({run.Engine.SmoothTotal})");
    }

    [Fact]
    public void 숨긴_소리가_끝나고_위협음이_아닌_라벨로_돌아오면_보관한_피크를_쓰지_않는다()
    {
        // 대화음을 숨긴 채 말소리가 지나가고, 끝난 뒤 라벨이 표시 중인 환경음으로 돌아온다.
        var run = new EngineHarness { Label = SoundType.Speech, Shown = false };
        run.Advance(10);
        Stereo(run, 0.7f, 0.7f);
        run.Advance(5);
        Stereo(run, 0f, 0f);
        run.Advance(5);
        double before = run.Engine.SmoothTotal;

        run.Label = SoundType.Ambient;
        run.Shown = true;
        run.Tick();
        Assert.True(run.Engine.SmoothTotal < before, $"지나간 말소리가 환경음으로 다시 커졌다 ({before} -> {run.Engine.SmoothTotal})");
    }

    [Fact]
    public void 한_번_쓴_소리로는_다시_커지지_않는다()
    {
        var run = HiddenAmbient();
        Stereo(run, 0.9f, 0.9f);
        run.Tick();
        Stereo(run, 0f, 0f);
        run.Advance(10);

        run.BecomeDanger();
        run.Tick();
        double released = run.Engine.SmoothTotal;

        // 라벨이 숨김과 위협음 사이를 오가도 새 소리가 없으면 줄어들기만 한다.
        double previous = released;
        for (int n = 0; n < 30; n++)
        {
            run.Shown = n % 10 != 5;
            run.Tick();
            Assert.True(run.Engine.SmoothTotal <= previous, $"프레임 {n}: 이미 그린 소리로 다시 커졌다");
            previous = run.Engine.SmoothTotal;
        }
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(60.0)]
    [InlineData(144.0)]
    public void 보관한_피크는_최대_프레임과_상관없이_약_1초_뒤_사라진다(double fps)
    {
        bool SeedsAfter(double quietSeconds)
        {
            var run = new EngineHarness { Label = SoundType.Ambient, Shown = false };
            run.Advance(10, 1000.0 / fps);
            Stereo(run, 0.9f, 0.9f);
            run.Tick(1000.0 / fps);
            Stereo(run, 0f, 0f);
            run.Advance((int)Math.Round(quietSeconds * fps), 1000.0 / fps);
            double before = run.Engine.SmoothTotal;
            run.BecomeDanger();
            run.Tick(1000.0 / fps);
            return run.Engine.SmoothTotal > before;
        }

        Assert.True(SeedsAfter(0.8), "0.8초 전 소리를 벌써 잊었다");
        Assert.False(SeedsAfter(1.2), "1.2초 전 소리를 아직 쓴다");
    }

    [Fact]
    public void 그릴_수_없는_설정일_때_지나간_소리는_보관하지_않는다()
    {
        // 진하기 0 이면 판정이 와도 보일 것이 없다. 그사이 지나간 소리를 나중에 진하기를 올린 뒤 그리지 않는다.
        var settings = new VisualModeSettings { VisualOpacity = 0 };
        var run = new EngineHarness { Settings = settings, Label = SoundType.Ambient, Shown = false };
        run.Advance(10);
        Stereo(run, 0.9f, 0.9f);
        run.Tick();
        Stereo(run, 0f, 0f);
        run.Advance(10);
        double before = run.Engine.SmoothTotal;

        settings.VisualOpacity = 50;
        run.BecomeDanger();
        run.Tick();
        Assert.True(run.Engine.SmoothTotal < before);
    }

    [Theory]
    [InlineData(0.0, 50.0, 30.0, false, false)] // 진하기 0
    [InlineData(50.0, 0.0, 30.0, false, false)] // 크기 0
    [InlineData(50.0, 50.0, 30.0, false, true)]
    [InlineData(0.0, 0.0, 30.0, true, true)]    // 크기 고정은 고정 크기와 최대 진하기를 본다
    [InlineData(50.0, 50.0, 0.0, true, false)]  // 고정 크기 0
    public void 그릴_수_있는지는_진하기와_크기로_정한다(double opacity, double intensity, double fixedSize, bool sizeLocked, bool expected)
    {
        var s = new VisualModeSettings
        {
            VisualOpacity = opacity,
            Intensity = intensity,
            OpacityFixedSize = fixedSize,
            IntensityAsOpacity = sizeLocked,
            OpacityFixedMaxOpacity = 100,
        };
        Assert.Equal(expected, VisualizerEngine.CanDraw(s));
    }

    [Fact]
    public void 크기_고정_모드는_최대_진하기가_0이면_그릴_수_없다()
    {
        var s = new VisualModeSettings { IntensityAsOpacity = true, OpacityFixedMaxOpacity = 0, VisualOpacity = 100 };
        Assert.False(VisualizerEngine.CanDraw(s));
    }

    [Fact]
    public void 소리_크기는_사운드_모드가_그리는_채널만_본다()
    {
        float[] lfeOnly = { 0, 0, 0, 0, 0, 0, 0, 0.9f };
        float[] centerOnly = { 0, 0, 0.5f, 0, 0, 0, 0, 0 };
        float[] rearRight = { 0.1f, 0.2f, 0, 0, 0.7f, 0, 0, 0 };

        Assert.Equal(0f, VisualizerEngine.Loudness(lfeOnly, 2)); // LFE 는 자리가 없다
        Assert.Equal(0.5f, VisualizerEngine.Loudness(centerOnly, 1));
        Assert.Equal(0f, VisualizerEngine.Loudness(centerOnly, 0)); // 2.0 은 앞 좌우만
        Assert.Equal(0.7f, VisualizerEngine.Loudness(rearRight, 2));
        Assert.Equal(0.2f, VisualizerEngine.Loudness(rearRight, 0));
    }
}
