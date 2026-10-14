// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 묶음 화면의 찾기와 거르기(#349). 기본 모드는 카드로, 고급 모드는 같은 결과를 줄로 편다. 모바일 SoundGroupSearchTest 를 옮겼다.
///
/// 보이는 이름은 실제 한국어 데이터(SoundNames/ko.json, GroupTitles/ko.json)다. 기본 종류는 매핑과 상관없이 정해 둔다(사이렌·경보음
/// 묶음만 위협음, 나머지는 환경음). 매핑이 바뀌어도 이 테스트가 흔들리지 않게 하려는 것이다.
/// </summary>
public class SoundGroupSearchTests
{
    private static readonly IReadOnlyList<SoundEntry> Entries = SoundTestData.Entries(name =>
        SoundTestData.Table.TryGetValue(name, out var row) && row.Group is SoundGroup.Sirens or SoundGroup.Alarms
            ? SoundType.Danger
            : SoundType.Ambient);

    private static readonly IReadOnlyList<SoundSection> Sections = SoundGroups.Sections(Entries);

    private static readonly IReadOnlyList<string> KoNames = SoundData.Names("ko");

    /// <summary>화면이 한국어일 때의 찾기 값. 분류 탭처럼 보이는 이름과 모델 이름을 잇는다.</summary>
    private static readonly List<string> KoKeys =
        Entries.Select(entry => SoundCatalog.SearchKey(KoNames[entry.Index], entry.Name)).ToList();

    private static readonly IReadOnlyDictionary<SoundGroup, string> KoTitles = SoundData.GroupTitles("ko")
        .ToDictionary(pair => pair.Key, pair => SoundCatalog.SearchKey(pair.Value, ""));

    private static readonly Dictionary<string, string> NoOverrides = new();

    private static IReadOnlyList<ShownSection> Search(
        string query,
        SoundFilter filter = SoundFilter.All,
        IReadOnlyDictionary<string, string>? overrides = null,
        IReadOnlySet<string>? kept = null) =>
        SoundGroups.Filter(Sections, KoKeys, KoTitles, overrides ?? NoOverrides, query, filter, kept);

    private static SoundEntry Entry(string name) => Entries.First(entry => entry.Name == name);

    private static List<string> Names(IEnumerable<SoundEntry> list) => SoundTestData.NamesOf(list);

    private static List<SoundGroup?> Groups(IReadOnlyList<ShownSection> shown) =>
        shown.SelectMany(section => section.Cards).Select(card => card.Card.Group).ToList();

    private static ShownCard CardOf(IReadOnlyList<ShownSection> shown, SoundGroup group) =>
        Assert.Single(shown.SelectMany(section => section.Cards), card => card.Card.Group == group);

    private static Dictionary<string, string> Map(params (string Name, SoundType Type)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Type.ToLabel());

    [Fact]
    public void 찾는_말과_거르기가_없으면_모든_카드와_소리가_남는다()
    {
        var all = Search("  ");
        Assert.Equal(Sections.Select(section => section.Type), all.Select(section => section.Type));
        Assert.Equal(Sections.SelectMany(section => section.Cards), all.SelectMany(section => section.Cards).Select(card => card.Card));
        Assert.All(all.SelectMany(section => section.Cards), card => Assert.Equal(card.Card.Members, card.Shown));
    }

    [Fact]
    public void 묶음_이름이_맞으면_묶음_전체가_남는다()
    {
        // 사이렌 묶음의 이름은 '사이렌' 소리의 이름이다.
        var sirens = Search("사이렌");
        Assert.Equal(new SoundGroup?[] { SoundGroup.Sirens }, Groups(sirens));
        Assert.Equal(CardOf(sirens, SoundGroup.Sirens).Card.Members, CardOf(sirens, SoundGroup.Sirens).Shown);
        // 새로 지은 묶음 이름으로도 찾는다. '부엌' 은 소리 이름에는 없는 말이다.
        var kitchen = Search("부엌");
        Assert.Equal(new SoundGroup?[] { SoundGroup.Kitchen }, Groups(kitchen));
        Assert.Equal(CardOf(kitchen, SoundGroup.Kitchen).Card.Members, CardOf(kitchen, SoundGroup.Kitchen).Shown);
    }

