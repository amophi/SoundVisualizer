// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 분류 탭의 카드 나누기(카드 = (묶음, 기본 종류)), 묶음 상태, 미리보기, 덮어쓰는 수(#349), 묻는 때와 거르기에 남길 소리(#374).
/// 모바일 SoundGroupsTest 를 옮겼다.
///
/// 지금 매핑으로는 어떤 매핑에서도 지켜야 할 규칙만 보고, 나머지는 기본 종류를 정해 둔 가짜 매핑을 써서 매핑이 바뀌어도 흔들리지 않게
/// 한다. 지금 매핑의 구역 구성(위협음 11, 대화음 6, 환경음 20)은 한 테스트에만 적어 둔다.
/// </summary>
public class SoundGroupsTests
{
    /// <summary>모든 소리의 기본 종류가 환경음인 목록. 매핑과 상관없이 묶음 하나가 카드 하나다.</summary>
    private static readonly IReadOnlyList<SoundEntry> Plain = SoundTestData.Entries(_ => SoundType.Ambient);

    private static readonly IReadOnlyList<SoundSection> PlainSections = SoundGroups.Sections(Plain);

    private static readonly Dictionary<string, string> NoOverrides = new();

    private static readonly SoundType[] Types = { SoundType.Ambient, SoundType.Speech, SoundType.Danger };

    private static SoundEntry Entry(string name, IReadOnlyList<SoundEntry>? list = null) =>
        (list ?? Plain).First(entry => entry.Name == name);

    private static List<string> Names(IEnumerable<SoundEntry> list) => SoundTestData.NamesOf(list);

    private static SoundCard Card(IReadOnlyList<SoundSection> sections, SoundGroup group, SoundType type = SoundType.Ambient) =>
        Assert.Single(Assert.Single(sections, section => section.Type == type).Cards, card => card.Group == group);

    private static Dictionary<string, string> AllOf(SoundCard card, SoundType type) =>
        card.Members.ToDictionary(entry => entry.Name, _ => type.ToLabel());

    private static Dictionary<string, string> Map(params (string Name, SoundType Type)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Type.ToLabel());

    private static Dictionary<string, string> With(Dictionary<string, string> map, params (string Name, SoundType Type)[] values)
    {
        var copy = new Dictionary<string, string>(map);
        foreach (var (name, type) in values) copy[name] = type.ToLabel();
        return copy;
    }

