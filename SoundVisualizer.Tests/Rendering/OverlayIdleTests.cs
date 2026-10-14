// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;
using static SoundVisualizer.Tests.Rendering.EngineHarness;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>
/// 보이는지, 쉬기와 깨어나기, 쉬는 동안 늦게 온 위협음 판정(Android VisualizerEngineTest 의 같은 묶음을 PC 값으로 옮겼다).
/// </summary>
public class OverlayIdleTests
{
    // 캡처 채널 번호
    private const int FL = 0, FR = 1, BL = 3, BR = 4, SL = 5, SR = 6, LFE = 7;

    /// <summary>환경음을 숨기고 위협음만 보이게 한 채, 조용해서 쉬기로 내려간 엔진.</summary>
    private static EngineHarness HiddenAmbientIdle()
    {
        var run = new EngineHarness { Label = SoundType.Ambient, Shown = false };
        run.TickUntilIdle();
        return run;
    }

    // ---------------------------------------------------------------
    // 보이는지
    // ---------------------------------------------------------------

    [Fact]
    public void 네_모드_모두_큰_소리에서_보인다()
    {
        foreach (int mode in new[] { VisualizerEngine.ModeWave, VisualizerEngine.ModePad, VisualizerEngine.ModeCircle, VisualizerEngine.ModeOutline })
        {
            var run = new EngineHarness { VisualMode = mode, Settings = new VisualModeSettings { Sensitivity = 25 } };
            run.Stereo(1f, 0.2f);
            run.Advance(200);
            Assert.True(run.Engine.Visible, $"모드 {mode} 가 큰 소리에서 보이지 않는다");
        }
    }

    [Fact]
    public void 무음에서는_보이지_않는다()
    {
        var run = new EngineHarness();
        run.Advance(10);
        Assert.False(run.Engine.Visible);
    }

    [Fact]
    public void 표시를_끈_라벨은_보이지_않는다()
    {
        var run = new EngineHarness { Settings = new VisualModeSettings { Sensitivity = 25 } };
        run.Stereo(1f, 1f);
        run.Advance(120);
        Assert.True(run.Engine.Visible, "소리가 크면 보여야 한다");

        run.Shown = false;
        run.Tick();
        Assert.False(run.Engine.Visible, "표시를 끄면 안 보여야 한다");
    }

    [Fact]
    public void 진하기_설정이_알파에_그대로_반영된다()
    {
        // 값이 클수록 진해야 한다. 1 - 진하기 로 뒤집히면 이 테스트가 깨진다.
        var run = new EngineHarness { Settings = new VisualModeSettings { VisualOpacity = 50 } };
        run.Tick();
        Assert.Equal(0.5, run.Engine.Alpha, 9);

        run.Settings.VisualOpacity = 100;
        run.Tick();
        Assert.Equal(1.0, run.Engine.Alpha, 9);
    }

    [Fact]
    public void 크기_고정_모드의_알파는_최대_진하기와_소리_크기를_따른다()
    {
        var run = new EngineHarness
        {
            Settings = new VisualModeSettings { IntensityAsOpacity = true, OpacityFixedMaxOpacity = 100, Sensitivity = 25, VisualOpacity = 10 },
        };
        run.Tick();
        Assert.Equal(0.0, run.Engine.Alpha, 9); // 무음

        run.All(1f);
        run.Advance(300);
        Assert.True(run.Engine.Alpha > 0.5, $"소리가 크면 알파가 올라야 한다 ({run.Engine.Alpha})");
    }

    // ---------------------------------------------------------------
    // 쉬기와 깨어나기
    // ---------------------------------------------------------------

    [Fact]
    public void 아무것도_보이지_않은_지_1초가_지나고_조용하면_쉰다()
    {
        var run = new EngineHarness();
        int frames = run.TickUntilIdle();
        // 1초 = 60 프레임. 그보다 이르게 내려가면 안 된다.
        Assert.True(frames >= 60, $"너무 빨리 쉬었다 (frames={frames})");
        Assert.False(run.Engine.Visible);
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(60.0)]
    [InlineData(144.0)]
    [InlineData(240.0)]
    public void 쉬기까지_걸리는_시간은_최대_프레임과_상관없다(double fps)
    {
        double interval = 1000.0 / fps;
        var run = new EngineHarness();
        int frames = run.TickUntilIdle(interval);
        double elapsed = frames * interval;
        Assert.InRange(elapsed, VisualizerEngine.IdleAfterMs, VisualizerEngine.IdleAfterMs + 2 * interval + 0.001);
    }

    [Fact]
    public void 보이는_동안에는_쉬지_않는다()
    {
        var run = new EngineHarness();
        run.All(0.5f);
        run.Advance(600);
        Assert.True(run.Engine.Visible);
        Assert.False(run.Engine.IsIdle);
    }

