// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests.Localization;

public class StringTableTests
{
    [Fact]
    public void 키로_문자열을_찾는다()
    {
        var table = StringTable.Parse("ko", """{ "launcher.tab.home": "홈", "multi.line": "첫 줄\n둘째 줄" }""");

        Assert.True(table.TryGet("launcher.tab.home", out var home));
        Assert.Equal("홈", home);
        Assert.True(table.TryGet("multi.line", out var multi));
        Assert.Equal("첫 줄\n둘째 줄", multi);
        Assert.False(table.TryGet("missing.key", out _));
    }

    [Fact]
    public void 키는_대소문자까지_같아야_찾는다()
    {
        var table = StringTable.Parse("en", """{ "a.b": "x" }""");

        Assert.False(table.TryGet("A.B", out _));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("""{ "a": 1 }""")]
    [InlineData("""{ "a": null }""")]
    [InlineData("""{ "a": { "b": "c" } }""")]
    [InlineData("""{ "a": "x", "a": "y" }""")]
    public void 형식이_맞지_않으면_읽지_않는다(string json)
    {
        Assert.Throws<FormatException>(() => StringTable.Parse("xx", json));
    }

    [Fact]
    public void 빈_표에는_아무_키도_없다()
    {
        var table = StringTable.Empty("fr");

        Assert.Equal("fr", table.Code);
        Assert.Empty(table.Entries);
        Assert.False(table.TryGet("launcher.tab.home", out _));
    }
}
