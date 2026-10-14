// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests;

public class UiFontChainTests
{
    [Fact]
    public void 모든_언어가_Pretendard_를_먼저_쓴다()
    {
        foreach (var lang in new[] { "KOR", "English", "日本語", "中文", "Español", "Français", "Deutsch", "Русский", "ENG" })
        {
            Assert.StartsWith("./Fonts/#Pretendard, ", UiFontChain.FamilyListFor(lang));
        }
    }

    [Fact]
    public void 한자는_언어에_맞는_나라의_글꼴로_채운다()
    {
        var japanese = UiFontChain.FallbacksFor("日本語");
        var chinese = UiFontChain.FallbacksFor("中文");

        Assert.Equal("Yu Gothic UI", japanese[0]);
        Assert.DoesNotContain("Microsoft YaHei UI", japanese);
        Assert.Equal("Microsoft YaHei UI", chinese[0]);
        Assert.DoesNotContain("Yu Gothic UI", chinese);
    }

    [Fact]
    public void 오버레이_키로_불러도_같은_순서다()
    {
        Assert.Equal(UiFontChain.FallbacksFor("日本語"), UiFontChain.FallbacksFor("Japanese"));
        Assert.Equal(UiFontChain.FallbacksFor("中文"), UiFontChain.FallbacksFor("Chinese"));
    }
}
