// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭의 묶음 화면에 필요한 계산. 모바일 SoundGroups.kt 와 같다. 화면을 모르므로 WPF 없이 테스트한다.
    ///
    /// 묶음 상태는 저장하지 않는다. 저장하는 것은 소리마다의 종류(설정의 sound_types, <see cref="SoundTypeOverrides"/>)뿐이고,
    /// 묶음이 모두 같은지·일부 다른지는 그때그때 정한다.
    /// </summary>
    public static class SoundGroups
    {
        /// <summary>미리보기에 이름을 보이는 소리 수. 나머지는 "+N" 으로 센다.</summary>
        public const int PreviewSize = 3;

        /// <summary>구역 순서. 작고 중요한 위협음을 맨 위에, 긴 환경음을 맨 아래에 둔다.</summary>
        public static IReadOnlyList<SoundType> SectionOrder { get; } =
            Array.AsReadOnly(new[] { SoundType.Danger, SoundType.Speech, SoundType.Ambient });

        /// <summary>
        /// 묶음 표(Data/sound_groups.tsv)를 읽는다. 키는 소리 이름(display_name)이고, 표의 줄 순서대로 늘어놓는다.
        ///
        /// '#' 로 시작하는 줄(라이선스 머리글)과 빈 줄은 건너뛴다. 줄 끝은 LF·CRLF 어느 쪽이든 된다. 윈도에서는 git 이 CRLF 로 바꿔
        /// 꺼내고, 그대로 DLL 에 들어간다. 모르는 묶음, 탭이 없는 줄, 이미 나온 소리는 버린다. 그 소리는 표에 없는 것이 되어 제 이름의
        /// 카드로 보인다. 탭이 깨지지 않는 쪽을 고른 것이고, 표가 맞는지는 테스트(SoundGroupTableTests)가 막는다.
        /// 받은 스트림은 닫지 않는다.
        /// </summary>
        public static IReadOnlyDictionary<string, SoundGroupRow> ParseTable(Stream input)
        {
            ArgumentNullException.ThrowIfNull(input);
            using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return ParseTable(reader);
        }

        /// <inheritdoc cref="ParseTable(Stream)"/>
        public static IReadOnlyDictionary<string, SoundGroupRow> ParseTable(TextReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            var rows = new OrderedDictionary<string, SoundGroupRow>(StringComparer.Ordinal);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                int tab = line.IndexOf('\t');
                if (tab < 0) continue;
                if (SoundGroupInfo.FromId(line[..tab]) is not { } group) continue;
                string name = line[(tab + 1)..];
                if (name.Length == 0 || rows.ContainsKey(name)) continue;
                rows.Add(name, new SoundGroupRow(group, rows.Count));
            }
            return new ReadOnlyDictionary<string, SoundGroupRow>(rows);
        }

        /// <summary>
        /// 카드를 구역별로 나눈다. 카드는 (묶음, 소리마다의 기본 종류)다.
        ///
        /// 기본 종류는 <see cref="SoundEntry.DefaultType"/> 그대로다. 매핑이 바뀌면 표를 고치지 않아도 그 소리가 새 구역에 같은 묶음
        /// 이름으로 따로 보인다. 구역은 <see cref="SectionOrder"/> 순서이고 카드가 없는 구역은 뺀다. 구역 안의 카드는
        /// <see cref="SoundGroup"/> 순서이고, 표에 없는 소리는 그 뒤에 한 소리씩 카드가 된다. 카드 안의 소리는 표의 순서다.
        /// </summary>
        public static IReadOnlyList<SoundSection> Sections(IReadOnlyList<SoundEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            // 이름이 겹치면 뒤의 것(모바일 associateBy). 모델 이름은 겹치지 않는다.
            var byName = new Dictionary<string, SoundEntry>(StringComparer.Ordinal);
            foreach (SoundEntry entry in entries) byName[entry.Name] = entry;

            var cards = entries
                .GroupBy(entry => new CardKey(entry.Group, entry.DefaultType, entry.Group is null ? entry.Index : -1))
                .Select(members =>
                {
                    SoundEntry[] sorted = members.OrderBy(entry => entry.Order).ThenBy(entry => entry.Index).ToArray();
                    SoundEntry? title = members.Key.Group switch
                    {
                        null => sorted[0],
                        { } group when group.TitleSound() is { } sound => byName.GetValueOrDefault(sound) ?? sorted[0],
                        _ => null,
                    };
                    return new SoundCard(members.Key.Group, members.Key.Type, Array.AsReadOnly(sorted), title);
                })
                .ToList();

            return Array.AsReadOnly(cards
                .GroupBy(card => card.DefaultType)
                .OrderBy(inSection => SectionRank(inSection.Key))
                .Select(inSection => new SoundSection(
                    inSection.Key,
                    Array.AsReadOnly(inSection
                        .OrderBy(card => card.Group is { } group ? (int)group : int.MaxValue)
                        .ThenBy(card => card.Members[0].Order)
                        .ToArray())))
                .ToArray());
        }

        /// <summary>표에 없는 소리는 <paramref name="Single"/> 에 번호를 넣어 저마다 카드가 되게 한다.</summary>
        private readonly record struct CardKey(SoundGroup? Group, SoundType Type, int Single);

        /// <summary>세 종류 밖의 값은 생기지 않지만, 생기더라도 소리를 잃지 않게 맨 뒤 구역으로 둔다.</summary>
        private static int SectionRank(SoundType type)
        {
            for (int i = 0; i < SectionOrder.Count; i++)
            {
                if (SectionOrder[i] == type) return i;
            }
            return SectionOrder.Count;
        }

        /// <summary>카드의 지금 종류. 사용자가 바꾼 것(<paramref name="overrides"/>)까지 반영한다.</summary>
        public static GroupState State(SoundCard card, IReadOnlyDictionary<string, string> overrides)
        {
            SoundType type = SoundCatalog.TypeOf(card.Members[0], overrides);
            foreach (SoundEntry entry in card.Members)
            {
                if (SoundCatalog.TypeOf(entry, overrides) != type) return GroupState.Mixed.Instance;
            }
            return new GroupState.Uniform(type);
        }

        /// <summary>
        /// 섞인 카드가 하나라도 있는지. 고급 모드를 끌 때 덮어쓰기를 먼저 알릴지 정한다.
        /// 찾는 말·거르기와 상관없이 모든 카드를 보고, 지난번에 바꿔 둔 것도 저장값으로 함께 본다.
        /// </summary>
        public static bool AnyMixed(IReadOnlyList<SoundSection> sections, IReadOnlyDictionary<string, string> overrides) =>
            sections.Any(section => section.Cards.Any(card => State(card, overrides) is GroupState.Mixed));

        /// <summary>
        /// 카드 이름 밑에 보일 소리. 카드 이름인 소리(<see cref="SoundCard.TitleEntry"/>)는 이미 보이므로 뺀다. 그래서 그 소리 하나뿐인
        /// 카드(전기톱 등)는 빈 미리보기다. <paramref name="first"/> 에 든 소리(찾는 말에 맞은 소리)를 앞에 두고, 나머지는 표의 순서다.
        /// </summary>
        public static GroupPreview Preview(SoundCard card, IEnumerable<SoundEntry>? first = null)
        {
            var firstIndexes = first is null ? new HashSet<int>() : first.Select(entry => entry.Index).ToHashSet();
            int? titleIndex = card.TitleEntry?.Index;
            var rest = card.Members.Where(entry => entry.Index != titleIndex).ToList();
            var ordered = rest.Where(entry => firstIndexes.Contains(entry.Index))
                .Concat(rest.Where(entry => !firstIndexes.Contains(entry.Index)))
                .ToList();
            SoundEntry[] names = ordered.Take(PreviewSize).ToArray();
            return new GroupPreview(Array.AsReadOnly(names), ordered.Count - names.Length);
        }

        /// <summary>
        /// 찾는 말과 거르기에 맞는 카드와 소리. 기본 모드와 고급 모드가 함께 쓴다.
        ///
        /// - 찾는 말은 카드 이름과 소리 이름에서, 보이는 이름과 모델 이름 어느 쪽으로든 찾는다(<see cref="SoundCatalog.SearchKey"/>).
        ///   그래서 한글을 치는 중의 모양("상", "홪")에서도 결과가 끊기지 않는다.
        /// - 카드 이름이 맞으면 묶음의 소리가 모두 맞은 것으로 본다. 소리 이름이 맞으면 그 소리만 맞은 것이다.
        /// - 거르기는 소리마다 지금 종류(또는 바꿨는지)로 본다. 구역은 기본 종류라, 바꾼 묶음도 제 구역에 그대로 있다.
        ///   <paramref name="kept"/> 에 든 소리(거르기를 켠 채로 방금 바꾼 소리, <see cref="KeptSounds"/>)는 거르기에 맞지 않아도 맞은
        ///   것으로 본다. 찾는 말은 그대로 본다.
        /// - 둘 다 맞는 소리가 하나라도 있으면 카드가 남는다. 남은 카드가 없는 구역은 뺀다.
        /// </summary>
        /// <param name="searchKeys">소리마다 <see cref="SoundCatalog.SearchKey"/> 로 만든 값. entry.Index 자리에 둔다.</param>
        /// <param name="titleKeys">
        /// 새로 지은 묶음 이름의 찾기 값(<see cref="TitleKeys"/>). 소리 이름이 곧 묶음 이름이면 그 소리의 값을 쓰므로 넣지 않아도 된다.
        /// </param>
        /// <param name="kept">거르기와 상관없이 남길 소리의 모델 이름(<see cref="KeptSounds.NamesFor"/>).</param>
        public static IReadOnlyList<ShownSection> Filter(
            IReadOnlyList<SoundSection> sections,
            IReadOnlyList<string> searchKeys,
            IReadOnlyDictionary<SoundGroup, string> titleKeys,
            IReadOnlyDictionary<string, string> overrides,
            string query,
            SoundFilter filter,
            IReadOnlySet<string>? kept = null)
        {
            string needle = SoundCatalog.Normalize(query);
            var result = new List<ShownSection>();
            foreach (SoundSection section in sections)
            {
                var cards = new List<ShownCard>();
                foreach (SoundCard card in section.Cards)
                {
                    bool titleMatched = needle.Length == 0 ||
                        TitleKey(card, searchKeys, titleKeys).Contains(needle, StringComparison.Ordinal);
                    var shown = new List<SoundEntry>();
                    foreach (SoundEntry entry in card.Members)
                    {
                        bool passes = SoundCatalog.Passes(entry, overrides, filter) || (kept?.Contains(entry.Name) ?? false);
                        if (passes && (titleMatched || KeyAt(searchKeys, entry.Index).Contains(needle, StringComparison.Ordinal)))
                        {
                            shown.Add(entry);
                        }
                    }
                    if (shown.Count > 0) cards.Add(new ShownCard(card, shown.AsReadOnly()));
                }
                if (cards.Count > 0) result.Add(new ShownSection(section.Type, cards.AsReadOnly()));
            }
            return result.AsReadOnly();
        }

        private static string KeyAt(IReadOnlyList<string> searchKeys, int index) =>
            (uint)index < (uint)searchKeys.Count ? searchKeys[index] : "";

        private static string TitleKey(SoundCard card, IReadOnlyList<string> searchKeys, IReadOnlyDictionary<SoundGroup, string> titleKeys)
        {
            if (card.TitleEntry is { } entry && (uint)entry.Index < (uint)searchKeys.Count) return searchKeys[entry.Index];
            if (card.Group is { } group && titleKeys.TryGetValue(group, out string? key)) return key;
            return "";
        }

        /// <summary>
        /// <see cref="Filter"/> 의 titleKeys. 새로 지은 묶음 이름을 소리 이름처럼 화면 언어의 이름과 영어 이름 어느 쪽으로도 찾게 한다.
        /// 이름이 소리 이름인 묶음은 그 소리의 찾기 값을 쓰므로 넣지 않는다.
        /// </summary>
        /// <param name="local">화면 언어의 묶음 이름.</param>
        /// <param name="english">영어의 묶음 이름.</param>
        public static IReadOnlyDictionary<SoundGroup, string> TitleKeys(Func<SoundGroup, string> local, Func<SoundGroup, string> english)
        {
            var keys = new Dictionary<SoundGroup, string>();
            foreach (SoundGroup group in SoundGroupInfo.All)
            {
                if (group.HasOwnTitle()) keys[group] = SoundCatalog.SearchKey(local(group), english(group));
            }
            return keys.ToFrozenDictionary();
        }

        /// <summary>
        /// 고급 모드 목록. <see cref="Filter"/> 의 결과를 구역 머리, 묶음 머리, 소리 줄 차례로 편다. 그래서 맞는 줄이 있는 묶음과
        /// 구역만 머리가 보이고, 카드 이름이 맞은 묶음은 소리가 모두 보인다.
        /// </summary>
        public static IReadOnlyList<AdvancedItem> AdvancedItems(IReadOnlyList<ShownSection> shown)
        {
            var items = new List<AdvancedItem>();
            foreach (ShownSection section in shown)
            {
                items.Add(new AdvancedItem.SectionHeading(section.Type));
                foreach (ShownCard card in section.Cards)
                {
                    items.Add(new AdvancedItem.GroupHeading(card.Card));
                    foreach (SoundEntry entry in card.Shown) items.Add(new AdvancedItem.Row(entry));
                }
            }
            return items.AsReadOnly();
        }

        /// <summary>
        /// 기본 모드에서 <paramref name="card"/> 를 <paramref name="picked"/> 로 고르면 종류가 바뀌는 소리 수. 묻는 창
        /// (<see cref="OverwritesOneByOne"/>)에 쓴다. 하나씩 바꿔 둔 소리만이 아니라 기본 종류 그대로인 소리까지, 고르면 종류가 바뀌는
        /// 소리를 모두 센다.
        /// </summary>
        public static int OverwriteCount(SoundCard card, IReadOnlyDictionary<string, string> overrides, SoundType picked) =>
            card.Members.Count(entry => SoundCatalog.TypeOf(entry, overrides) != picked);

        /// <summary>
        /// 기본 모드에서 <paramref name="card"/> 를 <paramref name="picked"/> 로 고를 때 먼저 물을지(#374). 하나씩 바꿔 둔 소리의 종류가
        /// 실제로 바뀔 때만 묻는다.
        ///
        /// 하나씩 바꿔 둔 소리는 섞인 카드(<see cref="GroupState.Mixed"/>)에서 기본 종류가 아닌 소리다. "바꾼 소리" 거르기와 수도 같은
        /// 기준이다. 그 소리와 같은 종류를 고르면 그 소리는 그대로라 묻지 않고, 기본 종류를 고르면 그 소리가 되돌려지므로 늘 묻는다.
        /// 모두 같은 카드는 묶음째 바꾼 것이라 묻지 않는다.
        ///
        /// 묶음째 X 로 바꾼 뒤 한 소리만 기본 종류로 되돌린 카드에서 X 를 고르면 묻지 않는다. 저장값에는 기본 종류가 아닌 것만 남아
        /// '되돌린 소리' 와 '건드리지 않은 소리' 를 가릴 수 없다.
        /// </summary>
        public static bool OverwritesOneByOne(SoundCard card, IReadOnlyDictionary<string, string> overrides, SoundType picked) =>
            State(card, overrides) is GroupState.Mixed && card.Members.Any(entry =>
            {
                SoundType type = SoundCatalog.TypeOf(entry, overrides);
                return type != entry.DefaultType && type != picked;
            });
    }
}
