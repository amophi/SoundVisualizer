// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;
using SoundVisualizer.Ai;

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
        AppSettings.Language = "ko";
        ClearSoundTab();
    }

    public void Dispose()
    {
        AppSettings.Flush();
        AppSettings.ResetToDefaults();
        ClearSoundTab();
        AppSettings.SettingsFilePath = _originalPath;
        if (File.Exists(_path)) File.Delete(_path);
    }

    // 전체 초기화는 소리 탭의 두 값과 튜토리얼을 본 기록을 건드리지 않으므로 테스트마다 따로 비운다.
    private static void ClearSoundTab()
    {
        AppSettings.SoundTypes = new Dictionary<string, string>();
        AppSettings.ClassifyAdvanced = false;
        AppSettings.TutorialSeen = false;
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
        AppSettings.DeveloperRecord = true;
        AppSettings.Language = "ja";

        AppSettings.ResetToDefaults();

        Assert.Equal(new[] { AppSettings.DefaultSoundModeKey }, AppSettings.StereoUpmixKeyBind);
        Assert.Equal(new[] { AppSettings.DefaultVisualModeKey }, AppSettings.VisualModeKeyBind);
        Assert.Equal(new[] { AppSettings.DefaultEditModeKey }, AppSettings.EditModeKeyBind);
        Assert.Equal(0, AppSettings.VisualMode);
        Assert.Equal(40.0, AppSettings.CircleMode.CircleRadius);
        Assert.Equal(AppSettings.DefaultColorDanger, AppSettings.ColorDanger);
        Assert.False(AppSettings.IsAdminMode);
        Assert.False(AppSettings.DeveloperRecord);
        Assert.Equal("ja", AppSettings.Language);
    }

    [Fact]
    public void 결과_기록은_꺼진_채로_시작하고_개발자_모드와_따로_저장한다()
    {
        // 켜 두고 잊으면 디스크를 먹으므로 기본은 꺼짐이다.
        Assert.False(AppSettings.DeveloperRecord);

        // 모바일처럼 개발자 모드를 꺼도 기록 스위치의 값은 남는다. 두 값은 서로 다른 항목에 저장한다.
        AppSettings.IsAdminMode = false;
        AppSettings.DeveloperRecord = true;
        AppSettings.SaveNow();
        AppSettings.ResetToDefaults();
        AppSettings.Load();

        Assert.False(AppSettings.IsAdminMode);
        Assert.True(AppSettings.DeveloperRecord);
    }

    [Fact]
    public void 결과_기록이_없는_예전_설정은_꺼진_채로_읽는다()
    {
        // 앞에서 들고 있던 값이 남지 않는지도 본다.
        AppSettings.DeveloperRecord = true;
        File.WriteAllText(_path, """{ "IsAdminMode": true, "Language": "ko" }""");

        AppSettings.Load();

        Assert.True(AppSettings.IsAdminMode);
        Assert.False(AppSettings.DeveloperRecord);
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
        Assert.Equal("de", AppSettings.Language); // 예전 판의 언어 이름은 코드로 옮겨 읽는다
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

    [Fact]
    public void 새로_설치하면_Windows_화면_언어를_따른다()
    {
        Assert.Equal("system", AppSettings.DefaultLanguage);
    }

    [Fact]
    public void 언어는_예전과_같은_이름의_항목에_코드로_저장한다()
    {
        AppSettings.Language = "zh-Hant";
        AppSettings.SaveNow();

        using (var doc = JsonDocument.Parse(File.ReadAllText(_path)))
        {
            Assert.Equal("zh-Hant", doc.RootElement.GetProperty("Language").GetString());
        }

        AppSettings.Language = "ko";
        AppSettings.Load();
        Assert.Equal("zh-Hant", AppSettings.Language);
    }

    [Theory]
    [InlineData("KOR", "ko")]
    [InlineData("English", "en")]
    [InlineData("ENG", "en")]
    [InlineData("日本語", "ja")]
    [InlineData("中文", "zh-Hans")]
    [InlineData("Español", "es")]
    [InlineData("Français", "fr")]
    [InlineData("Русский", "ru")]
    [InlineData("system", "system")]
    [InlineData("pt-BR", "pt-BR")]
    public void 예전_언어_값은_읽을_때_코드로_옮기고_다음_저장부터_코드로_쓴다(string stored, string expected)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(new { Language = stored }));

        AppSettings.Load();
        Assert.Equal(expected, AppSettings.Language);

        AppSettings.SaveNow();
        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal(expected, doc.RootElement.GetProperty("Language").GetString());
    }

    [Fact]
    public void 언어_항목이_없는_아주_예전_설정은_한국어로_읽는다()
    {
        File.WriteAllText(_path, """{ "VisualMode": 1 }""");

        AppSettings.Load();

        Assert.Equal("ko", AppSettings.Language);
    }

    [Theory]
    [InlineData("""{ "Language": "Klingon" }""")]
    [InlineData("""{ "Language": null }""")]
    [InlineData("""{ "Language": "" }""")]
    public void 읽을_수_없는_언어_값은_Windows_화면_언어를_따른다(string json)
    {
        File.WriteAllText(_path, json);

        AppSettings.Load();

        Assert.Equal("system", AppSettings.Language);
    }

    // 소리 탭의 두 값. 초인종(Doorbell)은 기본이 환경음, 사이렌(Siren)은 기본이 위협음인 소리로 쓴다.
    private static Dictionary<string, string> Types(params (string Name, string Label)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Label);

    private static List<(string, string)> Sorted(IReadOnlyDictionary<string, string> types) =>
        types.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value)).ToList();

    [Fact]
    public void 시험에_쓰는_소리의_기본_종류()
    {
        Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse("Doorbell"));
        Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse("Siren"));
    }

    [Fact]
    public void 바꾼_소리_종류와_고급_모드는_저장한_뒤_다시_읽힌다()
    {
        AppSettings.SoundTypes = Types(("Doorbell", SoundTypeLabels.Danger), ("Siren", SoundTypeLabels.Ambient));
        AppSettings.ClassifyAdvanced = true;
        AppSettings.SaveNow();

        ClearSoundTab();
        AppSettings.Load();

        Assert.Equal(new List<(string, string)> { ("Doorbell", "danger"), ("Siren", "ambient") }, Sorted(AppSettings.SoundTypes));
        Assert.True(AppSettings.ClassifyAdvanced);
    }

    [Fact]
    public void 소리_종류는_이름_순서의_JSON_객체로_저장한다()
    {
        AppSettings.SoundTypes = Types(("Siren", SoundTypeLabels.Ambient), ("Doorbell", SoundTypeLabels.Danger));
        AppSettings.SaveNow();

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        var types = doc.RootElement.GetProperty("SoundTypes");
        Assert.Equal(JsonValueKind.Object, types.ValueKind);
        Assert.Equal(new[] { "Doorbell", "Siren" }, types.EnumerateObject().Select(property => property.Name));
        Assert.Equal("danger", types.GetProperty("Doorbell").GetString());
        Assert.False(doc.RootElement.GetProperty("ClassifyAdvanced").GetBoolean());
    }

    [Fact]
    public void 바꾼_소리가_없으면_빈_객체로_저장한다()
    {
        AppSettings.SaveNow();

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Empty(doc.RootElement.GetProperty("SoundTypes").EnumerateObject());
    }

    [Fact]
    public void 소리_종류는_읽을_때_거른다()
    {
        File.WriteAllText(_path, """
            { "SoundTypes": { "Doorbell": "danger", "Siren": "danger", "Speech": "loud", "Dog": 3, "Old model name": "speech" },
              "ClassifyAdvanced": false }
            """);

        AppSettings.Load();

        // 기본 종류와 같은 값(사이렌 = 위협음), 세 라벨이 아닌 값, 문자열이 아닌 값은 버리고, 모르는 이름은 모바일처럼 남긴다.
        Assert.Equal(new List<(string, string)> { ("Doorbell", "danger"), ("Old model name", "speech") }, Sorted(AppSettings.SoundTypes));
    }

    [Fact]
    public void 소리_탭_항목이_없는_예전_설정은_바꾼_소리_없이_기본_모드로_읽는다()
    {
        // 앞에서 들고 있던 값이 남지 않는지도 본다.
        AppSettings.SoundTypes = Types(("Doorbell", SoundTypeLabels.Danger));
        AppSettings.ClassifyAdvanced = true;
        File.WriteAllText(_path, """{ "VisualMode": 1, "Language": "ko" }""");

        AppSettings.Load();

        Assert.Empty(AppSettings.SoundTypes);
        Assert.False(AppSettings.ClassifyAdvanced);
        Assert.Equal(1, AppSettings.VisualMode);
    }

    [Fact]
    public void 고급_모드를_저장한_적이_없으면_바꾼_소리가_있을_때만_고급_모드로_연다()
    {
        File.WriteAllText(_path, """{ "SoundTypes": { "Doorbell": "danger" } }""");
        AppSettings.Load();
        Assert.True(AppSettings.ClassifyAdvanced);

        // 거르고 나서 남은 것이 없으면 바꾼 소리가 없는 것이다.
        File.WriteAllText(_path, """{ "SoundTypes": { "Siren": "danger" } }""");
        AppSettings.Load();
        Assert.False(AppSettings.ClassifyAdvanced);

        // 저장한 값이 있으면 그것을 따른다.
        File.WriteAllText(_path, """{ "SoundTypes": { "Doorbell": "danger" }, "ClassifyAdvanced": false }""");
        AppSettings.Load();
        Assert.False(AppSettings.ClassifyAdvanced);
    }

    [Theory]
    [InlineData("\"danger\"")]
    [InlineData("""[ "Doorbell", "danger" ]""")]
    [InlineData("null")]
    public void 소리_종류를_읽지_못해도_다른_설정은_읽는다(string stored)
    {
        File.WriteAllText(_path, "{ \"VisualMode\": 2, \"SoundTypes\": " + stored + " }");

        AppSettings.Load();

        Assert.Equal(2, AppSettings.VisualMode);
        Assert.Empty(AppSettings.SoundTypes);
    }

    [Fact]
    public void 전체_초기화는_바꾼_소리_종류와_고급_모드를_건드리지_않는다()
    {
        AppSettings.SoundTypes = Types(("Doorbell", SoundTypeLabels.Danger));
        AppSettings.ClassifyAdvanced = true;

        AppSettings.ResetToDefaults();

        Assert.Equal(SoundTypeLabels.Danger, AppSettings.SoundTypes["Doorbell"]);
        Assert.True(AppSettings.ClassifyAdvanced);
    }

    [Fact]
    public void 넣은_소리_종류_맵은_얼려_두어_넘긴_쪽이_고쳐도_바뀌지_않는다()
    {
        var types = Types(("Doorbell", SoundTypeLabels.Danger));
        AppSettings.SoundTypes = types;
        types["Siren"] = SoundTypeLabels.Ambient;
        types.Remove("Doorbell");

        Assert.Equal(new[] { "Doorbell" }, AppSettings.SoundTypes.Keys);

        // AI 가 앞서 읽어 둔 맵도 바꿔 끼운 뒤에 그대로다.
        var before = AppSettings.SoundTypes;
        AppSettings.SoundTypes = Types(("Siren", SoundTypeLabels.Ambient));
        Assert.Equal(new[] { "Doorbell" }, before.Keys);
        Assert.Equal(new[] { "Siren" }, AppSettings.SoundTypes.Keys);
    }

    // ===== 튜토리얼을 본 적이 있는지(모바일 TutorialSettingTest) =====
    // 처음 여는 사람에게만 튜토리얼을 저절로 띄운다. 이 기능 전부터 쓰던 사람은 이미 앱을 알므로 띄우지 않는다.

    [Fact]
    public void 새로_설치하면_튜토리얼을_아직_보지_않은_것이다()
    {
        AppSettings.TutorialSeen = true; // 앞에서 들고 있던 값이 남지 않는지도 본다

        AppSettings.Load();

        Assert.False(File.Exists(_path));
        Assert.False(AppSettings.TutorialSeen);
    }

    [Fact]
    public void 이_기능_전부터_쓰던_사람은_튜토리얼을_본_것으로_친다()
    {
        File.WriteAllText(_path, """{ "VisualMode": 1, "Language": "ko" }""");

        AppSettings.Load();

        Assert.True(AppSettings.TutorialSeen);
    }

    [Fact]
    public void 깨진_설정_파일이_있어도_쓰던_사람이라_튜토리얼을_본_것으로_친다()
    {
        File.WriteAllText(_path, "{ not json");

        AppSettings.Load();

        Assert.True(AppSettings.TutorialSeen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 저장한_튜토리얼_값이_있으면_다른_설정과_상관없이_그_값을_쓴다(bool seen)
    {
        File.WriteAllText(_path, "{ \"VisualMode\": 1, \"TutorialSeen\": " + (seen ? "true" : "false") + " }");

        AppSettings.Load();

        Assert.Equal(seen, AppSettings.TutorialSeen);
    }

    [Fact]
    public void 처음_정한_값은_다른_설정을_먼저_저장해도_바뀌지_않는다()
    {
        // 새로 설치하고 튜토리얼을 닫지 않은 채 언어만 바꾸고 나갔다가 다시 여는 경우. 저장된 파일이 생겼다고 쓰던 사람으로 치면 안 된다.
        AppSettings.Load();
        Assert.False(AppSettings.TutorialSeen);

        AppSettings.Language = "ja";
        AppSettings.SaveNow();
        AppSettings.Load();

        Assert.False(AppSettings.TutorialSeen);
        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.False(doc.RootElement.GetProperty("TutorialSeen").GetBoolean());
    }

    [Fact]
    public void 닫아서_본_것으로_저장하면_다시_열어도_띄우지_않는다()
    {
        AppSettings.Load();
        AppSettings.TutorialSeen = true;
        AppSettings.SaveNow();

        AppSettings.TutorialSeen = false;
        AppSettings.Load();

        Assert.True(AppSettings.TutorialSeen);
    }

    [Fact]
    public void 전체_초기화는_튜토리얼을_본_기록을_건드리지_않는다()
    {
        // 모양 설정을 되돌렸다고 튜토리얼이 다시 뜨면 안 된다. 본 기록은 사람에 대한 값이다.
        AppSettings.TutorialSeen = true;

        AppSettings.ResetToDefaults();

        Assert.True(AppSettings.TutorialSeen);
    }
}
