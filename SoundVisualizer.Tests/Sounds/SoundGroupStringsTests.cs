// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 묶음 이름(#349)이 모든 언어에 있고 서로 가려지는지 본다. 모바일 SoundGroupStringsTest 가운데 이 데이터(소리 이름, 새로 지은 묶음
/// 이름)에 대한 것을 옮겼다. 분류 탭의 안내 문구(classify_*)에 대한 것은 화면 문구와 함께 옮긴다.
///
/// 묶음 이름은 다른 묶음의 이름이나 다른 묶음에 든 소리의 이름과 같지 않다(#349 의 번역 규칙, #370). 구역 머리(종류 이름)와도,
/// 새로 지은 이름이면 제 묶음에 든 소리의 이름과도 같지 않다(#374). 찾기와 같은 기준(대소문자·띄어쓰기·문장 부호 무시)으로 본다.
/// </summary>
public class SoundGroupStringsTests
{
    private static readonly IReadOnlyList<string> ClassNames = SoundTestData.Names;

    private static string SoundName(string language, string name) => SoundData.Names(language)[IndexOf(name)];

    private static int IndexOf(string name)
    {
        for (int i = 0; i < ClassNames.Count; i++)
        {
            if (ClassNames[i] == name) return i;
        }
        throw new ArgumentException(name);
    }

