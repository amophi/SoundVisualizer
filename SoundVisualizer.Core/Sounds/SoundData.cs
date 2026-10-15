// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭의 데이터. Core 에 리소스로 넣어 둔 파일(Sounds/Data)을 읽는다. 모바일 판의 res/values*/sound_names.xml,
    /// strings.xml 의 sound_group_* 문구, assets/sound_groups.tsv 를 옮긴 것이다.
    ///
    /// - SoundNames/{언어}.json: 소리 이름 521개의 배열(YAMNet 번호 순서). 안드로이드 이스케이프(\' 와 &amp;amp;)는 풀어 두었다.
    ///   AudioSet 온톨로지의 각색물이라 CC BY-SA 4.0 이다(Data/NOTICE.txt).
    /// - GroupTitles/{언어}.json: 새로 지은 묶음 이름 15개(묶음 id → 이름).
    /// - sound_groups.tsv: 소리마다의 묶음. CC BY-SA 4.0 머리글을 그대로 둔다.
    ///
    /// 언어마다 한 번 읽어 두고 같은(바꿀 수 없는) 목록을 돌려준다. 어느 스레드에서 불러도 된다.
    /// </summary>
    public static class SoundData
    {
        private const string Prefix = "SoundVisualizer.Sounds.Data.";
        private const string NamesFolder = "SoundNames.";
        private const string TitlesFolder = "GroupTitles.";
        private const string Extension = ".json";

        private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> NamesByLanguage = new(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<SoundGroup, string>> TitlesByLanguage =
            new(StringComparer.Ordinal);

        private static readonly Lazy<IReadOnlyDictionary<string, SoundGroupRow>> Table = new(() =>
        {
            using Stream stream = OpenGroupTable();
            return SoundGroups.ParseTable(stream);
        });

        /// <summary>
        /// <paramref name="language"/> 의 소리 이름(YAMNet 번호 순서). 데이터가 없는 언어면 영어이고(<see cref="SoundLanguages.Resolve"/>),
        /// 그 언어에 빠진 이름(빈 값)은 영어 이름으로 채운다.
        /// </summary>
        public static IReadOnlyList<string> Names(string? language) =>
            NamesByLanguage.GetOrAdd(SoundLanguages.Resolve(language), LoadNames);

        /// <summary>
        /// <paramref name="language"/> 의 새로 지은 묶음 이름(<see cref="SoundGroupInfo.HasOwnTitle"/> 인 묶음 15개). 데이터가 없는 언어나
        /// 그 언어에 빠진 이름은 영어다. 나머지 묶음의 이름은 소리 이름(<see cref="SoundGroupInfo.TitleSound"/>)이다.
        /// </summary>
        public static IReadOnlyDictionary<SoundGroup, string> GroupTitles(string? language) =>
            TitlesByLanguage.GetOrAdd(SoundLanguages.Resolve(language), LoadGroupTitles);

        /// <summary>묶음 표(<see cref="SoundGroups.ParseTable(Stream)"/>). 키는 모델의 소리 이름이고 표의 줄 순서다.</summary>
        public static IReadOnlyDictionary<string, SoundGroupRow> GroupTable => Table.Value;

        /// <summary>묶음 표 파일. 머리글(라이선스)까지 그대로다.</summary>
        internal static Stream OpenGroupTable() => Open(Prefix + "sound_groups.tsv")
            ?? throw new InvalidOperationException("sound_groups.tsv 리소스가 없습니다.");

        /// <summary>파일 그대로의 소리 이름(채우지 않은 것). 그 언어의 파일이 없으면 null.</summary>
        internal static IReadOnlyList<string>? ReadNamesFile(string code)
        {
            using Stream? stream = Open(Prefix + NamesFolder + code + Extension);
            if (stream is null) return null;
            using JsonDocument document = JsonDocument.Parse(stream);
            var names = new List<string>();
            foreach (JsonElement element in document.RootElement.EnumerateArray()) names.Add(element.GetString() ?? "");
            return names.AsReadOnly();
        }

        /// <summary>파일 그대로의 새로 지은 묶음 이름(묶음 id → 이름, 파일의 순서). 그 언어의 파일이 없으면 null.</summary>
        internal static IReadOnlyList<KeyValuePair<string, string>>? ReadGroupTitlesFile(string code)
        {
            using Stream? stream = Open(Prefix + TitlesFolder + code + Extension);
            if (stream is null) return null;
            using JsonDocument document = JsonDocument.Parse(stream);
            var titles = new List<KeyValuePair<string, string>>();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                titles.Add(new KeyValuePair<string, string>(property.Name, property.Value.GetString() ?? ""));
            }
            return titles.AsReadOnly();
        }

        /// <summary>리소스로 들어 있는 소리 이름 파일과 묶음 이름 파일의 언어. 지원 언어와 맞는지 테스트가 본다.</summary>
        internal static (IReadOnlyList<string> Names, IReadOnlyList<string> Titles) EmbeddedLanguages()
        {
            var names = new List<string>();
            var titles = new List<string>();
            foreach (string resource in typeof(SoundData).Assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(Prefix, StringComparison.Ordinal) || !resource.EndsWith(Extension, StringComparison.Ordinal)) continue;
                string rest = resource[Prefix.Length..^Extension.Length];
                if (rest.StartsWith(NamesFolder, StringComparison.Ordinal)) names.Add(rest[NamesFolder.Length..]);
                else if (rest.StartsWith(TitlesFolder, StringComparison.Ordinal)) titles.Add(rest[TitlesFolder.Length..]);
            }
            return (names, titles);
        }

        private static Stream? Open(string resource) => typeof(SoundData).Assembly.GetManifestResourceStream(resource);

        private static IReadOnlyList<string> LoadNames(string code)
        {
            IReadOnlyList<string> own = ReadNamesFile(code) ?? Array.Empty<string>();
            if (code == SoundLanguages.English) return own;

            IReadOnlyList<string> english = Names(SoundLanguages.English);
            var names = new string[Math.Max(own.Count, english.Count)];
            for (int i = 0; i < names.Length; i++)
            {
                string? name = i < own.Count ? own[i] : null;
                names[i] = string.IsNullOrWhiteSpace(name) ? (i < english.Count ? english[i] : "") : name;
            }
            return Array.AsReadOnly(names);
        }

        private static IReadOnlyDictionary<SoundGroup, string> LoadGroupTitles(string code)
        {
            var own = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (id, title) in ReadGroupTitlesFile(code) ?? Array.Empty<KeyValuePair<string, string>>())
            {
                if (!string.IsNullOrWhiteSpace(title)) own[id] = title;
            }
            IReadOnlyDictionary<SoundGroup, string>? english = code == SoundLanguages.English ? null : GroupTitles(SoundLanguages.English);

            var titles = new Dictionary<SoundGroup, string>();
            foreach (SoundGroup group in SoundGroupInfo.All)
            {
                if (!group.HasOwnTitle()) continue;
                if (own.TryGetValue(group.Id(), out string? title)) titles[group] = title;
                else if (english is not null && english.TryGetValue(group, out string? fallback)) titles[group] = fallback;
            }
            return titles.ToFrozenDictionary();
        }
    }
}
