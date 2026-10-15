// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Help
{
    /// <summary>
    /// 실행 파일의 버전(AssemblyInformationalVersion)을 화면과 제보에 맞게 적는다.
    ///
    /// .NET SDK 는 이 값 뒤에 빌드한 커밋을 붙인다("1.0.0+a8d9c377c4a4..."). 앱 정보에는 사람이 읽는 앞부분만 보이고,
    /// 제보에는 커밋을 짧게(7자) 남긴다. 같은 버전 번호로 여러 번 빌드해도 어느 빌드에서 난 문제인지 가릴 수 있다.
    /// </summary>
    public static class AppVersionText
    {
        private const int ShortCommitLength = 7;

        /// <summary>앱 정보에 보일 버전. 빌드 정보(+ 뒤)를 뗀다. 값이 없으면 빈 문자열.</summary>
        public static string Display(string? informationalVersion)
        {
            string value = (informationalVersion ?? "").Trim();
            int plus = value.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? value : value[..plus];
        }

        /// <summary>제보에 넣을 버전. 빌드 정보가 있으면 7자로 줄여 붙인다("1.0.0+a8d9c37").</summary>
        public static string ForReport(string? informationalVersion)
        {
            string value = (informationalVersion ?? "").Trim();
            int plus = value.IndexOf('+', StringComparison.Ordinal);
            if (plus < 0) return value;
            string build = value[(plus + 1)..];
            if (build.Length == 0) return value[..plus];
            return value[..plus] + "+" + (build.Length > ShortCommitLength ? build[..ShortCommitLength] : build);
        }
    }
}
