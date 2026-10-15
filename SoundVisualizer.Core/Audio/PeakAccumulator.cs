// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Audio
{
    /// <summary>
    /// 캡처 스레드가 버퍼마다 잰 채널별 최고값을, 렌더 스레드가 프레임마다 가져갈 때까지 모아 둔다.
    ///
    /// 프레임 사이에 버퍼가 여러 개 오면 마지막 값만 남기지 않고 가장 큰 값을 남긴다. 그래야 30~60ms 짜리 총성이
    /// 다음 버퍼의 조용한 값에 덮여 사라지지 않는다. 캡처 스레드와 렌더 스레드가 같은 필드를 따로 고치던 경합도 없앤다
    /// (렌더 스레드의 감쇠가 막 들어온 값을 덮어쓸 수 있었다).
    ///
    /// 채널 순서는 FL, FR, FC, BL, BR, SL, SR, LFE 이다.
    /// </summary>
    public sealed class PeakAccumulator
    {
        public const int ChannelCount = 8;

        private readonly object _gate = new();
        private readonly float[] _peaks = new float[ChannelCount];
        private bool _hasData;

        /// <summary>버퍼 하나의 채널별 최고값을 더한다. 이미 모인 값보다 큰 것만 남는다. 캡처 스레드에서 부른다.</summary>
        public void Offer(float fl, float fr, float fc, float bl, float br, float sl, float sr, float lfe)
        {
            lock (_gate)
            {
                Max(0, fl); Max(1, fr); Max(2, fc); Max(3, bl);
                Max(4, br); Max(5, sl); Max(6, sr); Max(7, lfe);
                _hasData = true;
            }
        }

        /// <summary>
        /// 지난번에 가져간 뒤로 모인 값을 <paramref name="destination"/> 에 쓰고 비운다. 그사이 버퍼가 하나도 오지 않았으면
        /// false 를 돌려주고 아무것도 쓰지 않는다(렌더 쪽은 그때 이전 값을 줄여 나간다). 렌더 스레드에서 부른다.
        /// </summary>
        public bool TryTake(Span<float> destination)
        {
            if (destination.Length < ChannelCount)
                throw new ArgumentException($"destination must hold {ChannelCount} channels", nameof(destination));

            lock (_gate)
            {
                if (!_hasData) return false;
                _peaks.AsSpan().CopyTo(destination);
                Array.Clear(_peaks);
                _hasData = false;
                return true;
            }
        }

        /// <summary>모인 값을 버린다. 캡처를 다시 시작할 때 부른다.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_peaks);
                _hasData = false;
            }
        }

        private void Max(int index, float value)
        {
            // NaN 은 버린다. 장치가 깨진 값을 보내도 그래픽이 굳지 않게 한다.
            if (value > _peaks[index]) _peaks[index] = value;
        }
    }
}
