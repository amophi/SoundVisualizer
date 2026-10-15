// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 튜토리얼 한 쪽의 그림을 계산한다(Android TutorialDemoInputs 와 DemoPhone 의 엔진 부분). <see cref="TutorialScript"/> 의 소리를
    /// 오버레이와 같은 <see cref="VisualizerEngine"/> 에 흘리므로, 그림은 앱이 실제로 그리는 모양 그대로다.
    ///
    /// - 엔진은 기준 화면(<see cref="ReferenceWidth"/>×<see cref="ReferenceHeight"/>, 16:9 모니터)의 크기로 돈다. 튜토리얼 창은 그 결과를
    ///   그림 크기로 줄여 그린다. 그래서 여백·두께·보이기 시작하는 깊이가 실제 화면을 줄여 놓은 비율 그대로다(안드로이드는 엔진의
    ///   density 를 줄여 같은 일을 한다). 모니터마다 달리 잡지 않고 한 화면으로 고정해 어느 PC 에서나 같은 그림이 된다.
    /// - 사운드 모드는 2.0 으로 고정한다. 대본이 좌우 두 채널이다.
    /// - 사용자가 꺼 둔 종류도 모두 그린다. 튜토리얼은 앱이 무엇을 하는지 보여 주는 곳이라 크기·진하기·반응도 지금 설정이 아니라
    ///   <see cref="DemoSettings"/> 로 그린다. 표현 모드와 색만 사용자 것을 쓴다(색은 창이 라벨마다 고른다).
    /// - 실제 소리·오버레이와는 닿지 않는다. 튜토리얼을 연 채 오버레이가 돌아도 둘이 서로를 건드리지 않는다.
    ///
    /// 쪽마다 하나씩 만들고, 처음에는 그 쪽이 말하려는 장면(<see cref="TutorialScript.StillAtSec"/>)까지 미리 돌려 둔다. 움직이기
    /// 시작하면 그 장면에서 이어서 움직이고, 멈추면 그 자리에서 멈춘다(안드로이드 #331). UI 스레드에서만 쓴다.
    /// </summary>
    public sealed class TutorialDemo
    {
        /// <summary>엔진이 그리는 기준 화면(DIP). 16:9 모니터를 100% 배율로 본 크기다.</summary>
        public const double ReferenceWidth = 1920;
        public const double ReferenceHeight = 1080;

        /// <summary>미리 돌릴 때의 프레임 간격(60fps).</summary>
        private const double PrerollFps = 60;

        private readonly float[] _frame = new float[VisualizerEngine.Channels];
        private readonly VisualModeSettings _settings = DemoSettings();

        /// <param name="scene">그릴 쪽.</param>
        /// <param name="visualMode">표현 모드.</param>
        /// <param name="runToStill">멈춰 둘 장면까지 미리 돌려 둔다. 끄면 아무것도 흘리지 않은 엔진으로 시작한다(시험이 0초부터 흘릴 때).</param>
        public TutorialDemo(TutorialScene scene, int visualMode, bool runToStill = true)
        {
            Scene = scene;
            VisualMode = visualMode;
            Engine.SetSurfaceSize(ReferenceWidth, ReferenceHeight);
            if (runToStill) RunTo(scene.StillAtSec());
        }

        public TutorialScene Scene { get; }

        /// <summary>그리는 표현 모드(<see cref="VisualizerEngine.ModeWave"/> 등). 패드·원형의 움직임은 그리는 모드만 계산한다.</summary>
        public int VisualMode { get; set; }

        /// <summary>오버레이와 같은 엔진. 기준 화면 크기로 돈다.</summary>
        public VisualizerEngine Engine { get; } = new();

        /// <summary>마지막으로 엔진에 흘린 대본 시각(초). 처음에는 멈춰 둘 장면의 시각이다.</summary>
        public double TimeSec { get; private set; }

        /// <summary>지금 그리는 소리의 종류. 창이 이것으로 색을 고르고 종류 쪽의 범례를 밝힌다.</summary>
        public SoundType Label => TutorialScript.Label(Scene, TimeSec);

        /// <summary>
        /// 그림의 모드 설정. 기본값(<see cref="VisualModeSettings"/>)에서 크기·진하기·반응을 조금씩 올렸다(안드로이드와 같은 값, 민감도는 그
        /// 표시값 40 의 내부값). 그림이 실제 화면보다 훨씬 작아서, 기본값 그대로면 어두운 장면 위의 빨간 위협음이 흐리고 박자가 잘 읽히지 않는다.
        /// </summary>
        public static VisualModeSettings DemoSettings() => new()
        {
            Intensity = 55,
            VisualOpacity = 80,
            PositionSpeed = 20,
            Sensitivity = 10,
        };

        /// <summary>대본 시각 <paramref name="t"/>(초)의 프레임 하나를 엔진에 흘린다. 시각은 앞으로만 간다.</summary>
        public void Step(double t)
        {
            TutorialScript.PeakFrame(Scene, t, _frame);
            var input = new OverlayInput(_settings, VisualMode, SoundMode: 0, TutorialScript.Label(Scene, t), Shown: true);
            // 엔진의 시계도 대본 시각으로 간다. 멈췄다 다시 움직여도 대본이 이어지므로 엔진이 한꺼번에 건너뛰지 않는다.
            // 그림은 조용한 틈에도 쉬지 않는다(쉬면 쉬는 동안의 확인을 따로 돌려야 한다).
            Engine.Tick(t * 1000.0, _frame, input, keepAwake: true);
            TimeSec = t;
        }

        /// <summary>멈춰 둘 때: 0초부터 <paramref name="seconds"/> 까지 60fps 로 미리 돌려 그 순간의 모양을 만든다.</summary>
        public void RunTo(double seconds)
        {
            // 2.4 × 60 처럼 소수 곱이 정수 바로 아래로 떨어져도 한 프레임을 빠뜨리지 않게 아주 조금 올려서 자른다.
            int frames = (int)Math.Floor(seconds * PrerollFps + 1e-9);
            for (int i = 0; i <= frames; i++) Step(i / PrerollFps);
        }
    }
}
