// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Diagnostics;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 확률 → 확정 결과까지 이은 단계(모바일 RealtimeAiPipeline 의 모델 뒤쪽). 첫 테스트는 모바일 계측 테스트
/// runtimeMappingChangeInvalidatesPreviousResultAndResetsConfirmation 을 모델 없이 고정 확률로 옮긴 것이다.
/// </summary>
public class AiClassificationStageTests
{
    private static readonly YamnetClassMap Names = AiTestData.ClassMap;

    [Fact] // runtimeMappingChangeInvalidatesPreviousResultAndResetsConfirmation
    public void 실행_중_매핑이_바뀌면_옛_결과를_바로_숨기고_확정을_처음부터_다시_한다()
    {
        var stage = new AiClassificationStage(Names);
        var probs = AiTestData.LoadCp4Probs("speech"); // Speech .75, Conversation .19, Music .06

        IReadOnlyDictionary<string, string> overrides = Names.ToDictionary(name => name, _ => "danger");
        Assert.True(stage.UpdateMapping(overrides));
        var danger = stage.Process(probs);
        Assert.Equal(SoundType.Danger, danger.Coarse);
        Assert.Equal(521 - 37, danger.MappingOverrideCount);
        Assert.Same(danger, stage.LastClassification(overrides));

        overrides = Names.ToDictionary(name => name, _ => "ambient");
        Assert.Null(stage.LastClassification(overrides)); // 다음 추론을 기다리지 않고 바로 사라진다.
        Assert.True(stage.UpdateMapping(overrides));
        var ambient = stage.Process(probs);
        Assert.Equal(SoundType.Ambient, ambient.Coarse);
        Assert.Equal(YamnetMappingPolicy.From(overrides, Names).Signature, ambient.MappingSignature);

        overrides = new Dictionary<string, string>();
        Assert.Null(stage.LastClassification(overrides));
        Assert.True(stage.UpdateMapping(overrides));
        Assert.Equal("default", stage.Process(probs).MappingSignature);
    }

    [Fact]
    public void 무음으로_건너뛴_틱에_바뀐_매핑도_다음_추론에_옛_위협음을_넘기지_않는다()
    {
        var stage = new AiClassificationStage(Names);
        var gunshot = AiTestData.LoadCp4Probs("danger"); // Gunshot .67, Explosion .27, Music .07
        var none = new Dictionary<string, string>();
        stage.UpdateMapping(none);
        Assert.Equal(SoundType.Danger, stage.Process(gunshot).Coarse);

        // 무음 틱: 추론은 건너뛰어도 매핑은 갱신한다.
        var demoted = new Dictionary<string, string> { ["Gunshot, gunfire"] = "ambient", ["Explosion"] = "ambient" };
        Assert.True(stage.UpdateMapping(demoted));
        Assert.Null(stage.LastClassification(demoted));

        var next = stage.Process(gunshot);
        Assert.Equal(SoundType.Ambient, next.Coarse);
        Assert.Equal("Gunshot, gunfire", next.Display); // 이름은 모델이 들은 그대로다. 종류만 사용자 지정을 따른다.
        Assert.False(next.DangerCuePromoted);
        Assert.Equal("Explosion=ambient;Gunshot, gunfire=ambient", next.MappingSignature);
    }

    [Fact]
    public void 서명이_같은_설정_변화는_확정을_유지하고_옛_결과는_다음_추론까지만_숨긴다()
    {
        var stage = new AiClassificationStage(Names);
        stage.UpdateMapping(new Dictionary<string, string>());
        Assert.Equal(SoundType.Danger, stage.Process(AiTestData.LoadCp4Probs("danger")).Coarse);

        // 기본 종류와 같은 값과 잘못된 값뿐이라 실제 매핑은 그대로다.
        var noOp = new Dictionary<string, string> { ["Gunshot, gunfire"] = "danger", ["Music"] = "invalid" };
        Assert.Null(stage.LastClassification(noOp));
        Assert.False(stage.UpdateMapping(noOp));
        Assert.Same(YamnetMappingPolicy.Default, stage.Mapping);

        // 확정을 지우지 않았으므로 0.27 미만의 환경음 한 프레임으로는 위협음이 풀리지 않는다.
        var weakAmbient = AiTestData.Probabilities(
            (Names.IndexOf("Wind"), .26f), (Names.IndexOf("Noise"), .25f), (Names.IndexOf("Music"), .24f));
        var kept = stage.Process(weakAmbient);
        Assert.Equal(SoundType.Danger, kept.Coarse);
        Assert.True(kept.MeetsThreshold);
        Assert.Same(kept, stage.LastClassification(noOp));
    }

