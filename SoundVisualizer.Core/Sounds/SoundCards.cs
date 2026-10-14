// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Sounds
{
    // 분류 탭의 묶음 화면이 다루는 값들. 모바일 SoundGroups.kt 의 데이터 클래스와 같다.
    // 목록을 든 레코드는 같음을 목록의 내용으로 본다(모바일 data class 처럼). 레코드가 스스로 만드는 같음은 목록의 참조만 본다.

    /// <summary>묶음 표의 한 줄. <paramref name="Order"/> 는 머리글을 뺀 줄 순서(0부터)이고, 묶음 화면에서 소리가 보이는 순서다.</summary>
    public sealed record SoundGroupRow(SoundGroup Group, int Order);

    /// <summary>
    /// 분류 탭의 카드 하나. 같은 묶음이면서 기본 종류가 같은 소리들이다(카드 = (묶음, 기본 종류)).
    ///
    /// 지금은 묶음마다 기본 종류가 하나라 카드 하나가 곧 묶음 하나다. AI 의 매핑이 바뀌어 한 소리의 기본 종류가 달라지면, 그 소리는
    /// 표를 고치지 않아도 새 종류의 구역에 같은 묶음 이름의 카드로 따로 보인다. 그래서 한 카드의 소리는 늘 기본 종류가 같고,
    /// 카드에서 고르는 "(기본)" 이 모든 소리에 맞다. 섞인 카드(<see cref="GroupState.Mixed"/>)도 하나씩 바꾼 소리가 있을 때만 생긴다.
    /// </summary>
    /// <param name="Group">표에 없는 소리면 null 이고, 그 소리 하나가 카드가 된다.</param>
    /// <param name="DefaultType">카드의 소리들의 기본 종류. 카드가 놓이는 구역이다.</param>
    /// <param name="Members">표의 순서대로.</param>
    /// <param name="TitleEntry">
    /// 이름(소리 이름)이 곧 카드 이름인 소리. 새로 지은 이름을 쓰는 묶음이면 null 이다. 매핑이 바뀌어 묶음이 두 구역에 나뉘면 다른
    /// 구역의 카드에 들어 있을 수 있다.
    /// </param>
    public sealed record SoundCard(SoundGroup? Group, SoundType DefaultType, IReadOnlyList<SoundEntry> Members, SoundEntry? TitleEntry)
    {
        /// <summary>새로 지은 카드 이름을 쓰면 그 묶음(모바일 titleRes). 카드 이름이 소리 이름(<see cref="TitleEntry"/>)이면 null.</summary>
        public SoundGroup? TitleGroup => TitleEntry is null ? Group : null;

        /// <summary>목록에서 카드를 가리키는 이름("sirens:danger"). 한 묶음이 두 구역에 나뉘어도 겹치지 않게 기본 종류를 붙인다.</summary>
        public string Id =>
            (Group?.Id() ?? "sound_" + (Members.Count > 0 ? Members[0].Index : -1).ToString(CultureInfo.InvariantCulture)) +
            ":" + DefaultType.ToLabel();

        public bool Equals(SoundCard? other) =>
            other is not null && Group == other.Group && DefaultType == other.DefaultType &&
            Equals(TitleEntry, other.TitleEntry) && ValueLists.SameItems(Members, other.Members);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Group);
            hash.Add(DefaultType);
            hash.Add(TitleEntry);
            ValueLists.AddItems(ref hash, Members);
            return hash.ToHashCode();
        }

        private bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Id = ").Append(Id).Append(", Members = ").Append(ValueLists.Names(Members));
            builder.Append(", TitleEntry = ").Append(TitleEntry?.Name);
            return true;
        }
    }

    /// <summary>구역 하나. <paramref name="Type"/> 이 기본 종류인 카드들이다.</summary>
    public sealed record SoundSection(SoundType Type, IReadOnlyList<SoundCard> Cards)
    {
        public bool Equals(SoundSection? other) =>
            other is not null && Type == other.Type && ValueLists.SameItems(Cards, other.Cards);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Type);
            ValueLists.AddItems(ref hash, Cards);
            return hash.ToHashCode();
        }

        private bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Type = ").Append(Type).Append(", Cards = [").AppendJoin(", ", Cards.Select(card => card.Id)).Append(']');
            return true;
        }
    }

    /// <summary>
    /// 카드의 지금 종류. 저장하지 않고 소리마다의 종류에서 그때그때 정한다(<see cref="SoundGroups.State"/>).
    /// </summary>
    public abstract record GroupState
    {
        private GroupState()
        {
        }

        /// <summary>모든 소리가 <paramref name="Type"/> 이다. 카드의 기본 종류와 다르면 묶음째 바꾼 것이다.</summary>
        public sealed record Uniform(SoundType Type) : GroupState;

        /// <summary>소리마다 종류가 다르다("일부 다름"). 카드의 기본 종류는 하나라 하나씩 바꾼 소리가 있다는 뜻이다.</summary>
        public sealed record Mixed : GroupState
        {
            private Mixed()
            {
            }

            public static Mixed Instance { get; } = new();
        }
    }

    /// <summary>
    /// 카드 이름 밑의 작은 글씨. <paramref name="Names"/> 는 <see cref="SoundGroups.PreviewSize"/> 개까지의 소리이고,
    /// <paramref name="More"/> 는 못 보인 소리의 수("+N")다.
    /// </summary>
    public sealed record GroupPreview(IReadOnlyList<SoundEntry> Names, int More)
    {
        public bool Equals(GroupPreview? other) =>
            other is not null && More == other.More && ValueLists.SameItems(Names, other.Names);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(More);
            ValueLists.AddItems(ref hash, Names);
            return hash.ToHashCode();
        }

        private bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Names = ").Append(ValueLists.Names(Names)).Append(", More = ").Append(More);
            return true;
        }
    }

    /// <summary>찾는 말과 거르기에 남은 카드.</summary>
    /// <param name="Card">남은 카드.</param>
    /// <param name="Shown">
    /// 찾는 말과 거르기에 맞는 소리(표 순서). 기본 모드는 이 소리를 미리보기 앞에 두고(<see cref="SoundGroups.Preview"/>),
    /// 고급 모드는 이 소리만 줄로 보인다.
    /// </param>
    public sealed record ShownCard(SoundCard Card, IReadOnlyList<SoundEntry> Shown)
    {
        public bool Equals(ShownCard? other) =>
            other is not null && Card.Equals(other.Card) && ValueLists.SameItems(Shown, other.Shown);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Card);
            ValueLists.AddItems(ref hash, Shown);
            return hash.ToHashCode();
        }

        private bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Card = ").Append(Card.Id).Append(", Shown = ").Append(ValueLists.Names(Shown));
            return true;
        }
    }

    /// <summary>남은 카드가 있는 구역. 남은 카드가 없는 구역은 목록에 넣지 않는다.</summary>
    public sealed record ShownSection(SoundType Type, IReadOnlyList<ShownCard> Cards)
    {
        public bool Equals(ShownSection? other) =>
            other is not null && Type == other.Type && ValueLists.SameItems(Cards, other.Cards);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Type);
            ValueLists.AddItems(ref hash, Cards);
            return hash.ToHashCode();
        }

        private bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Type = ").Append(Type).Append(", Cards = [").AppendJoin(", ", Cards).Append(']');
            return true;
        }
    }

    /// <summary>
    /// 고급 모드 목록의 한 칸. <see cref="Key"/> 는 목록의 키로, 칸의 종류마다 앞 글자를 달리해 서로 겹치지 않는다.
    /// </summary>
    public abstract record AdvancedItem
    {
        private AdvancedItem()
        {
        }

        public abstract string Key { get; }

        /// <summary>구역 머리. 그 아래 묶음들의 기본 종류다.</summary>
        public sealed record SectionHeading(SoundType Type) : AdvancedItem
        {
            public override string Key => "s:" + Type.ToLabel();
        }

        /// <summary>묶음 머리. 그 아래 줄들이 이 카드의 소리다.</summary>
        public sealed record GroupHeading(SoundCard Card) : AdvancedItem
        {
            public override string Key => "g:" + Card.Id;
        }

        /// <summary>소리 한 줄.</summary>
        public sealed record Row(SoundEntry Entry) : AdvancedItem
        {
            public override string Key => "r:" + Entry.Index.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>목록을 든 레코드의 같음과 해시, 표시.</summary>
    internal static class ValueLists
    {
        public static bool SameItems<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) => ReferenceEquals(a, b) || a.SequenceEqual(b);

        public static void AddItems<T>(ref HashCode hash, IReadOnlyList<T> items)
        {
            hash.Add(items.Count);
            foreach (T item in items) hash.Add(item);
        }

        public static string Names(IEnumerable<SoundEntry> entries) => "[" + string.Join(", ", entries.Select(entry => entry.Name)) + "]";
    }
}
