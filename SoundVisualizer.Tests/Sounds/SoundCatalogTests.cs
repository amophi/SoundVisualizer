// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>분류 탭의 목록(기본 종류)과 찾기·거르기. 모바일 SoundCatalogTest 를 옮겼다.</summary>
public class SoundCatalogTests
{
    private static readonly IReadOnlyList<SoundEntry> Entries = SoundCatalog.FromNames(SoundTestData.Names);

    private static readonly Dictionary<string, string> NoOverrides = new();

    private static SoundEntry Entry(string name) => Entries.First(entry => entry.Name == name);

    /// <summary>테스트에서는 보이는 이름으로 영어 이름을 그대로 쓰되, <paramref name="labels"/> 로 몇 개만 다른 말로 바꿔 본다.</summary>
    private static List<string> Keys(Dictionary<string, string>? labels = null) =>
        Entries.Select(entry => SoundCatalog.SearchKey(labels?.GetValueOrDefault(entry.Name) ?? entry.Name, entry.Name)).ToList();

    private static List<string> Find(
        string query,
        SoundFilter filter = SoundFilter.All,
        Dictionary<string, string>? labels = null,
        Dictionary<string, string>? overrides = null) =>
        SoundTestData.NamesOf(SoundCatalog.Filter(Entries, Keys(labels), overrides ?? NoOverrides, query, filter));

    [Fact]
    public void 모델의_소리_521가지를_번호_순서대로_늘어놓는다()
    {
        Assert.Equal(YamnetCoarseClassifier.NumClasses, Entries.Count);
        for (int i = 0; i < Entries.Count; i++) Assert.Equal(i, Entries[i].Index);
        Assert.Equal("Speech", Entries[0].Name);
        Assert.Equal("Gunshot, gunfire", Entries[421].Name);
    }

    [Fact]
    public void 기본_종류는_AI_의_매핑이고_사용자가_바꾼_것에_흔들리지_않는다()
    {
        // 모바일은 설정에 사이렌을 환경음으로 적은 뒤 목록을 새로 만든다. 여기서는 바꾼 맵을 만들고 목록을 새로 만든다.
        var overrides = SoundTypeOverrides.WithType(NoOverrides, new[] { Entry("Siren") }, SoundType.Ambient);
        var fresh = SoundCatalog.FromNames(SoundTestData.Names);

        Assert.Equal(SoundType.Danger, fresh.First(entry => entry.Name == "Siren").DefaultType);
        Assert.Equal(SoundType.Ambient, SoundCatalog.TypeOf(fresh.First(entry => entry.Name == "Siren"), overrides));
        Assert.All(fresh, entry => Assert.Equal(YamnetThreeClassMapper.MapDisplayNameToCoarse(entry.Name), entry.DefaultType));
    }

    [Fact]
    public void 지금_종류는_바꾼_것이_있으면_그것_없으면_기본_종류다()
    {
        var overrides = new Dictionary<string, string> { ["Siren"] = SoundTypeLabels.Ambient };
        Assert.Equal(SoundType.Ambient, SoundCatalog.TypeOf(Entry("Siren"), overrides));
        Assert.Equal(SoundType.Danger, SoundCatalog.TypeOf(Entry("Explosion"), overrides));
    }

    [Fact]
    public void 세_라벨이_아닌_값은_AI_처럼_바꾸지_않은_것으로_본다()
    {
        // 저장값을 읽을 때 버리는 값이라 보통은 없다. 그래도 들어오면 AI(YamnetMappingPolicy.From)처럼 기본 종류로 본다.
        var overrides = new Dictionary<string, string> { ["Siren"] = "Ambient", ["Explosion"] = "" };
        Assert.Equal(SoundType.Danger, SoundCatalog.TypeOf(Entry("Siren"), overrides));
        Assert.Equal(SoundType.Danger, SoundCatalog.TypeOf(Entry("Explosion"), overrides));
        Assert.Equal(0, SoundCatalog.CountChanged(Entries, overrides));
    }