    [Fact]
    public void 같은_설정을_다시_넘기면_아무것도_바꾸지_않는다()
    {
        var stage = new AiClassificationStage(Names);
        var overrides = new Dictionary<string, string> { ["Siren"] = "ambient" };
        Assert.True(stage.UpdateMapping(overrides));
        var mapping = stage.Mapping;
        var result = stage.Process(AiTestData.LoadCp4Probs("speech"));

        Assert.False(stage.UpdateMapping(new Dictionary<string, string> { ["Siren"] = "ambient" }));
        Assert.Same(mapping, stage.Mapping);
        Assert.Same(result, stage.LastClassification(overrides));

        // 입력은 복사해 두므로 설정 쪽 맵이 바뀌면 옛 결과는 숨는다.
        overrides["Siren"] = "speech";
        Assert.Null(stage.LastClassification(overrides));
    }

    [Fact]
    public void 경보_샘플은_승인한_경적_단서로_첫_프레임에_위협음이_된다()
    {
        var stage = new AiClassificationStage(Names);

        var result = stage.Process(AiTestData.LoadCp4Probs("alarm"));

        // 투표도 위협음이지만 확신도 0.14 는 기본 기준 0.25 아래다. 상위 5개의 Air horn 단서로 올려 기준이 0.12 로 내려간다.
        Assert.Equal(SoundType.Danger, result.Coarse);
        Assert.Equal("Air horn, truck horn", result.Display);
        Assert.True(result.DangerCuePromoted);
        Assert.True(result.MeetsThreshold);
        Assert.Equal(0.13773442804813385f, result.Confidence, 1e-6f);
    }

    [Fact]
    public void 투표가_환경음이어도_상위_5개의_경보_단서는_위협음으로_올린다()
    {
        var stage = new AiClassificationStage(Names);

        var result = stage.Process(AiTestData.LoadCp4Probs("gunshot")); // Ding .12, Sound effect .11, Alarm .07, …

        Assert.Equal(SoundType.Danger, result.Coarse);
        Assert.Equal("Alarm", result.Display);
        Assert.True(result.DangerCuePromoted);
        Assert.Equal(0.12252969294786453f, result.Confidence, 1e-6f); // 단서 확률이 아니라 1위 확신도를 그대로 쓴다.
    }

    [Fact]
    public void 기준을_못_넘으면_처음_상태가_그대로_남는다()
    {
        var stage = new AiClassificationStage(Names);

        // 효과음에 가려진 총소리를 표시 이름으로 고르지만, 총기 이름은 안전 단서로 올리지 않고 환경음 투표의 확신도 0.17 은 기준 아래다.
        var result = stage.Process(AiTestData.LoadCp4Probs("game_mix"));

        Assert.Equal(SoundType.Ambient, result.Coarse);
        Assert.Equal("", result.Display);
        Assert.Equal(0f, result.Confidence);
        Assert.False(result.MeetsThreshold);
        Assert.False(result.DangerCuePromoted);
        Assert.Equal("Sound effect", result.Top5[0].Name);
    }

    [Fact]
    public void 결과에_시간과_상위_5개와_매핑을_담는다()
    {
        var stage = new AiClassificationStage(Names);
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long started = Stopwatch.GetTimestamp();

        var result = stage.Process(AiTestData.LoadCp4Probs("silence"), started, preprocessMs: 1.5, yamnetMs: 2.5);

        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.InRange(result.TimestampMs, before, after);
        Assert.Equal(1.5, result.PreprocessMs);
        Assert.Equal(2.5, result.YamnetMs);
        Assert.True(result.TotalMs >= 0);
        Assert.Equal(SoundType.Ambient, result.Coarse);
        Assert.Equal("Silence", result.Display);
        Assert.Equal(5, result.Top5.Count);
        Assert.Equal(0, result.MappingOverrideCount);
        Assert.Equal("default", result.MappingSignature);
    }

    [Fact]
    public void Reset_은_확정과_마지막_결과를_지우고_매핑은_남긴다()
    {
        var stage = new AiClassificationStage(Names);
        var overrides = new Dictionary<string, string> { ["Wind"] = "speech" };
        stage.UpdateMapping(overrides);
        stage.Process(AiTestData.LoadCp4Probs("danger"));

        stage.Reset();

        Assert.Null(stage.LastClassification(overrides));
        Assert.Equal("Wind=speech", stage.Mapping.Signature);
        Assert.False(stage.UpdateMapping(overrides));
        var weakAmbient = AiTestData.Probabilities(
            (Names.IndexOf("Rain"), .26f), (Names.IndexOf("Noise"), .25f), (Names.IndexOf("Music"), .24f));
        Assert.Equal(SoundType.Ambient, stage.Process(weakAmbient).Coarse);
    }
}
