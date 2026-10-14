// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 분류 탭에서 바꾼 종류의 맵(설정의 sound_types). 모바일 SettingsManager.setSoundTypes·loadSoundTypes(SoundTypeSettingsTest)의 규칙이다.
/// 기본 종류와 같은 값은 두지 않아 AI 의 <see cref="YamnetMappingPolicy.From"/> 이 버리는 값이 없다.
/// </summary>
public class SoundTypeOverridesTests
{
    private static readonly IReadOnlyList<SoundEntry> Entries = SoundTestData.Entries();

    private static readonly IReadOnlyList<SoundSection> Sections = SoundGroups.Sections(Entries);

    private static readonly SoundType[] Types = { SoundType.Ambient, SoundType.Speech, SoundType.Danger };

    private static readonly IReadOnlyDictionary<string, string> NoOverrides = new Dictionary<string, string>();

    private static SoundCard Card(SoundGroup group) =>
        Sections.SelectMany(section => section.Cards).First(card => card.Group == group);

    private static SoundEntry Entry(string name) => Entries.First(entry => entry.Name == name);

    /// <summary>기본 종류와 같은 값이 없고, 모두 세 라벨 중 하나다.</summary>
    private static void AssertNoDefaults(IReadOnlyDictionary<string, string> overrides)
    {
        foreach (var (name, label) in overrides)
        {
            Assert.True(SoundTypeLabels.TryParse(label, out SoundType type), $"{name}: {label}");
            Assert.NotEqual(Entry(name).DefaultType, type);
        }
        // AI 가 같은 맵에서 버리는 값이 없다.
        Assert.Equal(overrides.Count, YamnetMappingPolicy.From(overrides, SoundTestData.Names).OverrideCount);
    }

    [Fact]
    public void 묶음째_바꾸면_소리마다_기본_종류와_다른_것만_적는다()
    {
        var sirens = Card(SoundGroup.Sirens);
        var ambient = SoundTypeOverrides.WithType(NoOverrides, sirens.Members, SoundType.Ambient);
        Assert.Equal(sirens.Members.Select(entry => entry.Name).Order(), ambient.Keys.Order());
        Assert.All(ambient.Values, label => Assert.Equal(SoundTypeLabels.Ambient, label));
        Assert.Equal(new GroupState.Uniform(SoundType.Ambient), SoundGroups.State(sirens, ambient));
        Assert.Equal(5, SoundCatalog.CountChanged(Entries, ambient)); // 묶음 하나를 바꾸면 그 소리 수만큼 는다.
        AssertNoDefaults(ambient);

        // 기본 종류를 고르면 묶음의 바꾼 것을 모두 지운다.
        var back = SoundTypeOverrides.WithType(ambient, sirens.Members, SoundType.Danger);
        Assert.Empty(back);
    }

    [Fact]
    public void 다른_소리의_값은_그대로_둔다()
    {
        var current = new Dictionary<string, string> { ["Doorbell"] = SoundTypeLabels.Danger, ["Civil defense siren"] = SoundTypeLabels.Speech };
        var updated = SoundTypeOverrides.WithType(current, Card(SoundGroup.Sirens).Members, SoundType.Ambient);
        Assert.Equal(SoundTypeLabels.Danger, updated["Doorbell"]);
        Assert.Equal(SoundTypeLabels.Ambient, updated["Civil defense siren"]); // 묶음의 소리는 하나씩 바꾼 것도 덮어쓴다.
        Assert.Equal(6, updated.Count);
        Assert.Equal(SoundTypeLabels.Speech, current["Civil defense siren"]); // 받은 맵은 고치지 않는다.
    }