    [Fact]
    public void 모델_이름으로_클래스_번호를_찾는다()
    {
        // 모델 이름은 서로 겹치지 않는다. 겹치면 다른 소리의 이름을 보고 종류를 바꾸게 된다.
        Assert.Equal(Entries.Count, Entries.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal).Count);
        Assert.All(Entries, entry => Assert.Equal(entry.Index, SoundCatalog.IndexOf(Entries, entry.Name)));
        Assert.Equal(390, SoundCatalog.IndexOf(Entries, "Siren"));
        Assert.Equal(421, SoundCatalog.IndexOf(Entries, "Gunshot, gunfire"));
        Assert.Equal(-1, SoundCatalog.IndexOf(Entries, "siren")); // 이름은 글자 그대로 맞춰 본다.
        Assert.Equal(-1, SoundCatalog.IndexOf(Entries, ""));
        Assert.Equal(-1, SoundCatalog.IndexOf(Entries, null));
        Assert.Equal(-1, SoundCatalog.IndexOf(Array.Empty<SoundEntry>(), "Siren")); // 목록을 아직 읽지 않았으면 찾지 못한다.
    }

    [Fact]
    public void 찾는_말이_없으면_거르기만_적용하고_순서를_지킨다()
    {
        var all = SoundCatalog.Filter(Entries, Keys(), NoOverrides, "", SoundFilter.All);
        Assert.Equal(Entries, all);

        var danger = SoundCatalog.Filter(Entries, Keys(), NoOverrides, "  ", SoundFilter.Danger);
        Assert.NotEmpty(danger);
        Assert.All(danger, entry => Assert.Equal(SoundType.Danger, entry.DefaultType));
        Assert.Equal(danger.OrderBy(entry => entry.Index), danger); // 번호 순서 그대로
    }

    [Fact]
    public void 보이는_이름과_모델_이름_어느_쪽으로도_찾는다()
    {
        var labels = new Dictionary<string, string> { ["Siren"] = "사이렌", ["Civil defense siren"] = "민방위 사이렌" };
        Assert.Equal(new[] { "Siren", "Civil defense siren" }, Find("사이렌", labels: labels));
        Assert.Equal(new[] { "Civil defense siren" }, Find("civil defense", labels: labels)); // 영어 이름으로도 찾는다.
    }

    [Fact]
    public void 한글은_치는_중의_모든_모양에서_결과가_끊기지_않는다()
    {
        // 한글 자판으로 칠 때 입력 칸에 차례로 들어오는 모양. 다음 글자의 첫소리가 앞 글자의 받침으로 먼저 붙는다.
        var typing = new Dictionary<string, string[]>
        {
            ["사이렌"] = new[] { "ㅅ", "사", "상", "사이", "사일", "사이레", "사이렌" },
            ["화재 경보음"] = new[] { "ㅎ", "호", "화", "홪", "화재", "화잭", "화재 겨", "화재 경", "화재 경보", "화재 경봉", "화재 경보음" },
            ["닭 울음소리"] = new[] { "ㄷ", "다", "달", "닭", "닭 울" },
        };
        foreach (var (label, steps) in typing)
        {
            var labels = new Dictionary<string, string> { ["Siren"] = label };
            foreach (string step in steps)
            {
                Assert.True(Find(step, labels: labels).Exists(name => name == "Siren"), $"\"{label}\" 을 치는 중 \"{step}\" 에서 찾아져야 한다");
            }
        }
    }

    [Fact]
    public void 대소문자_띄어쓰기_악센트를_가리지_않는다()
    {
        var labels = new Dictionary<string, string>
        {
            ["Siren"] = "Sirène",
            ["Fire engine, fire truck (siren)"] = "Camion de pompiers",
        };
        Assert.Equal(new[] { "Siren" }, Find("SIRENE", labels: labels));
        Assert.Equal(new[] { "Fire engine, fire truck (siren)" }, Find("firetruck", labels: labels));
        Assert.Equal(new[] { "Fire engine, fire truck (siren)" }, Find("camion de", labels: labels));
    }

    [Fact]
    public void 보이는_이름과_모델_이름을_이어_붙인_곳에서는_맞지_않는다()
    {
        // "Rain" 을 "비" 로 보일 때 "비rain" 처럼 둘을 잇는 말은 어느 이름에도 없다.
        Assert.Empty(Find("비rain", labels: new Dictionary<string, string> { ["Rain"] = "비" }));
    }

    [Fact]
    public void 종류_거르기는_사용자가_바꾼_종류를_따른다()
    {
        var overrides = new Dictionary<string, string> { ["Siren"] = SoundTypeLabels.Ambient, ["Doorbell"] = SoundTypeLabels.Danger };
        var danger = Find("", SoundFilter.Danger, overrides: overrides);
        Assert.Contains("Doorbell", danger);
        Assert.DoesNotContain("Siren", danger);
        Assert.Contains("Siren", Find("", SoundFilter.Ambient, overrides: overrides));
    }

    [Fact]
    public void 바꾼_소리_거르기는_기본_종류와_다른_것만_보여_준다()
    {
        var overrides = new Dictionary<string, string> { ["Siren"] = SoundTypeLabels.Ambient, ["Doorbell"] = SoundTypeLabels.Danger };
        Assert.Equal(new[] { "Siren", "Doorbell" }.OrderBy(name => Entry(name).Index), Find("", SoundFilter.Changed, overrides: overrides));
        Assert.Equal(new[] { "Doorbell" }, Find("door", SoundFilter.Changed, overrides: overrides)); // 찾는 말과 함께 쓴다.
        Assert.Empty(Find("", SoundFilter.Changed));
    }

    [Fact]
    public void 바꾼_소리_수는_묶음이_아니라_소리를_센다()
    {
        // 사이렌 묶음(5개)을 통째로 환경음으로, 초인종 하나를 위협음으로 바꾼 것이다. "바꾼 소리" 거르기와 같은 기준이다.
        var entries = SoundTestData.Entries();
        var sirens = entries.Where(entry => entry.Group == SoundGroup.Sirens).ToList();
        var overrides = SoundTypeOverrides.WithType(new Dictionary<string, string>(), sirens, SoundType.Ambient);
        overrides = SoundTypeOverrides.WithType(overrides, entries.Where(entry => entry.Name == "Doorbell"), SoundType.Danger);

        Assert.Equal(6, SoundCatalog.CountChanged(entries, overrides));
        Assert.Equal(
            SoundCatalog.Filter(entries, SoundTestData.Names.ToList(), overrides, "", SoundFilter.Changed).Count,
            SoundCatalog.CountChanged(entries, overrides));

        // 목록에 없는 이름이나 기본 종류와 같아진 값(매핑이 바뀐 뒤)이 저장값에 남아 있어도 세지 않는다.
        var stale = new Dictionary<string, string> { ["Siren"] = SoundTypeLabels.Danger, ["Old name"] = SoundTypeLabels.Speech };
        Assert.Equal(0, SoundCatalog.CountChanged(entries, stale));
    }

    [Fact]
    public void 다듬은_말은_부호와_띄어쓰기와_결합_부호가_없고_한글은_낱자로_푼다()
    {
        Assert.Equal(SoundCatalog.Normalize("부엌요리"), SoundCatalog.Normalize("부엌·요리"));
        Assert.Equal("tvradioandbackgroundsound", SoundCatalog.Normalize("TV, radio and background sound"));
        Assert.Equal("sirene", SoundCatalog.Normalize("Sirène"));
        Assert.Equal("カメラ", SoundCatalog.Normalize("カメ・ラ")); // 가운뎃점(・)도 문장 부호다.
        Assert.Equal("ab", SoundCatalog.Normalize("a 　b")); // 줄 바꿈 없는 띄어쓰기, 전각 띄어쓰기
        Assert.Equal("ㄷㅏㄹㄱ", SoundCatalog.Normalize("닭")); // 첫소리·받침은 호환용 낱자로, 겹받침은 둘로
        Assert.Equal(SoundCatalog.Normalize("ㄹㄱ"), SoundCatalog.Normalize("ㄺ")); // 치는 중의 겹받침
        Assert.Equal(SoundCatalog.Normalize("ㅗㅏ"), SoundCatalog.Normalize("ㅘ"));
        Assert.Equal("", SoundCatalog.Normalize(" ,·- "));
    }

    [Fact]
    public void 짝_없는_서로게이트가_있어도_다듬는다()
    {
        // 붙여 넣은 글 등. .NET 의 정규화는 이런 글을 받지 않으므로 대체 문자로 바꿔 다듬는다. 다른 글자는 그대로다.
        string normalized = SoundCatalog.Normalize("Si\uD800ren");
        Assert.Equal("si�ren", normalized);
        Assert.Empty(Find("\uDC00"));
    }
}