    [Fact]
    public void 새로_지은_묶음_이름은_모든_언어에_있고_다른_것은_두지_않는다()
    {
        // 한 언어라도 빠지면 그 언어의 탭에 영어 이름이 섞인다. 파일 안의 차례도 묶음 순서로 두어 언어끼리 맞춰 보기 쉽게 한다.
        var expected = SoundGroupInfo.All.Where(group => group.HasOwnTitle()).Select(group => group.Id()).ToList();
        Assert.Equal(15, expected.Count);
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var pairs = SoundData.ReadGroupTitlesFile(language);
            if (pairs is null)
            {
                problems.Add($"{language}: 파일이 없다");
                continue;
            }
            var ids = pairs.Select(pair => pair.Key).ToList();
            var blank = pairs.Where(pair => string.IsNullOrWhiteSpace(pair.Value) || pair.Value != pair.Value.Trim()).Select(pair => pair.Key).ToList();
            if (!ids.SequenceEqual(expected) || blank.Count > 0)
            {
                problems.Add($"{language}: id [{string.Join(", ", ids)}], 비었거나 앞뒤에 공백 [{string.Join(", ", blank)}]");
            }
        }
        Assert.True(problems.Count == 0, "묶음 이름은 모든 언어에 넣습니다:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void 묶음_이름은_다른_묶음의_이름이나_다른_묶음에_든_소리의_이름과_같지_않다()
    {
        // 같으면 화면에서도 찾기에서도 어느 묶음인지 가릴 수 없다. 소리를 다른 묶음으로 옮기면 그 소리 이름을 그대로 쓴 묶음 이름이
        // 여기에 걸린다. 여러 낱말을 이은 이름의 한 낱말이 다른 묶음의 소리 이름인지는 언어마다 낱말을 나누는 법이 달라 보지 않는다.
        var table = SoundTestData.Table;
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var titles = SoundGroupInfo.All.ToDictionary(group => group, group => SoundTestData.GroupTitle(language, group));
            problems.AddRange(titles
                .GroupBy(pair => SoundCatalog.Normalize(pair.Value), pair => pair.Key)
                .Where(same => same.Count() > 1)
                .Select(same => $"{language}: 이름이 같은 묶음 [{string.Join(", ", same)}]"));
            foreach (var (group, title) in titles.Where(pair => pair.Key.HasOwnTitle()))
            {
                foreach (string name in ClassNames)
                {
                    SoundGroup? owner = table.TryGetValue(name, out var row) ? row.Group : null;
                    if (owner != group && SoundCatalog.Normalize(SoundName(language, name)) == SoundCatalog.Normalize(title))
                    {
                        problems.Add($"{language}: {group} 의 이름 \"{title}\" 이 다른 묶음({owner})의 소리 {name} 의 이름이다");
                    }
                }
            }
        }
        Assert.True(problems.Count == 0, "묶음 이름이 겹친다:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void 묶음_이름은_구역_머리인_종류_이름과_같지_않다()
    {
        // 묶음은 기본 종류의 구역 머리 밑에 보인다. 같은 낱말이면 머리 밑에 같은 이름이 한 번 더 보여 무엇이 구역이고 무엇이 묶음인지
        // 가릴 수 없다. 말소리 묶음이 소리 이름 'Speech' 를 쓰던 때 영어 등 6개 언어가 그랬다(#374).
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var headings = SectionHeadings[language].ToDictionary(SoundCatalog.Normalize);
            foreach (SoundGroup group in SoundGroupInfo.All)
            {
                string title = SoundTestData.GroupTitle(language, group);
                if (headings.TryGetValue(SoundCatalog.Normalize(title), out string? heading))
                {
                    problems.Add($"{language}: {group} 의 이름 \"{title}\" 이 구역 머리 \"{heading}\" 와 같다");
                }
            }
        }
        Assert.True(problems.Count == 0, "묶음 이름이 구역 머리와 같다:\n" + string.Join("\n", problems));
        Assert.True(SoundLanguages.All.ToHashSet().SetEquals(SectionHeadings.Keys));
    }

    [Fact]
    public void 새로_지은_묶음_이름은_제_묶음에_든_소리의_이름과_같지_않다()
    {
        // 새로 지은 이름이 제 소리 하나의 이름과 같으면 그 소리 이름을 묶음 이름으로 쓴 것과 다르지 않다(#374). 다른 묶음의 소리와
        // 겹치는지는 위의 테스트가 본다.
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            foreach (SoundGroup group in SoundGroupInfo.All.Where(group => group.HasOwnTitle()))
            {
                string title = SoundData.GroupTitles(language)[group];
                problems.AddRange(SoundTestData.Members(group)
                    .Where(name => SoundCatalog.Normalize(SoundName(language, name)) == SoundCatalog.Normalize(title))
                    .Select(name => $"{language}: {group} 의 이름 \"{title}\" 이 제 소리 {name} 의 이름이다"));
            }
        }
        Assert.True(problems.Count == 0, "묶음 이름이 제 소리 이름과 같다:\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// 구역 머리(환경음·대화음·위협음). 모바일 values*/strings.xml 의 sound_type_ambient·speech·danger 를 옮겨 적었다.
    /// PC 화면의 구역 머리 문구를 바꾸면 여기도 함께 바꾼다.
    /// </summary>
    private static readonly Dictionary<string, string[]> SectionHeadings = new()
    {
        ["en"] = new[] { "Ambient", "Speech", "Danger" },
        ["ko"] = new[] { "환경음", "대화음", "위협음" },
        ["ja"] = new[] { "環境音", "話し声", "危険音" },
        ["zh-Hans"] = new[] { "环境音", "人声", "危险音" },
        ["zh-Hant"] = new[] { "環境音", "人聲", "危險音" },
        ["es"] = new[] { "Sonido ambiente", "Voz", "Peligro" },
        ["pt-BR"] = new[] { "Som ambiente", "Voz", "Som de perigo" },
        ["fr"] = new[] { "Ambiance", "Voix", "Danger" },
        ["de"] = new[] { "Umgebungsgeräusche", "Stimmen", "Gefahrengeräusche" },
        ["ru"] = new[] { "Фоновые звуки", "Речь", "Опасные звуки" },
        ["it"] = new[] { "Suoni ambientali", "Voci", "Suoni di pericolo" },
        ["vi"] = new[] { "Âm thanh môi trường", "Giọng nói", "Âm thanh nguy hiểm" },
        ["th"] = new[] { "เสียงแวดล้อม", "เสียงพูด", "เสียงอันตราย" },
        ["id"] = new[] { "Suara latar", "Percakapan", "Suara bahaya" },
        ["tr"] = new[] { "Ortam sesi", "Konuşma", "Tehlike sesi" },
        ["pl"] = new[] { "Dźwięki otoczenia", "Mowa", "Dźwięki zagrożenia" },
        ["hi"] = new[] { "माहौल की आवाज़ें", "बातचीत", "खतरे की आवाज़ें" },
        ["ar"] = new[] { "الأصوات المحيطة", "الكلام", "أصوات الخطر" },
    };
}
