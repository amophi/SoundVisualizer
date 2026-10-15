// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Visualizers
{
    public class OutlineVisualizer : IVisualizerMode
    {
        private const int WAVE_SAMPLE_COUNT = WaveShape.Samples;
        
        // 매 프레임 발생하는 GC 스파이크 방지용 전역 캐시 배열
        private readonly Point[] _innerPts = new Point[WAVE_SAMPLE_COUNT];
        private readonly List<Point> _bezierPts = new List<Point>(WAVE_SAMPLE_COUNT * 3);

        // 안쪽 곡선이 지나는 점(모서리 둥글리기 포함)은 Core 의 WaveShape 가 계산한다. 파도와 외곽선이 같은 점을 쓴다.
        private readonly WaveShape _shape = new();

        public Geometry GenerateGeometry(VisualizerContext context)
        {
            double[] channelDepths = context.ChannelDepths;
            double w = context.Width;
            double h = context.Height;

            bool anyActive = false;
            for (int i = 0; i < channelDepths.Length; i++)
            {
                if (channelDepths[i] > 3) { anyActive = true; break; }
            }
            if (!anyActive) return Geometry.Empty;

            _shape.Build(w, h, channelDepths, context.BaseDepth);
            int N = WAVE_SAMPLE_COUNT;
            for (int i = 0; i < N; i++)
            {
                _innerPts[i] = new Point(_shape.X[i], _shape.Y[i]);
            }

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            using (var ctx = geometry.Open())
            {
                // 외곽 사각형을 그리지 않고, 오직 파도의 내부 경계선(inner) 루프만 독립된 Figure로 그립니다.
                // 이를 통해 안쪽이 칠해지지 않고 외곽선만 그릴 수 있는 기하학 구조를 만듭니다.
                ctx.BeginFigure(_innerPts[0], true, true);

                _bezierPts.Clear();
                for (int i = N - 1; i >= 0; i--)
                {
                    int prev = (i - 1 + N) % N;
                    int next = (i + 1) % N;
                    int nnext = (i + 2) % N;

                    Point p0 = _innerPts[nnext];
                    Point p1 = _innerPts[next];
                    Point p2 = _innerPts[i];
                    Point p3 = _innerPts[prev];

                    Point cp1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
                    Point cp2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

                    _bezierPts.Add(cp1);
                    _bezierPts.Add(cp2);
                    _bezierPts.Add(p2);
                }

                ctx.PolyBezierTo(_bezierPts, true, true);
            }

            geometry.Freeze();
            return geometry;
        }

        public Brush GetFillBrush(Color activeColor)
        {
            var transparent = activeColor;
            transparent.A = 0;
            var semiTransparent = activeColor;
            semiTransparent.A = 160;

            var brush = new RadialGradientBrush();
            brush.GradientOrigin = new Point(0.5, 0.5);
            brush.Center = new Point(0.5, 0.5);
            brush.RadiusX = 0.5;
            brush.RadiusY = 0.5;
            brush.GradientStops.Add(new GradientStop(transparent, 0.0));
            brush.GradientStops.Add(new GradientStop(semiTransparent, 0.7));
            brush.GradientStops.Add(new GradientStop(activeColor, 1.0));
            
            brush.Freeze();
            return brush;
        }
    }
}

