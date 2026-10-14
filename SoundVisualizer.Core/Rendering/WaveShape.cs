// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 파도·외곽선 모드의 안쪽 곡선이 지나는 점들(Android VisualizerEngine 의 buildWavePoints·roundedInnerPoint).
    ///
    /// 화면 둘레를 상단 가운데부터 시계 방향으로 고르게 <see cref="Samples"/> 번 짚고, 자리 여덟 곳의 깊이를 Catmull-Rom 으로 이어
    /// 그만큼 안쪽으로 들인 점을 만든다. 모서리는 그 모서리 깊이의 두 배를 반경으로 2차 베지어로 둥글린다.
    /// 앱의 WaveVisualizer 와 OutlineVisualizer 가 같은 점으로 곡선을 그린다. 렌더 스레드에서만 쓴다. 할당은 없다.
    /// </summary>
    public sealed class WaveShape
    {
        /// <summary>둘레를 짚는 점 수.</summary>
        public const int Samples = 150;

        private const int Positions = 8;
        private readonly double[] _positions = new double[Positions];

        /// <summary>점의 좌표(DIP). <see cref="Build"/> 가 채운다.</summary>
        public double[] X { get; } = new double[Samples];
        public double[] Y { get; } = new double[Samples];

        /// <summary>
        /// 크기 <paramref name="w"/>×<paramref name="h"/> 화면에서 둘레 여덟 자리의 깊이 <paramref name="depths"/>
        /// (0:상단 가운데부터 시계 방향)로 점을 만든다. 깊이는 <paramref name="maxDepth"/> 에서 자른다.
        /// </summary>
        public void Build(double w, double h, ReadOnlySpan<double> depths, double maxDepth)
        {
            double P = 2 * (w + h);

            // 자리마다 둘레에서의 위치(0..1). 상단 가운데가 0 이다.
            _positions[0] = 0;
            _positions[1] = (w / 2.0) / P;
            _positions[2] = (w / 2.0 + h / 2.0) / P;
            _positions[3] = (w / 2.0 + h) / P;
            _positions[4] = (w / 2.0 + h + w / 2.0) / P;
            _positions[5] = (w / 2.0 + h + w) / P;
            _positions[6] = (w / 2.0 + h + w + h / 2.0) / P;
            _positions[7] = (w / 2.0 + h + w + h) / P;

            double d_tr = WaveDepth(_positions[1], depths, maxDepth);
            double d_br = WaveDepth(_positions[3], depths, maxDepth);
            double d_bl = WaveDepth(_positions[5], depths, maxDepth);
            double d_tl = WaveDepth(_positions[7], depths, maxDepth);

            for (int i = 0; i < Samples; i++)
            {
                double dist = (P * i) / Samples;
                double d = WaveDepth(dist / P, depths, maxDepth);
                RoundedInnerPoint(dist, w, h, P, d, d_tr, d_br, d_bl, d_tl, out X[i], out Y[i]);
            }
        }

        /// <summary>
        /// 둘레 거리 <paramref name="dist"/>(상단 가운데부터 시계 방향)에서 깊이 <paramref name="d"/> 만큼 들어온 점. 모서리 근처는 둥글린다.
        ///
        /// 둥글리는 반경이 0 인 모서리(깊이 0)는 건너뛴다(Android 의 r &gt; 0 조건). 짚는 점이 모서리에 정확히 놓이면 예전에는 0/0 으로
        /// 점이 NaN 이 되어 도형 전체가 그려지지 않았다. 16:9 화면은 150 점 가운데 넷이 네 모서리에 정확히 놓여, 모서리 자리 하나만
        /// 조용해도(2.0 의 가운데 소리처럼) 파도와 외곽선이 통째로 사라졌다.
        /// </summary>
        private static void RoundedInnerPoint(double dist, double w, double h, double P, double d,
            double d_tr, double d_br, double d_bl, double d_tl, out double x, out double y)
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

            // 1) Top-Right 코너: 상단 가로선 끝 → 구석의 꺾임(제어점) → 우측 세로선 시작
            if (Math.Abs(dist - c_tr) <= R_tr && R_tr > 0)
            {
                double t = (dist - c_tr + R_tr) / (2 * R_tr);
                QuadBezier(t, w - R_tr, d, w - d, d, w - d, R_tr, out x, out y);
                return;
            }
            // 2) Bottom-Right 코너
            if (Math.Abs(dist - c_br) <= R_br && R_br > 0)
            {
                double t = (dist - c_br + R_br) / (2 * R_br);
                QuadBezier(t, w - d, h - R_br, w - d, h - d, w - R_br, h - d, out x, out y);
                return;
            }
            // 3) Bottom-Left 코너
            if (Math.Abs(dist - c_bl) <= R_bl && R_bl > 0)
            {
                double t = (dist - c_bl + R_bl) / (2 * R_bl);
                QuadBezier(t, R_bl, h - d, d, h - d, d, h - R_bl, out x, out y);
                return;
            }
            // 4) Top-Left 코너
            if (Math.Abs(distToTL) <= R_tl && R_tl > 0)
            {
                double t = (distToTL + R_tl) / (2 * R_tl);
                QuadBezier(t, d, R_tl, d, d, R_tl, d, out x, out y);
                return;
            }

            // 모서리가 아닐 경우 (제한된 직선 구간)
            EdgePosition(dist, w, h, P, out double ex, out double ey);
            if (dist <= c_tr || dist > c_tl) { x = Math.Max(d, Math.Min(w - d, ex)); y = d; return; } // Top
            if (dist <= c_br) { x = w - d; y = Math.Max(d, Math.Min(h - d, ey)); return; }           // Right
            if (dist <= c_bl) { x = Math.Max(d, Math.Min(w - d, ex)); y = h - d; return; }           // Bottom
            x = d; y = Math.Max(d, Math.Min(h - d, ey));                                              // Left
        }

        /// <summary>2차 베지어로 안쪽으로 부드럽게 감싼다.</summary>
        private static void QuadBezier(double t, double p0x, double p0y, double p1x, double p1y, double p2x, double p2y, out double x, out double y)
        {
            x = (1 - t) * (1 - t) * p0x + 2 * (1 - t) * t * p1x + t * t * p2x;
            y = (1 - t) * (1 - t) * p0y + 2 * (1 - t) * t * p1y + t * t * p2y;
        }

        /// <summary>둘레 거리(상단 가운데부터 시계 방향) → 화면 테두리 위의 점.</summary>
        private static void EdgePosition(double dist, double w, double h, double P, out double x, out double y)
        {
            dist = ((dist % P) + P) % P;
            if (dist <= w / 2) { x = w / 2 + dist; y = 0; return; }
            if (dist <= w / 2 + h) { x = w; y = dist - w / 2; return; }
            if (dist <= w / 2 + h + w) { x = w - (dist - (w / 2 + h)); y = h; return; }
            if (dist <= w / 2 + h + w + h) { x = 0; y = h - (dist - (w / 2 + h + w)); return; }
            x = dist - (w / 2 + h + w + h);
            y = 0;
        }

        private double WaveDepth(double t, ReadOnlySpan<double> depths, double maxDepth) =>
            Math.Min(maxDepth, Math.Max(0, InterpolateDepthCatmullRom(t, depths)));

        /// <summary>둘레 위치 t(0..1)의 깊이. 자리 여덟 곳의 깊이를 지나는 Catmull-Rom 곡선이다.</summary>
        private double InterpolateDepthCatmullRom(double t, ReadOnlySpan<double> depths)
        {
            t = ((t % 1.0) + 1.0) % 1.0;
            int n = Positions;

            int idx1 = 0;
            for (int i = 0; i < n; i++)
            {
                if (_positions[i] <= t) idx1 = i;
            }
            int idx0 = (idx1 - 1 + n) % n;
            int idx2 = (idx1 + 1) % n;
            int idx3 = (idx1 + 2) % n;

            double p1 = _positions[idx1];
            double p2 = _positions[idx2];
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
}
