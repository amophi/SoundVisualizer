using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SoundVisualizer.Visualizers
{
    public class WaveVisualizer : IVisualizerMode
    {
        private const int WAVE_SAMPLE_COUNT = 400;
        private const double MAX_WAVE_DEPTH_RATIO = 0.22;
        private const double ROUND_CORNER_RATIO = 0.14;
        private const double BASE_INSET_RATIO = 0.015;

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

            double minSize = Math.Min(w, h);
            double cornerRadius = Math.Max(8.0, minSize * ROUND_CORNER_RATIO);
            cornerRadius = Math.Min(cornerRadius, Math.Min(w, h) * 0.5 - 1.0);
            double baseInset = minSize * BASE_INSET_RATIO;
            double P = GetRoundedPerimeter(w, h, cornerRadius);

            double topHalf = (w * 0.5) - cornerRadius;
            double rightLen = h - 2.0 * cornerRadius;
            double bottomLen = w - 2.0 * cornerRadius;
            double arcLen = Math.PI * 0.5 * cornerRadius;

            double dist_tc = 0.0;
            double dist_tr = topHalf;
            double dist_rc = topHalf + arcLen + rightLen * 0.5;
            double dist_br = topHalf + arcLen + rightLen;
            double dist_bc = topHalf + arcLen + rightLen + arcLen + bottomLen * 0.5;
            double dist_bl = topHalf + arcLen + rightLen + arcLen + bottomLen;
            double dist_lc = topHalf + arcLen + rightLen + arcLen + bottomLen + arcLen + rightLen * 0.5;
            double dist_tl = topHalf + arcLen + rightLen + arcLen + bottomLen + arcLen + rightLen;

            double[] channelPos = new double[]
            {
                dist_tc / P, dist_tr / P, dist_rc / P, dist_br / P,
                dist_bc / P, dist_bl / P, dist_lc / P, dist_tl / P
            };

            int N = WAVE_SAMPLE_COUNT;
            var inner = new Point[N];

            for (int i = 0; i < N; i++)
            {
                double dist = (P * i) / N; 
                double t = dist / P;

                double d = GetWaveDepth(t, time, channelDepths, channelPos);
                d = Math.Min(d, Math.Min(w, h) * MAX_WAVE_DEPTH_RATIO);

                GetRoundedEdgePointAndNormal(dist, w, h, cornerRadius, P, out Point edgePos, out Vector normal);
                double finalOffset = baseInset + d;
                inner[i] = new Point(edgePos.X + normal.X * finalOffset, edgePos.Y + normal.Y * finalOffset);
            }

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

        private static double GetRoundedPerimeter(double w, double h, double r)
            => 2.0 * (w + h - 4.0 * r) + 2.0 * Math.PI * r;

        private static void GetRoundedEdgePointAndNormal(
            double dist, double w, double h, double r, double perimeter,
            out Point point, out Vector normal)
        {
            if (r <= 1e-6)
            {
                GetRectEdgePointAndNormal(dist, w, h, out point, out normal);
                return;
            }

            dist = ((dist % perimeter) + perimeter) % perimeter;

            double topHalf = (w * 0.5) - r;
            double rightLen = h - 2.0 * r;
            double bottomLen = w - 2.0 * r;
            double arcLen = Math.PI * 0.5 * r;

            if (dist <= topHalf)
            {
                point = new Point(w * 0.5 + dist, 0);
                normal = new Vector(0, 1);
                return;
            }
            dist -= topHalf;

            if (dist <= arcLen)
            {
                double a = -Math.PI * 0.5 + (dist / arcLen) * (Math.PI * 0.5);
                point = new Point((w - r) + r * Math.Cos(a), r + r * Math.Sin(a));
                normal = new Vector(-Math.Cos(a), -Math.Sin(a));
                normal.Normalize();
                return;
            }
            dist -= arcLen;

            if (dist <= rightLen)
            {
                point = new Point(w, r + dist);
                normal = new Vector(-1, 0);
                return;
            }
            dist -= rightLen;

            if (dist <= arcLen)
            {
                double a = (dist / arcLen) * (Math.PI * 0.5);
                point = new Point((w - r) + r * Math.Cos(a), (h - r) + r * Math.Sin(a));
                normal = new Vector(-Math.Cos(a), -Math.Sin(a));
                normal.Normalize();
                return;
            }
            dist -= arcLen;

            if (dist <= bottomLen)
            {
                point = new Point((w - r) - dist, h);
                normal = new Vector(0, -1);
                return;
            }
            dist -= bottomLen;

            if (dist <= arcLen)
            {
                double a = Math.PI * 0.5 + (dist / arcLen) * (Math.PI * 0.5);
                point = new Point(r + r * Math.Cos(a), (h - r) + r * Math.Sin(a));
                normal = new Vector(-Math.Cos(a), -Math.Sin(a));
                normal.Normalize();
                return;
            }
            dist -= arcLen;

            if (dist <= rightLen)
            {
                point = new Point(0, (h - r) - dist);
                normal = new Vector(1, 0);
                return;
            }
            dist -= rightLen;

            if (dist <= arcLen)
            {
                double a = Math.PI + (dist / arcLen) * (Math.PI * 0.5);
                point = new Point(r + r * Math.Cos(a), r + r * Math.Sin(a));
                normal = new Vector(-Math.Cos(a), -Math.Sin(a));
                normal.Normalize();
                return;
            }
            dist -= arcLen;

            point = new Point(r + dist, 0);
            normal = new Vector(0, 1);
        }

        private static void GetRectEdgePointAndNormal(double dist, double w, double h, out Point point, out Vector normal)
        {
            double perimeter = 2.0 * (w + h);
            dist = ((dist % perimeter) + perimeter) % perimeter;

            if (dist <= w * 0.5)
            {
                point = new Point(w * 0.5 + dist, 0);
                normal = new Vector(0, 1);
                return;
            }
            dist -= (w * 0.5);

            if (dist <= h)
            {
                point = new Point(w, dist);
                normal = new Vector(-1, 0);
                return;
            }
            dist -= h;

            if (dist <= w)
            {
                point = new Point(w - dist, h);
                normal = new Vector(0, -1);
                return;
            }
            dist -= w;

            if (dist <= h)
            {
                point = new Point(0, h - dist);
                normal = new Vector(1, 0);
                return;
            }
            dist -= h;

            point = new Point(dist, 0);
            normal = new Vector(0, 1);
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