    [Fact]
    public void 소리_이름이_맞으면_묶음이_남고_맞은_소리가_미리보기_앞에_온다()
    {
        // '노크' 를 찾으면 환경음 › 초인종 묶음이 '노크 소리 • 딩동 • 톡톡 두드리는 소리' 로 보인다. 빈 구역은 숨는다.
        var result = Search("노크");
        Assert.Equal(new[] { SoundType.Ambient }, result.Select(section => section.Type));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Doorbell }, Groups(result));
        var doorbell = CardOf(result, SoundGroup.Doorbell);
        Assert.Equal(new[] { "Knock" }, Names(doorbell.Shown));
        var preview = SoundGroups.Preview(doorbell.Card, doorbell.Shown);
        Assert.Equal(new[] { "노크 소리", "딩동", "톡톡 두드리는 소리" }, preview.Names.Select(entry => KoNames[entry.Index]));
        Assert.Equal(0, preview.More);
    }

    [Fact]
    public void 묶음_이름에_맞은_묶음과_소리_이름에_맞은_묶음이_함께_남는다()
    {
        // '경적' 은 경적 묶음의 이름에도, 다른 묶음의 기차 경적·안개 경적·짧은 경적에도 들어 있다.
        var result = Search("경적");
        Assert.Equal(new SoundGroup?[] { SoundGroup.Horns, SoundGroup.VehicleWarnings, SoundGroup.Vehicle }, Groups(result));
        Assert.Equal(CardOf(result, SoundGroup.Horns).Card.Members, CardOf(result, SoundGroup.Horns).Shown);
        Assert.Equal(new[] { "Train horn", "Foghorn" }, Names(CardOf(result, SoundGroup.VehicleWarnings).Shown));
        Assert.Equal(new[] { "Toot" }, Names(CardOf(result, SoundGroup.Vehicle).Shown));
    }

    [Fact]
    public void 번역된_이름과_영어_이름_어느_쪽으로도_찾는다()
    {
        // 화면이 한국어여도 모델 이름(영어)으로 찾는다.
        var knock = Search("knock");
        Assert.Equal(new[] { "Knock" }, Names(CardOf(knock, SoundGroup.Doorbell).Shown));
        Assert.Equal(new[] { "Engine knocking" }, Names(CardOf(knock, SoundGroup.Vehicle).Shown));
        // 화면이 영어면 새로 지은 묶음 이름도 영어 문구로 찾는다.
        var enKeys = Entries.Select(entry => SoundCatalog.SearchKey(entry.Name, entry.Name)).ToList();
        var enTitles = SoundData.GroupTitles(SoundLanguages.English)
            .ToDictionary(pair => pair.Key, pair => SoundCatalog.SearchKey(pair.Value, ""));
        var kitchen = SoundGroups.Filter(Sections, enKeys, enTitles, NoOverrides, "Kitchen", SoundFilter.All);
        Assert.Equal(new SoundGroup?[] { SoundGroup.Kitchen }, Groups(kitchen));
    }

    [Fact]
    public void 화면이_한국어여도_새로_지은_묶음_이름을_영어로_찾는다()
    {
        // 분류 탭과 같은 방법(SoundGroups.TitleKeys)으로 한국어 이름과 영어 이름을 잇는다.
        var ko = SoundData.GroupTitles("ko");
        var en = SoundData.GroupTitles(SoundLanguages.English);
        var titles = SoundGroups.TitleKeys(local: group => ko[group], english: group => en[group]);
        foreach (string query in new[] { "부엌", "kitchen", "Kitchen and cooking" })
        {
            var result = SoundGroups.Filter(Sections, KoKeys, titles, NoOverrides, query, SoundFilter.All);
            Assert.Equal(new SoundGroup?[] { SoundGroup.Kitchen }, Groups(result));
            Assert.Equal(CardOf(result, SoundGroup.Kitchen).Card.Members, CardOf(result, SoundGroup.Kitchen).Shown);
        }
        // 영어 이름의 쉼표 자리를 띄어 쳐도 찾는다.
        var background = SoundGroups.Filter(Sections, KoKeys, titles, NoOverrides, "tv radio", SoundFilter.All);
        Assert.Equal(new SoundGroup?[] { SoundGroup.Background }, Groups(background));
    }

    [Fact]
    public void 문장_부호로_이은_이름은_띄어_쳐도_찾는다()
    {
        // 새로 지은 묶음 이름은 낱말을 '·' 로 잇는다(부엌·요리). 사람은 그 자리를 띄어 친다(#349).
        Assert.Equal(new SoundGroup?[] { SoundGroup.Kitchen }, Groups(Search("부엌 요리")));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Horns }, Groups(Search("경적 자동차")));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Background }, Groups(Search("TV 라디오")));
        // 소리 이름의 쉼표·괄호도 같다. 모델 이름은 "Fire engine, fire truck (siren)" 이다.
        var fireTruck = Search("fire engine fire truck siren");
        Assert.Equal(new SoundGroup?[] { SoundGroup.Sirens }, Groups(fireTruck));
        Assert.Equal(new[] { "Fire engine, fire truck (siren)" }, Names(CardOf(fireTruck, SoundGroup.Sirens).Shown));
    }

    [Fact]
    public void 보이는_이름과_모델_이름을_이어_붙인_곳에서는_맞지_않는다()
    {
        Assert.Empty(Search("노크 소리knock"));
        Assert.Empty(Search("zzzz"));
    }

    [Fact]
    public void 종류_거르기는_지금_종류를_따르고_바꾼_소리도_제_구역에_있다()
    {
        var overrides = Map(("Doorbell", SoundType.Danger), ("Siren", SoundType.Ambient));

        var danger = Search("", SoundFilter.Danger, overrides);
        Assert.Equal(new SoundGroup?[] { SoundGroup.Alarms, SoundGroup.Sirens, SoundGroup.Doorbell }, Groups(danger));
        Assert.DoesNotContain("Siren", Names(CardOf(danger, SoundGroup.Sirens).Shown));
        Assert.Equal(4, CardOf(danger, SoundGroup.Sirens).Shown.Count);
        Assert.Equal(new[] { "Doorbell" }, Names(CardOf(danger, SoundGroup.Doorbell).Shown));
        // 구역은 기본 종류라 위협음으로 바꾼 초인종도 환경음 구역에 있다.
        Assert.Equal(new[] { SoundType.Danger, SoundType.Ambient }, danger.Select(section => section.Type));

        var ambient = Search("", SoundFilter.Ambient, overrides);
        Assert.Equal(new[] { "Siren" }, Names(CardOf(ambient, SoundGroup.Sirens).Shown));
        Assert.Equal(new[] { "Ding-dong", "Knock", "Tap" }, Names(CardOf(ambient, SoundGroup.Doorbell).Shown));
        Assert.Empty(Search("", SoundFilter.Speech, overrides)); // 대화음인 소리가 없다.
    }

    [Fact]
    public void 바꾼_소리_거르기는_바꾼_소리가_있는_카드만_남긴다()
    {
        var overrides = Map(("Doorbell", SoundType.Danger), ("Siren", SoundType.Ambient));
        var changed = Search("", SoundFilter.Changed, overrides);
        Assert.Equal(new SoundGroup?[] { SoundGroup.Sirens, SoundGroup.Doorbell }, Groups(changed));
        Assert.Equal(new[] { "Siren" }, Names(CardOf(changed, SoundGroup.Sirens).Shown));
        Assert.Equal(new[] { "Doorbell" }, Names(CardOf(changed, SoundGroup.Doorbell).Shown));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Doorbell }, Groups(Search("초인종", SoundFilter.Changed, overrides))); // 찾는 말과 함께 쓴다.
        Assert.Empty(Search("", SoundFilter.Changed));
    }

    [Fact]
    public void 거르기에서_방금_바꾼_소리는_찾는_말이_그대로인_동안_남는다()
    {
        // 위협음만 보다가 사이렌 묶음을 환경음으로 바꾼 것이다(#374). 남기지 않으면 카드가 눈앞에서 사라진다.
        var sirenNames = SoundTestData.Members(SoundGroup.Sirens);
        var allAmbient = sirenNames.ToDictionary(name => name, _ => SoundTypeLabels.Ambient);
        Assert.DoesNotContain(SoundGroup.Sirens, Groups(Search("", SoundFilter.Danger, allAmbient)));
        var kept = Search("", SoundFilter.Danger, allAmbient, sirenNames.ToHashSet());
        Assert.Equal(CardOf(kept, SoundGroup.Sirens).Card.Members, CardOf(kept, SoundGroup.Sirens).Shown);

        // 남긴 소리는 거르기에 맞는 소리와 함께 표 순서로 보인다.
        var one = Search("", SoundFilter.Danger, Map(("Siren", SoundType.Ambient)), new HashSet<string> { "Siren" });
        Assert.Equal(sirenNames, Names(CardOf(one, SoundGroup.Sirens).Shown));

        // 거르기만 넘고 찾는 말은 넘지 않는다.
        Assert.DoesNotContain(SoundGroup.Sirens, Groups(Search("노크", SoundFilter.Danger, allAmbient, sirenNames.ToHashSet())));

        // "바꾼 소리" 거르기에서 기본 종류로 되돌린 소리도 남는다.
        var changed = Search("", SoundFilter.Changed, NoOverrides, new HashSet<string> { "Doorbell" });
        Assert.Equal(new SoundGroup?[] { SoundGroup.Doorbell }, Groups(changed));
        Assert.Equal(new[] { "Doorbell" }, Names(CardOf(changed, SoundGroup.Doorbell).Shown));

        // 고급 모드는 그 줄과 그 위의 묶음·구역 머리가 남는다.
        var rows = SoundGroups.AdvancedItems(Search("", SoundFilter.Danger, NoOverrides, new HashSet<string> { "Doorbell" }));
        Assert.Equal(
            new[] { "s:ambient", "g:doorbell:ambient", "r:" + Entry("Doorbell").Index },
            rows.SkipWhile(item => item.Key != "s:ambient").Select(item => item.Key));
    }

    [Fact]
    public void 한글은_치는_중의_모든_모양에서_묶음이_끊기지_않는다()
    {
        // SoundCatalogTests 와 같은 경우를 묶음 찾기로 본다. 보이는 이름은 영어 그대로 두고 사이렌만 다른 말로 보인다고 친다.
        var typing = new Dictionary<string, string[]>
        {
            ["사이렌"] = new[] { "ㅅ", "사", "상", "사이", "사일", "사이레", "사이렌" },
            ["화재 경보음"] = new[] { "ㅎ", "호", "화", "홪", "화재", "화잭", "화재 겨", "화재 경", "화재 경보", "화재 경봉", "화재 경보음" },
            ["닭 울음소리"] = new[] { "ㄷ", "다", "달", "닭", "닭 울" },
        };
        var noTitles = new Dictionary<SoundGroup, string>();
        foreach (var (label, steps) in typing)
        {
            var keys = Entries.Select(entry => SoundCatalog.SearchKey(entry.Name == "Siren" ? label : entry.Name, entry.Name)).ToList();
            foreach (string step in steps)
            {
                var sirens = SoundGroups.Filter(Sections, keys, noTitles, NoOverrides, step, SoundFilter.All)
                    .SelectMany(section => section.Cards)
                    .SingleOrDefault(card => card.Card.Group == SoundGroup.Sirens);
                Assert.True(sirens?.Shown.Count == 5, $"\"{label}\" 을 치는 중 \"{step}\" 에서 사이렌 묶음이 통째로 남아야 한다");
            }
        }
    }

    [Fact]
    public void 고급_모드는_맞는_줄과_그_위의_묶음과_구역_머리만_보인다()
    {
        var knock = SoundGroups.AdvancedItems(Search("노크"));
        Assert.Equal(new[] { "s:ambient", "g:doorbell:ambient", "r:" + Entry("Knock").Index }, knock.Select(item => item.Key));

        // 묶음 이름이 맞으면 그 묶음의 줄이 모두 보인다.
        var doorbell = SoundGroups.AdvancedItems(Search("초인종"));
        Assert.Equal(
            new[] { "s:ambient", "g:doorbell:ambient" }
                .Concat(new[] { "Doorbell", "Ding-dong", "Knock", "Tap" }.Select(name => "r:" + Entry(name).Index)),
            doorbell.Select(item => item.Key));

        // 거르기에 맞는 줄이 없는 묶음은 머리도 없다.
        var danger = SoundGroups.AdvancedItems(Search("", SoundFilter.Danger, Map(("Doorbell", SoundType.Danger))));
        Assert.Equal(
            new[] { "s:ambient", "g:doorbell:ambient", "r:" + Entry("Doorbell").Index },
            danger.SkipWhile(item => item.Key != "s:ambient").Select(item => item.Key));
    }

    [Fact]
    public void 고급_모드에는_모든_소리가_한_번씩_보이고_키가_겹치지_않는다()
    {
        var items = SoundGroups.AdvancedItems(Search(""));
        Assert.Equal(
            Entries.Select(entry => entry.Index).Order(),
            items.OfType<AdvancedItem.Row>().Select(row => row.Entry.Index).Order());
        Assert.Equal(Sections.Sum(section => section.Cards.Count), items.OfType<AdvancedItem.GroupHeading>().Count());
        Assert.Equal(Sections.Count, items.OfType<AdvancedItem.SectionHeading>().Count());
        Assert.Equal(items.Count, items.Select(item => item.Key).Distinct().Count()); // 목록의 키가 겹치지 않는다.
    }
}
