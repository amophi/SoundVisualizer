// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 분류 탭의 묶음 표(Sounds/Data/sound_groups.tsv, #349). 모바일 SoundGroupTableTest 를 옮겼다.
///
/// 표는 사람이 검토한 데이터라 만드는 스크립트가 없다(모바일 표를 글자 그대로 옮겼다). 지켜야 할 규칙은 여기서 막는다.
/// 묶음마다의 기본 종류는 일부러 보지 않는다. 그것은 AI 의 매핑이 정하고, 매핑이 바뀌어도 화면이 그때그때 나눈다(SoundGroupsTests).
/// </summary>
public class SoundGroupTableTests
{
    private static readonly string Text = SoundTestData.TableText();

    /// <summary>파일의 줄. 윈도에서는 git 이 CRLF 로 꺼내므로 줄 끝을 가리지 않고 나눈다.</summary>
    private static readonly string[] Lines = Text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToArray();

    private static readonly string[] Header = Lines.TakeWhile(line => line.StartsWith('#')).ToArray();
    private static readonly string[] Rows = Lines.Skip(Header.Length).ToArray();
    private static readonly IReadOnlyDictionary<string, SoundGroupRow> Table = SoundTestData.Table;

    private static SoundGroup Group(string name) => Table[name].Group;

    private static List<string> Members(SoundGroup group) => SoundTestData.Members(group);

    private static string IdOf(string row) => row[..row.IndexOf('\t')];

    private static string NameOf(string row) => row[(row.IndexOf('\t') + 1)..];

    [Fact]
    public void 머리글에_라이선스와_출처와_바꾼_점을_적는다()
    {
        // 온톨로지를 읽고 만든 표라 CC BY-SA 4.0 의 각색물로 본다. 출처, 라이선스와 링크, 바꾼 점을 파일 안에 적는다.
        Assert.Equal("# SPDX-License-Identifier: CC-BY-SA-4.0", Lines[0]);
        string joined = string.Join("\n", Header);
        foreach (string expected in new[]
                 {
                     "AudioSet Ontology",
                     "Google Inc.",
                     "https://github.com/audioset/ontology",
                     "d417d32",
                     "https://creativecommons.org/licenses/by-sa/4.0/",
                     "the 521 YAMNet sound names were regrouped into everyday groups by SoundVisualizer contributors",
                 })
        {
            Assert.Contains(expected, joined);
        }
        Assert.DoesNotContain(Rows, row => row.StartsWith('#')); // # 줄은 맨 위에만 둔다.
    }

    [Fact]
    public void 모델의_소리_521가지가_한_번씩_들어_있다()
    {
        Assert.Equal(521, Rows.Length);
        Assert.All(Rows, row => Assert.Single(row, c => c == '\t')); // 줄마다 묶음과 소리 이름 사이에 탭이 하나다.
        var names = Rows.Select(NameOf).ToList();
        var twice = names.GroupBy(name => name).Where(same => same.Count() > 1).Select(same => same.Key).ToList();
        Assert.True(twice.Count == 0, $"두 번 나온 소리(한 소리는 한 묶음에만 둔다): {string.Join(", ", twice)}");
        // 모델의 클래스 목록과 같아야 한다.
        Assert.True(SoundTestData.Names.ToHashSet().SetEquals(names), "표의 소리가 모델의 클래스 목록과 다르다");
    }

    [Fact]
    public void 묶음_이름은_모두_SoundGroup_항목이고_항목은_모두_쓰인다()
    {
        var ids = Rows.Select(IdOf).ToHashSet();
        Assert.True(SoundGroupInfo.All.Select(group => group.Id()).ToHashSet().SetEquals(ids));
        Assert.All(SoundGroupInfo.All, group => Assert.Equal(group, SoundGroupInfo.FromId(group.Id())));
        Assert.Null(SoundGroupInfo.FromId("Sirens")); // 대소문자까지 같아야 한다.
        Assert.Null(SoundGroupInfo.FromId("nope"));
        Assert.Null(SoundGroupInfo.FromId(null));
        Assert.Equal(37, SoundGroupInfo.All.Count);
    }

    [Fact]
    public void 한_묶음의_줄은_붙어_있고_SoundGroup_순서를_따른다()
    {
        var runs = new List<string>();
        foreach (string id in Rows.Select(IdOf))
        {
            if (runs.Count == 0 || runs[^1] != id) runs.Add(id);
        }
        Assert.True(runs.Distinct().Count() == runs.Count, $"한 묶음의 줄이 흩어져 있다: {string.Join(", ", runs)}");
        Assert.Equal(SoundGroupInfo.All.Select(group => group.Id()), runs);
    }

