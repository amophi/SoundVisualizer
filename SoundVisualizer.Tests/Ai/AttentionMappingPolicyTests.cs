// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>모바일 AttentionMappingPolicyTest 를 옮겼다. 제품 정책 #343 이지 샘플에 맞춘 감지 규칙이 아니다.</summary>
public class AttentionMappingPolicyTests
{
    private static readonly string[] Attention =
    {
        "Reversing beeps", "Train horn", "Train whistle", "Foghorn", "Bicycle bell",
        "Emergency vehicle", "Skidding", "Tire squeal", "Slam",
    };

    [Fact] // approvedLabelsVoteDangerWithoutBecomingSafetyCues
    public void 승인한_주의_신호는_위협음으로_투표하지만_안전_단서는_아니다()
    {
        Assert.All(Attention, name =>
        {
            Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse(name));
            Assert.False(YamnetSafetyCueDecision.IsStrongDangerKeyword(name));
            Assert.False(AiPostProcessor.IsCriticalDangerKeyword(name));
        });
    }

    [Fact] // instrumentsAndUnapprovedNeighborsStayAmbient
    public void 악기와_승인하지_않은_비슷한_소리는_환경음()
    {
        var ambient = new[]
        {
            "Singing bowl", "Truck", "Car", "Bus", "Motorcycle", "Train", "Vehicle",
            "Air brake", "Doorbell", "Alarm clock", "Honk", "French horn", "Whistle",
            "Plop", "Gargling",
        }.Concat(Attention.Select(name => name + "-like sound"));
        Assert.All(ambient, name => Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));

        // #345 는 고함과 울음까지 사람이 내는 소리를 대화음으로 넓혔다.
        Assert.All(new[] { "Singing", "Choir", "Screaming", "Shout", "Laughter", "Crying, sobbing", "Yell", "Baby cry, infant cry" }, name =>
            Assert.Equal(SoundType.Speech, YamnetThreeClassMapper.MapDisplayNameToCoarse(name)));
    }

    [Fact] // weakAttentionCandidateDoesNotPromoteAmbientVote
    public void 약한_주의_신호_후보는_환경음_투표를_올리지_않는다()
    {
        var names = new YamnetClassMap(AiTestData.Names("class_", "Wind", "Rain", "Water", "Slam", "Vehicle"));
        var probabilities = AiTestData.Probabilities((0, .55f), (1, .2f), (2, .15f), (3, .06f), (4, .04f));

        var pre = new YamnetCoarseClassifier(names).Classify(probabilities);
        var result = YamnetSafetyCueDecision.Decide(names, pre);

        Assert.Equal(SoundType.Ambient, result.PostCoarse);
        Assert.False(result.DangerCuePromoted);
    }
}
