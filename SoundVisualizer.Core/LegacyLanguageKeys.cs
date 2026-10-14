// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer
{
    /// <summary>
    /// 설정에 저장된 언어 값과 오버레이가 쓰는 언어 키를 서로 바꾼다.
    /// 런처는 언어를 그 언어의 이름("日本語")으로 저장하고, 오버레이(<c>MainWindow.ApplyLanguage</c>)는 영어 키("Japanese")를 받는다.
    /// 한국어("KOR")와 영어("English")는 양쪽이 같다. 첫 실행의 기본값 "ENG" 는 영어로 본다.
    /// </summary>
    public static class LegacyLanguageKeys
    {
        private static readonly (string Stored, string Overlay)[] Pairs =
        {
            ("KOR", "KOR"),
            ("English", "English"),
            ("日本語", "Japanese"),
            ("中文", "Chinese"),
            ("Español", "Spanish"),
            ("Français", "French"),
            ("Deutsch", "German"),
            ("Русский", "Russian"),
        };

        /// <summary>설정에 저장된 값을 오버레이 키로 바꾼다. 모르는 값은 그대로 돌려준다.</summary>
        public static string ToOverlayKey(string? stored)
        {
            if (stored == "ENG") return "English";
            foreach (var (s, o) in Pairs)
            {
                if (s == stored) return o;
            }
            return stored ?? "English";
        }

        /// <summary>오버레이 키를 설정에 저장할 값으로 바꾼다. 모르는 값은 그대로 돌려준다.</summary>
        public static string ToStoredKey(string? overlayKey)
        {
            foreach (var (s, o) in Pairs)
            {
                if (o == overlayKey) return s;
            }
            return overlayKey ?? "English";
        }
    }
}
