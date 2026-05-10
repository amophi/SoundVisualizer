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
            double maxDepth = 0.0;
            for (int i = 0; i < channelDepths.Length; i++)
            {
                if (channelDepths[i] > maxDepth) maxDepth = channelDepths[i];
                if (channelDepths[i] > 3) anyActive = true;
            }
            if (!anyActive) return Geometry.Empty;

            double maxCornerRadius = Math.Max(5.0, Math.Min(w, h) * 0.08);
            maxCornerRadius = Math.Min(maxCornerRadius, Math.Min(w, h) * 0.5 - 1.0);
            // 저레벨에서는 코너 라운드를 거의 0으로 줄여 "고정 코너 파형"을 방지합니다.
            double activity = Clamp01((maxDepth - 10.0) / 20.0);
            double cornerRadius = maxCornerRadius * activity;
            double P = GetRoundedPerimeter(w, h, cornerRadius);
            double rectPerimeter = 2.0 * (w + h);

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
            double[] cornerDists = new[]
            {
                dist_tr, dist_br, dist_bl, dist_tl
            };
            double cornerFade = Math.Max(12.0, Math.Min(w, h) * 0.09);
            double softCap = Math.Min(w, h) * 0.20;
            double hardCap = Math.Min(w, h) * 0.24;
            double noiseGate = 2.0;

            for (int i = 0; i < N; i++)
            {
                double t = (double)i / N;
                double dist = t * P;
                double rectDist = t * rectPerimeter;
                double dirActivity = GetDirectionalActivity(t, channelPos, channelDepths);
                double dirGate = SmoothStep01((dirActivity - 2.5) / 6.0);
                double rawDepth = GetWaveDepth(t, time, channelDepths, channelPos);
                double d = Math.Max(0.0, rawDepth - noiseGate) * dirGate;

                // 큰 진폭을 부드럽게 누르는 전역 soft-cap
                d = SoftCap(d, softCap);

                // 코너 근처에서는 허용 상한을 더 낮춰 말려 올라가는 현상을 줄입니다.
                double nearestCornerDist = GetNearestCornerDistance(dist, P, cornerDists);
                double cornerBlend = SmoothStep01(nearestCornerDist / cornerFade);
                double cornerCapScale = 0.60 + 0.40 * cornerBlend;
                d = Math.Min(d, hardCap * cornerCapScale);

                GetRectEdgePointAndNormal(rectDist, w, h, out Point rectEdgePos, out Vector rectNormal);
                GetRoundedEdgePointAndNormal(dist, w, h, cornerRadius, P, out Point roundedEdgePos, out Vector roundedNormal);
                Point edgePos = Lerp(rectEdgePos, roundedEdgePos, dirGate);
                Vector normal = Lerp(rectNormal, roundedNormal, dirGate);
                if (normal.LengthSquared < 1e-8)
                    normal = rectNormal;
                else
                    normal.Normalize();
                inner[i] = new Point(edgePos.X + normal.X * d, edgePos.Y + normal.Y * d);
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
                point = new Point((w * 0.5) + dist, 0);
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

        private static double GetNearestCornerDistance(double dist, double perimeter, double[] cornerDists)
        {
            double min = double.MaxValue;
            for (int i = 0; i < cornerDists.Length; i++)
            {
                double d = CircularDistance(dist, cornerDists[i], perimeter);
                if (d < min) min = d;
            }
            return min;
        }

        private static double CircularDistance(double a, double b, double period)
        {
            double diff = Math.Abs(a - b);
            return Math.Min(diff, period - diff);
        }

        private static double SoftCap(double x, double cap)
        {
            if (cap <= 1e-6 || x <= 0.0)
                return 0.0;
            return cap * (1.0 - Math.Exp(-x / cap));
        }

        private static double GetDirectionalActivity(double t, double[] channelPos, double[] channelDepths)
        {
            int n = Math.Min(channelPos.Length, channelDepths.Length);
            if (n == 0)
                return 0.0;

            int nearest = 0;
            double minDist = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double d = CircularDistance(t, channelPos[i], 1.0);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = i;
                }
            }

            return Math.Max(0.0, channelDepths[nearest]);
        }

        private static Point Lerp(Point a, Point b, double t)
        {
            t = Clamp01(t);
            return new Point(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t);
        }

        private static Vector Lerp(Vector a, Vector b, double t)
        {
            t = Clamp01(t);
            return new Vector(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t);
        }

        private static double SmoothStep01(double t)
        {
            t = Clamp01(t);
            return t * t * (3.0 - 2.0 * t);
        }

        private static double Clamp01(double v) => Math.Max(0.0, Math.Min(1.0, v));

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
