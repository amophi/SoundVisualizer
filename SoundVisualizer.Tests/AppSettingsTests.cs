// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;

namespace SoundVisualizer.Tests;

/// <summary>AppSettings 는 정적이라 이 묶음의 테스트는 한 번에 하나씩 돈다.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class AppSettingsCollection
{
    public const string Name = "AppSettings";
}

[Collection(AppSettingsCollection.Name)]
public sealed class AppSettingsTests : IDisposable
{
    private readonly string _originalPath = AppSettings.SettingsFilePath;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sv-settings-{Guid.NewGuid():N}.json");

    public AppSettingsTests()
    {
        AppSettings.SettingsFilePath = _path;
        AppSettings.ResetToDefaults();
        AppSettings.Language = "KOR";
    }

    public void Dispose()
    {
        AppSettings.Flush();
        AppSettings.ResetToDefaults();
        AppSettings.SettingsFilePath = _originalPath;
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void 전체_초기화는_실제로_쓰는_단축키_목록까지_되돌리고_언어는_남긴다()
    {
        AppSettings.StereoUpmixKeyBind = new List<int> { 0x11, 0x41 }; // Ctrl+A
        AppSettings.VisualModeKeyBind = new List<int> { 0x42 };
        AppSettings.EditModeKeyBind = new List<int> { 0x43 };
        AppSettings.VisualMode = 2;
        AppSettings.CircleRadius = 90;
        AppSettings.ColorDanger = "#FF00FF00";
        AppSettings.IsAdminMode = true;
        AppSettings.Language = "日本語";

        AppSettings.ResetToDefaults();

        Assert.Equal(new[] { AppSettings.DefaultSoundModeKey }, AppSettings.StereoUpmixKeyBind);
        Assert.Equal(new[] { AppSettings.DefaultVisualModeKey }, AppSettings.VisualModeKeyBind);
        Assert.Equal(new[] { AppSettings.DefaultEditModeKey }, AppSettings.EditModeKeyBind);
        Assert.Equal(0, AppSettings.VisualMode);
        Assert.Equal(40.0, AppSettings.CircleMode.CircleRadius);
        Assert.Equal(AppSettings.DefaultColorDanger, AppSettings.ColorDanger);
        Assert.False(AppSettings.IsAdminMode);
        Assert.Equal("日本語", AppSettings.Language);
    }

    [Fact]
    public void 저장은_모았다가_한_번에_쓴다()
    {
        AppSettings.VisualMode = 1;
        AppSettings.Save();
        AppSettings.VisualMode = 3;
        AppSettings.Save();

        Assert.True(AppSettings.HasPendingSave);
        Assert.False(File.Exists(_path));

        AppSettings.Flush();

        Assert.False(AppSettings.HasPendingSave);
        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal(3, doc.RootElement.GetProperty("VisualMode").GetInt32());
    }

    [Fact]
    public void 미룬_저장은_시간이_지나면_저절로_쓴다()
    {
        AppSettings.Save();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(_path) && DateTime.UtcNow < deadline) Thread.Sleep(20);

        Assert.True(File.Exists(_path));
        Assert.False(AppSettings.HasPendingSave);
    }

    [Fact]
    public void 저장한_값은_다시_읽힌다()
    {
        AppSettings.VisualMode = 2;
        AppSettings.CircleRadius = 77;
        AppSettings.VisualModeKeyBind = new List<int> { 0x11, 0x56 };
        AppSettings.ShowSpeech = false;
        AppSettings.SaveNow();

        AppSettings.ResetToDefaults();
        AppSettings.Load();

        Assert.Equal(2, AppSettings.VisualMode);
        Assert.Equal(77, AppSettings.CircleMode.CircleRadius);
        Assert.Equal(new[] { 0x11, 0x56 }, AppSettings.VisualModeKeyBind);
        Assert.False(AppSettings.ShowSpeech);
    }

    [Fact]
    public void 키_목록이_없는_예전_설정은_예전_단축키_하나로_읽는다()
    {
        File.WriteAllText(_path, """{ "VisualModeHotkey": 120, "Language": "Deutsch" }""");

        AppSettings.Load();

        Assert.Equal(new[] { 120 }, AppSettings.VisualModeKeyBind);
        Assert.Equal(new[] { AppSettings.DefaultSoundModeKey }, AppSettings.StereoUpmixKeyBind);
        Assert.Equal("Deutsch", AppSettings.Language);
    }

    [Fact]
    public void 깨진_설정_파일은_기본값으로_시작한다()
    {
        File.WriteAllText(_path, "{ not json");

        AppSettings.Load();

        Assert.Equal(0, AppSettings.VisualMode);
        Assert.Equal(new[] { AppSettings.DefaultEditModeKey }, AppSettings.EditModeKeyBind);
    }

    [Fact]
    public void 모드별_기본값은_한_곳에서_온다()
    {
        var fresh = new VisualModeSettings();
        foreach (var mode in new[] { AppSettings.WaveMode, AppSettings.PadMode, AppSettings.CircleMode, AppSettings.OutlineMode })
        {
            Assert.Equal(JsonSerializer.Serialize(fresh), JsonSerializer.Serialize(mode));
        }
    }
}
