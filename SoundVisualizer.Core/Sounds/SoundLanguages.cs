// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭의 소리 이름과 묶음 이름이 있는 언어. PC 판 화면 언어의 코드로 적는다.
    /// 모바일 판의 values 폴더 18개를 옮긴 것이다(values 는 en, values-in 은 id, values-zh-rCN·zh-rTW 는 zh-Hans·zh-Hant,
    /// values-pt-rBR 는 pt-BR).
    /// </summary>
    public static class SoundLanguages
    {
        /// <summary>기본 언어. 영어 소리 이름은 모델의 클래스 이름 그대로다.</summary>
        public const string English = "en";

        /// <summary>데이터가 있는 언어.</summary>
        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
        {
            "en", "ko", "ja", "zh-Hans", "zh-Hant", "es", "pt-BR", "fr", "de",
            "ru", "it", "vi", "th", "id", "tr", "pl", "hi", "ar",
        });

        /// <summary>
        /// <paramref name="language"/> 를 데이터의 언어 코드로 맞춘다. 대소문자는 가리지 않고("pt-br" → "pt-BR"), 데이터가 없는
        /// 언어나 null 이면 영어다. 데이터를 읽는 곳과 색인의 캐시가 모두 이 값을 키로 써서, 같은 언어를 두 번 만들지 않는다.
        /// </summary>
        public static string Resolve(string? language)
        {
            if (language is not null)
            {
                foreach (string code in All)
                {
                    if (string.Equals(code, language, StringComparison.OrdinalIgnoreCase)) return code;
                }
            }
            return English;
        }
    }
}
