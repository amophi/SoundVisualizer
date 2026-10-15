// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.RegularExpressions;
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 분류 탭의 소리 이름(Sounds/Data/SoundNames/{언어}.json)을 검사한다. 모바일 SoundNamesResourceTest 를 옮기고, JSON 으로 옮긴 데이터를
/// 몇 가지 더 본다.
///
/// - 영어는 모델의 클래스 목록(yamnet_class_map.csv)과 번호·이름이 같아야 한다. 탭은 이 번호로 번역된 이름과 모델 이름을 짝짓고,
///   사용자가 고른 종류는 모델 이름으로 저장한다. 하나라도 밀리면 다른 소리의 이름을 보고 종류를 바꾸게 된다.
/// - 번역은 언어마다 전부 있어야 한다. 일부만 있으면 목록에 두 언어가 섞여 보인다.
/// - 한 언어 안에서 두 소리가 같은 이름이면 어느 줄이 어느 소리인지 알 수 없다.
/// </summary>
public class SoundNamesDataTests
{
    [Fact]
    public void 영어_이름은_모델의_클래스_목록과_번호와_이름이_같다()
    {
        Assert.Equal(YamnetCoarseClassifier.NumClasses, SoundTestData.Names.Count);
        Assert.Equal(SoundTestData.Names, SoundTestData.NamesFile(SoundLanguages.English));
    }

    [Fact]
    public void 언어마다_이름_521개를_번호_순서의_배열로_둔다()
    {
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var names = SoundTestData.NamesFile(language);
            if (names.Count != YamnetCoarseClassifier.NumClasses) problems.Add($"{language}: {names.Count}개");
            var empty = Enumerable.Range(0, names.Count).Where(i => string.IsNullOrWhiteSpace(names[i])).ToList();
            if (empty.Count > 0) problems.Add($"{language}: 빈 이름 {string.Join(", ", empty)}");
        }
        Assert.True(problems.Count == 0, "번역은 521개를 모두 넣습니다:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void 한국어_이름이_있다()
    {
        Assert.Contains("ko", SoundData.EmbeddedLanguages().Names);
    }

    [Fact]
    public void 한_언어_안에서_두_소리가_같은_이름을_쓰지_않는다()
    {
        // 대소문자와 공백만 다른 이름도 같은 이름으로 본다.
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var names = SoundTestData.NamesFile(language);
            problems.AddRange(Enumerable.Range(0, names.Count)
                .GroupBy(i => Regex.Replace(names[i].ToLowerInvariant(), @"\s+", " ").Trim())
                .Where(same => same.Count() > 1)
                .Select(same => $"{language}: \"{same.Key}\" ← {string.Join(", ", same)}"));
        }
        Assert.True(problems.Count == 0, "같은 이름은 어느 줄이 어느 소리인지 알 수 없습니다:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void 이름은_비어_있지_않고_앞뒤에_공백이_없다()
    {
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var names = SoundTestData.NamesFile(language);
            problems.AddRange(Enumerable.Range(0, names.Count)
                .Where(i => string.IsNullOrWhiteSpace(names[i]) || names[i] != names[i].Trim())
                .Select(i => $"{language}: {i}"));
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void 데이터가_있는_언어는_모두_지원_언어이고_지원_언어는_모두_데이터가_있다()
    {
        var (names, titles) = SoundData.EmbeddedLanguages();
        Assert.True(SoundLanguages.All.ToHashSet().SetEquals(names), "SoundNames: " + string.Join(", ", names));
        Assert.True(SoundLanguages.All.ToHashSet().SetEquals(titles), "GroupTitles: " + string.Join(", ", titles));
        Assert.Equal(18, SoundLanguages.All.Count);
        Assert.Equal(SoundLanguages.English, SoundLanguages.All[0]);
    }

    [Fact]
    public void 한국어와_영어_이름을_몇_개_맞춰_본다()
    {
        var en = SoundData.Names("en");
        var ko = SoundData.Names("ko");
        Assert.Equal("Speech", en[0]);
        Assert.Equal("말소리", ko[0]);
        Assert.Equal("Siren", en[390]);
        Assert.Equal("사이렌", ko[390]);
        Assert.Equal("Alarm", en[382]);
        Assert.Equal("경보음", ko[382]);
        Assert.Equal("Gunshot, gunfire", en[421]);
        Assert.Equal("총소리", ko[421]);
        Assert.Equal("Knock", en[SoundTestData.Names.ToList().IndexOf("Knock")]);
        Assert.Equal("노크 소리", ko[SoundTestData.Names.ToList().IndexOf("Knock")]);
        Assert.Equal("Rhythm and blues", en[221]);
        Assert.Equal("R&B", ko[221]); // &amp; 를 풀었다.
        Assert.Equal("Dental drill, dentist's drill", en[339]); // \' 를 풀었다.
        Assert.Equal("Parole d'enfant", SoundData.Names("fr")[1]);
        Assert.Equal("Nhạc R&B", SoundData.Names("vi")[221]);
        Assert.Equal("Signaux d'alerte des véhicules", SoundData.GroupTitles("fr")[SoundGroup.VehicleWarnings]);
        Assert.Equal("Clacson e allarmi dell'auto", SoundData.GroupTitles("it")[SoundGroup.Horns]);
        Assert.Equal("부엌·요리", SoundData.GroupTitles("ko")[SoundGroup.Kitchen]);
        Assert.Equal("Kitchen and cooking", SoundData.GroupTitles("en")[SoundGroup.Kitchen]);
    }

    [Fact]
    public void 안드로이드_이스케이프는_남아_있지_않다()
    {
        // 모바일 문구 파일은 \' 와 &amp; 를 쓴다. 옮긴 JSON 에는 푼 글자만 있어야 한다.
        var problems = new List<string>();
        foreach (string language in SoundLanguages.All)
        {
            var texts = SoundTestData.NamesFile(language).Concat(SoundTestData.GroupTitlesFile(language).Values);
            problems.AddRange(texts
                .Where(text => text.Contains('\\') || text.Contains("&amp;", StringComparison.Ordinal) || text.Contains('"') || text.Contains('\n'))
                .Select(text => $"{language}: {text}"));
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void 없는_언어와_빠진_이름은_영어로_채운다()
    {
        Assert.Equal(SoundData.Names("en"), SoundData.Names("xx"));
        Assert.Equal(SoundData.Names("en"), SoundData.Names(null));
        Assert.Same(SoundData.Names("ko"), SoundData.Names("KO")); // 대소문자는 가리지 않고, 언어마다 한 번만 읽는다.
        Assert.Equal(SoundData.GroupTitles("en"), SoundData.GroupTitles("xx"));
        Assert.Equal(15, SoundData.GroupTitles("th").Count);
        Assert.Equal(SoundLanguages.English, SoundLanguages.Resolve("zh"));
        Assert.Equal("zh-Hant", SoundLanguages.Resolve("ZH-HANT"));
    }
}
