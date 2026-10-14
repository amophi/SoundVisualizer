// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests;

public class LegacyLanguageKeysTests
{
    [Theory]
    [InlineData("KOR", "KOR")]
    [InlineData("English", "English")]
    [InlineData("日本語", "Japanese")]
    [InlineData("中文", "Chinese")]
    [InlineData("Español", "Spanish")]
    [InlineData("Français", "French")]
    [InlineData("Deutsch", "German")]
    [InlineData("Русский", "Russian")]
    public void 저장된_언어_이름과_오버레이_키는_서로_바뀐다(string stored, string overlay)
    {
        Assert.Equal(overlay, LegacyLanguageKeys.ToOverlayKey(stored));
        Assert.Equal(stored, LegacyLanguageKeys.ToStoredKey(overlay));
    }

    [Fact]
    public void 첫_실행_기본값_ENG_와_빈_값은_영어다()
    {
        Assert.Equal("English", LegacyLanguageKeys.ToOverlayKey("ENG"));
        Assert.Equal("English", LegacyLanguageKeys.ToOverlayKey(null));
    }

    [Fact]
    public void 모르는_값은_그대로_넘긴다()
    {
        Assert.Equal("Klingon", LegacyLanguageKeys.ToOverlayKey("Klingon"));
        Assert.Equal("Klingon", LegacyLanguageKeys.ToStoredKey("Klingon"));
    }
}