    [Fact]
    public void 편집_중에는_조용해도_쉬지_않는다()
    {
        var run = new EngineHarness { KeepAwake = true };
        run.Advance(300);
        Assert.False(run.Engine.IsIdle);

        run.KeepAwake = false;
        run.TickUntilIdle();
    }

    [Fact]
    public void 편집을_시작하면_소리가_없어도_깨어난다()
    {
        var run = new EngineHarness();
        run.TickUntilIdle();
        run.Engine.Resume();
        Assert.False(run.Engine.IsIdle);
    }

    [Fact]
    public void 쉬는_중_소리가_나면_깨어난다()
    {
        var run = new EngineHarness();
        run.TickUntilIdle();
        run.Stereo(0.5f, 0f);
        Assert.True(run.Poll(), "소리가 오면 프레임마다 그리기로 돌아가야 한다");
        Assert.False(run.Engine.IsIdle);
    }

    [Fact]
    public void 쉬는_중_조용하면_그대로_쉰다()
    {
        var run = new EngineHarness();
        run.TickUntilIdle();
        Assert.False(run.Poll());
        Assert.True(run.Engine.IsIdle);

        run.Buffers = false; // 캡처가 아예 버퍼를 보내지 않아도 마찬가지다
        Assert.False(run.Poll());
        Assert.True(run.Engine.IsIdle);
    }

    [Fact]
    public void 깨어난_첫_프레임은_쉰_시간만큼_건너뛰지_않는다()
    {
        // 위협음을 보이는 중에 조용해서 쉬다가 10초 뒤 소리가 난다. 처음 켠 엔진이 같은 소리를 첫 프레임에 받았을 때와 같아야 한다.
        var run = new EngineHarness { Label = SoundType.Danger, Shown = true };
        run.TickUntilIdle();
        run.Stereo(0.9f, 0.9f);
        Assert.True(run.Poll(10_000));
        run.Tick();

        var fresh = new EngineHarness { Label = SoundType.Danger, Shown = true };
        fresh.Stereo(0.9f, 0.9f);
        fresh.Tick();

        Assert.Equal(fresh.Engine.SmoothTotal, run.Engine.SmoothTotal, 9);
        Assert.True(run.Engine.SmoothTotal < 0.5, $"보통의 깨어남이 한 번에 커졌다 ({run.Engine.SmoothTotal})");
    }

    [Fact]
    public void 표시가_꺼진_종류의_소리가_계속_나면_쉰다()
    {
        // 소리 조건만 보면 그릴 것도 없이 최대 프레임으로 계속 돈다.
        var run = new EngineHarness { Shown = false };
        run.Stereo(0.8f, 0.8f);
        Assert.True(run.TickUntilIdle() < 120);
    }

    [Fact]
    public void 진하기가_0이면_소리가_나도_쉰다()
    {
        var run = new EngineHarness { Settings = new VisualModeSettings { VisualOpacity = 0 } };
        run.Stereo(0.8f, 0.8f);
        Assert.True(run.TickUntilIdle() < 120);
    }

    [Fact]
    public void 자리가_없는_LFE_만_울리면_쉰다()
    {
        // LFE 는 전체 크기에는 들어가지만 그려지는 자리가 없다. 그것만 울리는 동안 그릴 것도 없이 돌지 않는다.
        var run = new EngineHarness { SoundMode = 2 };
        run.Peaks[LFE] = 0.9f;
        int frames = run.TickUntilIdle();
        Assert.True(frames < 120, $"LFE 만 울리는데 쉬지 않았다 (frames={frames})");
    }

    [Fact]
    public void 크기_고정_모드에서_소리가_잦아들면_보이지_않는_것으로_보고_쉰다()
    {
        // 크기 고정 모드는 도형이 늘 화면 둘레에 있고 진하기만 소리를 따라 0 으로 다가간다. 진하기 문턱이 없으면
        // 소리가 그친 뒤에도 보이지 않는 도형을 화면 전체 크기로 몇 분 동안 그린다.
        var run = new EngineHarness { Settings = new VisualModeSettings { IntensityAsOpacity = true } };
        run.Stereo(0.5f, 0.5f);
        run.Advance(60);
        Assert.True(run.Engine.Visible);

        run.Silence();
        int frames = run.TickUntilIdle();
        Assert.True(frames < 600, $"10초가 지나도 쉬지 않았다 (frames={frames})");
    }

    [Fact]
    public void 쉬는_중_표시_대상이_아닌_소리로는_깨어나지_않는다()
    {
        var run = new EngineHarness { Shown = false };
        run.Stereo(0.8f, 0.8f);
        Assert.True(run.TickUntilIdle() < 120);

        Assert.False(run.Poll());
        Assert.True(run.Engine.IsIdle, "표시가 꺼진 소리에 깨어났다");
    }

