// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Audio
{
    /// <summary>
    /// 지금 소리 크기(0..1). 가장 최근 캡처 버퍼의 채널 가운데 가장 큰 피크다(모바일 AudioEngine.currentLevel). 개발자 모드 HUD 와
    /// 결과 기록의 lvl 칸이 이 값이고, "왜 아무것도 그려지지 않았나" 를 조용해서인지 AI 탓인지 가르는 데 쓴다.
    ///
    /// <see cref="PeakAccumulator"/> 와 달리 읽어도 비우지 않고 버퍼끼리 모으지도 않는다. 버퍼 하나의 값이라 "그사이 소리가 났는가" 는
    /// 이것으로 묻지 않는다(모바일도 같다).
    ///
    /// 채널은 캡처한 것을 모두 본다(모바일 캡처는 좌우 둘뿐이다). 그리는 쪽(<see cref="Rendering.VisualizerEngine.Loudness"/>)은 사운드
    /// 모드가 그리지 못하는 채널을 빼지만, AI 무음 게이트는 버퍼의 모든 채널에서 가장 큰 값을 본다. 이 값은 AI 쪽에 맞춘다. 그래야
    /// 이 값이 <see cref="Ai.AiSilenceGate.ActiveThreshold"/> 아래에 머물 때 "AI 가 추론을 건너뛰었다" 로 읽을 수 있다.
    ///
    /// PC 에서 더한 것: WASAPI 루프백은 아무것도 재생하지 않으면 버퍼를 보내지 않는다. 그대로 두면 마지막 버퍼의 값이 남아 조용한데도
    /// 소리가 나는 것처럼 읽힌다. 그래서 마지막 버퍼가 <see cref="StaleAfterMs"/> 보다 오래되면 0 으로 읽는다. 모바일 캡처는 조용해도
    /// 버퍼를 계속 보내 이런 일이 없다.
    ///
    /// 캡처 스레드가 버퍼마다 <see cref="Offer"/> 하고, UI 스레드가 <see cref="Read"/> 한다. 할당은 없다.
    /// </summary>
    public sealed class CaptureLevel
    {
        /// <summary>
        /// 마지막 버퍼가 이보다 오래되면(밀리초) 소리가 없는 것으로 읽는다. 재생하는 동안 버퍼는 10~50ms 마다 오므로, 그 다섯 배가 넘게
        /// 오지 않으면 재생이 멈춘 것이다.
        /// </summary>
        public const long StaleAfterMs = 250;

        private readonly object _gate = new();
        private float _level;
        private long _receivedMs;
        private bool _hasBuffer;

        /// <summary>
        /// 버퍼 하나의 채널별 최고값을 받는다. 앞 버퍼의 값은 버린다. NaN 인 채널은 건너뛴다(장치가 깨진 값을 보내도 HUD 가 NaN 이 되지 않게).
        /// </summary>
        /// <param name="channelPeaks">이 버퍼의 채널별 최고값(0..1). 순서는 상관없다.</param>
        /// <param name="nowMs">받은 시각(단조 밀리초, <see cref="Environment.TickCount64"/>). <see cref="Read"/> 와 같은 시계다.</param>
        public void Offer(ReadOnlySpan<float> channelPeaks, long nowMs)
        {
            float level = 0f;
            foreach (float peak in channelPeaks)
            {
                if (peak > level) level = peak; // NaN 은 비교가 거짓이라 건너뛴다
            }
            lock (_gate)
            {
                _level = level;
                _receivedMs = nowMs;
                _hasBuffer = true;
            }
        }

        /// <summary>지금 소리 크기. 버퍼를 받은 적이 없거나 마지막 버퍼가 <see cref="StaleAfterMs"/> 보다 오래되면 0 이다.</summary>
        /// <param name="nowMs">읽는 시각(단조 밀리초, <see cref="Offer"/> 와 같은 시계).</param>
        public float Read(long nowMs)
        {
            lock (_gate)
            {
                if (!_hasBuffer || nowMs - _receivedMs > StaleAfterMs) return 0f;
                return _level;
            }
        }
    }
}
