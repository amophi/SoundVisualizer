// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭이 그리는 데 쓰는 것을 화면 언어 하나로 한 번 만들어 둔 것(#368). 모바일 SoundIndex 와 같다.
    /// <see cref="SoundCatalog.BuildIndex(string?, IReadOnlyList{SoundEntry})"/> 가 만들고, 보통은 <see cref="SoundIndexCache"/> 로 얻는다.
    ///
    /// 만들 때 이름 521개를 읽고 정규화를 1,042번 하므로, 탭에 들어올 때마다 화면 스레드에서 다시 만들지 않는다. 만든 뒤에는
    /// 바뀌지 않아 어느 스레드에서 읽어도 된다. 글자를 칠 때마다 하는 일은 <see cref="Filter"/>(소리 521개를 거르기)뿐이다.
    /// </summary>
    public sealed class SoundIndex
    {
        private readonly IReadOnlyList<string> _labels;
        private readonly IReadOnlyDictionary<SoundGroup, string> _titles;

        internal SoundIndex(
            string language,
            IReadOnlyList<SoundEntry> entries,
            IReadOnlyList<string> labels,
            IReadOnlyList<string> searchKeys,
            IReadOnlyList<SoundSection> sections,
            IReadOnlyDictionary<SoundGroup, string> titles,
            IReadOnlyDictionary<SoundGroup, string> titleKeys)
        {
            Language = language;
            Entries = entries;
            _labels = labels;
            SearchKeys = searchKeys;
            Sections = sections;
            _titles = titles;
            TitleKeys = titleKeys;
        }

        /// <summary>만든 언어(<see cref="SoundLanguages"/> 의 코드). 캐시의 키다.</summary>
        public string Language { get; }

        /// <summary>소리 목록(<see cref="SoundCatalog.Load"/>). 번호 순서다.</summary>
        public IReadOnlyList<SoundEntry> Entries { get; }

        /// <summary>소리마다의 <see cref="SoundCatalog.SearchKey"/>. entry.Index 자리에 둔다.</summary>
        public IReadOnlyList<string> SearchKeys { get; }

        /// <summary>묶음과 구역(<see cref="SoundGroups.Sections"/>). 언어와 상관없지만 목록이 같으면 늘 같으므로 함께 만들어 둔다.</summary>
        public IReadOnlyList<SoundSection> Sections { get; }

        /// <summary>새로 지은 묶음 이름의 찾기 값(<see cref="SoundGroups.TitleKeys"/>). 이 언어의 이름과 영어 이름을 잇는다.</summary>
        public IReadOnlyDictionary<SoundGroup, string> TitleKeys { get; }

        /// <summary>이 언어의 소리 이름. 이름 목록에 없는 번호(모델이 바뀐 직후 등)면 모델 이름이다.</summary>
        public string LabelOf(SoundEntry entry) => LabelOf(_labels, entry);

        /// <summary>
        /// 카드(묶음)의 이름. 이름이 곧 묶음 이름인 소리가 있으면 그 소리의 이름이고(사이렌, 초인종 등), 아니면 이 언어의 새로 지은
        /// 묶음 이름이다. 고급 모드의 묶음 머리도 같은 이름을 쓴다.
        /// </summary>
        public string CardTitle(SoundCard card)
        {
            if (card.TitleEntry is { } entry) return LabelOf(entry);
            if (card.Group is { } group) return _titles.TryGetValue(group, out string? title) ? title : group.Id();
            return card.Members.Count > 0 ? LabelOf(card.Members[0]) : "";
        }

        /// <summary>찾는 말과 거르기에 맞는 카드와 소리(<see cref="SoundGroups.Filter"/>). 글자를 칠 때마다 부른다.</summary>
        public IReadOnlyList<ShownSection> Filter(
            IReadOnlyDictionary<string, string> overrides,
            string query,
            SoundFilter filter,
            IReadOnlySet<string>? kept = null) =>
            SoundGroups.Filter(Sections, SearchKeys, TitleKeys, overrides, query, filter, kept);

        /// <summary>"바꾼 소리" 수(<see cref="SoundCatalog.CountChanged"/>). 묶음이 아니라 소리를 센다.</summary>
        public int CountChanged(IReadOnlyDictionary<string, string> overrides) => SoundCatalog.CountChanged(Entries, overrides);

        internal static string LabelOf(IReadOnlyList<string> labels, SoundEntry entry) =>
            (uint)entry.Index < (uint)labels.Count ? labels[entry.Index] : entry.Name;
    }

    /// <summary>
    /// 분류 탭의 색인을 언어 하나의 것만 들고 있는다(모바일 SoundCatalog.index·cachedIndexOrNull). 클래스 목록 하나에 하나를 둔다.
    ///
    /// 두는 것은 마지막으로 만든 언어의 것뿐이라, 언어가 바뀌면 새로 만들고 앞의 것은 버린다(그 언어로 돌아가면 다시 만든다).
    /// 여럿이 함께 불러도 한 번만 만든다. 만드는 중에 부른 쪽이 기다리기를 그만둬도(탭을 떠남) 다 만든 색인은 캐시에 남아,
    /// 곧 다시 들어온 탭은 앞의 것이 끝나기를 기다렸다가 그것을 쓴다.
    /// </summary>
    public sealed class SoundIndexCache
    {
        private readonly Lazy<IReadOnlyList<SoundEntry>> _entries;
        private readonly object _gate = new();
        private volatile SoundIndex? _cached;

        /// <param name="classNames">AI 가 읽는 클래스 목록(<see cref="Ai.YamnetClassMap"/>). 소리 목록은 처음 만들 때 한 번 짓는다.</param>
        public SoundIndexCache(IReadOnlyList<string> classNames)
        {
            ArgumentNullException.ThrowIfNull(classNames);
            _entries = new Lazy<IReadOnlyList<SoundEntry>>(() => SoundCatalog.Load(classNames));
        }

        /// <summary>
        /// <paramref name="language"/> 로 만들어 둔 색인. 아직 없거나 다른 언어로 만든 것이면 null. 아무것도 만들지 않으므로 화면
        /// 스레드에서 불러도 된다. 탭에 다시 들어올 때 이것이 있으면 바로 그린다.
        /// </summary>
        public SoundIndex? CachedOrNull(string? language) => Cached(SoundLanguages.Resolve(language));

        /// <summary>
        /// <paramref name="language"/> 의 색인. 없으면 만들어 캐시에 넣는다. 만드는 동안 부른 스레드를 붙잡으므로 화면 스레드에서는
        /// <see cref="GetOrBuildAsync"/> 를 쓴다.
        /// </summary>
        public SoundIndex GetOrBuild(string? language) =>
            GetOrBuild(language, code => SoundCatalog.BuildIndex(code, _entries.Value));

        /// <summary>
        /// <see cref="GetOrBuild(string?)"/> 를 스레드 풀에서 한다. 이미 만들어 두었으면 끝난 작업을 바로 돌려준다.
        /// 기다리기를 그만두려면 WaitAsync(취소 토큰)로 기다린다. 그래도 만드는 일은 끝까지 해서 캐시에 남긴다.
        /// </summary>
        public Task<SoundIndex> GetOrBuildAsync(string? language) =>
            CachedOrNull(language) is { } index ? Task.FromResult(index) : Task.Run(() => GetOrBuild(language));

        /// <summary>
        /// 만드는 일(<paramref name="build"/>, 맞춘 언어 코드를 받는다)을 밖에서 받는다. 그래서 테스트가 만드는 동안을 붙잡아 본다.
        /// 다 만든 색인은 여기서 캐시에 넣는다.
        /// </summary>
        internal SoundIndex GetOrBuild(string? language, Func<string, SoundIndex> build)
        {
            string code = SoundLanguages.Resolve(language);
            if (Cached(code) is { } hit) return hit;
            lock (_gate)
            {
                if (Cached(code) is { } again) return again;
                SoundIndex built = build(code);
                _cached = built;
                return built;
            }
        }

        private SoundIndex? Cached(string code) => _cached is { } index && index.Language == code ? index : null;
    }
}
