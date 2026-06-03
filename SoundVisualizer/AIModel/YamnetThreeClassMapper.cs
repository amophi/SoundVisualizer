using System;

namespace SoundVisualizer.AIModel
{
    /// <summary>
    /// YAMNet 521 클래스 표시명 → 시스템 3분류(ambient / speech / danger).
    /// 규칙은 키워드 우선순위: danger &gt; speech &gt; ambient &gt; 기본 ambient.
    /// </summary>
    public static class YamnetThreeClassMapper
    {
        public static string TranslateToKorean(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "알 수 없음";
            var s = displayName.ToLowerInvariant();

            if (s.Contains("gunshot") || s.Contains("gunfire")) return "총소리";
            if (s.Contains("machine gun")) return "기관총";
            if (s.Contains("explosion") || s.Contains("폭발")) return "폭발음";
            if (s.Contains("artillery") || s.Contains("fusillade") || s.Contains("cap gun")) return "총소리";

            if (s.Contains("footstep") || s.Contains("footsteps")) return "발소리";
            if (s.Contains("siren") || s.Contains("civil defense")) return "사이렌";
            if (s.Contains("horn")) return "경적";
            if (s.Contains("car") || s.Contains("truck") || s.Contains("bus") || s.Contains("motorcycle")) return "자동차";
            if (s.Contains("helicopter")) return "헬리콥터";
            if (s.Contains("engine") || s.Contains("idling") || s.Contains("accelerating")) return "엔진소리";
            if (s.Contains("alarm") || s.Contains("smoke detector")) return "사이렌";
            if (s.Contains("police car") || s.Contains("ambulance") || s.Contains("fire engine") || s.Contains("fire truck")) return "사이렌";

            if (s.Contains("speech") || s.Contains("conversation") || s.Contains("narration") || s.Contains("speaking") || s.Contains("babbling")) return "사람 목소리";
            if (s.Contains("shout") || s.Contains("screaming") || s.Contains("yell") || s.Contains("laughter") || s.Contains("crying") || s.Contains("sobbing")) return "사람 소리";
            if (s.Contains("music")) return "음악";
            if (s.Contains("wind") || s.Contains("rustling leaves")) return "바람 소리";
            if (s.Contains("rain") || s.Contains("water") || s.Contains("ocean") || s.Contains("waves")) return "비/물 소리";
            if (s.Contains("animal") || s.Contains("dog") || s.Contains("cat") || s.Contains("bird") || s.Contains("bark") || s.Contains("meow")) return "동물 소리";
            if (s.Contains("door") || s.Contains("knock") || s.Contains("slam")) return "문 소리";

            return displayName;
        }

        /// <summary>
        /// YAMNet 521 클래스 중 "Sound effect" 등. 게임/편집 총·폭발이 실제 총 클래스 대신 여기로 자주 붙습니다.
        /// </summary>
        public static bool IsGenericSoundEffectLabel(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return false;
            return displayName.ToLowerInvariant().Contains("sound effect");
        }

        public static string MapDisplayNameToCoarse(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return "ambient";

            var s = displayName.ToLowerInvariant();

            if (MatchesDanger(s))
                return "danger";
            if (MatchesSpeech(s))
                return "speech";
            if (MatchesAmbient(s))
                return "ambient";

            return "ambient";
        }

        /// <summary>
        /// 실험용: 총성이 Plop / Gargling / 비 계열로 top-1 나올 때 coarse를 danger로 올림.
        /// Water·Ocean·Waves/surf는 ambient 오탐 가능성으로 제외.
        /// </summary>
        private static bool MatchesTemporaryGunshotProxyDanger(string s)
        {
            if (s.Contains("plop"))
                return true;
            if (s.Contains("gargling"))
                return true;
            // "rain" 단독 contains는 Train(기차) 오탐 → 비 YAMNet 라벨만
            if (s == "rain" || s.Contains("raindrop") || s.Contains("rain on surface"))
                return true;
            if (s.Contains("waterfall"))
                return true;
            return false;
        }

        private static bool MatchesDanger(string s)
        {
            if (MatchesTemporaryGunshotProxyDanger(s))
                return true;

            // 발소리, 총·폭발, 경적/사이렌·비상 차량, 폭죽 등
            if (s.Contains("footstep") || s.Contains("footsteps"))
                return true;
            if (s.Contains("gunshot") || s.Contains("gunfire") || s.Contains("machine gun") ||
                s.Contains("artillery") || s.Contains("fusillade") || s.Contains("cap gun"))
                return true;
            if (s.Contains("explosion") || s.Contains("fireworks") || s.Contains("firecracker"))
                return true;
            if (s.Contains("civil defense siren"))
                return true;
            if (s.Contains("police car") && s.Contains("siren"))
                return true;
            if (s.Contains("ambulance") && s.Contains("siren"))
                return true;
            if (s.Contains("fire engine") || s.Contains("fire truck"))
                return true;
            if (s.Contains("siren") && !s.Contains("telephone"))
                return true;
            if (s.Contains("smoke detector") || s.Contains("fire alarm"))
                return true;
            if (s == "alarm" || s.Contains("car alarm"))
                return true;
            return false;
        }

        private static bool MatchesSpeech(string s)
        {
            // 대화·발화·웃음·울음·노래 등 사람 목소리 계열
            if (s.Contains("speech") || s.Contains("conversation") || s.Contains("narration"))
                return true;
            if (s.Contains("speaking") || s.Contains("babbling"))
                return true;
            if (s.Contains("shout") || s.Contains("whisper") || s.Contains("screaming"))
                return true;
            if (s.Contains("laughter") || s.Contains("crying") || s.Contains("sobbing"))
                return true;
            if (s.Contains("singing") || s.Contains("choir") || s.Contains("rapping"))
                return true;
            if (s.Contains("crowd") || s.Contains("chatter") || s.Contains("hubbub"))
                return true;
            if (s.Contains("children playing"))
                return true;
            return false;
        }

        private static bool MatchesAmbient(string s)
        {
            if (s.Contains("wind") || s.Contains("rustling leaves"))
                return true;
            if (s.Contains("traffic"))
                return true;
            if (s.Contains("music") || s.EndsWith(" music", StringComparison.Ordinal))
                return true;
            if (s.Contains("environmental noise") || s.Contains("field recording"))
                return true;
            if (s.Contains("ocean") || s.Contains("waves") || s.Contains("rain") || s.Contains("thunder"))
                return true;
            if (s.Contains("white noise") || s.Contains("pink noise") || s.Contains("static"))
                return true;
            return false;
        }
    }
}
