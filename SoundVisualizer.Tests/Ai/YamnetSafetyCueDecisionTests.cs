// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>모바일 YamnetSafetyCueDecisionTest 를 옮겼다.</summary>
public class YamnetSafetyCueDecisionTests
{
    private static string[] BaseNames() => AiTestData.Names("Ambient ", "Music", "Siren", "Gunshot, gunfire", "Speech");

    private static readonly YamnetClassMap Names = new(BaseNames());
    private static readonly YamnetCoarseClassifier Classifier = new(Names);

    /// <summary>1번 자리(원래 Siren)나 0번 자리(원래 Music)만 바꾼 목록으로 분류하고 판단한다.</summary>
    private static YamnetSafetyCueDecision.Result DecideWith(int slot, string name, float[] probabilities)
    {
        var names = BaseNames();
        names[slot] = name;
        var map = new YamnetClassMap(names);
        return YamnetSafetyCueDecision.Decide(map, new YamnetCoarseClassifier(map).Classify(probabilities));
    }

    [Fact] // strong non-gunshot cue is retained without an auxiliary model
    public void 총기가_아닌_강한_단서는_보조_모델_없이도_위협음으로_남는다()
    {
        var probabilities = AiTestData.Probabilities((0, .6f), (1, .2f),
            // 실제 softmax 에는 정확히 0 인 꼬리가 없다. 총기가 아닌 후보 다섯을 채워, 확률 0 인 총소리가 상위 5개에
            // 끼어드는 일을 테스트가 만들어 내지 않게 한다.
            (4, .001f), (5, .001f), (6, .001f));

        var decision = YamnetSafetyCueDecision.Decide(Names, Classifier.Classify(probabilities));

        Assert.Equal(SoundType.Danger, decision.PostCoarse);
        Assert.Equal("Siren", decision.PostDisplay);
        Assert.True(decision.DangerCuePromoted);
    }

    [Fact] // gunshot cue is not score-promoted by the removed Booster path
    public void 총소리_단서는_없앤_Booster_경로로_올리지_않는다()
    {
        var probabilities = AiTestData.Probabilities((0, .6f), (2, .1f));

        var decision = YamnetSafetyCueDecision.Decide(Names, Classifier.Classify(probabilities));

        Assert.Equal(SoundType.Ambient, decision.PostCoarse);
        Assert.True(decision.HasGunshotCue);
        Assert.False(decision.DangerCuePromoted);
    }

    [Fact] // gunshot cue does not veto an independent non-gunshot cue at the policy floor
    public void 총소리_후보가_있어도_기준_확률의_다른_단서는_막지_않는다()
    {
        var probabilities = AiTestData.Probabilities((0, .6f), (2, .2f), (1, .05f), (4, .04f), (5, .03f));

        var decision = YamnetSafetyCueDecision.Decide(Names, Classifier.Classify(probabilities));

        Assert.True(decision.HasGunshotCue);
        Assert.Equal(SoundType.Danger, decision.PostCoarse);
        Assert.Equal("Siren", decision.PostDisplay);
        Assert.True(decision.DangerCuePromoted);
    }

    [Fact] // gunshot cue cannot lend its evidence to a weaker non-gunshot cue
    public void 총소리_단서의_확률을_더_약한_다른_단서에_빌려주지_않는다()
    {
        var probabilities = AiTestData.Probabilities((0, .6f), (2, .2f), (1, .049f), (4, .04f), (5, .03f));

        var decision = YamnetSafetyCueDecision.Decide(Names, Classifier.Classify(probabilities));

        Assert.True(decision.HasStrongDangerCue);
        Assert.True(decision.HasGunshotCue);
        Assert.Equal(SoundType.Ambient, decision.PostCoarse);
        Assert.False(decision.DangerCuePromoted);
    }

    [Fact] // speech result is not safety-promoted
    public void 대화음_결과는_안전_단서로_올리지_않는다()
    {
        var probabilities = AiTestData.Probabilities((3, .6f), (1, .1f));

        var decision = YamnetSafetyCueDecision.Decide(Names, Classifier.Classify(probabilities));

        Assert.Equal(SoundType.Speech, decision.PostCoarse);
        Assert.False(decision.DangerCuePromoted);
    }

    [Fact] // approved firearm change keeps speech silence and low-confidence guards
    public void 말소리_무음_낮은_확신도_가드는_그대로다()
    {
        var cases = new[] { ("Speech", .6f, .1f), ("Silence", .6f, .1f), ("Music", .119f, .1f) };
        Assert.All(cases, c =>
        {
            var (display, topProbability, sirenProbability) = c;
            var probabilities = AiTestData.Probabilities((0, topProbability), (2, .08f), (1, sirenProbability), (4, .04f), (5, .03f));

            var decision = DecideWith(0, display, probabilities);

            Assert.False(decision.DangerCuePromoted);
        });
    }

    [Fact] // approved attention cues are safety-promoted
    public void 승인한_경적과_깨짐_단서는_위협음으로_올린다()
    {
        var attentionCues = new[]
        {
            "Vehicle horn, car horn, honking",
            "Air horn, truck horn",
            "Burst, pop",
            "Boom",
            "Bang",
            "Smash, crash",
            "Breaking",
            "Shatter",
        };
        Assert.All(attentionCues, cue =>
        {
            var probabilities = AiTestData.Probabilities((0, .6f), (1, .2f), (4, .001f), (5, .001f), (6, .001f));

            var decision = DecideWith(1, cue, probabilities);

            Assert.Equal(SoundType.Danger, decision.PostCoarse);
            Assert.Equal(cue, decision.PostDisplay);
            Assert.True(decision.DangerCuePromoted);
        });
    }

    [Fact] // temporary proxies and partial label matches are not safety-promoted
    public void 옛_대리_라벨과_이름이_일부만_겹치는_소리는_올리지_않는다()
    {
        Assert.All(new[] { "Plop", "Gargling", "Breaking news" }, proxy =>
        {
            var probabilities = AiTestData.Probabilities((0, .6f), (1, .2f), (4, .001f), (5, .001f), (6, .001f));

            var decision = DecideWith(1, proxy, probabilities);

            Assert.Equal(SoundType.Ambient, decision.PostCoarse);
            Assert.False(decision.DangerCuePromoted);
        });
    }

    [Fact] // chainsaw and thunder labels do not bypass mapper vote through safety cue
    public void 전기톱과_천둥은_안전_단서로_투표를_건너뛰지_않는다()
    {
        Assert.All(new[] { "Chainsaw", "Thunder", "Thunderstorm" }, cue =>
        {
            var probabilities = AiTestData.Probabilities((0, .6f), (1, .2f), (4, .001f), (5, .001f), (6, .001f));

            var decision = DecideWith(1, cue, probabilities);

            Assert.Equal(SoundType.Ambient, decision.PostCoarse);
            Assert.False(decision.DangerCuePromoted);
        });
    }
}
