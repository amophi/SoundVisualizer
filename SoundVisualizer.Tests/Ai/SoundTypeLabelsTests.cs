// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 소리 종류의 저장 문자열과, 모바일 AiLabelContractTest 를 옮긴 확인. 모바일은 라벨을 문자열로만 이어서 이름이 어긋나면
/// 위협음이 환경음처럼 그려졌다. C# 은 열거형이라 그 어긋남은 컴파일러가 막고, 남는 위험은 저장 문자열이 모바일과 다른 것이다.
/// </summary>
public class SoundTypeLabelsTests
{
    private static readonly (string Name, SoundType Expected)[] Representatives =
    {
        ("Gunshot, gunfire", SoundType.Danger),
        ("Speech", SoundType.Speech),
        ("Rain", SoundType.Ambient),
    };

    [Fact]
    public void 저장_문자열은_모바일_설정과_글자까지_같다()
    {
        Assert.Equal("ambient", SoundType.Ambient.ToLabel());
        Assert.Equal("speech", SoundType.Speech.ToLabel());
        Assert.Equal("danger", SoundType.Danger.ToLabel());
        Assert.Equal("ambient", SoundTypeLabels.Ambient);
        Assert.Equal("speech", SoundTypeLabels.Speech);
        Assert.Equal("danger", SoundTypeLabels.Danger);
        // 결과가 없을 때와 같은 뜻이 되게 초기값은 환경음이다.
        Assert.Equal(SoundType.Ambient, default);
    }

    [Fact]
    public void 저장_문자열은_정확히_같을_때만_읽는다()
    {
        Assert.All(Enum.GetValues<SoundType>(), type =>
        {
            Assert.True(SoundTypeLabels.TryParse(type.ToLabel(), out var parsed));
            Assert.Equal(type, parsed);
        });
        Assert.All(new[] { null, "", "Danger", "SPEECH", " ambient", "ambient ", "환경음", "threat" }, label =>
            Assert.False(SoundTypeLabels.TryParse(label, out _)));
    }

    [Fact]
    public void 정의되지_않은_값은_문자열로_바꾸지_않는다()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((SoundType)3).ToLabel());
    }

    [Fact] // YAMNet 모든 클래스 이름은 세 라벨 중 하나로 묶인다
    public void 모든_클래스_이름은_세_종류_중_하나로_묶인다()
    {
        var names = AiTestData.ClassMap;
        Assert.Equal(YamnetCoarseClassifier.NumClasses, names.Count);
        Assert.All(names, name => Assert.True(Enum.IsDefined(YamnetThreeClassMapper.MapDisplayNameToCoarse(name))));
        Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(null));
        Assert.Equal(SoundType.Ambient, YamnetThreeClassMapper.MapDisplayNameToCoarse(""));
    }

    [Fact] // 대표 소리는 화면과 진동이 기대하는 라벨 상수로 묶인다
    public void 대표_소리는_화면과_진동이_기대하는_종류로_묶인다()
    {
        var names = AiTestData.ClassMap;
        var classifier = new YamnetCoarseClassifier(names);
        Assert.All(Representatives, r =>
        {
            Assert.Equal(r.Expected, YamnetThreeClassMapper.MapDisplayNameToCoarse(r.Name));
            Assert.Equal(r.Expected, classifier.Classify(AiTestData.Probabilities((names.IndexOf(r.Name), 1f))).Coarse);
        });
    }

    [Fact] // 어떤 클래스가 1위여도 분류기 투표 결과는 세 라벨 중 하나다
    public void 어떤_클래스가_1위여도_투표_결과는_세_종류_중_하나다()
    {
        var classifier = new YamnetCoarseClassifier(AiTestData.ClassMap);
        for (int i = 0; i < YamnetCoarseClassifier.NumClasses; i++)
        {
            Assert.True(Enum.IsDefined(classifier.Classify(AiTestData.Probabilities((i, 1f))).Coarse));
        }
    }

    [Fact] // YAMNet gunshot top cue는 후처리까지 세 UI 라벨 중 하나다
    public void 음악에_섞인_총소리도_후처리까지_세_종류_중_하나다()
    {
        var names = AiTestData.ClassMap;
        var probs = AiTestData.Probabilities((names.IndexOf("Music"), .5f), (names.IndexOf("Gunshot, gunfire"), .45f));
        var pre = new YamnetCoarseClassifier(names).Classify(probs);
        Assert.True(Enum.IsDefined(pre.Coarse));

        var decision = YamnetSafetyCueDecision.Decide(names, pre);
        Assert.True(Enum.IsDefined(decision.PostCoarse));

        string topKSummary = string.Join(" | ", pre.Top5.Select(hit => hit.Name));
        var post = new AiPostProcessor().Process(new AiPostProcessor.FrameInput(
            decision.PostCoarse,
            decision.PostDisplay,
            decision.PostConfidence,
            DangerCuePromoted: decision.DangerCuePromoted,
            HasStrongDangerCue: decision.HasStrongDangerCue,
            HasCriticalDangerCue: AiPostProcessor.IsCriticalDangerEvent(decision.PostDisplay, topKSummary),
            TopKSummary: topKSummary));
        Assert.True(Enum.IsDefined(post.UiCoarse));
    }

    [Fact] // 후처리가 화면에 넘기는 라벨은 항상 세 상수 중 하나다
    public void 후처리는_환경음에서_시작해_종류마다_넘어가고_Reset_하면_처음으로_돌아간다()
    {
        // 입력 종류도 매퍼에서 받는다. 상수를 넣으면 후처리가 받은 값을 그대로 돌려줘서 어긋남이 가려진다.
        static AiPostProcessor.FrameInput Frame(string name, float confidence) => new(
            YamnetThreeClassMapper.MapDisplayNameToCoarse(name),
            name,
            confidence,
            HasStrongDangerCue: YamnetSafetyCueDecision.IsStrongDangerKeyword(name),
            HasCriticalDangerCue: AiPostProcessor.IsCriticalDangerKeyword(name));

        var pp = new AiPostProcessor();

        // 아직 기준을 넘은 결과가 없으면 처음 라벨이 그대로 화면에 간다.
        Assert.Equal(SoundType.Ambient, pp.Process(Frame("Speech", 0f)).UiCoarse);

        // 위협음 → 대화음 → 환경음 → 위협음 순서로 다섯 프레임씩 흘려, 히스테리시스가 넘어간 뒤의 라벨을 본다.
        foreach (var (name, expected) in Representatives.Append(Representatives[0]))
        {
            var results = Enumerable.Range(0, 5).Select(_ => pp.Process(Frame(name, .9f))).ToList();
            Assert.All(results, r =>
            {
                Assert.True(Enum.IsDefined(r.UiCoarse));
                Assert.True(Enum.IsDefined(r.ConfirmedCoarse));
            });
            Assert.Equal(expected, results[^1].UiCoarse);
        }

        // 캡처를 다시 시작하면(Reset) 처음 라벨로 돌아간다.
        pp.Reset();
        Assert.Equal(SoundType.Ambient, pp.Process(Frame("Gunshot, gunfire", 0f)).UiCoarse);
    }
}
