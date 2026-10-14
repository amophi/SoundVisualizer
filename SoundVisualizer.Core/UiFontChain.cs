// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer
{
    /// <summary>
    /// 앱의 글꼴 순서. 모든 화면은 함께 배포하는 Pretendard 를 먼저 쓴다.
    ///
    /// Pretendard 에는 한글, 라틴(베트남어·터키어·폴란드어 포함), 키릴 문자, 일본어 가나가 있고 한자, 태국 문자,
    /// 데바나가리, 아랍 문자는 없다. 없는 글자는 여기서 고른 Windows 글꼴로 채운다. 한자는 언어마다 모양이 달라서
    /// 중국어는 중국어 글꼴, 일본어는 일본어 글꼴을 먼저 둔다(한 목록에 섞으면 앞에 있는 나라의 모양으로 나온다).
    /// </summary>
    public static class UiFontChain
    {
        public const string Primary = "Pretendard";

        /// <summary>설정에 저장된 언어 값(<see cref="LegacyLanguageKeys"/>)에 맞는 대체 글꼴 순서.</summary>
        public static IReadOnlyList<string> FallbacksFor(string? languageKey) => LegacyLanguageKeys.ToOverlayKey(languageKey) switch
        {
            "Japanese" => new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" },
            "Chinese" => new[] { "Microsoft YaHei UI", "Segoe UI" },
            "KOR" => new[] { "Malgun Gothic", "Segoe UI" },
            _ => new[] { "Segoe UI", "Malgun Gothic" },
        };

        /// <summary>WPF FontFamily 에 그대로 넘길 목록. 첫 항목은 앱에 포함한 Pretendard 다.</summary>
        public static string FamilyListFor(string? languageKey, string primaryLocation = "./Fonts/#")
            => primaryLocation + Primary + ", " + string.Join(", ", FallbacksFor(languageKey));
    }
}
