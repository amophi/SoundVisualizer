// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text;
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;
using SoundVisualizer.Tests.Ai;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 분류 탭 테스트들이 함께 읽는 실제 데이터(모바일 SoundGroupFixtures). 앱과 같은 클래스 목록(Models/yamnet_class_map.csv),
/// Core 에 넣어 둔 묶음 표와 이름을 읽는다.
/// </summary>
internal static class SoundTestData
{
    /// <summary>모델의 소리 이름 521개. 번호 순서다.</summary>
    public static IReadOnlyList<string> Names => AiTestData.ClassMap;

    /// <summary>묶음 표. 앱이 읽는 것과 같다.</summary>
    public static IReadOnlyDictionary<string, SoundGroupRow> Table => SoundData.GroupTable;

    /// <summary>묶음 표 파일의 글자 그대로(머리글 포함).</summary>
    public static string TableText()
    {
        using var reader = new StreamReader(SoundData.OpenGroupTable(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 기본 종류를 <paramref name="defaultOf"/> 로 정한 목록. 앱(<see cref="SoundCatalog.Load"/>)과 같은 표를 쓰고, 기본 종류만 바꿔 끼운다.
    /// 묶음 표만 보는 테스트는 매핑이 바뀌어도 흔들리지 않게 기본 종류를 정해 두고 쓴다.
    /// </summary>
    public static IReadOnlyList<SoundEntry> Entries(Func<string, SoundType>? defaultOf = null) =>
        SoundCatalog.FromNames(Names, Table, defaultOf);

    /// <summary>묶음 표에서 <paramref name="group"/> 의 소리 이름. 표의 순서다.</summary>
    public static List<string> Members(SoundGroup group) =>
        Table.Where(row => row.Value.Group == group).Select(row => row.Key).ToList();

    /// <summary>[언어]의 소리 이름(파일 그대로, 채우지 않은 것).</summary>
    public static IReadOnlyList<string> NamesFile(string language) =>
        SoundData.ReadNamesFile(language) ?? throw new InvalidOperationException($"{language} 소리 이름 파일이 없다");

    /// <summary>[언어]의 새로 지은 묶음 이름(파일 그대로, 묶음 id → 이름).</summary>
    public static Dictionary<string, string> GroupTitlesFile(string language) =>
        (SoundData.ReadGroupTitlesFile(language) ?? throw new InvalidOperationException($"{language} 묶음 이름 파일이 없다"))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    /// <summary>[언어]에서 묶음의 이름. 소리 이름이 곧 묶음 이름이면 그 소리의 이름, 아니면 새로 지은 이름이다.</summary>
    public static string GroupTitle(string language, SoundGroup group) =>
        group.TitleSound() is { } sound
            ? SoundData.Names(language)[AiTestData.ClassMap.IndexOf(sound)]
            : SoundData.GroupTitles(language)[group];

    /// <summary>글을 UTF-8 스트림으로(모바일 테스트의 byteInputStream).</summary>
    public static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));

    /// <summary>이름 목록. 실패 메시지가 읽기 쉽게 비교할 때 쓴다.</summary>
    public static List<string> NamesOf(IEnumerable<SoundEntry> entries) => entries.Select(entry => entry.Name).ToList();
}
