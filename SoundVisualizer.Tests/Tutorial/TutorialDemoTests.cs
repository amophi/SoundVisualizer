// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;
using SoundVisualizer.Tutorial;

namespace SoundVisualizer.Tests.Tutorial;

/// <summary>
/// 튜토리얼 그림이 실제 오버레이 엔진을 거쳐 그리는 모양(Android TutorialScriptTest 의 엔진 시험).
/// 쪽의 글과 그림이 어긋나면 처음 보는 사람을 오히려 헷갈리게 한다. 엔진의 기본값이나 계산이 바뀌어 그림이 밋밋해져도 여기서 걸린다.
/// 둘레 자리는 0:상단 가운데부터 시계 방향이다(2:오른쪽 가운데, 6:왼쪽 가운데).
/// </summary>
public class TutorialDemoTests
{
    private const int RightSide = 2;
    private const int LeftSide = 6;

    /// <summary>튜토리얼 그림처럼 0초부터 60fps 로 대본을 흘린다. 미리 돌린 장면 없이 시작한다.</summary>
    private sealed class DemoRun
    {
        public readonly TutorialDemo Demo;
        private int _frame;

        public DemoRun(TutorialScene scene, int visualMode = VisualizerEngine.ModeWave)
        {
            Demo = new TutorialDemo(scene, visualMode, runToStill: false);
        }

        public VisualizerEngine Engine => Demo.Engine;

        /// <summary>지난번에 멈춘 프레임 다음부터 <paramref name="seconds"/> 까지 흘린다.</summary>
        public void RunTo(double seconds)
        {
            while (_frame / 60.0 <= seconds)
            {
                Demo.Step(_frame / 60.0);
                _frame++;
            }
        }

        public double Deepest => Engine.Depths.ToArray().Max();
    }

    [Fact]
    public void 멈춰_둔_장면에는_그_쪽이_말하는_것이_크게_그려져_있다()
    {
        // 멈춘 한 장면이 비거나 작으면, 애니메이션을 꺼 둔 사람에게 그 쪽의 그림은 빈 화면이 된다.
        foreach (var scene in Enum.GetValues<TutorialScene>())
        {
            var demo = new TutorialDemo(scene, VisualizerEngine.ModeWave);
            Assert.True(demo.Engine.Visible, $"{scene} 의 멈춘 장면에 아무것도 없다");
            double deepest = demo.Engine.Depths.ToArray().Max();
            Assert.True(deepest > demo.Engine.BaseDepth * 0.5, $"{scene} 의 멈춘 장면이 너무 작다: {deepest / demo.Engine.BaseDepth:0.00}");
            Assert.Equal(scene.StillAtSec(), demo.TimeSec, 2);
        }

        var depths = new TutorialDemo(TutorialScene.Direction, VisualizerEngine.ModeWave).Engine.Depths.ToArray();
        Assert.True(depths[LeftSide] > depths[RightSide] * 3, "방향 쪽의 멈춘 장면에서 왼쪽이 깊지 않다: " + string.Join(", ", depths));
        Assert.Equal(SoundType.Danger, new TutorialDemo(TutorialScene.Types, VisualizerEngine.ModeWave).Label); // 범례도 위협음을 밝힌다
    }

    [Fact]
    public void 엔진을_거치면_왼쪽_소리는_왼쪽이_오른쪽_소리는_오른쪽이_깊다()
    {
        var run = new DemoRun(TutorialScene.Direction);
        run.RunTo(1.5);
        var left = run.Engine.Depths.ToArray();
        Assert.True(run.Engine.Visible, "왼쪽 구간에 그리지 않는다");
        Assert.True(left[LeftSide] > left[RightSide] * 3, "왼쪽 소리인데 왼쪽이 깊지 않다: " + string.Join(", ", left));

        run.RunTo(3.7);
        var right = run.Engine.Depths.ToArray();
        Assert.True(run.Engine.Visible, "오른쪽 구간에 그리지 않는다");
        Assert.True(right[RightSide] > right[LeftSide] * 3, "오른쪽 소리인데 오른쪽이 깊지 않다: " + string.Join(", ", right));
    }

    [Fact]
    public void 엔진을_거치면_종류마다_그_종류로_그린다()
    {
        // 색은 창이 라벨로 고른다(오버레이와 같다). 라벨이 바뀌는 순간 그림이 보이고 있어야 그 색이 보인다.
        var run = new DemoRun(TutorialScene.Types);
        run.RunTo(1.5);
        Assert.Equal(SoundType.Ambient, run.Demo.Label);
        Assert.True(run.Engine.Visible);
        run.RunTo(3.5);
        Assert.Equal(SoundType.Speech, run.Demo.Label);
        Assert.True(run.Engine.Visible);
        run.RunTo(5.0);
        Assert.Equal(SoundType.Danger, run.Demo.Label);
        Assert.True(run.Engine.Visible);
    }

    [Fact]
    public void 첫_쪽의_쿵은_눈에_띄게_커졌다_작아진다()
    {
        // 짧게 치고 마는 소리는 엔진이 부드럽게 따라가는 사이에 묻혀, 작은 그림에서는 거의 움직이지 않는다.
        var run = new DemoRun(TutorialScene.Sound);
        double cycle = TutorialScene.Sound.CycleSec();
        run.RunTo(cycle); // 한 바퀴는 흘려보내고 두 번째 바퀴를 잰다
        double smallest = double.MaxValue, largest = 0;
        for (int frame = 0; frame < 240; frame++)
        {
            run.RunTo(cycle + frame / 60.0);
            smallest = Math.Min(smallest, run.Deepest);
            largest = Math.Max(largest, run.Deepest);
        }
        double baseDepth = run.Engine.BaseDepth;
        Assert.True(largest > baseDepth * 0.5, $"쿵이 너무 작다: {largest / baseDepth:0.00}");
        Assert.True(largest > smallest * 3, $"쿵 사이에 충분히 가라앉지 않는다: {smallest:0.0} → {largest:0.0}");
    }

    [Theory]
    [InlineData(VisualizerEngine.ModePad)]
    [InlineData(VisualizerEngine.ModeCircle)]
    [InlineData(VisualizerEngine.ModeOutline)]
    public void 다른_표현_모드로도_멈춰_둔_장면이_보인다(int mode)
    {
        // 그림은 사용자가 고른 표현 모드로 그린다. 패드·원형은 깊이를 한 번 더 따라가므로 그 모드로 미리 돌려야 보인다.
        foreach (var scene in Enum.GetValues<TutorialScene>())
        {
            var demo = new TutorialDemo(scene, mode);
            Assert.True(demo.Engine.Visible, $"{scene} 을 모드 {mode} 로 멈춰 둔 장면에 아무것도 없다");
        }
    }

    [Fact]
    public void 그림의_설정은_사용자_설정과_따로_만든다()
    {
        // 그림이 사용자의 모드 설정을 고치거나, 사용자의 설정(진하기 0 등)에 따라 그림이 사라지면 안 된다.
        var settings = TutorialDemo.DemoSettings();
        Assert.NotSame(settings, TutorialDemo.DemoSettings());
        Assert.True(VisualizerEngine.CanDraw(settings));
        Assert.False(settings.IntensityAsOpacity);
        Assert.False(settings.IsGlowMode);
    }
}
