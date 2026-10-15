// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 프레임마다 곱하던 감쇠·따라붙기 계수를 60fps 한 프레임을 기준으로 시간 정규화한다(Android VisualizerEngine 의 k 와 norm).
    ///
    /// 계수를 그린 프레임마다 그대로 곱하면 최대 프레임(30~모니터 주사율)에 따라 움직임이 달라진다. 144fps 에서는 60fps 보다
    /// 2.4배 빨리 줄고 빨리 따라붙었다. 지난 프레임 뒤로 흐른 시간을 60fps 프레임 수(step)로 재서, 감쇠는 decay^step,
    /// 따라붙기는 1-(1-c)^step 으로 바꾼다. 60fps 에서는 step 이 1 이라 예전 값 그대로다.
    /// </summary>
    public static class FrameStep
    {
        /// <summary>기준 프레임 길이(60fps). 예전 계수는 모두 이 간격으로 그릴 때의 값이다.</summary>
        public const double ReferenceFrameMs = 1000.0 / 60.0;

        /// <summary>
        /// 한 번에 진행하는 가장 긴 시간(60fps 프레임 4개). 창이 잠깐 멈췄다 돌아와도 한 프레임에 크게 건너뛰지 않는다.
        /// Android 는 아래로도 0.25(240fps)에서 자르지만 PC 는 최대 프레임을 모니터 주사율(240Hz 넘는 모니터도 있다)까지
        /// 올릴 수 있어 아래로는 자르지 않는다. 자르면 그보다 빠른 화면에서 다시 빨라진다.
        /// </summary>
        public const double MaxStep = 4.0;

        /// <summary>지난 프레임 뒤로 흐른 시간을 60fps 프레임 수로 바꾼다. 0 과 <see cref="MaxStep"/> 사이다.</summary>
        public static double FromElapsed(double elapsedMs)
        {
            if (!(elapsedMs > 0)) return 0; // 같은 시각에 두 번 불렸거나 시계가 뒤로 갔다. 진행하지 않는다.
            return Math.Min(elapsedMs / ReferenceFrameMs, MaxStep);
        }

        /// <summary>60fps 한 프레임에 <paramref name="perFrame"/> 을 곱하던 감쇠를 <paramref name="step"/> 프레임 분량으로 바꾼다.</summary>
        public static float Decay(float perFrame, double step) =>
            step == 1.0 ? perFrame : (float)Math.Pow(perFrame, step);

        /// <summary>
        /// 60fps 한 프레임에 남은 거리의 <paramref name="perFrame"/> 만큼 따라붙던 계수를 <paramref name="step"/> 프레임 분량으로 바꾼다.
        /// 1 이면 바로 붙고, 0 이면 움직이지 않는다.
        /// </summary>
        public static double Approach(double perFrame, double step) =>
            step == 1.0 ? perFrame : 1.0 - Math.Pow(1.0 - perFrame, step);
    }

    /// <summary>
    /// 그린 프레임 사이의 시간을 재서 <see cref="FrameStep"/> 의 step 으로 준다. 첫 프레임과 <see cref="Reset"/> 직후는 1 이다
    /// (쉬다가 깨어난 첫 프레임이 쉰 시간만큼 한꺼번에 진행하지 않게). 렌더 스레드에서만 쓴다.
    /// </summary>
    public sealed class FrameClock
    {
        private double _lastMs = double.NaN;

        /// <summary>이번 프레임의 시각(밀리초, 단조 증가)을 받아 지난 프레임 뒤로 흐른 60fps 프레임 수를 돌려준다.</summary>
        public double Next(double nowMs)
        {
            double last = _lastMs;
            _lastMs = nowMs;
            return double.IsNaN(last) ? 1.0 : FrameStep.FromElapsed(nowMs - last);
        }

        /// <summary>다음 프레임을 첫 프레임처럼 다룬다.</summary>
        public void Reset() => _lastMs = double.NaN;
    }
}
