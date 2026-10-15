// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>모바일 YamnetMappingPolicyTest 를 옮겼다(사용자 3분류 매핑, #291).</summary>
public class YamnetMappingPolicyTests
{
    // 0 Music, 1 Siren, 2 Explosion, 3 Gunshot, 4 Speech, 5 Footsteps, 6 Wind, 그 뒤는 class_7 …
    private static readonly YamnetClassMap Names =
        new(AiTestData.Names("class_", "Music", "Siren", "Explosion", "Gunshot, gunfire", "Speech", "Footsteps", "Wind"));

    private static readonly YamnetCoarseClassifier Classifier = new(Names);

    private static YamnetMappingPolicy Policy(params (string Name, string Label)[] values) =>
        YamnetMappingPolicy.From(values.ToDictionary(v => v.Name, v => v.Label), Names);

    [Fact] // defaultsPreserveEverySingleClassDecision
    public void 사용자_지정이_없으면_모든_클래스의_판단이_기본과_같다()
    {
        var actualNames = AiTestData.ClassMap;
        var actualClassifier = new YamnetCoarseClassifier(actualNames);
        var empty = YamnetMappingPolicy.From(new Dictionary<string, string>(), actualNames);
        for (int i = 0; i < actualNames.Count; i++)
        {
            Assert.Equal(YamnetThreeClassMapper.MapDisplayNameToCoarse(actualNames[i]), empty.Coarse(actualNames[i]));
            var p = AiTestData.Probabilities((i, 1f));
            Assert.Equal(actualClassifier.Classify(p), actualClassifier.Classify(p, empty));
            var pre = actualClassifier.Classify(p);
            Assert.Equal(YamnetSafetyCueDecision.Decide(actualNames, pre), YamnetSafetyCueDecision.Decide(actualNames, pre, empty));
        }
    }

    [Fact] // sirenAndExplosionDemotionsAffectVotesAndSafetyCues
    public void 사이렌과_폭발을_빼면_투표와_안전_단서_모두에서_빠진다()
    {
        Assert.All(new[] { 1, 2 }, index =>
        {
            var mapping = Policy((Names[index], "ambient"));

            var pre = Classifier.Classify(AiTestData.Probabilities((index, .8f), (6, .2f)), mapping);
            var decision = YamnetSafetyCueDecision.Decide(Names, pre, mapping);

            Assert.Equal(SoundType.Ambient, pre.Coarse);
            Assert.Equal(SoundType.Ambient, decision.PostCoarse);
            Assert.False(decision.HasStrongDangerCue);
            Assert.False(decision.DangerCuePromoted);
        });
    }

    [Fact] // anotherIndependentCueStillPromotesDanger
    public void 다른_독립_단서는_여전히_위협음으로_올린다()
    {
        var mapping = Policy(("Siren", "ambient"));
        // 실제 후보 다섯. 확률 0 인 총소리가 동점 자리를 채우지 않게 한다.
        var pre = Classifier.Classify(AiTestData.Probabilities((1, .6f), (2, .2f), (6, .1f), (0, .06f), (5, .04f)), mapping);
        Assert.Equal(SoundType.Ambient, pre.Coarse);

        var decision = YamnetSafetyCueDecision.Decide(Names, pre, mapping);

        Assert.Equal(SoundType.Danger, decision.PostCoarse);
        Assert.Equal("Explosion", decision.PostDisplay);
    }

    [Fact] // speechOverrideDoesNotBlockIndependentDangerCue
    public void 대화음으로_바꾼_라벨이_다른_독립_단서를_막지_않는다()
    {
        var p = AiTestData.Probabilities((1, .6f), (2, .2f), (6, .1f), (0, .06f), (5, .04f));
        var mapping = Policy(("Siren", "speech"));

        var pre = Classifier.Classify(p, mapping);
        Assert.Equal(SoundType.Speech, pre.Coarse);
        Assert.Equal(SoundType.Danger, pre.DefaultCoarse);

        var decision = YamnetSafetyCueDecision.Decide(Names, pre, mapping);
        Assert.Equal(SoundType.Danger, decision.PostCoarse);
        Assert.Equal("Explosion", decision.PostDisplay);
        Assert.True(decision.DangerCuePromoted);
    }

