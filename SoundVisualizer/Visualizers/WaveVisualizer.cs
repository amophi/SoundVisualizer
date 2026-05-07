using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SoundVisualizer.Visualizers
{
    public class WaveVisualizer : IVisualizerMode
    {
        private const int WAVE_SAMPLE_COUNT = 400;

        public Geometry GenerateGeometry(VisualizerContext context)
        {
            double[] channelDepths = context.ChannelDepths;
            double time = context.AnimationTime;
            double w = context.Width;
            double h = context.Height;

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

            double[] channelPos = new double[]
            {
                dist_tc / P, dist_tr / P, dist_rc / P, dist_br / P,
                dist_bc / P, dist_bl / P, dist_lc / P, dist_tl / P
            };

            double d_tr = GetWaveDepth(dist_tr / P, time, channelDepths, channelPos);
            double d_br = GetWaveDepth(dist_br / P, time, channelDepths, channelPos);
            double d_bl = GetWaveDepth(dist_bl / P, time, channelDepths, channelPos);
            double d_tl = GetWaveDepth(dist_tl / P, time, channelDepths, channelPos);

            double activeCornerThreshold = 8.0;

            var activeCorners = new List<double>();

            if (d_tr > activeCornerThreshold) activeCorners.Add(dist_tr); // 우상단
            if (d_br > activeCornerThreshold) activeCorners.Add(dist_br); // 우하단
            if (d_bl > activeCornerThreshold) activeCorners.Add(dist_bl); // 좌하단
            if (d_tl > activeCornerThreshold) activeCorners.Add(dist_tl); // 좌상단

            double baseStep = P / WAVE_SAMPLE_COUNT;

            // 모서리 바로 근처는 점을 아예 찍지 않는 범위
            double cornerSkipRadius = Math.Min(w, h) * 0.025;
            double cornerSparseRadius = Math.Min(w, h) * 0.5;
            double cornerStepMultiplier = 50.0;

            // 고정 배열이 아니라, skip 가능한 List로 변경
            var inner = new List<Point>(WAVE_SAMPLE_COUNT);

            for (double dist = 0; dist < P; )
            {
                double distToCorner = activeCorners.Count > 0
                                    ? GetMinDistanceToCorners(dist, P, activeCorners.ToArray())
                                    : double.MaxValue;

                // 모서리 정점에 너무 가까운 점은 아예 찍지 않음
                if (distToCorner < cornerSkipRadius)
                {
                    dist += baseStep * cornerStepMultiplier;
                    continue;
                }

                double t = dist / P;

                Point edgePos = GetEdgePosition(dist, w, h, P);
                double d = GetWaveDepth(t, time, channelDepths, channelPos);

                Point p;

                if (dist <= dist_tr || dist > dist_tl)
                {
                    // 상단 직선 구간
                    double x = Math.Max(d_tl, Math.Min(w - d_tr, edgePos.X));
                    p = new Point(x, d);
                }
                else if (dist <= dist_br)
                {
                    // 우측 직선 구간
                    double y = Math.Max(d_tr, Math.Min(h - d_br, edgePos.Y));
                    p = new Point(w - d, y);
                }
                else if (dist <= dist_bl)
                {
                    // 하단 직선 구간
                    double x = Math.Max(d_bl, Math.Min(w - d_br, edgePos.X));
                    p = new Point(x, h - d);
                }
                else
                {
                    // 좌측 직선 구간
                    double y = Math.Max(d_tl, Math.Min(h - d_bl, edgePos.Y));
                    p = new Point(d, y);
                }

                inner.Add(p);

                // 모서리 근처는 점 간격을 넓게,
                // 상단/하단/좌측/우측 중앙부는 점 간격을 촘촘하게 유지
                if (distToCorner < cornerSparseRadius)
                {
                    dist += baseStep * cornerStepMultiplier;
                }
                else
                {
                    dist += baseStep;
                }
            }

            if (inner.Count < 4)
                return Geometry.Empty;

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, 0), true, true);
                ctx.LineTo(new Point(w, 0), false, false);
                ctx.LineTo(new Point(w, h), false, false);
                ctx.LineTo(new Point(0, h), false, false);

                ctx.BeginFigure(inner[0], true, true);

                int M = inner.Count;

                var bezierPts = new List<Point>(M * 3);

                for (int i = 0; i < M; i++)
                {
                    Point p0 = inner[(i - 1 + M) % M];
                    Point p1 = inner[i];
                    Point p2 = inner[(i + 1) % M];
                    Point p3 = inner[(i + 2) % M];

                    Point cp1 = new Point(
                        p1.X + (p2.X - p0.X) / 8.0,
                        p1.Y + (p2.Y - p0.Y) / 8.0
                    );

                    Point cp2 = new Point(
                        p2.X - (p3.X - p1.X) / 8.0,
                        p2.Y - (p3.Y - p1.Y) / 8.0
                    );

                    bezierPts.Add(cp1);
                    bezierPts.Add(cp2);
                    bezierPts.Add(p2);
                }

                ctx.PolyBezierTo(bezierPts, true, true);
            }

            geometry.Freeze();
            return geometry;
        }

        private double GetMinDistanceToCorners(double dist, double P, params double[] corners)
        {
            double min = double.MaxValue;

            foreach (double corner in corners)
            {
                double d = GetCircularDistance(dist, corner, P);
                if (d < min)
                    min = d;
            }

            return min;
        }

        private double GetCircularDistance(double a, double b, double P)
        {
            double diff = Math.Abs(a - b);
            return Math.Min(diff, P - diff);
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

        private double GetWaveDepth(double t, double time, double[] depths, double[] positions)
        {
            return Math.Max(0, InterpolateDepthCatmullRom(t, depths, positions));
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
