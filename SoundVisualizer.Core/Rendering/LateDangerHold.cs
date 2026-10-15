// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Audio;

namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 표시를 꺼 둔 종류라 그리지 못한 피크를 약 1초 보관하는 링 버퍼(Android VisualizerEngine 의 holdL/holdR).
    ///
    /// 위협음만 보이게 해 두면 총성 같은 짧은 소리는 먼저 숨긴 라벨(환경음 등)로 들어오고, 위협음 판정은 다음 분류(250ms 주기)와
    /// 추론 시간만큼 늦게 온다. 그때 피크를 이미 버렸다면 남은 잔향으로 0 에서 자라 거의 보이지 않는다. 그래서 칸마다 가장 큰
    /// 프레임 하나를 남겨 두고, 늦은 판정이 오면 엔진이 이것으로 크기와 방향을 다시 잡는다(<see cref="VisualizerEngine"/>).
    ///
    /// 칸 하나는 60fps 두 프레임(약 33ms)이라 30칸이면 약 1초다. 분류기는 최근 약 1초 창을 250ms 마다 판정하므로, 판정이 몇 번
    /// 늦어져도 그 판정을 부른 소리는 이 안에 있다. 방향이 섞이지 않게 채널들을 한 프레임으로 묶어 다룬다.
    /// 렌더 스레드에서만 쓴다. 할당은 없다.
    /// </summary>
    public sealed class LateDangerHold
    {
        public const int Slots = 30;
        public const int Channels = PeakAccumulator.ChannelCount;

        /// <summary>칸 하나의 길이(60fps 프레임 수).</summary>
        public const double SlotFrames = 2;

        private readonly float[] _ring = new float[Slots * Channels];
        private readonly float[] _ringLevels = new float[Slots];
        private readonly float[] _current = new float[Channels]; // 채우는 중인 칸
        private float _currentLevel;
        private int _index;      // 다음에 덮어쓸(가장 오래된) 칸
        private double _frames;  // 채우는 중인 칸에 쌓인 시간(60fps 프레임 수)

        /// <summary>마지막으로 큰 피크를 넣은 뒤 그 피크가 밀려나기까지 남은 칸 수. 0 이면 보관한 것이 없어 링을 훑지 않는다.</summary>
        private int _liveSlots;

        /// <summary>보관한 피크가 있는지.</summary>
        public bool HasData => _liveSlots > 0;

        /// <summary>
        /// 그리지 못한 프레임의 피크를 지금 칸에 모은다. <paramref name="level"/> 은 그 프레임의 크기(지금 사운드 모드가 그리는 채널의
        /// 최댓값)이고, <paramref name="threshold"/> 이하면 깨울 일도 없으니 넣지 않는다. 칸마다 가장 큰 프레임 하나만 남는다.
        /// </summary>
        public void Keep(ReadOnlySpan<float> peaks, float level, float threshold)
        {
            if (!(level > threshold)) return;
            if (level > _currentLevel)
            {
                peaks[..Channels].CopyTo(_current);
                _currentLevel = level;
            }
            _liveSlots = Slots + 1; // 지금 칸 + 링 한 바퀴
        }

        /// <summary>
        /// 보관 시간을 <paramref name="frames"/>(60fps 프레임 수)만큼 흘린다. 칸이 차면 링에 넣고 가장 오래된 칸을 덮어쓴다.
        /// 큰 피크가 모두 밀려나면 링은 전부 0 이 되므로 그다음부터는 아무것도 하지 않는다.
        /// </summary>
        public void Advance(double frames)
        {
            if (_liveSlots == 0) return;
            _frames += frames;
            while (_frames >= SlotFrames)
            {
                _frames -= SlotFrames;
                _current.AsSpan().CopyTo(_ring.AsSpan(_index * Channels, Channels));
                _ringLevels[_index] = _currentLevel;
                _index = (_index + 1) % Slots;
                Array.Clear(_current);
                _currentLevel = 0;
                if (--_liveSlots == 0)
                {
                    _frames = 0;
                    return;
                }
            }
        }

        /// <summary>
        /// 오래된 칸부터 <paramref name="n"/> 번째 칸(0..<see cref="Slots"/>). 마지막(<see cref="Slots"/>)은 채우는 중인 칸이다.
        /// </summary>
        public ReadOnlySpan<float> Slot(int n) =>
            n < Slots ? _ring.AsSpan((_index + n) % Slots * Channels, Channels) : _current;

        /// <summary><see cref="Slot"/> 과 같은 순서로 그 칸의 크기.</summary>
        public float SlotLevel(int n) => n < Slots ? _ringLevels[(_index + n) % Slots] : _currentLevel;

        /// <summary>보관한 것을 모두 버린다. 한 번 쓴 소리로 두 번 번쩍이지 않게, 쓰고 나면 부른다.</summary>
        public void Clear()
        {
            Array.Clear(_ring);
            Array.Clear(_ringLevels);
            Array.Clear(_current);
            _currentLevel = 0;
            _frames = 0;
            _liveSlots = 0;
        }
    }
}