    [Fact] // ambientToSpeechOverrideDoesNotBlockIndependentDangerCue
    public void 환경음을_대화음으로_바꿔도_독립_단서를_막지_않는다()
    {
        var mapping = Policy(("Wind", "speech"));

        var pre = Classifier.Classify(AiTestData.Probabilities((6, .5f), (1, .2f), (0, .08f), (5, .05f), (2, .01f)), mapping);

        Assert.Equal(SoundType.Speech, pre.Coarse);
        Assert.Equal(SoundType.Ambient, pre.DefaultCoarse);
        Assert.Equal(SoundType.Danger, YamnetSafetyCueDecision.Decide(Names, pre, mapping).PostCoarse);
    }

    [Fact] // demotedSoleCueStaysSpeechWithoutShortcutReentry
    public void 뺀_단서가_하나뿐이면_키워드로_다시_들어오지_않고_대화음으로_남는다()
    {
        var mapping = Policy(("Siren", "speech"));

        var pre = Classifier.Classify(AiTestData.Probabilities((1, .6f), (6, .2f), (0, .1f), (5, .06f), (4, .04f)), mapping);
        var decision = YamnetSafetyCueDecision.Decide(Names, pre, mapping);

        Assert.Equal(SoundType.Speech, decision.PostCoarse);
        Assert.False(decision.DangerCuePromoted);
    }

    [Fact] // defaultSpeechVoteStillBlocksAfterUserDemotion
    public void 사용자가_대화음을_빼도_기본_대화음_투표는_승격을_막는다()
    {
        // 표시 이름은 Wind 다. 여기서 막는 것은 말소리 같은 표시 이름이 아니라 기본 매핑으로 낸 투표다.
        var localNames = new YamnetClassMap(
            AiTestData.Names("class_", "Music", "Siren", "Explosion", "Gunshot, gunfire", "Speech", "Footsteps", "Wind", "Conversation"));
        var mapping = YamnetMappingPolicy.From(
            new Dictionary<string, string> { ["Speech"] = "ambient", ["Conversation"] = "ambient" }, localNames);

        var pre = new YamnetCoarseClassifier(localNames).Classify(
            AiTestData.Probabilities((6, .3f), (4, .25f), (7, .24f), (1, .15f), (5, .06f)), mapping);

        Assert.Equal(SoundType.Ambient, pre.Coarse);
        Assert.Equal(SoundType.Speech, pre.DefaultCoarse);
        Assert.False(YamnetSafetyCueDecision.Decide(localNames, pre, mapping).DangerCuePromoted);
    }

    [Fact] // demotedGunshotDoesNotSuppressIndependentSirenCue
    public void 뺀_총소리는_독립된_사이렌_단서를_막지_않는다()
    {
        var mapping = Policy(("Gunshot, gunfire", "ambient"));

        var pre = Classifier.Classify(AiTestData.Probabilities((3, .7f), (1, .2f), (6, .1f)), mapping);
        var decision = YamnetSafetyCueDecision.Decide(Names, pre, mapping);

        Assert.False(decision.HasGunshotCue);
        Assert.Equal("Siren", decision.PostDisplay);
        Assert.True(decision.DangerCuePromoted);
    }

    [Fact] // newlyPromotedFootstepsVoteButDoNotBecomeSafetyCue
    public void 새로_위협음으로_지정한_발소리는_투표만_하고_안전_단서가_되지_않는다()
    {
        var mapping = Policy(("Footsteps", "danger"));

        var strong = Classifier.Classify(AiTestData.Probabilities((5, .8f), (6, .2f)), mapping);
        Assert.Equal(SoundType.Danger, strong.Coarse);
        Assert.False(YamnetSafetyCueDecision.Decide(Names, strong, mapping).HasStrongDangerCue);

        var weak = Classifier.Classify(AiTestData.Probabilities((0, .5f), (6, .3f), (5, .2f)), mapping);
        Assert.Equal(SoundType.Ambient, weak.Coarse);
        Assert.Equal("Music", weak.DisplayName);
        Assert.False(YamnetSafetyCueDecision.Decide(Names, weak, mapping).DangerCuePromoted);
    }

