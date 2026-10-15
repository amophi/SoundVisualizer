// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 거르기를 켠 채로 방금 바꾼 소리(모델 이름, #374). 모바일 KeptSounds 와 같다. 바꾼 소리가 거르기에서 빠져도 찾는 말이나 거르기를
    /// 바꿀 때까지 목록에 남겨, 누른 카드(고급 모드는 그 줄)가 눈앞에서 사라지지 않게 한다.
    ///
    /// 카드가 아니라 소리를 남긴다. 기본 모드는 남은 소리가 있는 카드가, 고급 모드는 그 줄과 그 위의 머리가 남고, "바꾼 소리" 거르기도
    /// 같은 방법으로 된다. <see cref="Query"/>·<see cref="Filter"/> 는 남기기 시작할 때의 것이고, 지금 것과 다르면 아무것도 남기지 않는다.
    /// 화면은 찾는 말이나 거르기를 바꿀 때와 모두 되돌리기를 할 때 <see cref="Empty"/> 로 비운다.
    /// </summary>
    public sealed record KeptSounds(string Query, SoundFilter Filter, IReadOnlySet<string> Names)
    {
        private static readonly IReadOnlySet<string> NoNames = FrozenSet<string>.Empty;

        /// <summary>남길 것이 없다.</summary>
        public static KeptSounds Empty { get; } = new("", SoundFilter.All, NoNames);

        /// <summary>지금의 찾는 말과 거르기에서 남길 소리. 남기기 시작할 때와 다르면 빈 것이다.</summary>
        public IReadOnlySet<string> NamesFor(string query, SoundFilter filter) =>
            string.Equals(query, Query, StringComparison.Ordinal) && filter == Filter ? Names : NoNames;

        /// <summary>
        /// <paramref name="added"/> 를 더한 것. 찾는 말이나 거르기가 남기기 시작할 때와 다르면 앞의 것은 버리고 새로 시작한다.
        /// 전체에서는 모든 소리가 거르기에 맞아 남길 것이 없으니 빈 것이다.
        /// </summary>
        public KeptSounds Plus(string query, SoundFilter filter, IEnumerable<string> added)
        {
            if (filter == SoundFilter.All) return Empty;
            var names = new HashSet<string>(NamesFor(query, filter), StringComparer.Ordinal);
            names.UnionWith(added);
            return new KeptSounds(query, filter, names.ToFrozenSet(StringComparer.Ordinal));
        }

        public bool Equals(KeptSounds? other) =>
            other is not null &&
            string.Equals(Query, other.Query, StringComparison.Ordinal) &&
            Filter == other.Filter &&
            Names.SetEquals(other.Names);

        // 집합의 순서와 상관없이 같은 값이어야 해서 수만 넣는다.
        public override int GetHashCode() => HashCode.Combine(Query, Filter, Names.Count);
    }
}
