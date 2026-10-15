// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Concurrent;
using System.Reflection;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// Core 에 넣어 둔 언어별 문자열 표(<c>Localization/Strings/{코드}.json</c>)를 읽는다. 언어마다 처음 쓸 때 한 번만 읽는다.
    /// 번역 파일이 아직 없는 언어는 null 이고, 그 언어의 화면은 키마다 영어로 나온다.
    /// </summary>
    public static class StringTables
    {
        // csproj 의 EmbeddedResource LogicalName 과 맞춘다.
        private const string ResourcePrefix = "SoundVisualizer.Localization.Strings.";
        private const string ResourceSuffix = ".json";

        private static readonly Assembly Owner = typeof(StringTables).Assembly;
        private static readonly ConcurrentDictionary<string, StringTable?> Cache = new(StringComparer.Ordinal);

        /// <summary>표 파일이 들어 있는 언어 코드(파일 이름 그대로).</summary>
        public static IReadOnlyList<string> AvailableCodes { get; } = Owner.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        /// <summary>표 파일의 내용. 파일이 없으면 null.</summary>
        public static string? ReadJson(string code)
        {
            using var stream = Owner.GetManifestResourceStream(ResourcePrefix + code + ResourceSuffix);
            if (stream == null) return null;
            using var reader = new StreamReader(stream); // BOM 이 있어도 떼고 읽는다
            return reader.ReadToEnd();
        }

        /// <summary>언어의 표. 파일이 없으면 null, 파일이 깨졌으면 <see cref="FormatException"/>.</summary>
        public static StringTable? Get(string code) =>
            Cache.GetOrAdd(code, c => ReadJson(c) is { } json ? StringTable.Parse(c, json) : null);
    }
}