    [Fact] // demotedDangerCannotWinGameMixDisplayPreference
    public void 뺀_위협음은_가려진_위협음_표시_이름으로도_뽑히지_않는다()
    {
        var mapping = Policy(("Siren", "ambient"));

        var pre = Classifier.Classify(AiTestData.Probabilities((0, .7f), (1, .3f)), mapping);

        Assert.Equal("Music", pre.DisplayName);
        Assert.False(pre.GameMixPreferenceApplied);
    }

    [Fact] // policyCopiesValidExactLabelsAndHasCanonicalIdentity
    public void 유효한_정확한_이름만_복사하고_내용이_같으면_서명도_같다()
    {
        var input = new Dictionary<string, string> { ["Siren"] = "ambient", ["Footsteps"] = "danger" };
        var snapshot = YamnetMappingPolicy.From(input, Names);
        input.Clear();

        Assert.Equal(SoundType.Ambient, snapshot.Coarse("Siren"));
        Assert.Equal(2, snapshot.OverrideCount);
        Assert.Equal(Policy(("Footsteps", "danger"), ("Siren", "ambient")).Signature, snapshot.Signature);
        Assert.Equal("default", Policy(("siren", "ambient"), ("Music", "invalid"), ("unknown", "danger")).Signature);
    }

    [Fact] // explicitCriticalDecisionPreventsKeywordReentryInPostprocessor
    public void 매핑으로_정한_치명적_단서_판단은_후처리에서_키워드로_되살아나지_않는다()
    {
        var mapping = Policy(("Gunshot, gunfire", "ambient"), ("Explosion", "ambient"));
        var hits = new[] { new YamnetCoarseClassifier.TopClassHit(2, "Explosion", .1f) };

        bool critical = mapping.HasCriticalDangerCue("Gunshot, gunfire", hits);

        Assert.False(critical);
        Assert.True(Policy(("Gunshot, gunfire", "ambient")).HasCriticalDangerCue("Gunshot, gunfire", hits));
        var post = new AiPostProcessor();
        var result = post.Process(new AiPostProcessor.FrameInput(
            Coarse: SoundType.Danger, Display: "Gunshot, gunfire", Confidence: .21f,
            TopKSummary: "Explosion", CriticalDangerEvent: critical));
        Assert.Equal(.25f, result.EffectiveThreshold);
        Assert.False(result.MeetsThreshold);
        Assert.Equal(SoundType.Ambient, result.UiCoarse);
    }

    // 아래는 모바일에 없는 C# 확인이다.

    [Fact]
    public void 서명은_서수_순서로_이름과_종류를_잇는다()
    {
        // 모바일 toSortedMap 은 UTF-16 서수 순이라 대문자 W 가 소문자 c 보다 앞이다. 문화권 정렬이면 반대가 된다.
        var mapping = Policy(("class_7", "danger"), ("Wind", "speech"), ("Siren", "ambient"));

        Assert.Equal("Siren=ambient;Wind=speech;class_7=danger", mapping.Signature);
        Assert.Equal("default", YamnetMappingPolicy.Default.Signature);
        Assert.Equal(0, YamnetMappingPolicy.Default.OverrideCount);
    }

    [Fact]
    public void 기본_종류와_같은_값과_대소문자가_다른_라벨은_버린다()
    {
        var mapping = Policy(("Siren", "danger"), ("Wind", "Speech"), ("Music", " speech"), ("Footsteps", "speech"));

        Assert.Equal(1, mapping.OverrideCount);
        Assert.Equal("Footsteps=speech", mapping.Signature);
        Assert.Equal(SoundType.Danger, mapping.Coarse("Siren"));
        Assert.Equal(SoundType.Ambient, mapping.Coarse("Wind"));
    }
}
