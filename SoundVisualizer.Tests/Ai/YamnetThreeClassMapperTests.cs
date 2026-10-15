// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 모바일 YamnetThreeClassMapperTest 를 옮겼다. CP4: 확률 521개 → 상위 5개 → 가려진 위협음 표시 → 상위 3개 투표.
/// 안전 단서·기준·히스테리시스는 보지 않는다. 기대값은 Python 기준 구현이 만든 yamnet_cp4_meta.json 이다.
/// </summary>
public class YamnetThreeClassMapperTests
{
    private static readonly YamnetClassMap ClassNames = AiTestData.ClassMap;
    private static readonly YamnetCoarseClassifier Classifier = new(ClassNames);
    private static readonly JsonElement Meta = AiTestData.LoadJson("yamnet_cp4_meta.json");

    [Fact] // classMap_has521Entries
    public void 클래스_목록은_521개이고_따옴표로_감싼_이름도_읽는다()
    {
        Assert.Equal(521, ClassNames.Count);
        Assert.Equal(521, Meta.GetProperty("num_classes").GetInt32());
        Assert.Equal(521, Meta.GetProperty("class_map_filled").GetInt32());
        Assert.Equal("Speech", ClassNames[0]);
        Assert.Equal("Field recording", ClassNames[520]);
        Assert.Equal("Child speech, kid speaking", ClassNames[1]);
        Assert.Equal("Gunshot, gunfire", ClassNames[421]);
    }

    [Fact] // mappingChecks_matchPython
    public void 이름별_기본_종류가_Python_기준과_같다()
    {
        var checks = Meta.GetProperty("mapping_checks").EnumerateObject().ToList();
        Assert.NotEmpty(checks);
        Assert.All(checks, check =>
            Assert.Equal(check.Value.GetString(), YamnetThreeClassMapper.MapDisplayNameToCoarse(check.Name).ToLabel()));
    }

    [Fact] // A_ambient
    public void A_환경음() => AssertCase("ambient");

    [Fact] // B_speech
    public void B_대화음() => AssertCase("speech");

    [Fact] // C_danger
    public void C_위협음() => AssertCase("danger");

    [Fact] // D_defaultUnmappedToAmbient
    public void D_규칙에_없는_소리는_환경음() => AssertCase("default_ambient");

    [Fact] // E_voteOverTop1
    public void E_1위보다_상위_3개_투표가_이긴다()
    {
        AssertCase("vote_over_top1");
        var r = Classifier.Classify(AiTestData.LoadCp4Probs("vote_over_top1"));
        Assert.Equal("Music", r.Top5[0].Name);
        Assert.Equal(SoundType.Speech, r.Coarse);
    }

    [Fact] // F_priorityDangerWinsTie
    public void F_점수가_같으면_위협음이_이긴다()
    {
        AssertCase("priority_tie");
        var r = Classifier.Classify(AiTestData.LoadCp4Probs("priority_tie"));
        Assert.Equal(r.DangerScore, r.SpeechScore, 1e-6f);
        Assert.Equal(r.DangerScore, r.AmbientScore, 1e-6f);
        Assert.Equal(SoundType.Danger, r.Coarse);
    }

