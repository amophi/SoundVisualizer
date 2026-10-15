// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 소리 탭 목록의 한 줄. 모바일 ClassifyTab 의 LazyColumn 항목과 같은 차례이고 키도 같다(intro, changed, empty, 구역·카드 또는
    /// 고급 모드의 칸, end). 화면은 키가 같은 줄을 다시 만들지 않고 고쳐 쓴다. 그래야 종류를 고른 카드가 키보드 초점과 자리를 잃지 않는다.
    ///
    /// 모바일은 찾기·거르기를 목록 안에 붙는 머리(stickyHeader)로 두지만, PC 는 목록 위에 붙박아 두므로 목록에 없다.
    /// </summary>
    public abstract record SoundListItem
    {
        private SoundListItem()
        {
        }

        /// <summary>목록의 키. 칸의 종류마다 앞 글자를 달리해 서로 겹치지 않는다.</summary>
        public abstract string Key { get; }

        /// <summary>제목·설명·고급 모드 스위치. 늘 맨 위다.</summary>
        public sealed record Intro : SoundListItem
        {
            private Intro()
            {
            }

            public static Intro Instance { get; } = new();

            public override string Key => "intro";
        }

        /// <summary>"바꾼 소리: N" 과 모두 되돌리기. <paramref name="Count"/> 는 묶음이 아니라 소리의 수다(<see cref="SoundIndex.CountChanged"/>).</summary>
        public sealed record Changed(int Count) : SoundListItem
        {
            public override string Key => "changed";
        }

        /// <summary>찾는 말과 거르기에 맞는 소리가 없다.</summary>
        public sealed record Empty : SoundListItem
        {
            private Empty()
            {
            }

            public static Empty Instance { get; } = new();

            public override string Key => "empty";
        }

        /// <summary>구역 머리. 두 모드가 같은 키를 쓴다(<see cref="AdvancedItem.SectionHeading"/>).</summary>
        public sealed record SectionHeading(SoundType Type) : SoundListItem
        {
            public override string Key => "s:" + Type.ToLabel();
        }

        /// <summary>기본 모드의 카드.</summary>
        /// <param name="Shown">카드와 찾는 말·거르기에 맞은 소리. 고르면 맞은 소리를 거르기에서 빠져도 남긴다(<see cref="KeptSounds"/>).</param>
        /// <param name="State">카드의 지금 종류(<see cref="SoundGroups.State"/>).</param>
        /// <param name="Preview">카드 이름 밑의 소리들. 맞은 소리가 앞에 온다(<see cref="SoundGroups.Preview"/>).</param>
        public sealed record GroupCard(ShownCard Shown, GroupState State, GroupPreview Preview) : SoundListItem
        {
            public override string Key => "c:" + Shown.Card.Id;
        }

        /// <summary>고급 모드의 묶음 머리(<see cref="AdvancedItem.GroupHeading"/>).</summary>
        public sealed record GroupHeading(SoundCard Card) : SoundListItem
        {
            public override string Key => "g:" + Card.Id;
        }

        /// <summary>고급 모드의 소리 한 줄(<see cref="AdvancedItem.Row"/>). <paramref name="Type"/> 은 사용자가 바꾼 것까지 반영한 지금 종류다.</summary>
        public sealed record Sound(SoundEntry Entry, SoundType Type) : SoundListItem
        {
            public override string Key => "r:" + Entry.Index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>목록 끝의 여백.</summary>
        public sealed record End : SoundListItem
        {
            private End()
            {
            }

            public static End Instance { get; } = new();

            public override string Key => "end";
        }
    }

    /// <summary>
    /// 소리 탭의 목록을 만든다. 화면은 이 목록을 그대로 그리고, 줄마다의 글은 문자열 표에서 고른다(<see cref="SoundRowText"/>).
    /// 기본 모드와 고급 모드가 같은 찾기·거르기 결과(<see cref="SoundIndex.Filter"/>)를 쓴다.
    /// </summary>
    public static class SoundList
    {
        /// <summary>
        /// 결과가 시작하는 줄(바꾼 소리 줄)의 자리. 그 앞은 제목·설명·고급 모드 스위치(<see cref="SoundListItem.Intro"/>)뿐이다.
        /// 찾는 말이나 거르기를 바꾸면 이 줄을 찾기·거르기 바로 밑으로 올린다(<see cref="ShouldScrollToResults"/>).
        /// </summary>
        public const int ResultsIndex = 1;

        /// <summary>
        /// 지금 목록. 모바일 ClassifyTab 의 LazyColumn 과 같은 차례다: 소개, 바꾼 소리 수, (맞는 것이 없으면) 빈 결과 안내, 기본 모드는
        /// 구역 머리와 카드, 고급 모드는 구역 머리·묶음 머리·소리 줄(<see cref="SoundGroups.AdvancedItems"/>), 끝 여백.
        /// </summary>
        /// <param name="index">화면 언어의 색인.</param>
        /// <param name="overrides">사용자가 바꾼 종류(설정의 소리 종류).</param>
        /// <param name="query">찾는 말. 다듬지 않은 그대로 넘긴다.</param>
        /// <param name="filter">거르기.</param>
        /// <param name="kept">거르기를 켠 채로 방금 바꾼 소리. 찾는 말과 거르기가 남기기 시작할 때와 같을 때만 남긴다.</param>
        /// <param name="advanced">고급 모드인지.</param>
        public static IReadOnlyList<SoundListItem> Build(
            SoundIndex index,
            IReadOnlyDictionary<string, string> overrides,
            string query,
            SoundFilter filter,
            KeptSounds kept,
            bool advanced)
        {
            ArgumentNullException.ThrowIfNull(index);
            ArgumentNullException.ThrowIfNull(overrides);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(kept);

            IReadOnlyList<ShownSection> shown = index.Filter(overrides, query, filter, kept.NamesFor(query, filter));
            var items = new List<SoundListItem> { SoundListItem.Intro.Instance, new SoundListItem.Changed(index.CountChanged(overrides)) };
            if (shown.Count == 0) items.Add(SoundListItem.Empty.Instance);

            if (advanced)
            {
                foreach (AdvancedItem item in SoundGroups.AdvancedItems(shown))
                {
                    items.Add(item switch
                    {
                        AdvancedItem.SectionHeading heading => new SoundListItem.SectionHeading(heading.Type),
                        AdvancedItem.GroupHeading group => new SoundListItem.GroupHeading(group.Card),
                        AdvancedItem.Row row => new SoundListItem.Sound(row.Entry, SoundCatalog.TypeOf(row.Entry, overrides)),
                        _ => throw new InvalidOperationException("모르는 고급 모드 칸: " + item),
                    });
                }
            }
            else
            {
                foreach (ShownSection section in shown)
                {
                    items.Add(new SoundListItem.SectionHeading(section.Type));
                    foreach (ShownCard card in section.Cards)
                    {
                        items.Add(new SoundListItem.GroupCard(card, SoundGroups.State(card.Card, overrides), SoundGroups.Preview(card.Card, card.Shown)));
                    }
                }
            }

            items.Add(SoundListItem.End.Instance);
            return items.AsReadOnly();
        }

        /// <summary>
        /// 찾는 말이나 거르기를 바꾼 뒤 결과(<see cref="ResultsIndex"/>)를 찾기·거르기 바로 밑으로 올릴지(모바일 ClassifyTab 의
        /// LaunchedEffect). 한참 내려간 채로 바꾸면 결과의 중간부터 보이므로 올린다. 찾는 말이 있으면 맨 위에 있어도 올린다.
        /// 모바일에서는 찾기 칸을 누르면 자판이 화면 아래를 가려, 맨 위의 제목·안내가 남은 자리를 다 차지하고 결과가 보이지 않았다(#283).
        /// 거르기만 바꿀 때는 맨 위면 그대로 둔다. 탭을 처음 그릴 때는 부르지 않는다.
        /// </summary>
        /// <param name="query">바뀐 뒤의 찾는 말.</param>
        /// <param name="introScrolledAway">소개 줄이 목록 위로 다 올라가 보이지 않는지(모바일의 firstVisibleItemIndex &gt; 1).</param>
        public static bool ShouldScrollToResults(string query, bool introScrolledAway) =>
            !string.IsNullOrEmpty(query) || introScrolledAway;
    }

    /// <summary>화면 읽어주기에 줄을 읽힐 꼴(모바일 GroupCard·SoundTypeRow 의 description). 문구는 화면이 문자열 표에서 고른다.</summary>
    public enum SoundRowSpeech
    {
        /// <summary>"이름, 종류: X". 소리 한 줄, 또는 미리보기가 없는(소리 하나뿐인) 카드다.</summary>
        Sound,

        /// <summary>"이름, 종류: X, 기본: Y".</summary>
        SoundChanged,

        /// <summary>"이름 묶음, 종류: X, 소리 N개 (미리보기)".</summary>
        Group,

        /// <summary>"이름 묶음, 종류: X, 기본: Y, 소리 N개 (미리보기)". 일부만 바꾼 카드도 이것이다.</summary>
        GroupChanged,
    }

    /// <summary>이름 밑의 밝은 글씨(모바일 GroupCard 의 accent, SoundTypeRow 의 "기본: Y").</summary>
    public enum SoundRowNote
    {
        None,

        /// <summary>카드의 소리마다 종류가 다르다("일부 소리를 하나씩 바꿈").</summary>
        Mixed,

        /// <summary>카드를 묶음째 바꿨다("지금: X · 기본: Y").</summary>
        GroupChanged,

        /// <summary>고급 모드에서 소리를 바꿨다("기본: Y").</summary>
        SoundChanged,
    }

    /// <summary>
    /// 소리 탭 줄의 글을 어느 꼴로 적을지. 모바일 GroupCard·SoundTypeRow 의 갈래를 그대로 옮겼다. 문구는 화면 쪽 문자열 표에 있고,
    /// 여기서는 어느 문구를 쓸지와 번역하지 않는 기호만 정한다.
    /// </summary>
    public static class SoundRowText
    {
        /// <summary>미리보기의 이름 사이(모바일과 같다).</summary>
        public const string ShownSeparator = " • ";

        /// <summary>화면 읽어주기가 읽는 미리보기의 이름 사이. 화면의 " • " 는 기호라 읽히지 않게 쉼표로 바꾼다.</summary>
        public const string SpokenSeparator = ", ";

        /// <summary>미리보기에 못 보인 소리의 수("+N"). 숫자뿐이라 번역하지 않는다(모바일 classify_group_more).</summary>
        public static string More(int count) => "+" + count.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// 카드를 읽힐 꼴. 미리보기가 없는 카드(전기톱처럼 소리 하나뿐인 묶음, 표에 없는 소리)는 소리 한 줄과 같은 글로 읽는다.
        /// 일부만 바꾼 카드는 지금 종류가 기본 종류가 아니므로 기본 종류를 함께 읽는다.
        /// </summary>
        public static SoundRowSpeech SpeechOf(SoundListItem.GroupCard card)
        {
            ArgumentNullException.ThrowIfNull(card);
            bool unchanged = card.State is GroupState.Uniform uniform && uniform.Type == card.Shown.Card.DefaultType;
            if (card.Preview.Names.Count == 0) return unchanged ? SoundRowSpeech.Sound : SoundRowSpeech.SoundChanged;
            return unchanged ? SoundRowSpeech.Group : SoundRowSpeech.GroupChanged;
        }

        /// <summary>소리 한 줄을 읽힐 꼴.</summary>
        public static SoundRowSpeech SpeechOf(SoundListItem.Sound sound)
        {
            ArgumentNullException.ThrowIfNull(sound);
            return sound.Type == sound.Entry.DefaultType ? SoundRowSpeech.Sound : SoundRowSpeech.SoundChanged;
        }

        /// <summary>카드 이름 밑의 밝은 글씨. 묶음째 바꿨으면 지금 종류와 기본 종류를, 일부만 바꿨으면 그렇다고 적는다.</summary>
        public static SoundRowNote NoteOf(SoundListItem.GroupCard card)
        {
            ArgumentNullException.ThrowIfNull(card);
            return card.State switch
            {
                GroupState.Mixed => SoundRowNote.Mixed,
                GroupState.Uniform uniform when uniform.Type != card.Shown.Card.DefaultType => SoundRowNote.GroupChanged,
                _ => SoundRowNote.None,
            };
        }

        /// <summary>소리 한 줄 밑의 밝은 글씨. 바꾼 소리는 기본 종류를 함께 보인다. 되돌리려면 목록에서 "(기본)" 을 고른다.</summary>
        public static SoundRowNote NoteOf(SoundListItem.Sound sound)
        {
            ArgumentNullException.ThrowIfNull(sound);
            return sound.Type == sound.Entry.DefaultType ? SoundRowNote.None : SoundRowNote.SoundChanged;
        }
    }
}
