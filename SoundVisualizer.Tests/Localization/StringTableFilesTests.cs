// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text.RegularExpressions;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests.Localization;

/// <summary>
/// 앱에 넣는 문자열 표 파일(Localization/Strings/*.json) 검사. 번역가가 en.json 으로 새 언어 파일을 만들어 넣으면
/// 그 파일도 같은 검사를 받는다. 키가 빠지거나 자리표시자가 어긋난 번역은 여기서 걸린다.
/// </summary>
public class StringTableFilesTests
{
    // 화면별로 점을 찍어 묶은 소문자 키(launcher.tab.home)
    private static readonly Regex KeyPattern = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$");
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^{}]*)?\}");

    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();
        foreach (var code in StringTables.AvailableCodes) data.Add(code);
        return data;
    }

    private static StringTable English => StringTables.Get(UiLanguages.DefaultCode)!;

    [Fact]
    public void 영어와_한국어_표가_들어_있다()
    {
        Assert.Contains("en", StringTables.AvailableCodes);
        Assert.Contains("ko", StringTables.AvailableCodes);
    }

    [Fact]
    public void 표_파일은_목록에_있는_언어의_코드로만_만든다()
    {
        Assert.All(StringTables.AvailableCodes, code =>
        {
            var language = UiLanguages.Find(code);
            Assert.NotNull(language);
            Assert.Equal(language.Code, code); // 파일 이름도 정해진 표기(zh-Hans, pt-BR)여야 찾을 수 있다
        });
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 표는_평평한_JSON_객체로_읽힌다(string code)
    {
        var table = StringTable.Parse(code, StringTables.ReadJson(code)!);
        Assert.NotEmpty(table.Entries);
        Assert.Equal(code, table.Code);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 모든_언어_표는_영어와_똑같은_키를_가진다(string code)
    {
        var keys = StringTables.Get(code)!.Entries.Keys.ToHashSet(StringComparer.Ordinal);
        var englishKeys = English.Entries.Keys.ToHashSet(StringComparer.Ordinal);

        var missing = englishKeys.Except(keys).Order(StringComparer.Ordinal).ToArray();
        var extra = keys.Except(englishKeys).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0 && extra.Length == 0,
            $"{code}: 빠진 키 [{string.Join(", ", missing)}], 영어에 없는 키 [{string.Join(", ", extra)}]");
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 자리표시자는_영어와_같다(string code)
    {
        var table = StringTables.Get(code)!;
        foreach (var (key, english) in English.Entries)
        {
            if (!table.TryGet(key, out var text)) continue; // 빠진 키는 위 테스트가 잡는다
            Assert.True(Placeholders(english).SequenceEqual(Placeholders(text)),
                $"{code}: '{key}' 의 자리표시자가 영어({string.Join(",", Placeholders(english))})와 다릅니다({string.Join(",", Placeholders(text))}).");
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 빈_값이나_앞뒤_공백이_없다(string code)
    {
        Assert.All(StringTables.Get(code)!.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{code}: '{entry.Key}' 가 비었습니다.");
            Assert.True(entry.Value == entry.Value.Trim(), $"{code}: '{entry.Key}' 의 앞뒤에 공백이 있습니다.");
        });
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 모든_값은_자리표시자를_채울_수_있는_꼴이다(string code)
    {
        // 화면은 일부 문자열을 string.Format 으로 채운다. 짝이 안 맞는 중괄호가 있으면 그 화면에서 예외가 난다.
        var args = Enumerable.Range(0, 10).Select(i => (object)i).ToArray();
        Assert.All(StringTables.Get(code)!.Entries, entry =>
        {
            var error = Record.Exception(() => string.Format(CultureInfo.InvariantCulture, entry.Value, args));
            Assert.True(error == null, $"{code}: '{entry.Key}' 를 채우지 못합니다({error?.Message}).");
        });
    }

    [Fact]
    public void 키는_화면별로_점을_찍은_소문자다()
    {
        Assert.All(English.Entries.Keys, key => Assert.Matches(KeyPattern, key));
    }

    [Fact]
    public void 어떤_키도_다른_키의_묶음_이름이_아니다()
    {
        // "a.b" 와 "a.b.c" 가 함께 있으면 키를 중첩 구조로 바꿔 쓰는 번역 도구에서 둘 중 하나가 사라진다.
        var keys = English.Entries.Keys.ToArray();
        Assert.All(keys, key => Assert.DoesNotContain(keys, other => other.StartsWith(key + ".", StringComparison.Ordinal)));
    }

    [Fact]
    public void 읽은_표는_고칠_수_없다()
    {
        var entries = Assert.IsAssignableFrom<IDictionary<string, string>>(English.Entries);
        Assert.Throws<NotSupportedException>(() => entries["launcher.tab.home"] = "x");
        Assert.Throws<NotSupportedException>(() => entries.Add("new.key", "x"));
    }

    private static int[] Placeholders(string text)
    {
        string unescaped = text.Replace("{{", "", StringComparison.Ordinal).Replace("}}", "", StringComparison.Ordinal);
        return Placeholder.Matches(unescaped).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
    }
}