    [Fact]
    public void 쉬는_중_표시_대상이_되면_깨어난다()
    {
        var run = new EngineHarness { Shown = false };
        run.Stereo(0.8f, 0.8f);
        Assert.True(run.TickUntilIdle() < 120);

        run.Shown = true;
        Assert.True(run.Poll(), "표시를 켰는데 깨어나지 않았다");
    }

    // ---------------------------------------------------------------
    // 쉬는 동안 소리 신호로 잠들어도 되는지 (#170)
    // ---------------------------------------------------------------

    [Fact]
    public void 조용한_대기는_한_번_확인한_뒤부터_소리가_날_때까지_잠들어도_된다()
    {
        var run = new EngineHarness();
        run.TickUntilIdle();
        Assert.False(run.Engine.CanSleepUntilLoud, "쉬기로 내려오자마자 확인도 없이 잠든다");

        run.Poll();
        Assert.True(run.Engine.CanSleepUntilLoud);
    }

    [Fact]
    public void 표시를_꺼_둔_소리가_나는_동안에는_잠들지_않고_33ms_확인을_이어_간다()
    {
        // 잠들면 캡처 스레드가 버퍼마다 깨워서 확인이 오히려 잦아진다.
        var run = HiddenAmbientIdle();
        run.Stereo(0.8f, 0.8f);
        run.Poll();
        Assert.True(run.Engine.IsIdle);
        Assert.False(run.Engine.CanSleepUntilLoud);
    }

    [Fact]
    public void 소리를_보관하는_동안에는_잠들지_않고_보관이_끝나면_잠든다()
    {
        // 늦게 올 위협음 판정을 보려면 소리가 끝난 뒤에도 약 1초 동안 확인해야 한다.
        var run = HiddenAmbientIdle();
        run.Stereo(0.9f, 0.9f);
        run.Poll();
        run.Silence();
        run.Polls(24); // 0.8초
        Assert.False(run.Engine.CanSleepUntilLoud, "보관한 소리가 남았는데 잠든다");

        run.Polls(12); // 1.2초
        Assert.True(run.Engine.CanSleepUntilLoud, "보관이 끝났는데 계속 확인한다");
    }

    [Fact]
    public void 그릴_수_없는_설정에서_소리가_나면_잠들지_않는다()
    {
        var run = new EngineHarness { Settings = new VisualModeSettings { VisualOpacity = 0 } };
        run.Stereo(0.8f, 0.8f);
        Assert.True(run.TickUntilIdle() < 120);

        run.Poll();
        Assert.False(run.Engine.CanSleepUntilLoud);
    }

    // ---------------------------------------------------------------
    // 쉬는 동안 늦게 온 위협음 판정
    // ---------------------------------------------------------------

    [Fact]
    public void 숨긴_라벨로_지나간_짧은_소리는_쉬는_중에도_늦게_온_위협음_판정에_제때_표시했을_크기로_보인다()
    {
        var run = HiddenAmbientIdle();

        // 총성 한 번(33ms) 뒤로는 약한 잔향만 남는다. 아직 라벨은 숨긴 환경음이다.
        run.Stereo(0.9f, 0.9f);
        run.Poll();
        run.Stereo(0.03f, 0.03f);
        run.Polls(10);
        Assert.True(run.Engine.IsIdle, "숨긴 라벨의 소리에 깨어났다");

        // 약 0.36초 뒤 위협음 판정이 도착한다.
        run.BecomeDanger();
        Assert.True(run.Poll(), "위협음 판정이 왔는데 깨어나지 않았다");

        // 위협음을 제때 표시했다면 그렸을 크기로 시작한다. 확인 한 번(33ms)은 60fps 두 프레임이다.
        double seeded = run.Engine.SmoothTotal;
        double onTime = OnTimePeakSmoothTotal(0.9f, 2, 0.03f, 20);
        Assert.InRange(seeded, onTime * 0.8, onTime * 1.2);

        run.Advance(3);
        // 잔향(0.03)으로 0 에서 자라면 보이지 않는다.
        Assert.True(run.Engine.Visible, $"잔향만으로 그려서 보이지 않는다 (smoothTotal={run.Engine.SmoothTotal})");
    }

    [Fact]
    public void 제때_표시해도_안_보일_작은_위협음도_쉬는_중_늦게_온_판정에서는_보이게_그린다()
    {
        var run = HiddenAmbientIdle();
        run.Stereo(0.1f, 0.1f);
        run.Poll();
        run.Silence();
        run.Polls(10);

        run.BecomeDanger();
        Assert.True(run.Poll());
        run.Advance(3);
        Assert.True(run.Engine.Visible, $"판정까지 난 작은 위협음이 보이지 않는다 (가장 깊은 곳 {run.DeepestDepth})");
    }