    /// <summary>어떤 매핑에서도 지켜야 할 것.</summary>
    private static void AssertCardRules(IReadOnlyList<SoundSection> sections, IReadOnlyList<SoundEntry> entries)
    {
        var types = sections.Select(section => section.Type).ToList();
        Assert.Equal(SoundGroups.SectionOrder.Where(types.Contains), types); // 구역은 위협음, 대화음, 환경음 순서다.
        foreach (SoundSection section in sections)
        {
            Assert.NotEmpty(section.Cards); // 빈 구역이 없다.
            foreach (SoundCard card in section.Cards)
            {
                Assert.Equal(section.Type, card.DefaultType);
                Assert.All(card.Members, entry => Assert.Equal(card.DefaultType, entry.DefaultType)); // 카드에 기본 종류가 섞이지 않는다.
                Assert.All(card.Members, entry => Assert.Equal(card.Group, entry.Group)); // 카드에 묶음이 섞이지 않는다.
                Assert.Equal(card.Members.OrderBy(entry => entry.Order), card.Members); // 카드의 소리는 표 순서다.
            }
            var groups = section.Cards.Where(card => card.Group is not null).Select(card => card.Group!.Value).ToList();
            Assert.Equal(groups.Order(), groups); // 구역 안의 카드는 묶음 순서다.
            Assert.Equal(groups.Count, groups.Distinct().Count()); // 구역 안에서 묶음 하나는 카드 하나다.
            int firstSingle = section.Cards.ToList().FindIndex(card => card.Group is null);
            if (firstSingle >= 0)
            {
                Assert.All(section.Cards.Skip(firstSingle), card => Assert.Null(card.Group)); // 표에 없는 소리는 구역 끝에 온다.
            }
        }
        var shown = sections.SelectMany(section => section.Cards).SelectMany(card => card.Members).Select(entry => entry.Index);
        Assert.Equal(entries.Select(entry => entry.Index).Order(), shown.Order()); // 모든 소리가 한 번씩 카드에 든다.
        // 카드 id 는 목록의 키다. 겹치면 목록이 깨진다. 표에 없는 소리 둘이 같은 구역에 와도 겹치지 않아야 한다.
        var ids = sections.SelectMany(section => section.Cards).Select(card => card.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void 지금_매핑으로_나눠도_카드마다_기본_종류가_하나다()
    {
        var entries = SoundTestData.Entries();
        AssertCardRules(SoundGroups.Sections(entries), entries);
        Assert.Equal(new[] { SoundType.Danger, SoundType.Speech, SoundType.Ambient }, SoundGroups.SectionOrder);
    }

    [Fact]
    public void 지금_매핑으로는_위협음_11_대화음_6_환경음_20_묶음이다()
    {
        // 지금 매핑에서는 묶음마다 기본 종류가 하나라 카드 하나가 곧 묶음 하나다. 매핑을 모바일에서 옮겨 바꾸면 이 수도 함께 본다
        // (YamnetLabelListTests 와 같다). 카드 나누기 자체는 매핑과 상관없이 위의 규칙을 지킨다.
        var sections = SoundGroups.Sections(SoundTestData.Entries());
        Assert.Equal(new[] { SoundType.Danger, SoundType.Speech, SoundType.Ambient }, sections.Select(section => section.Type));
        Assert.Equal(new[] { 11, 6, 20 }, sections.Select(section => section.Cards.Count));
        Assert.Equal(SoundGroupInfo.All, sections.SelectMany(section => section.Cards).Select(card => card.Group!.Value));
    }

    [Fact]
    public void 매핑이_바뀐_소리는_새_구역에_같은_묶음_이름으로_따로_보인다()
    {
        // 매핑이 노크를 위협음으로, 사이렌을 대화음으로 옮겼다고 친다. 표는 고치지 않는다.
        var flipped = new Dictionary<string, SoundType> { ["Knock"] = SoundType.Danger, ["Siren"] = SoundType.Speech };
        var drifted = SoundTestData.Entries(name => flipped.GetValueOrDefault(name, SoundType.Ambient));
        var sections = SoundGroups.Sections(drifted);
        AssertCardRules(sections, drifted);
        Assert.Equal(new[] { SoundType.Danger, SoundType.Speech, SoundType.Ambient }, sections.Select(section => section.Type));

        var knock = Card(sections, SoundGroup.Doorbell, SoundType.Danger);
        Assert.Equal(new[] { "Knock" }, Names(knock.Members));
        Assert.Equal("Doorbell", knock.TitleEntry?.Name); // 묶음 이름은 그대로 초인종이다.
        Assert.Equal(new[] { "Doorbell", "Ding-dong", "Tap" }, Names(Card(sections, SoundGroup.Doorbell).Members));

        var siren = Card(sections, SoundGroup.Sirens, SoundType.Speech);
        Assert.Equal(new[] { "Siren" }, Names(siren.Members));
        var otherSirens = Card(sections, SoundGroup.Sirens);
        Assert.Equal(4, otherSirens.Members.Count);
        Assert.Equal("Siren", otherSirens.TitleEntry?.Name); // 다른 구역으로 간 사이렌의 이름을 그대로 쓴다.
        Assert.NotEqual(siren.Id, otherSirens.Id); // 한 묶음이 두 구역에 나뉘어도 카드 이름은 겹치지 않는다.
    }

    [Fact]
    public void 표에_없는_소리는_그_소리_하나로_구역_끝에_온다()
    {
        // 모델이 바뀌어 표에 없는 소리가 생겼다고 친다. 하나는 표에서 빠졌고, 하나는 처음 보는 이름이다.
        var table = SoundTestData.Table.Where(row => row.Key != "Doorbell").ToDictionary(row => row.Key, row => row.Value);
        var list = SoundCatalog.FromNames(SoundTestData.Names.Append("Brand new sound").ToList(), table, _ => SoundType.Ambient);
        var sections = SoundGroups.Sections(list);
        AssertCardRules(sections, list);

        var singles = Assert.Single(sections).Cards.TakeLast(2).ToList();
        Assert.Equal(new[] { "Doorbell", "Brand new sound" }, singles.Select(card => Assert.Single(card.Members).Name));
        foreach (SoundCard single in singles)
        {
            Assert.Null(single.Group);
            Assert.Equal(single.Members[0], single.TitleEntry); // 제 이름이 카드 이름이다.
            Assert.Null(single.TitleGroup);
            Assert.Equal(new GroupPreview(Array.Empty<SoundEntry>(), 0), SoundGroups.Preview(single));
        }
        Assert.Equal("sound_" + list.First(entry => entry.Name == "Doorbell").Index + ":ambient", singles[0].Id);
        var doorbell = Card(sections, SoundGroup.Doorbell);
        Assert.Equal(new[] { "Ding-dong", "Knock", "Tap" }, Names(doorbell.Members));
        Assert.Equal("Doorbell", doorbell.TitleEntry?.Name); // 표에서 빠져도 모델에 있는 소리의 이름은 쓴다.
    }

    [Fact]
    public void 이름인_소리가_모델에서_사라지면_맨_앞_소리의_이름을_쓴다()
    {
        var names = SoundTestData.Names.Select(name => name == "Doorbell" ? "Door bell" : name).ToList();
        var sections = SoundGroups.Sections(SoundCatalog.FromNames(names, SoundTestData.Table, _ => SoundType.Ambient));
        Assert.Equal("Ding-dong", Card(sections, SoundGroup.Doorbell).TitleEntry?.Name);
    }

    [Fact]
    public void 카드_이름은_새로_지은_문구이거나_소리_이름이다()
    {
        var horns = Card(PlainSections, SoundGroup.Horns);
        Assert.Equal(SoundGroup.Horns, horns.TitleGroup);
        Assert.Null(horns.TitleEntry);
        var sirens = Card(PlainSections, SoundGroup.Sirens);
        Assert.Null(sirens.TitleGroup);
        Assert.Equal(Entry("Siren"), sirens.TitleEntry);
        Assert.Equal("sirens:ambient", sirens.Id);
    }

    [Fact]
    public void 묶음_상태는_소리마다의_지금_종류로_정한다()
    {
        var sirens = Card(PlainSections, SoundGroup.Sirens);
        Assert.Equal(new GroupState.Uniform(SoundType.Ambient), SoundGroups.State(sirens, NoOverrides)); // 기본 그대로
        Assert.Equal(new GroupState.Uniform(SoundType.Danger), SoundGroups.State(sirens, AllOf(sirens, SoundType.Danger))); // 묶음째 바꾼 것
        Assert.Same(GroupState.Mixed.Instance, SoundGroups.State(sirens, Map(("Civil defense siren", SoundType.Danger)))); // 하나만 바꾼 것
        // 바꿔도 모두 같은 종류면 섞인 것이 아니다.
        Assert.Equal(new GroupState.Uniform(SoundType.Speech), SoundGroups.State(sirens, AllOf(sirens, SoundType.Speech)));
    }

    [Fact]
    public void 섞인_카드가_있는지는_모든_카드를_본다()
    {
        Assert.False(SoundGroups.AnyMixed(PlainSections, NoOverrides));
        Assert.False(SoundGroups.AnyMixed(PlainSections, Map(("Chainsaw", SoundType.Danger)))); // 소리 하나뿐인 묶음은 섞일 수 없다.
        var wholeSirens = AllOf(Card(PlainSections, SoundGroup.Sirens), SoundType.Danger);
        Assert.False(SoundGroups.AnyMixed(PlainSections, wholeSirens)); // 묶음째 바꾼 것은 섞인 것이 아니다.
        Assert.True(SoundGroups.AnyMixed(PlainSections, With(wholeSirens, ("Doorbell", SoundType.Danger))));
        Assert.True(SoundGroups.AnyMixed(PlainSections, Map(("Singing bowl", SoundType.Speech))));

        // 구역이 여럿이어도 모두 본다. 사이렌 묶음만 위협음인 목록에서 초인종 묶음은 두 번째(환경음) 구역에 있다.
        var two = SoundGroups.Sections(SoundTestData.Entries(name =>
            SoundTestData.Table.TryGetValue(name, out var row) && row.Group == SoundGroup.Sirens ? SoundType.Danger : SoundType.Ambient));
        Assert.Equal(new[] { SoundType.Danger, SoundType.Ambient }, two.Select(section => section.Type));
        Assert.False(SoundGroups.AnyMixed(two, NoOverrides));
        Assert.True(SoundGroups.AnyMixed(two, Map(("Knock", SoundType.Danger)))); // 첫 구역 밖의 섞인 묶음
    }

    [Fact]
    public void 미리보기는_이름인_소리를_빼고_세_개까지_보이고_나머지는_센다()
    {
        Assert.Equal(
            new GroupPreview(new[] { Entry("Ambulance (siren)"), Entry("Fire engine, fire truck (siren)"), Entry("Police car (siren)") }, 1),
            SoundGroups.Preview(Card(PlainSections, SoundGroup.Sirens)));
        // 새로 지은 이름을 쓰는 묶음은 맨 앞 소리부터 보인다.
        Assert.Equal(
            new GroupPreview(new[] { Entry("Vehicle horn, car horn, honking"), Entry("Air horn, truck horn"), Entry("Car alarm") }, 0),
            SoundGroups.Preview(Card(PlainSections, SoundGroup.Horns)));
        var music = Card(PlainSections, SoundGroup.Music);
        Assert.Equal(music.Members.Count - 1 - SoundGroups.PreviewSize, SoundGroups.Preview(music).More);
    }

    [Fact]
    public void 이름인_소리_하나뿐인_묶음은_미리보기가_없다()
    {
        Assert.Equal(new GroupPreview(Array.Empty<SoundEntry>(), 0), SoundGroups.Preview(Card(PlainSections, SoundGroup.Chainsaw)));
        Assert.Equal(new GroupPreview(Array.Empty<SoundEntry>(), 0), SoundGroups.Preview(Card(PlainSections, SoundGroup.Slam)));
    }

    [Fact]
    public void 찾은_소리는_미리보기_앞에_온다()
    {
        var doorbell = Card(PlainSections, SoundGroup.Doorbell);
        Assert.Equal(
            new GroupPreview(new[] { Entry("Knock"), Entry("Ding-dong"), Entry("Tap") }, 0),
            SoundGroups.Preview(doorbell, new[] { Entry("Knock") }));
        // 묶음 이름인 소리는 찾아도 미리보기에 다시 넣지 않는다.
        var music = Card(PlainSections, SoundGroup.Music);
        var preview = SoundGroups.Preview(music, new[] { Entry("Music"), Entry("Singing bowl"), Entry("Piano") });
        Assert.Equal(new[] { Entry("Piano"), Entry("Singing bowl"), Entry("Musical instrument") }, preview.Names);
        Assert.Equal(music.Members.Count - 1 - SoundGroups.PreviewSize, preview.More);
    }

    [Fact]
    public void 덮어쓰는_수는_고른_종류와_지금_종류가_다른_소리다()
    {
        var sirens = Card(PlainSections, SoundGroup.Sirens);
        var one = Map(("Civil defense siren", SoundType.Danger));
        Assert.Equal(1, SoundGroups.OverwriteCount(sirens, one, SoundType.Ambient)); // 기본을 고르면 하나씩 바꾼 소리만 바뀐다.
        Assert.Equal(4, SoundGroups.OverwriteCount(sirens, one, SoundType.Danger));
        Assert.Equal(5, SoundGroups.OverwriteCount(sirens, one, SoundType.Speech));
        Assert.Equal(0, SoundGroups.OverwriteCount(sirens, NoOverrides, SoundType.Ambient));
    }

    [Fact]
    public void 하나씩_바꾼_소리의_종류가_바뀔_때만_묻는다()
    {
        // 사이렌 카드의 기본 종류는 환경음이다(Plain).
        var sirens = Card(PlainSections, SoundGroup.Sirens);
        bool Asks(IReadOnlyDictionary<string, string> overrides, SoundType picked) => SoundGroups.OverwritesOneByOne(sirens, overrides, picked);

        foreach (SoundType type in Types) Assert.False(Asks(NoOverrides, type), $"바꾼 것이 없다: {type}");

        var whole = AllOf(sirens, SoundType.Danger);
        Assert.False(Asks(whole, SoundType.Speech)); // 묶음째 바꾼 카드는 묻지 않는다.
        Assert.False(Asks(whole, SoundType.Ambient)); // 묶음째 바꾼 카드는 기본 종류를 골라도 묻지 않는다.

        var one = Map(("Civil defense siren", SoundType.Danger));
        Assert.False(Asks(one, SoundType.Danger)); // 하나씩 바꾼 소리와 같은 종류를 고르면 그 소리는 그대로다.
        Assert.True(Asks(one, SoundType.Ambient)); // 기본 종류를 고르면 하나씩 바꾼 것이 되돌려진다.
        Assert.True(Asks(one, SoundType.Speech));

        var two = With(one, ("Police car (siren)", SoundType.Speech));
        foreach (SoundType type in Types)
        {
            Assert.True(Asks(two, type), $"하나씩 바꾼 소리 둘의 종류가 다르면 어느 쪽을 골라도 하나는 바뀐다: {type}");
        }

        // 묶음째 위협음으로 바꾼 뒤 민방위 사이렌만 기본 종류로 되돌린 카드. 저장값으로는 되돌린 소리와 건드리지 않은 소리를 가릴 수
        // 없어서, 위협음을 고르면 묻지 않는다(알고 둔 한계).
        var reverted = new Dictionary<string, string>(whole);
        reverted.Remove("Civil defense siren");
        Assert.Same(GroupState.Mixed.Instance, SoundGroups.State(sirens, reverted));
        Assert.False(Asks(reverted, SoundType.Danger));
        Assert.True(Asks(reverted, SoundType.Ambient));

        var chainsaw = Card(PlainSections, SoundGroup.Chainsaw);
        var changedChainsaw = Map(("Chainsaw", SoundType.Danger));
        foreach (SoundType type in Types)
        {
            Assert.False(SoundGroups.OverwritesOneByOne(chainsaw, changedChainsaw, type)); // 소리 하나뿐인 카드는 섞이지 않는다.
        }
    }

    [Fact]
    public void 남길_소리는_찾는_말과_거르기가_그대로인_동안만_남는다()
    {
        var empty = KeptSounds.Empty;
        Assert.Empty(empty.NamesFor("", SoundFilter.All));

        var kept = empty.Plus("사이렌", SoundFilter.Danger, new[] { "Siren" });
        Assert.Equal(new[] { "Siren" }, kept.NamesFor("사이렌", SoundFilter.Danger));
        Assert.Empty(kept.NamesFor("사이레", SoundFilter.Danger)); // 찾는 말이 다르다.
        Assert.Empty(kept.NamesFor("사이렌", SoundFilter.Changed)); // 거르기가 다르다.

        var more = kept.Plus("사이렌", SoundFilter.Danger, new[] { "Civil defense siren" });
        // 같은 찾는 말과 거르기에서는 쌓인다.
        Assert.True(more.NamesFor("사이렌", SoundFilter.Danger).SetEquals(new[] { "Siren", "Civil defense siren" }));

        var restarted = more.Plus("", SoundFilter.Danger, new[] { "Doorbell" });
        Assert.Equal(new[] { "Doorbell" }, restarted.NamesFor("", SoundFilter.Danger)); // 찾는 말이 바뀌면 앞의 것은 버린다.
        Assert.Equal(new[] { "Knock" }, restarted.Plus("", SoundFilter.Speech, new[] { "Knock" }).Names);
        Assert.Equal(KeptSounds.Empty, restarted.Plus("", SoundFilter.All, new[] { "Siren" })); // 전체에서는 남기지 않는다.
        Assert.Equal(
            new KeptSounds("a", SoundFilter.Danger, new HashSet<string> { "x", "y" }),
            new KeptSounds("a", SoundFilter.Danger, new HashSet<string> { "y", "x" })); // 집합은 순서와 상관없이 같다.
    }
}
