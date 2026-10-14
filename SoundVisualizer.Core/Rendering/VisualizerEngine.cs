// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Audio;

namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 오버레이가 그린 프레임마다 하는 계산: 채널 피크 → 전체 크기와 방향 분포의 스무딩 → 화면 둘레 여덟 자리의 깊이 → 패드·원형의 움직임.
    /// Android VisualizerEngine.kt 에서 그리기를 뺀 계산부다. 도형은 앱의 Visualizers 가 이 결과로 만든다.
    ///
    /// 감쇠와 따라붙기는 모두 60fps 한 프레임 기준으로 시간 정규화한다(<see cref="FrameStep"/>). 그래서 최대 프레임을 30 으로 두든
    /// 모니터 주사율로 두든 같은 시간 뒤에 같은 모양이 되고, 60fps 에서는 예전과 같다.
    ///
    /// 렌더 스레드(UI 스레드)에서만 쓴다. 프레임마다 힙 할당이 없다.
    /// 채널은 캡처 순서 FL, FR, FC, BL, BR, SL, SR, LFE 다(<see cref="PeakAccumulator"/> 와 같다).
    /// 둘레 자리는 0:FC(상단 중앙) 1:FR(우상단) 2:SR(우측 중앙) 3:BR(우하단) 4:BC(하단 중앙) 5:BL(좌하단) 6:SL(좌측 중앙) 7:FL(좌상단)이다.
    /// </summary>
    public sealed class VisualizerEngine
    {
        public const int Channels = PeakAccumulator.ChannelCount;
        public const int Positions = 8;

        // 캡처 채널 번호
        private const int FL = 0, FR = 1, FC = 2, BL = 3, BR = 4, SL = 5, SR = 6, LFE = 7;

        // 표현 모드(AppSettings.VisualMode)
        public const int ModeWave = 0, ModePad = 1, ModeCircle = 2, ModeOutline = 3;

        /// <summary>버퍼가 오지 않은 프레임마다 채널 목표값에 곱하는 감쇠(60fps 기준).</summary>
        public const float TargetDecay = 0.87f;

        /// <summary>패드 두께·원형 깊이가 프레임마다 목표를 따라붙는 비율(60fps 기준).</summary>
        public const double PadLerp = 0.3;
        public const double CircleLerp = 0.25;

        /// <summary>프레임마다 애니메이션 시각에 더하는 값(60fps 기준).</summary>
        public const double AnimationStep = 0.035;

        /// <summary>크기 100% 가 닿는 한계선은 화면 가운데에서 이만큼(DIP) 바깥이다. 한계선이 이보다 가깝지는 않다.</summary>
        public const double EdgeMargin = 10;

        /// <summary>패드는 파도 깊이의 이 비율을 두께로 쓰고, <see cref="PadMaxThickness"/> 에서 자른다.</summary>
        public const double PadDepthScale = 0.25;
        public const double PadMaxThickness = 55;

        /// <summary>원형은 파도 깊이의 이 비율로 부풀고, 화면 높이의 <see cref="CircleMaxHeightRatio"/> 에서 자른다.</summary>
        public const double CircleDepthScale = 0.35;
        public const double CircleMaxHeightRatio = 0.4;

        private readonly FrameClock _clock = new();

        // 채널별 목표(최근 피크). 버퍼가 오면 그 피크로 바꾸고, 안 오면 줄인다.
        private readonly float[] _targets = new float[Channels];
        // 이번 프레임에 스무딩에 넣은 채널 값
        private readonly float[] _sources = new float[Channels];
        private float _totalTarget;

        // 전체 크기(민감도로 따라붙음)와 방향 분포(속도로 따라붙음)
        private double _smoothTotal;
        private readonly double[] _dist = { 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125 };

        private readonly double[] _depths = new double[Positions];
        private readonly double[] _padThickness = new double[Positions];
        private readonly double[] _circleDepths = new double[Positions];
        private double _baseDepth;
        private double _animationTime;

        private double _width, _height;

        /// <summary>그릴 면의 크기(DIP). 프레임마다 알려 준다.</summary>
        public void SetSurfaceSize(double width, double height)
        {
            _width = width;
            _height = height;
        }

        /// <summary>
        /// 그린 프레임 하나를 계산한다.
        /// </summary>
        /// <param name="nowMs">이번 프레임 시각(밀리초, 단조 증가).</param>
        /// <param name="peaks">지난 프레임 뒤로 들어온 버퍼들의 채널별 최고값. 버퍼가 하나도 오지 않았으면 비운다.</param>
        /// <param name="settings">지금 표현 모드의 설정.</param>
        /// <param name="visualMode">표현 모드(<see cref="ModeWave"/> 등).</param>
        /// <param name="soundMode">사운드 모드(0: 2.0, 1: 5.1, 2: 7.1).</param>
        public void Tick(double nowMs, ReadOnlySpan<float> peaks, VisualModeSettings settings, int visualMode, int soundMode)
        {
            double step = _clock.Next(nowMs);

            // 1. 목표값. 버퍼가 왔으면 그 피크, 안 왔으면 60fps 기준 프레임마다 0.87 씩 줄인 값.
            if (!peaks.IsEmpty)
            {
                peaks[..Channels].CopyTo(_targets);
            }
            else
            {
                float decay = FrameStep.Decay(TargetDecay, step);
                for (int i = 0; i < Channels; i++) _targets[i] *= decay;
            }

            // 2. 스무딩. 전체 크기(민감도·떨림)와 소리가 어디서 나는지의 분포(위치 변화 속도)를 따로 따라간다.
            _targets.AsSpan().CopyTo(_sources);
            float total = 0;
            for (int i = 0; i < Channels; i++) total += _sources[i];
            _totalTarget = total;

            _smoothTotal += (total - _smoothTotal) * FrameStep.Approach(TremorCoefficient(settings), step);

            double sfPosition = FrameStep.Approach(PositionCoefficient(settings), step);
            if (total > 0.0001f)
            {
                for (int i = 0; i < Channels; i++) _dist[i] += ((_sources[i] / total) - _dist[i]) * sfPosition;
            }

            // 3. 깊이. 크기 100% 가 화면 가운데 한계선에 닿게 맞춘다.
            _baseDepth = BaseDepthFor(settings, _width, _height);
            FillDepths(settings.IntensityAsOpacity, soundMode);

            // 4. 패드·원형은 깊이를 다시 한 번 부드럽게 따라간다. 그리는 모드만 움직인다.
            if (visualMode == ModePad) StepPad(step);
            else if (visualMode == ModeCircle) StepCircle(step);

            _animationTime += AnimationStep * step;
        }

        /// <summary>민감도(떨림) → 60fps 한 프레임에 전체 크기가 소리를 따라붙는 비율.</summary>
        internal static float TremorCoefficient(VisualModeSettings s)
        {
            float c = (float)(Math.Max(0.1, s.Sensitivity) / 100.0);
            return c > 1.0f ? 1.0f : c;
        }

        /// <summary>위치 변화 속도 → 60fps 한 프레임에 방향 분포가 새 위치를 따라붙는 비율.</summary>
        internal static float PositionCoefficient(VisualModeSettings s)
        {
            float c = (float)(Math.Max(0.1, s.PositionSpeed) / 100.0);
            return c > 1.0f ? 1.0f : c;
        }

        /// <summary>크기 100% 가 화면 가운데 한계선에 닿는 깊이(DIP). 크기 고정 모드는 고정 크기의 절반을 쓴다.</summary>
        public static double BaseDepthFor(VisualModeSettings s, double width, double height)
        {
            double maxBase = Math.Min(width, height) / 2.0 - EdgeMargin;
            if (maxBase < EdgeMargin) maxBase = EdgeMargin;
            double size = s.IntensityAsOpacity ? s.OpacityFixedSize / 2.0 : s.Intensity;
            return maxBase * (Math.Max(0.0, size) / 100.0);
        }

        private void FillDepths(bool sizeLocked, int soundMode)
        {
            double bd = _baseDepth;
            double total = _smoothTotal;
            if (soundMode == 0)
            {
                // 2.0: 캡처 스레드가 좌우를 옆·뒤로 복사해 두었다. 좌우 가운데 두 자리만 그린다.
                Array.Clear(_depths);
                _depths[2] = Math.Min(bd, bd * (sizeLocked ? 1.0 : total * _dist[FR]));
                _depths[6] = Math.Min(bd, bd * (sizeLocked ? 1.0 : total * _dist[FL]));
                return;
            }

            // 5.1·7.1: 채널마다 제자리에 그린다. 하단 가운데는 뒤 좌우의 가운데(팬텀), LFE 는 자리가 없다.
            _depths[0] = Depth(bd, sizeLocked, total, _dist[FC]);
            _depths[1] = Depth(bd, sizeLocked, total, _dist[FR]);
            _depths[2] = Depth(bd, sizeLocked, total, _dist[SR]);
            _depths[3] = Depth(bd, sizeLocked, total, _dist[BR]);
            _depths[4] = sizeLocked
                ? Math.Min(bd, bd * ((_dist[BL] + _dist[BR]) / 2.0 * 4.0))
                : Math.Min(bd, bd * ((total * _dist[BL] + total * _dist[BR]) / 2.0));
            _depths[5] = Depth(bd, sizeLocked, total, _dist[BL]);
            _depths[6] = Depth(bd, sizeLocked, total, _dist[SL]);
            _depths[7] = Depth(bd, sizeLocked, total, _dist[FL]);
        }

        /// <summary>한 자리의 깊이. 크기 고정이면 분포만으로(4배), 아니면 전체 크기 × 분포로 정하고 기준 깊이에서 자른다.</summary>
        private static double Depth(double baseDepth, bool sizeLocked, double smoothTotal, double share) =>
            Math.Min(baseDepth, baseDepth * (sizeLocked ? share * 4.0 : smoothTotal * share));

        private void StepPad(double step)
        {
            double lerp = FrameStep.Approach(PadLerp, step);
            for (int c = 0; c < Positions; c++)
            {
                double target = _depths[c] * PadDepthScale;
                if (target > PadMaxThickness) target = PadMaxThickness;
                _padThickness[c] += (target - _padThickness[c]) * lerp;
            }
        }

        private void StepCircle(double step)
        {
            double lerp = FrameStep.Approach(CircleLerp, step);
            double cap = _height * CircleMaxHeightRatio;
            for (int i = 0; i < Positions; i++)
            {
                double target = _depths[i] * CircleDepthScale;
                if (target > cap) target = cap;
                _circleDepths[i] += (target - _circleDepths[i]) * lerp;
            }
        }

        /// <summary>스무딩한 전체 크기. 크기 고정 모드의 진하기도 이것으로 정한다.</summary>
        public double SmoothTotal => _smoothTotal;

        /// <summary>이번 프레임 채널 값의 합.</summary>
        public float TotalTarget => _totalTarget;

        /// <summary>크기 100% 의 깊이(DIP).</summary>
        public double BaseDepth => _baseDepth;

        public double AnimationTime => _animationTime;

        /// <summary>둘레 여덟 자리의 깊이(DIP). 파도·외곽선은 이것으로 그린다.</summary>
        public ReadOnlySpan<double> Depths => _depths;

        /// <summary>채널별 방향 분포(캡처 순서, 합이 대체로 1).</summary>
        public ReadOnlySpan<double> Distribution => _dist;

        /// <summary>둘레 여덟 자리의 패드 두께(DIP). 패드 모드에서만 움직인다.</summary>
        public ReadOnlySpan<double> PadThickness => _padThickness;

        /// <summary>둘레 여덟 자리의 원형 깊이(DIP, 반지름에 더한다). 원형 모드에서만 움직인다.</summary>
        public ReadOnlySpan<double> CircleDepths => _circleDepths;
    }
}