    [Fact] // G_formerGunshotProxiesAndWaterSoundsAreAmbient
    public void G_옛_총소리_대리_라벨과_물소리는_환경음()
    {
        AssertCase("proxy_plop");
        Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse("Plop"));
        Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse("Gargling"));
        Assert.All(new[] { "Rain", "Raindrop", "Rain on surface", "Waterfall" }, name =>
            Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
    }

    [Fact] // approvedFootstepAndVehicleHornPolicy
    public void 발소리는_환경음이고_차량_경적_두_라벨만_위협음()
    {
        Assert.All(new[] { "Footstep", "Footsteps" }, name =>
            Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
        Assert.All(new[] { "Vehicle horn, car horn, honking", "Air horn, truck horn" }, name =>
            Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
    }

    [Fact] // approvedSuddenImpactPolicy
    public void 터지거나_부서지는_소리는_위협음이고_비슷한_충격음은_환경음()
    {
        Assert.All(new[] { "Burst, pop", "Boom", "Bang", "Smash, crash", "Breaking", "Shatter" }, name =>
            Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
        Assert.All(new[] { "Glass", "Door", "Knock", "Thump, thud", "Whack, thwack", "Clang" }, name =>
            Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
    }

    [Fact] // approvedChainsawAndThunderPolicyUsesExactLabels
    public void 전기톱과_천둥은_정확히_같은_이름만_위협음()
    {
        Assert.All(new[] { "Chainsaw", "Thunder", "Thunderstorm" }, name =>
            Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
        Assert.All(new[] { "Rain", "Wind", "Distant thunder", "Chainsaw-like sound" }, name =>
            Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
    }

    [Fact] // approvedHumanVocalizationsAreSpeechUsingExactLabels
    public void 사람이_내는_소리는_정확히_같은_이름만_대화음()
    {
        Assert.All(new[]
        {
            "Bellow", "Whoop", "Yell", "Cheering",
            "Giggle", "Snicker", "Belly laugh", "Chuckle, chortle",
            "Baby cry, infant cry", "Whimper", "Wail, moan",
        }, name => Assert.Equal(SoundType.Speech, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
        Assert.All(new[] { "Whimper (dog)", "Bellow-like sound", "Cheering music" }, name =>
            Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
    }

    [Fact] // waterSoundsDoNotProduceDangerCoarseClassification
    public void 물소리가_1위여도_투표는_환경음()
    {
        Assert.All(new[] { "Rain", "Raindrop", "Rain on surface", "Waterfall" }, name =>
        {
            var probabilities = AiTestData.Probabilities((ClassNames.IndexOf(name), 1f));

            var result = Classifier.Classify(probabilities);

            Assert.Equal(SoundType.Ambient, result.Coarse);
            Assert.Equal(name, result.DisplayName);
        });
    }

    [Fact] // H_gameMixPreference
    public void H_효과음에_가려진_총소리를_표시_이름으로_고른다()
    {
        AssertCase("game_mix");
        var r = Classifier.Classify(AiTestData.LoadCp4Probs("game_mix"));
        Assert.True(r.GameMixPreferenceApplied);
        Assert.Equal("Gunshot, gunfire", r.DisplayName);
        // 투표는 원래 상위 5개 그대로라 환경음이 이길 수 있다.
        Assert.Equal(SoundType.Ambient, r.Coarse);
    }

    [Fact] // sample_silence_preBoosterAmbient
    public void 무음_샘플은_환경음()
    {
        AssertSample("silence");
        var r = Classifier.Classify(AiTestData.LoadCp4Probs("silence"));
        Assert.Equal(SoundType.Ambient, r.Coarse);
        Assert.False(r.GameMixPreferenceApplied);
    }

    [Fact] // sample_gunshot_preBoosterAmbientIsExpected
    public void 총소리_샘플은_투표_단계에서는_환경음이_맞다()
    {
        // 투표 단계만 본다. 위협음 판단은 그다음 안전 단서와 후처리가 한다.
        AssertSample("gunshot");
        var r = Classifier.Classify(AiTestData.LoadCp4Probs("gunshot"));
        Assert.Equal(SoundType.Ambient, r.Coarse);
    }

    [Fact] // sample_alarm_preBooster
    public void 경보_샘플() => AssertSample("alarm");

    // 아래는 모바일에 없는 C# 확인이다.

    [Theory]
    [InlineData(.5f, .11f, true)]     // 1위 × 0.22 = 0.11 와 같으면 고른다.
    [InlineData(.5f, .1099f, false)]
    [InlineData(.2f, .06f, true)]     // 1위 × 0.22 = 0.044 보다 하한 0.06 이 높다.
    [InlineData(.2f, .0599f, false)]
    public void 가려진_위협음은_1위의_22퍼센트와_0_06_중_큰_값_이상이어야_고른다(float music, float gunshot, bool applied)
    {
        var probabilities = AiTestData.Probabilities(
            (ClassNames.IndexOf("Music"), music), (ClassNames.IndexOf("Gunshot, gunfire"), gunshot));

        var r = Classifier.Classify(probabilities);

        Assert.Equal(applied, r.GameMixPreferenceApplied);
        Assert.Equal(applied ? "Gunshot, gunfire" : "Music", r.DisplayName);
        Assert.Equal(applied ? gunshot : music, r.Confidence);
    }

    private static void AssertCase(string name) =>
        AssertResultMatches(Meta.GetProperty("handcrafted").GetProperty(name), Classifier.Classify(AiTestData.LoadCp4Probs(name)), name);

    private static void AssertSample(string name) =>
        AssertResultMatches(Meta.GetProperty("samples").GetProperty(name), Classifier.Classify(AiTestData.LoadCp4Probs(name)), "sample_" + name);

    private static void AssertResultMatches(JsonElement expected, YamnetCoarseClassifier.Result actual, string label)
    {
        Assert.Equal(expected.GetProperty("coarse").GetString(), actual.Coarse.ToLabel());
        Assert.Equal(expected.GetProperty("display").GetString(), actual.DisplayName);
        Assert.Equal(expected.GetProperty("yamnet_class_index").GetInt32(), actual.YamnetClassIndex);
        Assert.Equal(expected.GetProperty("game_mix_preference_applied").GetBoolean(), actual.GameMixPreferenceApplied);
        AssertClose(expected.GetProperty("confidence"), actual.Confidence, $"{label} confidence");

        var votes = expected.GetProperty("vote_scores");
        AssertClose(votes.GetProperty("ambient"), actual.AmbientScore, $"{label} ambient vote");
        AssertClose(votes.GetProperty("speech"), actual.SpeechScore, $"{label} speech vote");
        AssertClose(votes.GetProperty("danger"), actual.DangerScore, $"{label} danger vote");

        var top5 = expected.GetProperty("top5");
        Assert.Equal(top5.GetArrayLength(), actual.Top5.Count);
        for (int i = 0; i < top5.GetArrayLength(); i++)
        {
            var e = top5[i];
            var a = actual.Top5[i];
            Assert.Equal(e.GetProperty("index").GetInt32(), a.Index);
            Assert.Equal(e.GetProperty("name").GetString(), a.Name);
            AssertClose(e.GetProperty("prob"), a.Probability, $"{label} top5[{i}].prob");
        }
    }

    private static void AssertClose(JsonElement expected, float actual, string what)
    {
        float e = (float)expected.GetDouble();
        Assert.True(MathF.Abs(actual - e) < 1e-5f, $"{what}: {actual} vs {e}");
    }
}
