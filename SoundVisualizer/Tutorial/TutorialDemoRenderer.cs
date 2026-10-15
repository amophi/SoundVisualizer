// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Media;
using SoundVisualizer.Rendering;
using SoundVisualizer.Visualizers;

namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 튜토리얼 그림의 엔진 결과(<see cref="TutorialDemo"/>)를 오버레이와 같은 Visualizers 로 도형으로 만든다(MainWindow.DrawWave 와 같은 길).
    /// 도형은 기준 화면(1920×1080) 좌표로 만들고, 창이 Viewbox 로 그림 크기에 맞춰 줄인다. 패드 두께 같은 크기도 함께 줄어 실제 화면을 줄인
    /// 모양이 된다(외곽선 두께만 예외, 아래).
    /// 광원(Glow)은 그림 설정에서 꺼 두어 그리지 않는다(TutorialDemo.DemoSettings). UI 스레드에서만 쓴다.
    /// </summary>
    internal sealed class TutorialDemoRenderer
    {
        /// <summary>
        /// 외곽선 모드의 선 두께(기준 화면 DIP). 오버레이는 4 다. 그림은 기준 화면을 4분의 1 남짓으로 줄여 그려 4 면 1px 아래로
        /// 가늘어져 잘 보이지 않으므로, 그림에서만 두 배로 그린다(줄인 뒤 2px 쯤). 다른 모드의 모양은 줄인 비율 그대로다.
        /// </summary>
        private const double OutlineThickness = 8.0;

        // 표현 모드 번호(AppSettings.VisualMode) 순서. 모드마다 점 배열을 들고 있어 그림 전용으로 따로 만든다.
        private readonly IVisualizerMode[] _visualizers =
        {
            new WaveVisualizer(), new PadVisualizer(), new CircleRippleVisualizer(), new OutlineVisualizer(),
        };

        private readonly VisualizerContext _context = new();
        private readonly double[] _depths = new double[VisualizerEngine.Positions];
        private readonly double[] _dists = new double[VisualizerEngine.Positions];

        private Color _brushColor;
        private int _brushMode = -1;

        /// <summary>
        /// 엔진의 이번 프레임을 <paramref name="shape"/> 에 그린다. 보이는 것이 없으면 접는다(오버레이처럼 보이지 않는 도형을 만들지 않는다).
        /// </summary>
        /// <param name="color">지금 소리 종류의 색(사용자가 설정 탭에서 고른 색).</param>
        public void Draw(TutorialDemo demo, System.Windows.Shapes.Path shape, Color color)
        {
            var engine = demo.Engine;
            if (!engine.Visible)
            {
                shape.Visibility = Visibility.Collapsed;
                return;
            }

            // 설정 파일의 모드 번호가 범위를 벗어나면 오버레이처럼 파도로 그린다(MainWindow.DrawWave).
            int mode = demo.VisualMode >= 0 && demo.VisualMode < _visualizers.Length ? demo.VisualMode : VisualizerEngine.ModeWave;
            var visualizer = _visualizers[mode];

            // 사운드 모드 2.0 이라 분포가 이미 둘레 자리 순서다(MainWindow.DrawWave 의 2.0 경로).
            engine.Depths.CopyTo(_depths);
            engine.Distribution.CopyTo(_dists);
            _context.Width = TutorialDemo.ReferenceWidth;
            _context.Height = TutorialDemo.ReferenceHeight;
            _context.BaseDepth = engine.BaseDepth;
            _context.ChannelDepths = _depths;
            _context.ChannelDists = _dists;
            engine.PadThickness.CopyTo(_context.PadThickness);
            engine.CircleDepths.CopyTo(_context.CircleDepths);
            _context.TotalVolume = engine.TotalTarget;
            _context.AnimationTime = engine.AnimationTime;

            shape.Data = visualizer.GenerateGeometry(_context);

            if (_brushMode != mode || _brushColor != color || shape.Fill == null)
            {
                _brushMode = mode;
                _brushColor = color;
                var brush = visualizer.GetFillBrush(color);
                if (mode == VisualizerEngine.ModeOutline)
                {
                    shape.Fill = Brushes.Transparent;
                    shape.Stroke = brush;
                    shape.StrokeThickness = OutlineThickness;
                }
                else
                {
                    shape.Fill = brush;
                    shape.Stroke = null;
                    shape.StrokeThickness = 0;
                }
            }

            shape.Opacity = engine.Alpha;
            shape.Visibility = Visibility.Visible;
        }
    }
}
