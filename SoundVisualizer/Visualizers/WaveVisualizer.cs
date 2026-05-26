using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SoundVisualizer.Visualizers
{
    public class WaveVisualizer : IVisualizerMode
    {
        private const int WAVE_SAMPLE_COUNT = 150;
        
        // 매 프레임 발생하는 GC 스파이크 방지용 전역 캐시 배열
        private readonly Point[] _innerPts = new Point[WAVE_SAMPLE_COUNT];
        private readonly double[] _channelPos = new double[8];

        public Geometry GenerateGeometry(VisualizerContext context)
        {
            double w = context.Width;
            double h = context.Height;
            double[] channelDepths = context.ChannelDepths;
            double time = context.AnimationTime;

            bool anyActive = false;
            for (int i = 0; i < channelDepths.Length; i++)
            {
                if (channelDepths[i] > 3) { anyActive = true; break; }
            }
            if (!anyActive) return Geometry.Empty;

            double P = 2 * (w + h);

            double dist_tc = 0;
            double dist_tr = w / 2.0;
            double dist_rc = w / 2.0 + h / 2.0;
            double dist_br = w / 2.0 + h;
            double dist_bc = w / 2.0 + h + w / 2.0;
            double dist_bl = w / 2.0 + h + w;
            double dist_lc = w / 2.0 + h + w + h / 2.0;
            double dist_tl = w / 2.0 + h + w + h;

            _channelPos[0] = dist_tc / P;
            _channelPos[1] = dist_tr / P;
            _channelPos[2] = dist_rc / P;
            _channelPos[3] = dist_br / P;
            _channelPos[4] = dist_bc / P;
            _channelPos[5] = dist_bl / P;
            _channelPos[6] = dist_lc / P;
            _channelPos[7] = dist_tl / P;

            double d_tr = GetWaveDepth(dist_tr / P, time, channelDepths, _channelPos, context.BaseDepth);
            double d_br = GetWaveDepth(dist_br / P, time, channelDepths, _channelPos, context.BaseDepth);
            double d_bl = GetWaveDepth(dist_bl / P, time, channelDepths, _channelPos, context.BaseDepth);
            double d_tl = GetWaveDepth(dist_tl / P, time, channelDepths, _channelPos, context.BaseDepth);

            int N = WAVE_SAMPLE_COUNT;

            for (int i = 0; i < N; i++)
            {
                double dist = (P * i) / N; 
                double t = dist / P;
                
                double d = GetWaveDepth(t, time, channelDepths, _channelPos, context.BaseDepth);
                
                _innerPts[i] = GetRoundedInnerPoint(dist, w, h, P, d, d_tr, d_br, d_bl, d_tl);
            }

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, 0), true, true);
                ctx.LineTo(new Point(w, 0), false, false);
                ctx.LineTo(new Point(w, h), false, false);
                ctx.LineTo(new Point(0, h), false, false);

                ctx.BeginFigure(_innerPts[0], true, true);

                var bezierPts = new List<Point>(N * 3);
                for (int i = 0; i < N; i++)
                {
                    Point p0 = _innerPts[(i - 1 + N) % N];
                    Point p1 = _innerPts[i];
                    Point p2 = _innerPts[(i + 1) % N];
                    Point p3 = _innerPts[(i + 2) % N];

                    Point cp1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
                    Point cp2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

                    bezierPts.Add(cp1);
                    bezierPts.Add(cp2);
                    bezierPts.Add(p2);
                }

                ctx.PolyBezierTo(bezierPts, true, true);
            }

            geometry.Freeze();
            return geometry;
        }

        private Point GetRoundedInnerPoint(double dist, double w, double h, double P, double d, double d_tr, double d_br, double d_bl, double d_tl)
        {
            double c_tr = w / 2.0;
            double c_br = w / 2.0 + h;
            double c_bl = c_br + w;
            double c_tl = c_bl + h;

            // 라운딩 반경 (파도 깊이에 비례하도록 설정하여 깊이가 0일 때는 깎이지 않도록 함)
            double R_tr = Math.Min(d_tr * 2.0, Math.Min(w / 2.0, h / 2.0));
            double R_br = Math.Min(d_br * 2.0, Math.Min(w / 2.0, h / 2.0));
            double R_bl = Math.Min(d_bl * 2.0, Math.Min(w / 2.0, h / 2.0));
            double R_tl = Math.Min(d_tl * 2.0, Math.Min(w / 2.0, h / 2.0));

            double distToTL = dist - c_tl;
            if (dist < w / 2.0) distToTL = dist + P - c_tl;

            // 1) Top-Right 코너
            if (Math.Abs(dist - c_tr) <= R_tr)
            {
                double t = (dist - c_tr + R_tr) / (2 * R_tr);
                Point p0 = new Point(w - R_tr, d);        // 상단 가로선 끝
                Point p1 = new Point(w - d, d);           // 구석의 날카로운 꺾임(Control)
                Point p2 = new Point(w - d, R_tr);        // 우측 세로선 시작
                // 2차 베지에 곡선으로 안쪽으로 부드럽게 감싸줌
                return new Point((1 - t) * (1 - t) * p0.X + 2 * (1 - t) * t * p1.X + t * t * p2.X,
                                 (1 - t) * (1 - t) * p0.Y + 2 * (1 - t) * t * p1.Y + t * t * p2.Y);
            }
            // 2) Bottom-Right 코너
            if (Math.Abs(dist - c_br) <= R_br)
            {
                double t = (dist - c_br + R_br) / (2 * R_br);
                Point p0 = new Point(w - d, h - R_br);
                Point p1 = new Point(w - d, h - d);
                Point p2 = new Point(w - R_br, h - d);
                return new Point((1 - t) * (1 - t) * p0.X + 2 * (1 - t) * t * p1.X + t * t * p2.X,
                                 (1 - t) * (1 - t) * p0.Y + 2 * (1 - t) * t * p1.Y + t * t * p2.Y);
            }
            // 3) Bottom-Left 코너
            if (Math.Abs(dist - c_bl) <= R_bl)
            {
                double t = (dist - c_bl + R_bl) / (2 * R_bl);
                Point p0 = new Point(R_bl, h - d);
                Point p1 = new Point(d, h - d);
                Point p2 = new Point(d, h - R_bl);
                return new Point((1 - t) * (1 - t) * p0.X + 2 * (1 - t) * t * p1.X + t * t * p2.X,
                                 (1 - t) * (1 - t) * p0.Y + 2 * (1 - t) * t * p1.Y + t * t * p2.Y);
            }
            // 4) Top-Left 코너
            if (Math.Abs(distToTL) <= R_tl)
            {
                double t = (distToTL + R_tl) / (2 * R_tl);
                Point p0 = new Point(d, R_tl);
                Point p1 = new Point(d, d);
                Point p2 = new Point(R_tl, d);
                return new Point((1 - t) * (1 - t) * p0.X + 2 * (1 - t) * t * p1.X + t * t * p2.X,
                                 (1 - t) * (1 - t) * p0.Y + 2 * (1 - t) * t * p1.Y + t * t * p2.Y);
            }

            // 모서리가 아닐 경우 (제한된 직선 구간)
            Point edgePos = GetEdgePosition(dist, w, h, P);
            if (dist <= c_tr || dist > c_tl) return new Point(Math.Max(d, Math.Min(w - d, edgePos.X)), d); // Top
            if (dist <= c_br) return new Point(w - d, Math.Max(d, Math.Min(h - d, edgePos.Y))); // Right
            if (dist <= c_bl) return new Point(Math.Max(d, Math.Min(w - d, edgePos.X)), h - d); // Bottom
            return new Point(d, Math.Max(d, Math.Min(h - d, edgePos.Y))); // Left
        }

        private Point GetEdgePosition(double dist, double w, double h, double P)
        {
            dist = ((dist % P) + P) % P;
            if (dist <= w / 2) return new Point(w / 2 + dist, 0);
            if (dist <= w / 2 + h) return new Point(w, dist - w / 2);
            if (dist <= w / 2 + h + w) return new Point(w - (dist - (w / 2 + h)), h);
            if (dist <= w / 2 + h + w + h) return new Point(0, h - (dist - (w / 2 + h + w)));
            return new Point(dist - (w / 2 + h + w + h), 0);
        }

        private double GetWaveDepth(double t, double time, double[] depths, double[] positions, double maxDepth)
        {
            return Math.Min(maxDepth, Math.Max(0, InterpolateDepthCatmullRom(t, depths, positions)));
        }

        private double InterpolateDepthCatmullRom(double t, double[] depths, double[] positions)
        {
            t = ((t % 1.0) + 1.0) % 1.0;
            int n = positions.Length;

            int idx1 = 0;
            for (int i = 0; i < n; i++)
            {
                if (positions[i] <= t) idx1 = i;
            }
            int idx0 = (idx1 - 1 + n) % n;
            int idx2 = (idx1 + 1) % n;
            int idx3 = (idx1 + 2) % n;

            double p1 = positions[idx1];
            double p2 = positions[idx2];
            if (p2 <= p1) p2 += 1.0;

            double at = t;
            if (at < p1) at += 1.0;

            double segLen = p2 - p1;
            double lt = (segLen > 0) ? (at - p1) / segLen : 0;
            lt = Math.Max(0, Math.Min(1, lt));

            double d0 = depths[idx0], d1 = depths[idx1];
            double d2 = depths[idx2], d3 = depths[idx3];

            double a = -0.5 * d0 + 1.5 * d1 - 1.5 * d2 + 0.5 * d3;
            double b = d0 - 2.5 * d1 + 2.0 * d2 - 0.5 * d3;
            double c = -0.5 * d0 + 0.5 * d2;
            double dv = d1;

            return Math.Max(0, a * lt * lt * lt + b * lt * lt + c * lt + dv);
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