    [Fact]
    public void 바뀌는_것이_없으면_같은_맵을_돌려준다()
    {
        // 그때는 저장하지 않는다. AI 도 맵이 그대로라 판정을 다시 시작하지 않는다.
        var sirens = Card(SoundGroup.Sirens);
        Assert.Same(NoOverrides, SoundTypeOverrides.WithType(NoOverrides, sirens.Members, SoundType.Danger));
        var ambient = SoundTypeOverrides.WithType(NoOverrides, sirens.Members, SoundType.Ambient);
        Assert.Same(ambient, SoundTypeOverrides.WithType(ambient, sirens.Members, SoundType.Ambient));
        Assert.Same(ambient, SoundTypeOverrides.WithType(ambient, Array.Empty<SoundEntry>(), SoundType.Speech));
    }

    [Fact]
    public void 고급_모드는_소리_하나를_바꾼다()
    {
        var one = SoundTypeOverrides.WithType(NoOverrides, new[] { Entry("Knock") }, SoundType.Danger);
        Assert.Equal(new[] { "Knock" }, one.Keys);
        Assert.Same(GroupState.Mixed.Instance, SoundGroups.State(Card(SoundGroup.Doorbell), one));
        Assert.True(SoundGroups.OverwritesOneByOne(Card(SoundGroup.Doorbell), one, SoundType.Ambient));
        Assert.Empty(SoundTypeOverrides.WithType(one, new[] { Entry("Knock") }, SoundType.Ambient)); // 기본으로 되돌리면 지운다.
    }

    [Fact]
    public void 어느_묶음을_어느_종류로_바꿔도_기본값을_저장하지_않는다()
    {
        // 바꾼 것이 없을 때, 하나씩 바꿔 둔 소리가 섞였을 때, 다른 묶음을 바꿔 둔 때에서 모든 카드를 세 종류로 차례로 바꿔 본다.
        var mixed = new Dictionary<string, string>
        {
            ["Knock"] = SoundTypeLabels.Danger,
            ["Siren"] = SoundTypeLabels.Speech,
            ["Music"] = SoundTypeLabels.Danger,
            ["Chainsaw"] = SoundTypeLabels.Ambient,
        };
        var starts = new IReadOnlyDictionary<string, string>[] { NoOverrides, mixed };
        foreach (var start in starts)
        {
            IReadOnlyDictionary<string, string> current = start;
            foreach (SoundCard card in Sections.SelectMany(section => section.Cards))
            {
                foreach (SoundType type in Types)
                {
                    current = SoundTypeOverrides.WithType(current, card.Members, type);
                    Assert.Equal(new GroupState.Uniform(type), SoundGroups.State(card, current));
                    Assert.False(SoundGroups.OverwritesOneByOne(card, current, type));
                }
            }
            AssertNoDefaults(current);
            Assert.Equal(Entries.Count(entry => entry.DefaultType != SoundType.Danger), SoundCatalog.CountChanged(Entries, current));
        }
    }

    [Fact]
    public void 저장값을_읽을_때_세_라벨이_아닌_값과_기본_종류와_같은_값은_버린다()
    {
        var stored = new Dictionary<string, string>
        {
            ["Siren"] = SoundTypeLabels.Ambient, // 남는다
            ["Explosion"] = SoundTypeLabels.Danger, // 기본 종류와 같다(매핑이 바뀌어 같아진 값)
            ["Doorbell"] = "Danger", // 대소문자가 다르다
            ["Knock"] = "", // 라벨이 아니다
            ["Old name"] = SoundTypeLabels.Speech, // 목록에 없는 이름은 모바일처럼 남긴다
        };
        var kept = SoundTypeOverrides.Sanitize(stored);
        Assert.Equal(new[] { "Old name", "Siren" }, kept.Keys.Order());
        Assert.Equal(SoundTypeLabels.Ambient, kept["Siren"]);
        Assert.Empty(SoundTypeOverrides.Sanitize(new Dictionary<string, string>()));
        Assert.Equal(1, SoundCatalog.CountChanged(Entries, kept));
        Assert.Equal(1, YamnetMappingPolicy.From(kept, SoundTestData.Names).OverrideCount); // AI 는 목록에 없는 이름도 버린다.
    }
}
