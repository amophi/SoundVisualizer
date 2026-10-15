// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// 고를 수 있는 화면 언어 18개와, 설정값(<see cref="SystemCode"/> 또는 언어 코드)을 실제 화면 언어로 푸는 규칙.
    /// 목록 순서는 언어 콤보에 그대로 나온다. 안드로이드 판과 같은 언어들이다.
    /// </summary>
    public static class UiLanguages
    {
        /// <summary>Windows 화면 언어를 따르는 선택. 새로 설치하면 이것으로 시작한다.</summary>
        public const string SystemCode = "system";

        /// <summary>지원하지 않는 언어와, 표에 없는 문자열이 기대는 언어.</summary>
        public const string DefaultCode = "en";

        public static IReadOnlyList<UiLanguage> All { get; } = new[]
        {
            new UiLanguage("en", "English"),
            new UiLanguage("ko", "한국어"),
            new UiLanguage("ja", "日本語"),
            new UiLanguage("zh-Hans", "简体中文"),
            new UiLanguage("zh-Hant", "繁體中文"),
            new UiLanguage("es", "Español"),
            new UiLanguage("pt-BR", "Português (Brasil)"),
            new UiLanguage("fr", "Français"),
            new UiLanguage("de", "Deutsch"),
            new UiLanguage("ru", "Русский"),
            new UiLanguage("it", "Italiano"),
            new UiLanguage("vi", "Tiếng Việt"),
            new UiLanguage("th", "ไทย"),
            new UiLanguage("id", "Bahasa Indonesia"),
            new UiLanguage("tr", "Türkçe"),
            new UiLanguage("pl", "Polski"),
            new UiLanguage("hi", "हिन्दी"),
            new UiLanguage("ar", "العربية", IsRightToLeft: true),
        };

        // 예전 판이 설정 파일에 남긴 언어 값(런처 콤보에 보이던 이름과 약어). 읽을 때 한 번 코드로 바꾸고, 저장은 코드로 한다.
        // 첫 실행 기본값이던 "ENG" 도 영어다.
        private static readonly Dictionary<string, string> LegacySettingValues = new(StringComparer.OrdinalIgnoreCase)
        {
            ["KOR"] = "ko",
            ["English"] = "en",
            ["ENG"] = "en",
            ["日本語"] = "ja",
            ["中文"] = "zh-Hans",
            ["Español"] = "es",
            ["Français"] = "fr",
            ["Deutsch"] = "de",
            ["Русский"] = "ru",
        };

        /// <summary>코드로 언어를 찾는다. 대소문자는 가리지 않는다("zh-hans" 도 간체 중국어). 없으면 null.</summary>
        public static UiLanguage? Find(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            string trimmed = code.Trim();
            foreach (var language in All)
            {
                if (string.Equals(language.Code, trimmed, StringComparison.OrdinalIgnoreCase)) return language;
            }
            return null;
        }

        public static bool IsRightToLeft(string? code) => Find(code)?.IsRightToLeft ?? false;

        /// <summary>
        /// 설정 파일에서 읽은 값을 저장할 값으로 맞춘다. 결과는 <see cref="SystemCode"/> 이거나 <see cref="All"/> 의 코드다.
        /// 예전 판의 값("KOR", "日本語" 등)은 코드로 옮긴다. 비었거나 모르는 값(다음 판의 새 언어 등)은 시스템 언어를 따르게 한다.
        /// </summary>
        public static string NormalizeSetting(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return SystemCode;

            // 예전 값은 XAML 에 적힌 글자 그대로라 보통 조합형(NFC)이지만, 다른 편집기를 거쳤을 수도 있어 맞춰서 찾는다.
            string value = stored.Trim().Normalize(NormalizationForm.FormC);
            if (string.Equals(value, SystemCode, StringComparison.OrdinalIgnoreCase)) return SystemCode;
            if (LegacySettingValues.TryGetValue(value, out var migrated)) return migrated;
            return Find(value)?.Code ?? SystemCode;
        }

        /// <summary>설정값을 실제 화면 언어 코드로 푼다. <see cref="SystemCode"/> 이면 <paramref name="uiCulture"/>(Windows 화면 언어)를 따른다.</summary>
        public static string Resolve(string? setting, CultureInfo uiCulture)
        {
            string normalized = NormalizeSetting(setting);
            return normalized == SystemCode ? FromCultureName(uiCulture.Name) : normalized;
        }

        /// <summary>
        /// Windows 문화권 이름("ko-KR", "zh-TW", "pt-PT" 등)을 지원하는 언어로 바꾼다.
        /// 중국어는 번체를 쓰는 대만·홍콩·마카오(또는 Hant 문자 표기)만 번체, 나머지는 간체다. 포르투갈어는 모두 브라질 포르투갈어,
        /// 인도네시아어는 옛 코드 "in" 도 받는다. 그 밖에는 언어 부분이 지원 목록에 있으면 그 언어, 없으면 영어다.
        /// </summary>
        public static string FromCultureName(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName)) return DefaultCode;

            string[] parts = cultureName.Trim().Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return DefaultCode;
            string[] subtags = parts[1..];

            switch (parts[0].ToLowerInvariant())
            {
                case "zh":
                    // 문자 표기가 있으면 그것을 따른다. 옛 .NET 의 "zh-CHT"/"zh-CHS" 도 같은 뜻이다.
                    if (HasSubtag(subtags, "Hant", "CHT")) return "zh-Hant";
                    if (HasSubtag(subtags, "Hans", "CHS")) return "zh-Hans";
                    return HasSubtag(subtags, "TW", "HK", "MO") ? "zh-Hant" : "zh-Hans";
                case "pt":
                    return "pt-BR";
                case "in":
                case "id":
                    return "id";
                default:
                    return Find(parts[0])?.Code ?? DefaultCode;
            }
        }

        private static bool HasSubtag(string[] subtags, params string[] wanted)
        {
            foreach (var subtag in subtags)
            {
                foreach (var w in wanted)
                {
                    if (string.Equals(subtag, w, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }
    }
}
