// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 사운드 모드 2.0: 앞 좌우 두 채널을 화면 둘레 여덟 자리로 펼친다(Android VisualizerEngine 의 upmix, Mid/Side).
    ///
    /// 좌우에 함께 있는 몫(Mid = 작은 쪽)은 앞(상단 가운데·양쪽 위 모서리)에, 한쪽이 더 큰 몫(Side = 차이)만 그쪽 옆에 그린다.
    /// 그래서 가운데 소리는 5.1·7.1 의 앞 가운데(FC)처럼 위에 서고, 한쪽으로 치우친 소리는 그쪽 옆만 깊어진다. 예전처럼 좌우를
    /// 옆·뒤로 그대로 복사하면 가운데 소리가 양옆에서 똑같이 그려져 "양쪽에서 나는 소리" 와 구별되지 않았다.
    ///
    /// 뒤쪽 자리(좌하단·하단 가운데·우하단)는 같은 계산을 조금 늦은 좌우로 한다. 소리가 앞에서 뒤로 퍼지는 것처럼 보이는
    /// Android 의 공간 리플(기본값 켜짐)이다. Android 는 5칸 히스토리의 가장 오래된 값(60fps 네 프레임 전)을 쓰지만 틱 수로 세면
    /// 최대 프레임에 따라 지연이 달라지므로, 여기서는 <see cref="RearDelayMs"/> 라는 시간으로 센다.
    /// 렌더 스레드에서만 쓴다. 할당은 없다.
    /// </summary>
    public sealed class StereoUpmix
    {
        /// <summary>뒤쪽 자리가 따라오는 지연. Android 의 60fps 네 프레임과 같다.</summary>
        public const double RearDelayMs = 4 * FrameStep.ReferenceFrameMs;

        // 프레임 시각이 조금 흔들려도 60fps 네 프레임 전 값을 고르게 1ms 를 봐준다.
        private const double DelayToleranceMs = 1;

        // 지연만큼의 프레임을 담는 링. 960fps 까지 지연 시간만큼을 담는다.
        private const int Capacity = 64;
        private readonly double[] _times = new double[Capacity];
        private readonly float[] _left = new float[Capacity];
        private readonly float[] _right = new float[Capacity];
        private int _head; // 다음에 쓸 칸
        private int _count;

        /// <summary>
        /// 이번 프레임의 앞 좌우를 넣고, 뒤쪽 자리에 쓸 좌우(<see cref="RearDelayMs"/> 전의 값)를 돌려준다.
        /// 그만큼 오래된 프레임이 아직 없으면 0 이다(Android 도 히스토리를 0 으로 채워 시작한다).
        /// </summary>
        public void Push(double nowMs, float left, float right, out float rearLeft, out float rearRight)
        {
            _times[_head] = nowMs;
            _left[_head] = left;
            _right[_head] = right;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;

            rearLeft = 0;
            rearRight = 0;
            // 가장 최근 것부터 거슬러 올라가, 지연만큼 지난 첫 프레임을 쓴다. 링이 다 차도 그만큼 지난 것이 없으면 가장 오래된 것을 쓴다.
            for (int n = 1; n <= _count; n++)
            {
                int i = (_head - n + Capacity) % Capacity;
                if (nowMs - _times[i] >= RearDelayMs - DelayToleranceMs || n == Capacity)
                {
                    rearLeft = _left[i];
                    rearRight = _right[i];
                    return;
                }
            }
        }

        /// <summary>지난 프레임을 모두 버린다. 쉬기로 내려갈 때와 사운드 모드를 바꿀 때 부른다.</summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// 앞 좌우와 뒤쪽 좌우를 둘레 여덟 자리(0:FC 1:FR 2:SR 3:BR 4:BC 5:BL 6:SL 7:FL)로 펼치고 그 합을 돌려준다.
        /// 계수는 Android 와 같다.
        /// </summary>
        public static float Spread(float left, float right, float rearLeft, float rearRight, Span<float> positions)
        {
            float mid = Math.Min(left, right);
            float sideL = Math.Max(0f, left - right);
            float sideR = Math.Max(0f, right - left);
            positions[0] = mid * 1.2f;   // FC
            positions[1] = right * 0.9f; // FR
            positions[7] = left * 0.9f;  // FL
            positions[2] = sideR * 1.5f; // SR
            positions[6] = sideL * 1.5f; // SL

            float rearMid = Math.Min(rearLeft, rearRight);
            float rearSideL = Math.Max(0f, rearLeft - rearRight);
            float rearSideR = Math.Max(0f, rearRight - rearLeft);
            positions[3] = rearSideR * 1.2f; // BR
            positions[4] = rearMid * 0.8f;   // BC
            positions[5] = rearSideL * 1.2f; // BL

            float total = 0;
            for (int i = 0; i < 8; i++) total += positions[i];
            return total;
        }
    }
}
