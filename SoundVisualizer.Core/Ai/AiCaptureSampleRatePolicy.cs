// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 어떤 캡처 레이트를 AI 에 넣을지 정한다(Android 의 AiCaptureSampleRatePolicy.kt). 링 버퍼와 리샘플러가 이 목록을 따른다.
    ///
    /// 16·44.1·48kHz 는 Android 와 같다. 16kHz 는 바꾸지 않고 그대로 넣는다.
    /// PC 에서 더한 것: 루프백 캡처는 장치의 믹스 형식 레이트 그대로 받으므로 레이트가 Windows 의 장치 설정을 따른다.
    /// 그래서 흔한 88.2·96·176.4·192kHz 도 FIR 을 레이트에 비례해 늘려(<see cref="CaptureAudioMath.FirTapsFor"/>)
    /// 44.1·48kHz 와 같은 통과·차단 특성으로 받는다. 그 밖의 레이트는 Android 처럼 AI 를 끄고 화면만 돈다.
    /// </summary>
    public static class AiCaptureSampleRatePolicy
    {
        public const int DefaultCaptureSampleRate = 48000;

        private static readonly int[] PreferredCaptureRates = { 48000, 44100 };

        public static bool IsSupportedForAi(int sampleRate) => sampleRate switch
        {
            16000 or 44100 or 48000 => true,
            88200 or 96000 or 176400 or 192000 => true, // PC 에서 더한 레이트
            _ => false,
        };

        /// <summary>
        /// 시도할 캡처 레이트의 순서. 보고된 장치 레이트는 AI 가 받을 수 있을 때만 맨 앞에 두고, 아니면 48·44.1kHz 를 먼저
        /// 해 본 뒤 마지막에 화면만 도는 대안으로 남긴다. 0 이하나 null 은 보고되지 않은 것으로 본다.
        /// </summary>
        public static int[] OrderedCaptureCandidates(int? reportedSampleRate)
        {
            int? reported = reportedSampleRate > 0 ? reportedSampleRate : null;
            var candidates = new List<int>(PreferredCaptureRates.Length + 1);

            if (reported is int supported && IsSupportedForAi(supported)) candidates.Add(supported);
            foreach (int rate in PreferredCaptureRates)
            {
                if (!candidates.Contains(rate)) candidates.Add(rate);
            }
            if (reported is int fallback && !candidates.Contains(fallback)) candidates.Add(fallback);

            return candidates.ToArray();
        }

        /// <summary>후보 가운데 장치가 받아 준 첫 레이트. 하나도 없으면 <see cref="DefaultCaptureSampleRate"/>.</summary>
        public static int SelectCaptureRate(int? reportedSampleRate, Func<int, bool> isAccepted)
        {
            foreach (int rate in OrderedCaptureCandidates(reportedSampleRate))
            {
                if (isAccepted(rate)) return rate;
            }
            return DefaultCaptureSampleRate;
        }
    }
}
