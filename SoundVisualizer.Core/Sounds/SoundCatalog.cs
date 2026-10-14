// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭의 한 줄. AI 가 알아듣는 소리 하나와 그 소리의 기본 종류. 모바일 SoundEntry(SoundCatalog.kt)와 같다.
    /// </summary>
    /// <param name="Index">YAMNet 클래스 번호. 언어마다의 소리 이름(Data/SoundNames)도 이 순서다.</param>
    /// <param name="Name">
    /// 모델이 쓰는 영어 이름. 사용자가 고른 종류를 저장하는 키이고, AI(<see cref="YamnetMappingPolicy"/>)가 받는 이름과 같아야 해서
    /// 번역된 이름이 아니라 이것을 쓴다.
    /// </param>
    /// <param name="DefaultType">키워드 규칙(<see cref="YamnetThreeClassMapper"/>)만으로 정한 종류.</param>
    /// <param name="Group">이 소리가 든 묶음(Data/sound_groups.tsv, #349). 표에 없으면 null 이고, 그 소리 하나가 카드가 된다.</param>
    /// <param name="Order">묶음 화면에서 보이는 자리. 표의 줄 순서이고, 표에 없는 소리는 표 뒤에 번호 순서로 온다.</param>
    public sealed record SoundEntry(int Index, string Name, SoundType DefaultType, SoundGroup? Group, int Order);

    /// <summary>분류 탭의 거르기. 종류는 사용자가 바꾼 것까지 반영한 지금 종류로 본다.</summary>
    public enum SoundFilter
    {
        All,
        Ambient,
        Speech,
        Danger,

        /// <summary>바꾼 소리. 지금 종류가 기본 종류와 다른 소리다.</summary>
        Changed,
    }

    /// <summary>
    /// 분류 탭의 소리 목록과 찾기·거르기. 모바일 SoundCatalog.kt 와 같다. 화면을 모르므로 WPF 없이 테스트한다.
    ///
    /// 사용자가 바꾼 종류(overrides)는 설정에 저장하는 그대로 받는다. 키는 모델의 영어 이름, 값은 <see cref="SoundTypeLabels"/> 의
    /// 문자열이고, <see cref="YamnetMappingPolicy.From"/> 이 받는 것과 같은 맵이다.
    /// </summary>
    public static class SoundCatalog
    {
        /// <summary>
        /// 모델의 소리 이름(번호 순서)과 묶음 표(Data/sound_groups.tsv)로 목록을 만든다. 기본 종류는 AI 의 매핑이다.
        /// <paramref name="classNames"/> 는 AI 가 읽는 클래스 목록(<see cref="YamnetClassMap"/>)을 그대로 넘긴다.
        /// </summary>
        public static IReadOnlyList<SoundEntry> Load(IReadOnlyList<string> classNames) => FromNames(classNames, SoundData.GroupTable);

        /// <summary>
        /// 모델의 소리 이름(번호 순서)으로 목록을 만든다.
        /// </summary>
        /// <param name="names">모델의 소리 이름. 번호 순서다.</param>
        /// <param name="groups">소리 이름별 묶음(<see cref="SoundGroups.ParseTable(Stream)"/>). 표에 없는 소리는 묶음이 없다.</param>
        /// <param name="defaultOf">기본 종류. AI 의 매핑이 바뀐 경우를 테스트가 흉내 낼 때만 바꾼다.</param>
        public static IReadOnlyList<SoundEntry> FromNames(
            IReadOnlyList<string> names,
            IReadOnlyDictionary<string, SoundGroupRow>? groups = null,
            Func<string, SoundType>? defaultOf = null)
        {
            ArgumentNullException.ThrowIfNull(names);
            defaultOf ??= YamnetThreeClassMapper.MapDisplayNameToCoarse;
            int afterTable = (groups is { Count: > 0 } ? groups.Values.Max(row => row.Order) : -1) + 1;
            var entries = new SoundEntry[names.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                string name = names[index];
                SoundGroupRow? row = groups is not null && groups.TryGetValue(name, out var found) ? found : null;
                entries[index] = new SoundEntry(index, name, defaultOf(name), row?.Group, row?.Order ?? afterTable + index);
            }
            return Array.AsReadOnly(entries);
        }

        /// <summary>
        /// 분류 탭의 색인을 넣어 둔 데이터로 만든다. 이름은 <paramref name="language"/> 의 것이고, 없는 언어나 빠진 이름은 영어다.
        /// 이름을 1,042번 다듬으므로 화면 스레드에서 부르지 않는다(<see cref="SoundIndexCache"/> 가 스레드 풀에서 부른다).
        /// </summary>
        public static SoundIndex BuildIndex(string? language, IReadOnlyList<SoundEntry> entries)
        {
            string code = SoundLanguages.Resolve(language);
            return BuildIndex(code, entries, SoundData.Names(code), SoundData.GroupTitles(code), SoundData.GroupTitles(SoundLanguages.English));
        }

        /// <summary>
        /// 색인 하나를 만든다. 시간은 거의 찾기 값을 만드는 데(소리마다 두 이름을 <see cref="Normalize"/>) 든다.
        /// </summary>
        /// <param name="language">색인의 언어. 캐시의 키다.</param>
        /// <param name="entries">소리 목록(<see cref="Load"/>).</param>
        /// <param name="labels">그 언어의 소리 이름. 클래스 번호 순서다. 모자라면 모델 이름을 쓴다.</param>
        /// <param name="titles">그 언어의 새로 지은 묶음 이름.</param>
        /// <param name="englishTitles">영어의 새로 지은 묶음 이름. 그 이름으로도 찾게 하고, 그 언어에 빠진 이름을 채운다.</param>
        public static SoundIndex BuildIndex(
            string language,
            IReadOnlyList<SoundEntry> entries,
            IReadOnlyList<string> labels,
            IReadOnlyDictionary<SoundGroup, string> titles,
            IReadOnlyDictionary<SoundGroup, string> englishTitles)
        {
            ArgumentNullException.ThrowIfNull(language);
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(labels);
            ArgumentNullException.ThrowIfNull(titles);
            ArgumentNullException.ThrowIfNull(englishTitles);

            string English(SoundGroup group) => englishTitles.TryGetValue(group, out string? title) ? title : group.Id();
            string Local(SoundGroup group) => titles.TryGetValue(group, out string? title) ? title : English(group);

            var localTitles = new Dictionary<SoundGroup, string>();
            foreach (SoundGroup group in SoundGroupInfo.All)
            {
                if (group.HasOwnTitle()) localTitles[group] = Local(group);
            }

            var searchKeys = new string[entries.Count];
            for (int i = 0; i < searchKeys.Length; i++)
            {
                SoundEntry entry = entries[i];
                searchKeys[i] = SearchKey(SoundIndex.LabelOf(labels, entry), entry.Name);
            }

            return new SoundIndex(
                language,
                entries,
                labels,
                Array.AsReadOnly(searchKeys),
                SoundGroups.Sections(entries),
                localTitles.ToFrozenDictionary(),
                SoundGroups.TitleKeys(Local, English));
        }

        /// <summary>사용자가 바꾼 것까지 반영한 지금 종류. 세 라벨이 아닌 값은 AI 처럼(<see cref="YamnetMappingPolicy.From"/>) 없는 것으로 본다.</summary>
        public static SoundType TypeOf(SoundEntry entry, IReadOnlyDictionary<string, string> overrides) =>
            overrides.TryGetValue(entry.Name, out string? label) && SoundTypeLabels.TryParse(label, out SoundType type)
                ? type
                : entry.DefaultType;

        /// <summary>
        /// 모델 이름의 클래스 번호. 모델 이름은 서로 겹치지 않는다. 글자 그대로(대소문자 포함) 맞춰 보고, 목록에 없으면 -1.
        /// </summary>
        public static int IndexOf(IReadOnlyList<SoundEntry> entries, string? name)
        {
            foreach (SoundEntry entry in entries)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal)) return entry.Index;
            }
            return -1;
        }

        /// <summary>
        /// 찾는 말과 거르기에 맞는 줄만 남긴다. 순서는 그대로다.
        /// </summary>
        /// <param name="searchKeys">줄마다 <see cref="SearchKey"/> 로 만든 값. 입력할 때마다 521개 이름을 다시 다듬지 않게 미리 만든다.</param>
        public static IReadOnlyList<SoundEntry> Filter(
            IReadOnlyList<SoundEntry> entries,
            IReadOnlyList<string> searchKeys,
            IReadOnlyDictionary<string, string> overrides,
            string query,
            SoundFilter filter)
        {
            string needle = Normalize(query);
            var result = new List<SoundEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                SoundEntry entry = entries[i];
                if (Passes(entry, overrides, filter) &&
                    (needle.Length == 0 || searchKeys[i].Contains(needle, StringComparison.Ordinal)))
                {
                    result.Add(entry);
                }
            }
            return result.AsReadOnly();
        }

        /// <summary>
        /// "바꾼 소리" 수. 묶음이 아니라 소리를 센다(묶음 하나를 바꾸면 그 소리 수만큼 는다, #374). 저장값에는 지금 목록에 없는
        /// 이름이나 기본 종류와 같아진 값이 남아 있을 수 있어, "바꾼 소리" 거르기와 같은 기준으로 목록에서 센다.
        /// </summary>
        public static int CountChanged(IReadOnlyList<SoundEntry> entries, IReadOnlyDictionary<string, string> overrides)
        {
            int count = 0;
            foreach (SoundEntry entry in entries)
            {
                if (TypeOf(entry, overrides) != entry.DefaultType) count++;
            }
            return count;
        }

        /// <summary><paramref name="entry"/> 가 거르기에 맞는지. 묶음 화면(<see cref="SoundGroups.Filter"/>)도 소리마다 같은 기준으로 본다.</summary>
        internal static bool Passes(SoundEntry entry, IReadOnlyDictionary<string, string> overrides, SoundFilter filter)
        {
            SoundType type = TypeOf(entry, overrides);
            return filter switch
            {
                SoundFilter.All => true,
                SoundFilter.Ambient => type == SoundType.Ambient,
                SoundFilter.Speech => type == SoundType.Speech,
                SoundFilter.Danger => type == SoundType.Danger,
                SoundFilter.Changed => type != entry.DefaultType,
                _ => false,
            };
        }

        /// <summary>
        /// 찾기에 쓰는 값. 보이는 이름과 모델 이름 어느 쪽으로 찾아도 맞게 둘을 잇는다. 줄바꿈으로 이어서, 찾는 말(줄바꿈이 없다)이
        /// 둘이 맞닿은 곳에서 맞지 않는다. 영어 이름을 함께 넣는 것은 어느 화면 언어에서도 개발자 모드(영어)에서 본 이름으로 찾게 하려는 것이다.
        /// </summary>
        public static string SearchKey(string label, string name) => Normalize(label) + "\n" + Normalize(name);

        /// <summary>
        /// 대소문자, 띄어쓰기, 악센트 같은 결합 부호를 가리지 않는다. "fire truck" 은 "Firetruck" 으로도, "sirène" 은 "sirene" 으로도 찾힌다.
        ///
        /// 문장 부호(쉼표, 가운뎃점 '·'·'・', 붙임표, 괄호)도 띄어쓰기처럼 뺀다. 묶음 이름은 낱말을 이런 부호로 잇는데("부엌·요리",
        /// "TV, radio and …"), 사람은 띄어 쳐서 찾는다. 부호를 남겨 두면 "부엌 요리" 처럼 이어진 두 낱말로는 아무것도 찾지 못했다(#349).
        ///
        /// 한글은 낱자로 풀어 첫소리와 받침을 같은 글자로 본다. 글자를 치는 중에는 입력 칸에 "ㅅ", "상"(다음 글자의 첫소리가 받침으로
        /// 붙은 모양), "홪" 같은 모양이 잠깐 들어오는데, 음절째 비교하면 한 글자 칠 때마다 결과가 사라졌다 나타난다. 겹받침·겹모음(ㄺ, ㅘ)도
        /// 낱자 둘로 푼다.
        ///
        /// 모바일과 같은 차례다(NFD → 결합 부호(Mn) 빼기 → 띄어쓰기·문장 부호(P) 빼기 → 소문자 → 한글 낱자). 띄어쓰기는 유니코드의 것
        /// 모두(모바일 앱의 ICU 정규식과 같다)이고, 소문자는 문화권을 타지 않는다.
        /// </summary>
        internal static string Normalize(string text)
        {
            string decomposed = Decompose(text);
            var builder = new StringBuilder(decomposed.Length);
            Span<char> buffer = stackalloc char[2];
            foreach (Rune rune in decomposed.EnumerateRunes())
            {
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                if (category == UnicodeCategory.NonSpacingMark || IsPunctuation(category) || Rune.IsWhiteSpace(rune)) continue;

                Rune lower = Rune.ToLowerInvariant(rune);
                if (lower.IsBmp && HangulLetters.TryGetValue((char)lower.Value, out string? letters))
                {
                    builder.Append(letters);
                }
                else
                {
                    builder.Append(buffer[..lower.EncodeToUtf16(buffer)]);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// NFD 로 푼다. 짝 없는 서로게이트가 든 글(붙여 넣은 글 등)은 .NET 의 정규화가 받지 않으므로 대체 문자(U+FFFD)로 바꿔 다시 한다.
        /// 모바일(Java)은 그 글자를 그대로 두지만, 어느 쪽이든 실제 이름과는 맞지 않는다.
        /// </summary>
        private static string Decompose(string text)
        {
            try
            {
                return text.Normalize(NormalizationForm.FormD);
            }
            catch (ArgumentException)
            {
                var sanitized = new StringBuilder(text.Length);
                foreach (Rune rune in text.EnumerateRunes()) sanitized.Append(rune.ToString());
                return sanitized.ToString().Normalize(NormalizationForm.FormD);
            }
        }

        private static bool IsPunctuation(UnicodeCategory category) => category is
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.DashPunctuation or
            UnicodeCategory.OpenPunctuation or
            UnicodeCategory.ClosePunctuation or
            UnicodeCategory.InitialQuotePunctuation or
            UnicodeCategory.FinalQuotePunctuation or
            UnicodeCategory.OtherPunctuation;

        /// <summary>한글 낱자(첫소리·가운뎃소리·받침, 겹자모)를 입력 칸에 보이는 호환용 낱자로. NFD 가 음절을 이 낱자들로 푼다.</summary>
        private static readonly FrozenDictionary<char, string> HangulLetters = BuildHangulLetters();

        private static FrozenDictionary<char, string> BuildHangulLetters()
        {
            var map = new Dictionary<char, string>();
            const string initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
            for (int i = 0; i < initials.Length; i++) map[(char)('ᄀ' + i)] = initials[i].ToString();

            string[] vowels =
            {
                "ㅏ", "ㅐ", "ㅑ", "ㅒ", "ㅓ", "ㅔ", "ㅕ", "ㅖ", "ㅗ", "ㅗㅏ", "ㅗㅐ", "ㅗㅣ", "ㅛ", "ㅜ", "ㅜㅓ", "ㅜㅔ", "ㅜㅣ",
                "ㅠ", "ㅡ", "ㅡㅣ", "ㅣ",
            };
            for (int i = 0; i < vowels.Length; i++) map[(char)('ᅡ' + i)] = vowels[i];

            string[] finals =
            {
                "ㄱ", "ㄲ", "ㄱㅅ", "ㄴ", "ㄴㅈ", "ㄴㅎ", "ㄷ", "ㄹ", "ㄹㄱ", "ㄹㅁ", "ㄹㅂ", "ㄹㅅ", "ㄹㅌ", "ㄹㅍ", "ㄹㅎ",
                "ㅁ", "ㅂ", "ㅂㅅ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ",
            };
            for (int i = 0; i < finals.Length; i++) map[(char)('ᆨ' + i)] = finals[i];

            // 치는 중에 입력 칸에 보이는 호환용 겹자모
            map['ㄳ'] = "ㄱㅅ";
            map['ㄵ'] = "ㄴㅈ";
            map['ㄶ'] = "ㄴㅎ";
            map['ㄺ'] = "ㄹㄱ";
            map['ㄻ'] = "ㄹㅁ";
            map['ㄼ'] = "ㄹㅂ";
            map['ㄽ'] = "ㄹㅅ";
            map['ㄾ'] = "ㄹㅌ";
            map['ㄿ'] = "ㄹㅍ";
            map['ㅀ'] = "ㄹㅎ";
            map['ㅄ'] = "ㅂㅅ";
            map['ㅘ'] = "ㅗㅏ";
            map['ㅙ'] = "ㅗㅐ";
            map['ㅚ'] = "ㅗㅣ";
            map['ㅝ'] = "ㅜㅓ";
            map['ㅞ'] = "ㅜㅔ";
            map['ㅟ'] = "ㅜㅣ";
            map['ㅢ'] = "ㅡㅣ";
            return map.ToFrozenDictionary();
        }
    }
}
