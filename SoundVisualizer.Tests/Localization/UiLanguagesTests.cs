// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests.Localization;

public class UiLanguagesTests
{
    [Fact]
    public void 화면_언어는_18개이고_코드가_겹치지_않는다()
    {
        Assert.Equal(18, UiLanguages.All.Count);
        Assert.Equal(18, UiLanguages.All.Select(l => l.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(UiLanguages.All, l => Assert.False(string.IsNullOrWhiteSpace(l.NativeName)));
        Assert.DoesNotContain(UiLanguages.All, l => l.Code == UiLanguages.SystemCode);
    }

    [Fact]
    public void 목록은_안드로이드_판과_같은_언어를_정해진_순서로_담는다()
    {
        string[] expected = { "en", "ko", "ja", "zh-Hans", "zh-Hant", "es", "pt-BR", "fr", "de", "ru", "it", "vi", "th", "id", "tr", "pl", "hi", "ar" };
        Assert.Equal(expected, UiLanguages.All.Select(l => l.Code));
        Assert.Equal("한국어", UiLanguages.Find("ko")!.NativeName);
        Assert.Equal("简体中文", UiLanguages.Find("zh-Hans")!.NativeName);
        Assert.Equal("繁體中文", UiLanguages.Find("zh-Hant")!.NativeName);
        Assert.Equal("Português (Brasil)", UiLanguages.Find("pt-BR")!.NativeName);
        Assert.Equal("العربية", UiLanguages.Find("ar")!.NativeName);
    }

    [Fact]
    public void 오른쪽에서_왼쪽으로_쓰는_언어는_아랍어_하나다()
    {
        var rtl = Assert.Single(UiLanguages.All, l => l.IsRightToLeft);
        Assert.Equal("ar", rtl.Code);
        Assert.True(UiLanguages.IsRightToLeft("ar"));
        Assert.False(UiLanguages.IsRightToLeft("ko"));
        Assert.False(UiLanguages.IsRightToLeft(UiLanguages.SystemCode));
        Assert.False(UiLanguages.IsRightToLeft(null));
    }

    [Theory]
    [InlineData("ko-KR", "ko")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("ja-JP", "ja")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh", "zh-Hans")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("zh-Hans-HK", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-MO", "zh-Hant")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("zh-Hant-TW", "zh-Hant")]
    [InlineData("zh-CHT", "zh-Hant")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("id-ID", "id")]
    [InlineData("in-ID", "id")]
    [InlineData("in", "id")]
    [InlineData("ar-SA", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("hi-IN", "hi")]
    [InlineData("th-TH", "th")]
    [InlineData("vi-VN", "vi")]
    [InlineData("tr-TR", "tr")]
    [InlineData("pl-PL", "pl")]
    [InlineData("it-IT", "it")]
    [InlineData("es-MX", "es")]
    [InlineData("fr-CA", "fr")]
    [InlineData("de-AT", "de")]
    [InlineData("ru-RU", "ru")]
    [InlineData("sw-KE", "en")]
    [InlineData("nb-NO", "en")]
    [InlineData("", "en")]
    public void 시스템_언어는_Windows_문화권_이름에서_고른다(string cultureName, string expected)
    {
        Assert.Equal(expected, UiLanguages.FromCultureName(cultureName));
    }

    [Theory]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("ko-KR", "ko")]
    [InlineData("id-ID", "id")]
    [InlineData("de-CH", "de")]
    [InlineData("fil-PH", "en")]
    public void 시스템_설정은_실제_문화권으로도_같은_언어가_된다(string culture, string expected)
    {
        var uiCulture = new CultureInfo(culture);
        Assert.Equal(expected, UiLanguages.Resolve(UiLanguages.SystemCode, uiCulture));
        Assert.Equal(expected, UiLanguages.Resolve(null, uiCulture));
    }

    [Fact]
    public void 언어를_고른_설정은_Windows_언어와_상관없다()
    {
        Assert.Equal("ja", UiLanguages.Resolve("ja", new CultureInfo("ko-KR")));
        Assert.Equal("ar", UiLanguages.Resolve("ar", CultureInfo.InvariantCulture));
        Assert.Equal("ko", UiLanguages.Resolve("KOR", new CultureInfo("en-US")));
    }

    [Theory]
    [InlineData("KOR", "ko")]
    [InlineData("English", "en")]
    [InlineData("ENG", "en")]
    [InlineData("日本語", "ja")]
    [InlineData("中文", "zh-Hans")]
    [InlineData("Español", "es")]
    [InlineData("Français", "fr")]
    [InlineData("Deutsch", "de")]
    [InlineData("Русский", "ru")]
    public void 예전_판의_언어_값은_코드로_옮긴다(string legacy, string code)
    {
        Assert.Equal(code, UiLanguages.NormalizeSetting(legacy));
    }

    [Fact]
    public void 분해형으로_적힌_예전_값도_옮긴다()
    {
        Assert.Equal("es", UiLanguages.NormalizeSetting("Español".Normalize(NormalizationForm.FormD)));
        Assert.Equal("fr", UiLanguages.NormalizeSetting("Français".Normalize(NormalizationForm.FormD)));
    }

    [Theory]
    [InlineData("ko", "ko")]
    [InlineData("zh-Hant", "zh-Hant")]
    [InlineData("ZH-HANS", "zh-Hans")]
    [InlineData(" pt-br ", "pt-BR")]
    [InlineData("system", "system")]
    [InlineData("System", "system")]
    public void 코드로_저장된_값은_정해진_표기로_읽는다(string stored, string expected)
    {
        Assert.Equal(expected, UiLanguages.NormalizeSetting(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Klingon")]
    [InlineData("sw")]
    [InlineData("Japanese")]
    public void 비었거나_모르는_값은_시스템_언어를_따른다(string? stored)
    {
        Assert.Equal(UiLanguages.SystemCode, UiLanguages.NormalizeSetting(stored));
    }

    [Fact]
    public void 저장값을_다시_맞춰도_그대로다()
    {
        foreach (var value in UiLanguages.All.Select(l => l.Code).Append(UiLanguages.SystemCode))
        {
            Assert.Equal(value, UiLanguages.NormalizeSetting(value));
        }
    }
}
