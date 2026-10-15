// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 소리 탭 목록의 줄(모바일 ClassifyTab 의 LazyColumn 항목)과 결과까지 올릴지의 규칙.
/// 기본 종류는 매핑과 상관없이 정해 둔다(사이렌·경보음·전기톱 묶음만 위협음, 나머지는 환경음). 이름은 실제 영어 데이터다.
/// </summary>
public class SoundListTests
{
    private static readonly IReadOnlyList<SoundEntry> Entries = SoundTestData.Entries(name =>
        SoundTestData.Table.TryGetValue(name, out var row) && row.Group is SoundGroup.Sirens or SoundGroup.Alarms or SoundGroup.Chainsaw
            ? SoundType.Danger
            : SoundType.Ambient);

    private static readonly SoundIndex Index = SoundCatalog.BuildIndex(
        SoundLanguages.English, Entries, SoundData.Names(SoundLanguages.English),
        SoundData.GroupTitles(SoundLanguages.English), SoundData.GroupTitles(SoundLanguages.English));

    private static readonly Dictionary<string, string> NoOverrides = new();

    private static IReadOnlyList<SoundListItem> Build(
        IReadOnlyDictionary<string, string>? overrides = null,
        string query = "",
        SoundFilter filter = SoundFilter.All,
        KeptSounds? kept = null,
        bool advanced = false) =>
        SoundList.Build(Index, overrides ?? NoOverrides, query, filter, kept ?? KeptSounds.Empty, advanced);

    private static SoundCard Card(SoundGroup group) =>
        Index.Sections.SelectMany(section => section.Cards).Single(card => card.Group == group);

    private static Dictionary<string, string> AllOf(SoundCard card, SoundType type) =>
        card.Members.ToDictionary(entry => entry.Name, _ => type.ToLabel());

    private static List<SoundListItem.GroupCard> Cards(IReadOnlyList<SoundListItem> items) => items.OfType<SoundListItem.GroupCard>().ToList();

    [Fact]
    public void 기본_모드는_소개_바꾼_수_구역과_카드_끝_여백의_차례다()
    {
        var items = Build();

        Assert.Same(SoundListItem.Intro.Instance, items[0]);
        Assert.Equal(new SoundListItem.Changed(0), items[SoundList.ResultsIndex]);
        Assert.Same(SoundListItem.End.Instance, items[^1]);
        Assert.DoesNotContain(SoundListItem.Empty.Instance, items);

        // 구역 머리 밑에 그 구역의 카드가 색인의 차례 그대로 온다. 고급 모드의 칸은 없다.
        var expected = new List<SoundListItem>();
        foreach (SoundSection section in Index.Sections)
        {
            expected.Add(new SoundListItem.SectionHeading(section.Type));
            expected.AddRange(section.Cards.Select(card => (SoundListItem)new SoundListItem.GroupCard(
                new ShownCard(card, card.Members), new GroupState.Uniform(card.DefaultType), SoundGroups.Preview(card, card.Members))));
        }
        Assert.Equal(expected, items.Skip(2).SkipLast(1));
        Assert.Equal(new[] { SoundType.Danger, SoundType.Ambient }, items.OfType<SoundListItem.SectionHeading>().Select(heading => heading.Type));
    }

    [Fact]
    public void 고급_모드는_구역_머리_묶음_머리_소리_줄을_편다()
    {
        var overrides = new Dictionary<string, string> { ["Siren"] = SoundTypeLabels.Speech };

        var items = Build(overrides, advanced: true);

        Assert.Same(SoundListItem.Intro.Instance, items[0]);
        Assert.Equal(new SoundListItem.Changed(1), items[1]);
        Assert.Same(SoundListItem.End.Instance, items[^1]);
        Assert.Empty(Cards(items));
        var advanced = SoundGroups.AdvancedItems(Index.Filter(overrides, "", SoundFilter.All));
        Assert.Equal(advanced.Select(item => item.Key), items.Skip(2).SkipLast(1).Select(item => item.Key));
        // 소리 줄은 바꾼 것까지 반영한 지금 종류를 든다.
        var siren = Assert.Single(items.OfType<SoundListItem.Sound>(), row => row.Entry.Name == "Siren");
        Assert.Equal(SoundType.Speech, siren.Type);
        Assert.All(items.OfType<SoundListItem.Sound>().Where(row => row.Entry.Name != "Siren"), row => Assert.Equal(row.Entry.DefaultType, row.Type));
    }

    [Fact]
    public void 맞는_소리가_없으면_바꾼_수_밑에_빈_결과_안내를_둔다()
    {
        foreach (bool advanced in new[] { false, true })
        {
            var items = Build(query: "zzzzqqq", advanced: advanced);

            Assert.Equal(new[] { "intro", "changed", "empty", "end" }, items.Select(item => item.Key));
        }
    }