    [Fact]
    public void 쉬는_중_위협음_판정이_소리가_끝난_뒤에_와도_보관한_방향으로_보인다()
    {
        var run = HiddenAmbientIdle();

        // 왼쪽에서 난 짧은 소리. 판정이 올 때는 완전히 조용하다.
        run.Peaks[FL] = 0.6f; run.Peaks[SL] = 0.6f; run.Peaks[BL] = 0.6f;
        run.Peaks[FR] = 0.2f; run.Peaks[SR] = 0.2f; run.Peaks[BR] = 0.2f;
        run.Poll();
        run.Silence();
        run.Polls(15);

        run.BecomeDanger();
        Assert.True(run.Poll(), "소리가 끝났다고 위협음 판정을 무시했다");

        run.Advance(3);
        var d = run.Engine.Depths;
        Assert.True(run.Engine.Visible);
        Assert.True(d[6] > d[2], $"좌측({d[6]}) 이 우측({d[2]}) 보다 깊어야 한다");
        Assert.True(d[7] > d[1], $"좌상단({d[7]}) 이 우상단({d[1]}) 보다 깊어야 한다");
    }

    [Fact]
    public void 쉬는_중_숨긴_소리가_끝나고_위협음이_아닌_라벨로_돌아오면_깨어나지_않는다()
    {
        // 대화음을 숨긴 채 말소리가 지나가고, 끝난 뒤 라벨이 표시 중인 환경음으로 돌아온다.
        var run = new EngineHarness { Label = SoundType.Speech, Shown = false };
        run.TickUntilIdle();
        run.Stereo(0.7f, 0.7f);
        run.Polls(5);
        run.Silence();
        run.Polls(5);

        run.Label = SoundType.Ambient;
        run.Shown = true;
        Assert.False(run.Poll(), "지나간 말소리가 환경음으로 그려졌다");
    }

    [Theory]
    [InlineData(PollMs)]
    [InlineData(20.0)] // 캡처 스레드가 앞당긴 확인이 섞여 촘촘할 때
    [InlineData(47.0)] // 타이머 해상도(15.6ms) 때문에 33ms 가 47ms 로 늘어날 때
    public void 쉬는_중_보관한_피크는_확인_간격과_상관없이_약_1초_뒤_사라진다(double pollMs)
    {
        bool WakesAfter(double quietMs)
        {
            var run = HiddenAmbientIdle();
            run.Stereo(0.9f, 0.9f);
            run.Poll(pollMs);
            run.Silence();
            run.Polls((int)Math.Round(quietMs / pollMs), pollMs);
            run.BecomeDanger();
            return run.Poll(pollMs);
        }

        Assert.True(WakesAfter(800), "0.8초 전 소리를 벌써 잊었다");
        Assert.False(WakesAfter(1200), "1.2초 전 소리로 깨어났다");
    }

    [Fact]
    public void 쉬기로_내려가기_직전에_숨긴_라벨로_받은_소리도_위협음_판정에_쓴다()
    {
        var run = new EngineHarness { Label = SoundType.Ambient, Shown = false };
        // 보이지 않은 지 1초가 안 되어 아직 프레임마다 그린다.
        run.Advance(50);
        Assert.False(run.Engine.IsIdle);

        run.Stereo(0.9f, 0.9f);
        run.Tick();
        run.Silence();
        run.TickUntilIdle();

        run.BecomeDanger();
        Assert.True(run.Poll(), "쉬기로 내려가며 직전 소리를 잊었다");
        run.Advance(3);
        Assert.True(run.Engine.Visible);
    }

    [Fact]
    public void 깨어_있는_중_위협음_판정으로_그린_소리로는_쉬다가_다시_깨어나지_않는다()
    {
        var run = new EngineHarness { Label = SoundType.Ambient, Shown = false };
        run.Advance(10);
        run.Stereo(0.9f, 0.9f);
        run.Tick();
        run.Silence();
        run.Advance(10);
        double beforeLabel = run.Engine.SmoothTotal;

        run.BecomeDanger();
        run.Tick();
        Assert.True(run.Engine.Visible);
        Assert.True(run.Engine.SmoothTotal > beforeLabel * 2, $"보관한 크기로 커지지 않았다 ({beforeLabel} -> {run.Engine.SmoothTotal})");

        // 다 사라지고 쉬기로 내려간 뒤, 라벨이 그대로여도 이미 그린 소리로 다시 깨어나면 안 된다.
        run.TickUntilIdle();
        Assert.False(run.Poll(), "이미 그린 소리로 다시 깨어났다");
    }
}
