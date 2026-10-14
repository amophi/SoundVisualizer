// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests.Localization;

public class LocalizerTests
{
    private static readonly StringTable English = StringTable.Parse("en", """{ "a": "Apple", "b": "Banana", "size": "Size: {0}", "pair": "{0} / {1}" }""");
    private static readonly StringTable Korean = StringTable.Parse("ko", """{ "a": "사과", "size": "크기: {0" }""");

    private static Localizer WithTables(params StringTable[] tables) => new(code => tables.FirstOrDefault(t => t.Code == code));

    [Fact]
    public void 처음에는_영어로_시작한다()
    {
        var localizer = new Localizer();

        Assert.Equal("en", localizer.Code);
        Assert.Equal("Home", localizer.Get("launcher.tab.home"));
    }

    [Fact]
    public void 언어를_바꾸면_그_언어의_표에서_찾는다()
    {
        var localizer = new Localizer();

        localizer.SetLanguage("ko");

        Assert.Equal("ko", localizer.Code);
        Assert.Equal("홈", localizer.Get("launcher.tab.home"));
        Assert.Equal("파도", localizer.Get("settings.mode.wave"));
        Assert.Equal("표현 모드: [F3] 파도", localizer.Format("hud.visual_mode", "F3", localizer.Get("settings.mode.wave")));
    }

    [Fact]
    public void 그_언어_표에_없는_키는_영어로_보인다()
    {
        var localizer = WithTables(English, Korean);

        localizer.SetLanguage("ko");

        Assert.Equal("사과", localizer.Get("a"));
        Assert.Equal("Banana", localizer.Get("b"));
    }

    [Fact]
    public void 번역_파일이_없는_언어는_키마다_영어로_보인다()
    {
        var localizer = WithTables(English);

        localizer.SetLanguage("fr");

        Assert.Equal("fr", localizer.Code); // 화면 방향과 글꼴은 그 언어를 따른다
        Assert.Equal("Apple", localizer.Get("a"));
    }

    [Fact]
    public void 영어에도_없는_키는_키를_그대로_보여_준다()
    {
        var localizer = WithTables(English);

        Assert.Equal("no.such.key", localizer.Get("no.such.key"));
        Assert.Equal("no.such.key", localizer.Format("no.such.key", 1));
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("system")]
    [InlineData("")]
    public void 지원하지_않는_코드는_영어가_된다(string code)
    {
        var localizer = WithTables(English, Korean);
        localizer.SetLanguage("ko");

        localizer.SetLanguage(code);

        Assert.Equal("en", localizer.Code);
        Assert.Equal("Apple", localizer.Get("a"));
    }

    [Fact]
    public void 코드는_정해진_표기로_맞춘다()
    {
        var localizer = WithTables(English);

        localizer.SetLanguage("ZH-HANT");

        Assert.Equal("zh-Hant", localizer.Code);
    }

    [Fact]
    public void 숫자는_Windows_지역_설정과_상관없이_채운다()
    {
        var localizer = WithTables(English);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // 소수점이 쉼표인 지역
            Assert.Equal("1.5 / 2", localizer.Format("pair", 1.5, 2));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void 자리표시자가_깨진_번역은_영어_문장으로_채운다()
    {
        var localizer = WithTables(English, Korean);
        localizer.SetLanguage("ko");

        Assert.Equal("Size: 5", localizer.Format("size", 5));
    }

    [Fact]
    public void 깨진_표는_건너뛰고_영어로_보인다()
    {
        var localizer = new Localizer(code => code == "ko" ? throw new FormatException("broken") : English);

        localizer.SetLanguage("ko");

        Assert.Equal("ko", localizer.Code);
        Assert.Equal("Apple", localizer.Get("a"));
    }

    [Fact]
    public void 오른쪽에서_왼쪽으로_쓰는지는_지금_언어를_따른다()
    {
        var localizer = WithTables(English);

        localizer.SetLanguage("ar");
        Assert.True(localizer.IsRightToLeft);

        localizer.SetLanguage("ko");
        Assert.False(localizer.IsRightToLeft);
    }

    [Fact]
    public async Task 언어를_바꾸는_도중에도_어느_한_언어의_문자열만_읽힌다()
    {
        var localizer = WithTables(English, Korean);
        using var stop = new CancellationTokenSource();
        var unexpected = new List<string>();

        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                string text = localizer.Get("a");
                if (text != "Apple" && text != "사과")
                {
                    lock (unexpected) unexpected.Add(text);
                }
            }
        });

        for (int i = 0; i < 2000; i++) localizer.SetLanguage(i % 2 == 0 ? "ko" : "en");
        stop.Cancel();
        await reader;

        Assert.Empty(unexpected);
    }
}