    [Fact]
    public void 바꾼_수는_묶음이_아니라_소리를_센다()
    {
        SoundCard sirens = Card(SoundGroup.Sirens);
        var overrides = AllOf(sirens, SoundType.Ambient);
        overrides["Doorbell"] = SoundTypeLabels.Danger;

        var items = Build(overrides);

        Assert.Equal(new SoundListItem.Changed(sirens.Members.Count + 1), items[1]);
        Assert.Equal(Index.CountChanged(overrides), ((SoundListItem.Changed)items[1]).Count);
    }

    [Fact]
    public void 카드는_지금_종류와_찾는_말에_맞은_소리를_앞에_둔_미리보기를_든다()
    {
        SoundCard sirens = Card(SoundGroup.Sirens);
        var mixed = new Dictionary<string, string> { ["Police car (siren)"] = SoundTypeLabels.Ambient };

        var card = Assert.Single(Cards(Build(mixed, query: "police")));

        Assert.Equal(sirens, card.Shown.Card);
        Assert.Equal(new[] { "Police car (siren)" }, SoundTestData.NamesOf(card.Shown.Shown));
        Assert.Same(GroupState.Mixed.Instance, card.State);
        Assert.Equal("Police car (siren)", card.Preview.Names[0].Name);
        Assert.Equal(SoundGroups.Preview(sirens, card.Shown.Shown), card.Preview);

        var changed = Assert.Single(Cards(Build(AllOf(sirens, SoundType.Speech), query: "police")));
        Assert.Equal(new GroupState.Uniform(SoundType.Speech), changed.State);
    }

    [Fact]
    public void 거르기를_켠_채로_바꾼_카드는_찾는_말과_거르기가_그대로일_때만_남는다()
    {
        SoundCard sirens = Card(SoundGroup.Sirens);
        var overrides = AllOf(sirens, SoundType.Ambient); // 위협음 거르기에서 빠진다
        var kept = KeptSounds.Empty.Plus("", SoundFilter.Danger, sirens.Members.Select(entry => entry.Name));

        Assert.Contains(Cards(Build(overrides, filter: SoundFilter.Danger, kept: kept)), card => card.Shown.Card == sirens);
        Assert.DoesNotContain(Cards(Build(overrides, filter: SoundFilter.Danger)), card => card.Shown.Card == sirens);
        // 찾는 말이나 거르기가 남기기 시작할 때와 다르면 남기지 않는다.
        Assert.DoesNotContain(Cards(Build(overrides, query: "s", filter: SoundFilter.Danger, kept: kept)), card => card.Shown.Card == sirens);
        Assert.DoesNotContain(Cards(Build(overrides, filter: SoundFilter.Speech, kept: kept)), card => card.Shown.Card == sirens);
    }

    [Fact]
    public void 키는_겹치지_않고_두_모드의_구역_머리는_키가_같다()
    {
        var basic = Build().Select(item => item.Key).ToList();
        var advanced = Build(advanced: true).Select(item => item.Key).ToList();

        Assert.Equal(basic.Count, basic.Distinct().Count());
        Assert.Equal(advanced.Count, advanced.Distinct().Count());
        Assert.Equal(basic.Where(key => key.StartsWith("s:", StringComparison.Ordinal)), advanced.Where(key => key.StartsWith("s:", StringComparison.Ordinal)));
        Assert.Equal(new[] { "s:danger", "s:ambient" }, basic.Where(key => key.StartsWith("s:", StringComparison.Ordinal)));
        Assert.Contains("c:sirens:danger", basic);
        Assert.Contains("g:sirens:danger", advanced);
        Assert.Contains("r:" + SoundCatalog.IndexOf(Entries, "Siren"), advanced);
    }

    [Fact]
    public void 줄의_차례는_바꾼_종류와_상관없다()
    {
        // 바꾼 카드는 다른 구역으로 옮겨 가지 않는다. 그래서 종류를 골라도 화면은 줄을 옮기지 않고 고쳐 쓰기만 한다.
        var before = Build().Select(item => item.Key);
        var after = Build(AllOf(Card(SoundGroup.Sirens), SoundType.Ambient)).Select(item => item.Key);

        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("", false, false)]
    [InlineData("", true, true)]
    [InlineData("bell", false, true)]
    [InlineData("bell", true, true)]
    public void 찾는_말이_있으면_맨_위에서도_결과까지_올리고_거르기만_바꾸면_내려가_있을_때만_올린다(string query, bool introScrolledAway, bool expected)
    {
        Assert.Equal(expected, SoundList.ShouldScrollToResults(query, introScrolledAway));
    }
}
