// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// C# 라벨 목록과 규칙이 모바일과 같은 이름을 같은 종류로 묶는지. 모바일 저장소를 읽지 않도록, 모바일 규칙을 실제 521개 이름에
/// 적용한 결과(위협음 37, 대화음 33, 환경음 451 — 모바일 tools/ai_reference/mapper.py 결과와도 같다)를 여기에 그대로 적어 둔다.
/// 규칙을 바꾸면 이 목록도 모바일과 함께 바꾼다.
/// </summary>
public class YamnetLabelListTests
{
    // 클래스 목록 순서다.
    private static readonly string[] ExpectedDanger =
    {
        "Bicycle bell", "Thunderstorm", "Thunder", "Vehicle horn, car horn, honking", "Car alarm",
        "Skidding", "Tire squeal", "Air horn, truck horn", "Reversing beeps", "Emergency vehicle",
        "Police car (siren)", "Ambulance (siren)", "Fire engine, fire truck (siren)", "Train whistle", "Train horn",
        "Chainsaw", "Slam", "Alarm", "Siren", "Civil defense siren",
        "Smoke detector, smoke alarm", "Fire alarm", "Foghorn", "Explosion", "Gunshot, gunfire",
        "Machine gun", "Fusillade", "Artillery fire", "Cap gun", "Fireworks",
        "Firecracker", "Burst, pop", "Boom", "Shatter", "Bang",
        "Smash, crash", "Breaking",
    };

    private static readonly string[] ExpectedSpeech =
    {
        "Speech", "Child speech, kid speaking", "Conversation", "Narration, monologue", "Babbling",
        "Speech synthesizer", "Shout", "Bellow", "Whoop", "Yell",
        "Children shouting", "Screaming", "Whispering", "Laughter", "Baby laughter",
        "Giggle", "Snicker", "Belly laugh", "Chuckle, chortle", "Crying, sobbing",
        "Baby cry, infant cry", "Whimper", "Wail, moan", "Singing", "Choir",
        "Child singing", "Synthetic singing", "Rapping", "Cheering", "Chatter",
        "Crowd", "Hubbub, speech noise, speech babble", "Children playing",
    };

    private static List<string> NamesMappedTo(SoundType type) =>
        AiTestData.ClassMap.Where(name => YamnetThreeClassMapper.MapDisplayNameToCoarse(name) == type).ToList();

    [Fact]
    public void 실제_클래스는_위협음_37_대화음_33_환경음_451로_나뉜다()
    {
        Assert.Equal(37, NamesMappedTo(SoundType.Danger).Count);
        Assert.Equal(33, NamesMappedTo(SoundType.Speech).Count);
        Assert.Equal(451, NamesMappedTo(SoundType.Ambient).Count);
    }

    [Fact]
    public void 위협음으로_묶이는_이름은_모바일과_같다()
    {
        Assert.Equal(ExpectedDanger, NamesMappedTo(SoundType.Danger));
    }

    [Fact]
    public void 대화음으로_묶이는_이름은_모바일과_같다()
    {
        Assert.Equal(ExpectedSpeech, NamesMappedTo(SoundType.Speech));
    }

    [Fact]
    public void 정확한_이름_목록은_개수가_같고_모두_실제_클래스_이름이다()
    {
        // 목록은 소문자로 비교하므로 실제 이름을 소문자로 바꿔 찾는다. 한 글자라도 틀리면 그 라벨은 조용히 규칙에서 빠진다.
        var lowered = AiTestData.ClassMap.Select(name => name.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(9, YamnetThreeClassMapper.AttentionLabels.Count);
        Assert.Equal(11, YamnetThreeClassMapper.HumanVocalizationLabels.Count);
        Assert.Equal(8, YamnetSafetyCueDecision.ApprovedAttentionCueLabels.Count);
        Assert.All(
            YamnetThreeClassMapper.AttentionLabels
                .Concat(YamnetThreeClassMapper.HumanVocalizationLabels)
                .Concat(YamnetSafetyCueDecision.ApprovedAttentionCueLabels),
            label => Assert.Contains(label, lowered));
    }

    [Fact]
    public void 목록의_라벨은_정한_종류로_묶인다()
    {
        Assert.All(YamnetThreeClassMapper.AttentionLabels, label =>
            Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse(label)));
        Assert.All(YamnetThreeClassMapper.HumanVocalizationLabels, label =>
            Assert.Equal(SoundType.Speech, YamnetThreeClassMapper.MapDisplayNameToCoarse(label)));
        // 안전 단서로 승인한 라벨은 매퍼가 이미 위협음으로 보는 것만이다.
        Assert.All(YamnetSafetyCueDecision.ApprovedAttentionCueLabels, label =>
        {
            Assert.Equal(SoundType.Danger, YamnetThreeClassMapper.MapDisplayNameToCoarse(label));
            Assert.True(YamnetSafetyCueDecision.IsStrongDangerKeyword(label));
        });
    }

    [Fact]
    public void 미리_계산한_값은_문자열_함수_결과와_같다()
    {
        var map = AiTestData.ClassMap;
        for (int i = 0; i < map.Count; i++)
        {
            Assert.Equal(YamnetThreeClassMapper.MapDisplayNameToCoarse(map[i]), map.DefaultCoarse(i));
            Assert.Equal(YamnetClassMap.ComputeTraits(map[i]), map.TraitsAt(i));
        }
        Assert.Equal(
            Enumerable.Range(0, map.Count).Where(i => map.DefaultCoarse(i) == SoundType.Danger),
            map.DefaultDangerIndices.ToArray());
    }
}
