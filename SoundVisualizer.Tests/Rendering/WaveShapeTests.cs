// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>파도·외곽선의 안쪽 곡선 점(WaveVisualizer·OutlineVisualizer 가 쓰던 계산을 Core 로 옮긴 것).</summary>
public class WaveShapeTests
{
    /// <summary>16:9 화면. 150 점 가운데 넷(24·51·99·126번)이 네 모서리에 정확히 놓인다(배율 125%·150% 도 같다).</summary>
    public static readonly TheoryData<double, double> WideScreens = new()
    {
        { 1920, 1080 }, { 2560, 1440 }, { 3840, 2160 }, { 1536, 864 }, { 1280, 720 },
    };

    [Theory]
    [MemberData(nameof(WideScreens))]
    public void 모서리_자리가_조용해도_점이_모두_화면_안의_수다(double w, double h)
    {
        // 예전 2.0 처럼 좌우 가운데만 깊고 네 모서리가 0 인 경우와, Mid/Side 의 가운데 소리처럼 아래 모서리만 0 인 경우.
        // 예전에는 모서리에 놓인 점이 0/0 으로 NaN 이 되어 파도·외곽선이 통째로 그려지지 않았다.
        double[][] cases =
        {
            new double[] { 0, 0, 80, 0, 0, 0, 80, 0 },
            new double[] { 120, 90, 0, 0, 80, 0, 0, 90 },
            new double[] { 0, 0, 0, 0, 0, 0, 0, 0 },
        };
        foreach (var depths in cases)
        {
            var shape = new WaveShape();
            shape.Build(w, h, depths, maxDepth: 265);
            for (int i = 0; i < WaveShape.Samples; i++)
            {
                Assert.True(double.IsFinite(shape.X[i]) && double.IsFinite(shape.Y[i]), $"{w}x{h} 점 {i}: ({shape.X[i]}, {shape.Y[i]})");
                Assert.InRange(shape.X[i], 0, w);
                Assert.InRange(shape.Y[i], 0, h);
            }
        }
    }

    [Fact]
    public void 깊이가_0인_모서리의_점은_화면_모서리에_놓인다()
    {
        var shape = new WaveShape();
        shape.Build(1920, 1080, new double[] { 0, 0, 80, 0, 0, 0, 80, 0 }, maxDepth: 265);
        AssertPoint(1920, 0, shape, 24);     // 우상단
        AssertPoint(1920, 1080, shape, 51);  // 우하단
        AssertPoint(0, 1080, shape, 99);     // 좌하단
        AssertPoint(0, 0, shape, 126);       // 좌상단
    }

    private static void AssertPoint(double x, double y, WaveShape shape, int i)
    {
        Assert.Equal(x, shape.X[i], 9);
        Assert.Equal(y, shape.Y[i], 9);
    }

    [Fact]
    public void 깊이가_있는_모서리는_깊이의_두_배_반경으로_둥글린다()
    {
        // 모서리 깊이 50 이면 반경 100 의 2차 베지어 한가운데: (w-62.5, 62.5)
        var shape = new WaveShape();
        shape.Build(1920, 1080, new double[] { 50, 50, 50, 50, 50, 50, 50, 50 }, maxDepth: 265);
        Assert.Equal(1920 - 62.5, shape.X[24], 9);
        Assert.Equal(62.5, shape.Y[24], 9);
    }

    [Fact]
    public void 점은_화면_안에_있고_둥근_모서리를_빼면_기준_깊이보다_깊지_않다()
    {
        // 모서리는 그 모서리 깊이의 두 배를 반경으로 둥글리므로 그만큼까지 들어올 수 있다(Android 도 같다).
        var rnd = new Random(350);
        for (int n = 0; n < 200; n++)
        {
            double w = 800 + rnd.Next(3000), h = 600 + rnd.Next(1600);
            double maxDepth = rnd.NextDouble() * (Math.Min(w, h) / 2 - 10);
            var depths = Enumerable.Range(0, 8).Select(_ => rnd.NextDouble() < 0.3 ? 0 : rnd.NextDouble() * maxDepth * 1.5).ToArray();
            var shape = new WaveShape();
            shape.Build(w, h, depths, maxDepth);
            double rounding = Math.Min(Math.Min(w, h) / 2, 2 * maxDepth);
            for (int i = 0; i < WaveShape.Samples; i++)
            {
                double x = shape.X[i], y = shape.Y[i];
                Assert.True(double.IsFinite(x) && double.IsFinite(y));
                Assert.InRange(x, 0, w);
                Assert.InRange(y, 0, h);
                double edge = Math.Min(Math.Min(x, w - x), Math.Min(y, h - y));
                Assert.True(edge <= Math.Max(maxDepth, rounding) + 1e-9, $"{w}x{h} 점 {i} 가 가장자리에서 {edge} 들어왔다 (기준 {maxDepth})");
            }
        }
    }

