// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// YAMNet 클래스 이름(영어 display_name) → 기본 소리 종류. 모바일 YamnetThreeClassMapper.kt 와 규칙이 같다.
    ///
    /// 위협음 → 대화음 → 환경음 순서로 보고 어디에도 걸리지 않으면 환경음이다. 실제 521개 클래스는 위협음 37개,
    /// 대화음 33개, 환경음 451개로 나뉜다. 옛 PC 판이 위협음으로 보던 총소리 대리 라벨(Plop, Gargling, 비·폭포 소리)과
    /// 발소리는 모바일에서 원래 뜻대로 환경음으로 돌렸으므로(#319) 여기에도 없다.
    ///
    /// 이름을 소문자로 바꿔 비교하므로 부를 때마다 문자열을 하나 만든다. 추론 중에는 부르지 않고,
    /// <see cref="YamnetClassMap"/> 이 클래스마다 한 번 계산해 둔 값을 쓴다.
    /// </summary>
    public static class YamnetThreeClassMapper
    {
        /// <summary>이름에 "sound effect" 가 든 범용 효과음 라벨인지. 효과음에 가려진 위협음을 찾을 때 쓴다.</summary>
        public static bool IsGenericSoundEffectLabel(string? displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            return displayName.ToLowerInvariant().Contains("sound effect");
        }

        /// <summary>사용자 지정을 반영하지 않은 기본 종류. 이름이 없으면 환경음이고, 목록에 없는 이름도 같은 키워드 규칙으로 본다.</summary>
        public static SoundType MapDisplayNameToCoarse(string? displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return SoundType.Ambient;
            string s = displayName.ToLowerInvariant();
            if (MatchesDanger(s)) return SoundType.Danger;
            if (MatchesSpeech(s)) return SoundType.Speech;
            if (MatchesAmbient(s)) return SoundType.Ambient;
            return SoundType.Ambient;
        }

        private static bool MatchesDanger(string s)
        {
            // 주의를 끌어야 하는 신호·갑작스러운 사건으로 승인한 라벨(#343). 정확히 같은 이름만 본다.
            // 투표만 바꾸고, 안전 단서 목록(YamnetSafetyCueDecision)에는 넣지 않는다.
            if (AttentionLabels.Contains(s)) return true;
            // 발소리는 환경음이다. 경적은 이름에 horn 이 든 소리 전부가 아니라 차량 경적 두 라벨만 위협음이다.
            if (s == "vehicle horn, car horn, honking") return true;
            if (s == "air horn, truck horn") return true;
            // 바로 알아야 하는 소리. Rain·Wind 처럼 비슷한 이름이 따라오지 않게 정확히 같은 이름만 본다.
            if (s == "chainsaw" || s == "thunder" || s == "thunderstorm") return true;
            if (s.Contains("gunshot") || s.Contains("gunfire") || s.Contains("machine gun") ||
                s.Contains("artillery") || s.Contains("fusillade") || s.Contains("cap gun"))
            {
                return true;
            }
            if (s.Contains("explosion") || s.Contains("fireworks") || s.Contains("firecracker")) return true;
            // 터지거나 부서지는 소리는 YAMNet 이 원인(총성인지 풍선인지)을 가리지 못해도 위협음으로 본다.
            if (s == "burst, pop" || s == "boom" || s == "bang" ||
                s == "smash, crash" || s == "breaking" || s == "shatter")
            {
                return true;
            }
            if (s.Contains("civil defense siren")) return true;
            if (s.Contains("police car") && s.Contains("siren")) return true;
            if (s.Contains("ambulance") && s.Contains("siren")) return true;
            if (s.Contains("fire engine") || s.Contains("fire truck")) return true;
            if (s.Contains("siren") && !s.Contains("telephone")) return true;
            if (s.Contains("smoke detector") || s.Contains("fire alarm")) return true;
            if (s == "alarm" || s.Contains("car alarm")) return true;
            return false;
        }

        private static bool MatchesSpeech(string s)
        {
            // 악기 이름이다. 사람 목소리가 아니다.
            if (s == "singing bowl") return false;
            // 대화음은 알아듣는 말만이 아니라 사람이 내는 소리 전체다. 개의 낑낑거림 같은 비슷한 이름이 따라오지 않게
            // 정확히 같은 이름만 넣는다(#345).
            if (HumanVocalizationLabels.Contains(s)) return true;
            if (s.Contains("speech") || s.Contains("conversation") || s.Contains("narration")) return true;
            if (s.Contains("speaking") || s.Contains("babbling")) return true;
            if (s.Contains("shout") || s.Contains("whisper") || s.Contains("screaming")) return true;
            if (s.Contains("laughter") || s.Contains("crying") || s.Contains("sobbing")) return true;
            if (s.Contains("singing") || s.Contains("choir") || s.Contains("rapping")) return true;
            if (s.Contains("crowd") || s.Contains("chatter") || s.Contains("hubbub")) return true;
            if (s.Contains("children playing")) return true;
            return false;
        }

        // 어디에도 안 걸린 소리도 환경음이라 결과는 바뀌지 않는다. 일부러 환경음으로 둔 소리를 적어 두고,
        // 모바일 코드와 줄마다 맞춰 보기 쉽게 그대로 옮긴다.
        private static bool MatchesAmbient(string s)
        {
            if (s.Contains("wind") || s.Contains("rustling leaves")) return true;
            if (s.Contains("traffic")) return true;
            if (s.Contains("music") || s.EndsWith(" music", StringComparison.Ordinal)) return true;
            if (s.Contains("environmental noise") || s.Contains("field recording")) return true;
            if (s.Contains("ocean") || s.Contains("waves") || s.Contains("rain") || s.Contains("thunder")) return true;
            if (s.Contains("white noise") || s.Contains("pink noise") || s.Contains("static")) return true;
            return false;
        }

        /// <summary>위협음으로 투표하는 주의 신호 9개(#343). 소문자. 테스트가 실제 클래스 이름과 맞는지 확인한다.</summary>
        internal static readonly FrozenSet<string> AttentionLabels = new[]
        {
            "reversing beeps", "train horn", "train whistle", "foghorn", "bicycle bell",
            "emergency vehicle", "skidding", "tire squeal", "slam",
        }.ToFrozenSet(StringComparer.Ordinal);

        /// <summary>대화음으로 옮긴 사람 소리 11개(#345). 소문자. 테스트가 실제 클래스 이름과 맞는지 확인한다.</summary>
        internal static readonly FrozenSet<string> HumanVocalizationLabels = new[]
        {
            "bellow", "whoop", "yell", "cheering",
            "giggle", "snicker", "belly laugh", "chuckle, chortle",
            "baby cry, infant cry", "whimper", "wail, moan",
        }.ToFrozenSet(StringComparer.Ordinal);
    }
}
