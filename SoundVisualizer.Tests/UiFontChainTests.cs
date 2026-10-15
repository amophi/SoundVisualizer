// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests;

public class UiFontChainTests
{
    [Fact]
    public void 모든_언어가_Pretendard_를_먼저_쓴다()
    {
        foreach (var code in UiLanguages.All.Select(l => (string?)l.Code).Append("xx").Append(null))
        {
            Assert.StartsWith("./Fonts/#Pretendard, ", UiFontChain.FamilyListFor(code));
        }
    }

    [Theory]
    [InlineData("ko", "Malgun Gothic")]
    [InlineData("ja", "Yu Gothic UI")]
    [InlineData("zh-Hans", "Microsoft YaHei UI")]
    [InlineData("zh-Hant", "Microsoft JhengHei UI")]
    [InlineData("th", "Leelawadee UI")]
    [InlineData("hi", "Nirmala UI")]
    [InlineData("ar", "Segoe UI")]
    [InlineData("en", "Segoe UI")]
    [InlineData("ru", "Segoe UI")]
    [InlineData("vi", "Segoe UI")]
    public void Pretendard_에_없는_글자는_그_문자에_맞는_Windows_글꼴로_채운다(string code, string firstFallback)
    {
        var fallbacks = UiFontChain.FallbacksFor(code);

        Assert.Equal(firstFallback, fallbacks[0]);
        Assert.Contains("Segoe UI", fallbacks);
    }

    [Fact]
    public void 한자는_언어에_맞는_지역의_글꼴로_채운다()
    {
        var japanese = UiFontChain.FallbacksFor("ja");
        var simplified = UiFontChain.FallbacksFor("zh-Hans");
        var traditional = UiFontChain.FallbacksFor("zh-Hant");

        Assert.DoesNotContain("Microsoft YaHei UI", japanese);
        Assert.DoesNotContain("Yu Gothic UI", simplified);
        Assert.DoesNotContain("Microsoft JhengHei UI", simplified);
        Assert.DoesNotContain("Microsoft YaHei UI", traditional);
        Assert.Contains("Meiryo UI", japanese);
    }

    [Fact]
    public void 코드의_대소문자와_모르는_코드도_받는다()
    {
        Assert.Equal(UiFontChain.FallbacksFor("zh-Hant"), UiFontChain.FallbacksFor("ZH-HANT"));
        Assert.Equal(UiFontChain.FallbacksFor("en"), UiFontChain.FallbacksFor("xx"));
        Assert.Equal(UiFontChain.FallbacksFor("en"), UiFontChain.FallbacksFor(null));
    }
}