    [Fact]
    public void 읽은_자리는_표의_줄_순서다()
    {
        Assert.Equal(Rows.Select(NameOf), Table.Keys);
        Assert.Equal(Enumerable.Range(0, Rows.Length), Table.Values.Select(row => row.Order));
        Assert.Equal(Rows.Select(IdOf), Table.Values.Select(row => row.Group.Id()));
    }

    [Fact]
    public void 줄_끝이_CRLF_여도_같게_읽고_머리글과_빈_줄은_건너뛴다()
    {
        string lf = Text.Replace("\r\n", "\n");
        AssertSameTable(Table, SoundGroups.ParseTable(SoundTestData.Stream(lf)));
        AssertSameTable(Table, SoundGroups.ParseTable(SoundTestData.Stream(lf.Replace("\n", "\r\n"))));

        const string sample = "# SPDX-License-Identifier: CC-BY-SA-4.0\r\n#\r\nsirens\tSiren\r\n\r\n# 가운데 줄\r\nalarms\tAlarm\r\n";
        AssertSameTable(
            new Dictionary<string, SoundGroupRow>
            {
                ["Siren"] = new(SoundGroup.Sirens, 0),
                ["Alarm"] = new(SoundGroup.Alarms, 1),
            },
            SoundGroups.ParseTable(SoundTestData.Stream(sample)));
    }

    [Fact]
    public void 모르는_묶음과_망가진_줄은_건너뛰고_먼저_나온_줄을_쓴다()
    {
        // 그런 소리는 표에 없는 것이 되어 제 이름의 카드로 보인다. 탭이 깨지지 않는 쪽이다.
        const string sample = "sirens\tSiren\nnope\tAlarm\nno tab here\nalarms\tSiren\nalarms\t\nalarms\tFire alarm\n";
        AssertSameTable(
            new Dictionary<string, SoundGroupRow>
            {
                ["Siren"] = new(SoundGroup.Sirens, 0),
                ["Fire alarm"] = new(SoundGroup.Alarms, 1),
            },
            SoundGroups.ParseTable(SoundTestData.Stream(sample)));
    }