    [Fact]
    public void 모서리_깊이가_0이_아니면_예전_계산과_같다()
    {
        // Core 로 옮기며 바꾼 것은 반경 0 인 모서리를 건너뛰는 조건뿐이다. 그 밖의 점은 예전 Visualizers 의 계산과 같아야 한다.
        var rnd = new Random(24);
        foreach (var (w, h) in new[] { (1920.0, 1080.0), (2560.0, 1080.0), (1366.0, 768.0), (1080.0, 1920.0), (1919.0, 1080.0) })
        {
            for (int n = 0; n < 50; n++)
            {
                var depths = Enumerable.Range(0, 8).Select(_ => 0.5 + rnd.NextDouble() * 300).ToArray();
                double maxDepth = 1 + rnd.NextDouble() * 400;
                var shape = new WaveShape();
                shape.Build(w, h, depths, maxDepth);
                var legacy = LegacyWavePoints(w, h, depths, maxDepth);
                for (int i = 0; i < WaveShape.Samples; i++)
                {
                    Assert.Equal(legacy[i].X, shape.X[i]);
                    Assert.Equal(legacy[i].Y, shape.Y[i]);
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // 예전 WaveVisualizer(=OutlineVisualizer)의 점 계산. WPF Point 만 튜플로 바꿨다.
    // ---------------------------------------------------------------

    private static (double X, double Y)[] LegacyWavePoints(double w, double h, double[] channelDepths, double baseDepth)
    {
        var channelPos = new double[8];
        double P = 2 * (w + h);
        double dist_tr = w / 2.0;
        double dist_rc = w / 2.0 + h / 2.0;
        double dist_br = w / 2.0 + h;
        double dist_bc = w / 2.0 + h + w / 2.0;
        double dist_bl = w / 2.0 + h + w;
        double dist_lc = w / 2.0 + h + w + h / 2.0;
        double dist_tl = w / 2.0 + h + w + h;
        channelPos[0] = 0;
        channelPos[1] = dist_tr / P;
        channelPos[2] = dist_rc / P;
        channelPos[3] = dist_br / P;
        channelPos[4] = dist_bc / P;
        channelPos[5] = dist_bl / P;
        channelPos[6] = dist_lc / P;
        channelPos[7] = dist_tl / P;

        double d_tr = GetWaveDepth(dist_tr / P, channelDepths, channelPos, baseDepth);
        double d_br = GetWaveDepth(dist_br / P, channelDepths, channelPos, baseDepth);
        double d_bl = GetWaveDepth(dist_bl / P, channelDepths, channelPos, baseDepth);
        double d_tl = GetWaveDepth(dist_tl / P, channelDepths, channelPos, baseDepth);

        int N = 150;
        var pts = new (double, double)[N];
        for (int i = 0; i < N; i++)
        {
            double dist = (P * i) / N;
            double t = dist / P;
            double d = GetWaveDepth(t, channelDepths, channelPos, baseDepth);
            pts[i] = GetRoundedInnerPoint(dist, w, h, P, d, d_tr, d_br, d_bl, d_tl);
        }
        return pts;
    }

    private static (double, double) GetRoundedInnerPoint(double dist, double w, double h, double P, double d, double d_tr, double d_br, double d_bl, double d_tl)
    {
        double c_tr = w / 2.0;
        double c_br = w / 2.0 + h;
        double c_bl = c_br + w;
        double c_tl = c_bl + h;
        double R_tr = Math.Min(d_tr * 2.0, Math.Min(w / 2.0, h / 2.0));
        double R_br = Math.Min(d_br * 2.0, Math.Min(w / 2.0, h / 2.0));
        double R_bl = Math.Min(d_bl * 2.0, Math.Min(w / 2.0, h / 2.0));
        double R_tl = Math.Min(d_tl * 2.0, Math.Min(w / 2.0, h / 2.0));
        double distToTL = dist - c_tl;
        if (dist < w / 2.0) distToTL = dist + P - c_tl;

        if (Math.Abs(dist - c_tr) <= R_tr)
        {
            double t = (dist - c_tr + R_tr) / (2 * R_tr);
            return Bezier(t, (w - R_tr, d), (w - d, d), (w - d, R_tr));
        }
        if (Math.Abs(dist - c_br) <= R_br)
        {
            double t = (dist - c_br + R_br) / (2 * R_br);
            return Bezier(t, (w - d, h - R_br), (w - d, h - d), (w - R_br, h - d));
        }
        if (Math.Abs(dist - c_bl) <= R_bl)
        {
            double t = (dist - c_bl + R_bl) / (2 * R_bl);
            return Bezier(t, (R_bl, h - d), (d, h - d), (d, h - R_bl));
        }
        if (Math.Abs(distToTL) <= R_tl)
        {
            double t = (distToTL + R_tl) / (2 * R_tl);
            return Bezier(t, (d, R_tl), (d, d), (R_tl, d));
        }

        var edgePos = GetEdgePosition(dist, w, h, P);
        if (dist <= c_tr || dist > c_tl) return (Math.Max(d, Math.Min(w - d, edgePos.X)), d);
        if (dist <= c_br) return (w - d, Math.Max(d, Math.Min(h - d, edgePos.Y)));
        if (dist <= c_bl) return (Math.Max(d, Math.Min(w - d, edgePos.X)), h - d);
        return (d, Math.Max(d, Math.Min(h - d, edgePos.Y)));
    }

    private static (double, double) Bezier(double t, (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2) =>
        ((1 - t) * (1 - t) * p0.X + 2 * (1 - t) * t * p1.X + t * t * p2.X,
         (1 - t) * (1 - t) * p0.Y + 2 * (1 - t) * t * p1.Y + t * t * p2.Y);

    private static (double X, double Y) GetEdgePosition(double dist, double w, double h, double P)
    {
        dist = ((dist % P) + P) % P;
        if (dist <= w / 2) return (w / 2 + dist, 0);
        if (dist <= w / 2 + h) return (w, dist - w / 2);
        if (dist <= w / 2 + h + w) return (w - (dist - (w / 2 + h)), h);
        if (dist <= w / 2 + h + w + h) return (0, h - (dist - (w / 2 + h + w)));
        return (dist - (w / 2 + h + w + h), 0);
    }

    private static double GetWaveDepth(double t, double[] depths, double[] positions, double maxDepth) =>
        Math.Min(maxDepth, Math.Max(0, InterpolateDepthCatmullRom(t, depths, positions)));

    private static double InterpolateDepthCatmullRom(double t, double[] depths, double[] positions)
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
}
