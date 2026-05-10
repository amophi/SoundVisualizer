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

            int N = WAVE_SAMPLE_COUNT;
            var inner = new Point[N];

            for (int i = 0; i < N; i++)
            {
                double dist = (P * i) / N; 
                double t = dist / P;
                
                Point edgePos = GetEdgePosition(dist, w, h, P);
                double d = GetWaveDepth(t, time, channelDepths, channelPos);
                
                if (dist <= dist_tr || dist > dist_tl) 
                {
                    double x = Math.Max(d_tl, Math.Min(w - d_tr, edgePos.X));
                    inner[i] = new Point(x, d);
                }
                else if (dist <= dist_br) 
                {
                    double y = Math.Max(d_tr, Math.Min(h - d_br, edgePos.Y));
                    inner[i] = new Point(w - d, y);
                }
                else if (dist <= dist_bl) 
                {
                    double x = Math.Max(d_bl, Math.Min(w - d_br, edgePos.X));
                    inner[i] = new Point(x, h - d);
                }
                else 
                {
                    double y = Math.Max(d_tl, Math.Min(h - d_bl, edgePos.Y));
                    inner[i] = new Point(d, y);
                }
            }

            // 코너 전환부(상/우/하/좌 모서리)만 국소적으로 부드럽게 이어
            // 직선 구간의 형태는 유지하면서 각진 느낌을 완화합니다.
            int[] cornerIndices = new[]
            {
                ((int)Math.Round(N * (dist_tr / P))) % N,
                ((int)Math.Round(N * (dist_br / P))) % N,
                ((int)Math.Round(N * (dist_bl / P))) % N,
                ((int)Math.Round(N * (dist_tl / P))) % N
            };
            int cornerRadius = Math.Max(3, N / 70);
            ApplyCornerSmoothing(inner, cornerIndices, cornerRadius, passes: 2);

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, 0), true, true);
                ctx.LineTo(new Point(w, 0), false, false);
                ctx.LineTo(new Point(w, h), false, false);
                ctx.LineTo(new Point(0, h), false, false);

                ctx.BeginFigure(inner[0], true, true);

                var bezierPts = new List<Point>(N * 3);
                for (int i = 0; i < N; i++)
                {
                    Point p0 = inner[(i - 1 + N) % N];
                    Point p1 = inner[i];
                    Point p2 = inner[(i + 1) % N];
                    Point p3 = inner[(i + 2) % N];

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

        private static void ApplyCornerSmoothing(Point[] points, int[] cornerIndices, int radius, int passes)
        {
            int n = points.Length;
            if (n < 8 || radius <= 0 || passes <= 0)
                return;

            for (int pass = 0; pass < passes; pass++)
            {
                var next = (Point[])points.Clone();

                foreach (int corner in cornerIndices)
                {
                    for (int off = -radius; off <= radius; off++)
                    {
                        int idx = Mod(corner + off, n);
                        int prevIdx = Mod(idx - 1, n);
                        int nextIdx = Mod(idx + 1, n);

                        double proximity = 1.0 - Math.Abs(off) / (double)(radius + 1);
                        double alpha = 0.55 * proximity;

                        Point cur = points[idx];
                        Point smooth = new Point(
                            (points[prevIdx].X + 2.0 * cur.X + points[nextIdx].X) * 0.25,
                            (points[prevIdx].Y + 2.0 * cur.Y + points[nextIdx].Y) * 0.25);

                        next[idx] = Lerp(cur, smooth, alpha);
                    }
                }

                Array.Copy(next, points, n);
            }
        }

        private static Point Lerp(Point a, Point b, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return new Point(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t);
        }

        private static int Mod(int value, int modulo)
        {
            int r = value % modulo;
            return r < 0 ? r + modulo : r;
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