    [Fact]
    public void 받은_스트림은_닫지_않는다()
    {
        using var stream = SoundTestData.Stream("sirens\tSiren\n");
        SoundGroups.ParseTable(stream);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void 묶음_이름은_새로_지은_문구이거나_그_묶음의_소리_이름이다()
    {
        var english = SoundTestData.GroupTitlesFile(SoundLanguages.English);
        foreach (SoundGroup group in SoundGroupInfo.All)
        {
            string? sound = group.TitleSound();
            Assert.Equal(sound is null, group.HasOwnTitle());
            if (sound is not null)
            {
                // 이름인 소리가 그 묶음에 있어야 한다.
                Assert.True(Table.TryGetValue(sound, out var row) && row.Group == group, $"{group} 의 이름인 소리 {sound} 가 그 묶음에 없다");
            }
            else
            {
                Assert.True(english.ContainsKey(group.Id()), $"{group} 의 이름이 GroupTitles/en.json 에 없다");
            }
        }
        // 새로 지은 묶음 이름은 15개다. 나머지는 이미 번역된 소리 이름을 쓴다.
        Assert.Equal(
            new[]
            {
                SoundGroup.Horns, SoundGroup.VehicleWarnings, SoundGroup.Breaking, SoundGroup.Talking, SoundGroup.Distress,
                SoundGroup.Signals, SoundGroup.Voices, SoundGroup.Water, SoundGroup.Kitchen, SoundGroup.Appliances,
                SoundGroup.Tools, SoundGroup.Nature, SoundGroup.Body, SoundGroup.Objects, SoundGroup.Background,
            },
            SoundGroupInfo.All.Where(group => group.HasOwnTitle()));
    }

    [Fact]
    public void 사이렌_5개와_일반_경보음_3개는_따로_한_묶음씩이다()
    {
        Assert.Equal(
            new[] { "Siren", "Ambulance (siren)", "Fire engine, fire truck (siren)", "Police car (siren)", "Civil defense siren" },
            Members(SoundGroup.Sirens));
        Assert.Equal(new[] { "Alarm", "Smoke detector, smoke alarm", "Fire alarm" }, Members(SoundGroup.Alarms));
    }

    [Fact]
    public void 자동차_경적과_트럭_경적과_자동차_경보음은_한_묶음이다()
    {
        Assert.Equal(new[] { "Vehicle horn, car horn, honking", "Air horn, truck horn", "Car alarm" }, Members(SoundGroup.Horns));
    }

    [Fact]
    public void 초인종은_노크와_한_묶음이고_사이렌이나_전화와_섞이지_않는다()
    {
        Assert.Equal(new[] { "Doorbell", "Ding-dong", "Knock", "Tap" }, Members(SoundGroup.Doorbell));
        Assert.NotEqual(Group("Doorbell"), Group("Siren"));
        Assert.NotEqual(Group("Doorbell"), Group("Telephone"));
    }

    [Fact]
    public void 함께_들리는_비슷한_소리는_같은_묶음이다()
    {
        Assert.Equal(Group("Singing"), Group("Choir"));
        Assert.Equal(Group("Baby cry, infant cry"), Group("Whimper"));
        Assert.Equal(Group("Music"), Group("Musical instrument"));
        Assert.Equal(Group("Music"), Group("Guitar"));
        Assert.Equal(Group("Bell"), Group("Cowbell"));
    }

    [Fact]
    public void 기본_종류가_바뀐_소리에_맞춰_묶음을_옮겼다()
    {
        // #344 로 위협음이 된 차량 경고음은 경적 바로 뒤에, 쾅 닫는 소리는 유리 깨짐 바로 뒤에 저 혼자 묶음으로 둔다.
        // 어느 구역에 보이는지는 매핑이 정하므로 여기서는 표와 순서만 본다.
        Assert.Equal(
            new[]
            {
                "Reversing beeps", "Bicycle bell", "Tire squeal", "Skidding", "Emergency vehicle", "Train horn",
                "Train whistle", "Foghorn",
            },
            Members(SoundGroup.VehicleWarnings));
        Assert.Equal((int)SoundGroup.Horns + 1, (int)SoundGroup.VehicleWarnings);
        Assert.Equal(new[] { "Slam" }, Members(SoundGroup.Slam));
        Assert.Equal("Slam", SoundGroup.Slam.TitleSound()); // 전기톱처럼 제 이름이 묶음 이름이다.
        Assert.Equal((int)SoundGroup.Breaking + 1, (int)SoundGroup.Slam);
        Assert.DoesNotContain("Slam", Members(SoundGroup.Door));
        // 환경음으로 남은 짧은 경적은 탈것 끝으로, 환경음이 된 싱잉볼은 음악 끝으로 옮겼다.
        Assert.Equal("Toot", Members(SoundGroup.Vehicle)[^1]);
        Assert.Equal("Singing bowl", Members(SoundGroup.Music)[^1]);
        Assert.DoesNotContain("Singing bowl", Members(SoundGroup.Singing));
    }

    [Fact]
    public void 대화음이_된_사람_발성은_대화음_묶음으로_옮겼다()
    {
        // #369 로 웃음·고함·울음·환호 11개가 대화음이 되어 환경음 묶음(목소리, 아기, 몸)에서 옮겼다(#370). 이미 있던 소리는
        // 순서를 그대로 두고 뒤에 붙여 미리보기가 바뀌지 않는다. 어느 구역에 보이는지는 매핑이 정하므로 표와 순서만 본다.
        Assert.Equal(
            new[] { "Laughter", "Baby laughter", "Giggle", "Belly laugh", "Chuckle, chortle", "Snicker" },
            Members(SoundGroup.Laughter));
        Assert.Equal(
            new[] { "Shout", "Screaming", "Crying, sobbing", "Children shouting", "Yell", "Bellow", "Whoop", "Wail, moan" },
            Members(SoundGroup.Distress));
        Assert.Equal(
            new[] { "Crowd", "Chatter", "Hubbub, speech noise, speech babble", "Children playing", "Cheering" },
            Members(SoundGroup.Crowd));
        // 아기 울음 묶음은 그대로 두고 자리만 외침·비명·울음 바로 뒤로 옮겼다. 개의 낑낑거림은 개 묶음에 남는다.
        Assert.Equal(new[] { "Baby cry, infant cry", "Whimper" }, Members(SoundGroup.Baby));
        Assert.Equal((int)SoundGroup.Distress + 1, (int)SoundGroup.Baby);
        Assert.Equal(SoundGroup.Dog, Group("Whimper (dog)"));
        // 목소리 묶음에는 박수·콧노래·휘파람 같은 소리만 남고(이름도 그에 맞춤), 몸 소리 묶음에서는 신음이 빠졌다.
        Assert.Equal(
            new[] { "Applause", "Clapping", "Humming", "Whistling", "Yodeling", "Chant", "Mantra", "Finger snapping", "Hands" },
            Members(SoundGroup.Voices));
        Assert.DoesNotContain("Wail, moan", Members(SoundGroup.Body));
    }

    [Fact]
    public void 부모가_여럿인_소리_36개는_정해_둔_묶음에_있다()
    {
        var moved = MultiParent.Where(pair => !Table.TryGetValue(pair.Key, out var row) || row.Group != pair.Value)
            .Select(pair => $"{pair.Key} → {pair.Value}")
            .ToList();
        Assert.True(moved.Count == 0, $"규칙 D 로 정한 자리에서 옮겼다. 일부러 옮긴 것이면 이 목록도 고친다: {string.Join(", ", moved)}");
        Assert.Equal(36, MultiParent.Count);
    }

    private static void AssertSameTable(IReadOnlyDictionary<string, SoundGroupRow> expected, IReadOnlyDictionary<string, SoundGroupRow> actual)
    {
        // 순서까지 같아야 한다(키 순서가 표의 줄 순서다).
        Assert.Equal(expected.Select(pair => (pair.Key, pair.Value)), actual.Select(pair => (pair.Key, pair.Value)));
    }

    /// <summary>
    /// AudioSet 온톨로지(ontology.json, 커밋 d417d32, SHA-256 9c685f44…d15d)에서 부모가 둘 이상인 소리 36개와 그 묶음.
    ///
    /// 이런 소리는 어느 부모를 따를지 정해야 했다(규칙 D). 규칙대로 정한 것과 뜻으로 정한 것(Knock·Tap 은 초인종, Bicycle bell 은
    /// 차량 경고음, Air horn 은 경적, Rattle·Hiss 는 물건 소리, Cowbell 은 종, Wind noise (microphone) 는 배경 소리)이 있다.
    /// #344 로 Emergency vehicle 등이 위협음이 된 뒤로는 기계적인 규칙이 사이렌 셋과 자동차 경적·경보음을 차량 경고음 쪽으로
    /// 보내지만, 묶음은 그대로 둔다. 온톨로지 파일은 저장소에 두지 않아 목록을 여기에 적는다(모바일 테스트와 같은 목록).
    /// 하나를 옮기면 이 목록도 일부러 고쳐야 한다.
    /// </summary>
    private static readonly Dictionary<string, SoundGroup> MultiParent = new()
    {
        ["Children shouting"] = SoundGroup.Distress,
        ["Choir"] = SoundGroup.Singing,
        ["Chant"] = SoundGroup.Voices,
        ["Clapping"] = SoundGroup.Voices,
        ["Hubbub, speech noise, speech babble"] = SoundGroup.Crowd,
        ["Howl"] = SoundGroup.Dog,
        ["Growling"] = SoundGroup.Dog,
        ["Hiss"] = SoundGroup.Objects,
        ["Clip-clop"] = SoundGroup.Animal,
        ["Cowbell"] = SoundGroup.Bell,
        ["Bleat"] = SoundGroup.Animal,
        ["Chirp, tweet"] = SoundGroup.Animal,
        ["Buzz"] = SoundGroup.Animal,
        ["Rattle"] = SoundGroup.Objects,
        ["Bell"] = SoundGroup.Bell,
        ["Bicycle bell"] = SoundGroup.VehicleWarnings,
        ["Beatboxing"] = SoundGroup.Music,
        ["Wind noise (microphone)"] = SoundGroup.Background,
        ["Crackle"] = SoundGroup.Fire,
        ["Vehicle horn, car horn, honking"] = SoundGroup.Horns,
        ["Car alarm"] = SoundGroup.Horns,
        ["Air horn, truck horn"] = SoundGroup.Horns,
        ["Police car (siren)"] = SoundGroup.Sirens,
        ["Ambulance (siren)"] = SoundGroup.Sirens,
        ["Fire engine, fire truck (siren)"] = SoundGroup.Sirens,
        ["Jet engine"] = SoundGroup.Vehicle,
        ["Dental drill, dentist's drill"] = SoundGroup.Tools,
        ["Doorbell"] = SoundGroup.Doorbell,
        ["Knock"] = SoundGroup.Doorbell,
        ["Tap"] = SoundGroup.Doorbell,
        ["Squeak"] = SoundGroup.Door,
        ["Tick"] = SoundGroup.Appliances,
        ["Crack"] = SoundGroup.Objects,
        ["Squish"] = SoundGroup.Water,
        ["Whoosh, swoosh, swish"] = SoundGroup.Objects,
        ["Thump, thud"] = SoundGroup.Objects,
    };
}
