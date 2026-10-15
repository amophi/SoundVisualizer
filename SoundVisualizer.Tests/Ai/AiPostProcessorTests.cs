// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.Json;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>모바일 AiPostProcessorTest 를 옮기고, 히스테리시스 골든(yamnet_cp6_meta.json)과 기준 경계를 더했다.</summary>
public class AiPostProcessorTests
{
    private static AiPostProcessor.FrameInput Frame(SoundType coarse, string display, float confidence,
        bool promoted = false, bool strong = false, bool critical = false) =>
        new(coarse, display, confidence, DangerCuePromoted: promoted, HasStrongDangerCue: strong, HasCriticalDangerCue: critical);

    [Fact] // speech requires two frames
    public void 대화음은_두_프레임이_필요하다()
    {
        var pp = new AiPostProcessor();
        pp.Process(Frame(SoundType.Ambient, "Wind", .4f));
        Assert.Equal(SoundType.Ambient, pp.Process(Frame(SoundType.Speech, "Speech", .4f)).ConfirmedCoarse);
        Assert.Equal(SoundType.Speech, pp.Process(Frame(SoundType.Speech, "Speech", .4f)).ConfirmedCoarse);
    }

    [Fact] // danger switches immediately at configured confidence
    public void 위협음은_정한_확신도에서_바로_바뀐다()
    {
        var result = new AiPostProcessor().Process(Frame(SoundType.Danger, "Siren", .28f));
        Assert.Equal(SoundType.Danger, result.UiCoarse);
    }

    [Fact] // promoted strong cue uses documented threshold
    public void 안전_단서로_올린_위협음은_문서의_기준을_쓴다()
    {
        var pp = new AiPostProcessor();
        Assert.False(pp.Process(Frame(SoundType.Danger, "Smoke detector, smoke alarm", .119f, promoted: true, strong: true)).MeetsThreshold);

        var confirmed = pp.Process(Frame(SoundType.Danger, "Smoke detector, smoke alarm", .12f, promoted: true, strong: true));

        Assert.True(confirmed.MeetsThreshold);
        Assert.Equal(SoundType.Danger, confirmed.UiCoarse);
    }

    [Fact] // thresholds have no Booster-specific path
    public void 기준에_Booster_전용_경로가_없다()
    {
        Assert.Equal(.25f, AiPostProcessor.ComputeEffectiveThreshold(SoundType.Danger, .25f, false, false));
        Assert.Equal(.20f, AiPostProcessor.ComputeEffectiveThreshold(SoundType.Danger, .25f, true, false));
        Assert.Equal(.12f, AiPostProcessor.ComputeEffectiveThreshold(SoundType.Danger, .25f, true, false, true));
    }

    [Fact] // water remains ambient
    public void 물소리는_환경음으로_남는다()
    {
        var result = new AiPostProcessor().Process(Frame(SoundType.Ambient, "Rain", 1f));
        Assert.Equal(SoundType.Ambient, result.UiCoarse);
    }

    // 아래는 모바일 JVM 테스트에 없는 확인이다.

    /// <summary>
    /// Python 기준 구현이 만든 후처리 골든. 모바일은 Booster 를 없앤 뒤 이 파일을 더는 돌리지 않는다. Booster 입력이 든 사례는
    /// 없앤 기준(0.18)을 기대하므로 빼고, 나머지 8개 사례는 지금 규칙과 프레임마다 모든 값이 같아야 한다.
    /// </summary>
    [Fact]
    public void Booster_없는_히스테리시스_골든_사례와_프레임마다_같다()
    {
        var cases = AiTestData.LoadJson("yamnet_cp6_meta.json").EnumerateObject()
            .Where(c => c.Value.GetProperty("frames").EnumerateArray()
                .All(f => !f.GetProperty("input").GetProperty("adopted_danger_from_booster").GetBoolean()))
            .ToList();
        Assert.Equal(
            new[] { "A_stable_ambient", "B_ambient_to_speech", "C_ambient_to_danger_streak1", "D_immediate_danger_conf",
                "F_low_confidence", "G_candidate_reset", "H_danger_exit", "H2_danger_exit_needs_two" },
            cases.Select(c => c.Name));

        Assert.All(cases, c =>
        {
            var pp = new AiPostProcessor();
            foreach (var frame in c.Value.GetProperty("frames").EnumerateArray())
            {
                var input = frame.GetProperty("input");
                Assert.True(SoundTypeLabels.TryParse(input.GetProperty("coarse").GetString(), out var coarse));
                var actual = pp.Process(new AiPostProcessor.FrameInput(
                    coarse,
                    input.GetProperty("display").GetString()!,
                    (float)input.GetProperty("confidence").GetDouble(),
                    HasStrongDangerCue: input.GetProperty("has_strong_danger_cue").GetBoolean(),
                    HasCriticalDangerCue: input.GetProperty("has_critical_danger_cue").GetBoolean(),
                    TopKSummary: input.GetProperty("top_k_summary").GetString()!));

                string at = $"{c.Name} frame {frame.GetProperty("frame").GetInt32()}\nexpected {frame.GetRawText()}\nactual {actual}";
                Assert.True((float)frame.GetProperty("effective_threshold").GetDouble() == actual.EffectiveThreshold, at);
                Assert.True(frame.GetProperty("meets_threshold").GetBoolean() == actual.MeetsThreshold, at);
                Assert.True(frame.GetProperty("candidate_coarse").GetString() == (actual.CandidateCoarse?.ToLabel() ?? ""), at);
                Assert.True(frame.GetProperty("candidate_streak").GetInt32() == actual.CandidateStreak, at);
                Assert.True(frame.GetProperty("confirmed_coarse").GetString() == actual.ConfirmedCoarse.ToLabel(), at);
                Assert.True(frame.GetProperty("confirmed_display").GetString() == actual.ConfirmedDisplay, at);
                Assert.True((float)frame.GetProperty("confirmed_confidence").GetDouble() == actual.ConfirmedConfidence, at);
                Assert.True(frame.GetProperty("ui_coarse").GetString() == actual.UiCoarse.ToLabel(), at);
                Assert.True(frame.GetProperty("ui_display").GetString() == actual.UiDisplay, at);
                Assert.True((float)frame.GetProperty("ui_confidence").GetDouble() == actual.UiConfidence, at);
            }
        });
    }

    [Fact]
    public void 기준을_못_넘은_프레임은_확정_라벨을_건드리지_않는다()
    {
        var pp = new AiPostProcessor();
        pp.Process(Frame(SoundType.Speech, "Speech", .4f));
        pp.Process(Frame(SoundType.Speech, "Speech", .4f));

        var low = pp.Process(Frame(SoundType.Danger, "Siren", .19f));

        Assert.False(low.MeetsThreshold);
        Assert.Equal(SoundType.Speech, low.ConfirmedCoarse);
        Assert.Equal("Speech", low.ConfirmedDisplay);
        Assert.Equal(.4f, low.ConfirmedConfidence);
        Assert.Null(low.CandidateCoarse);
    }

    [Fact]
    public void 기준_경계값()
    {
        var pp = new AiPostProcessor();
        // 대화음 0.25 미만은 못 넘고, 0.25 부터 두 번 이어지면 확정한다.
        Assert.False(pp.Process(Frame(SoundType.Speech, "Speech", .2499f)).MeetsThreshold);
        var first = pp.Process(Frame(SoundType.Speech, "Speech", .25f));
        Assert.True(first.MeetsThreshold);
        Assert.Equal(SoundType.Speech, first.CandidateCoarse);
        Assert.Equal(1, first.CandidateStreak);
        Assert.Equal(SoundType.Speech, pp.Process(Frame(SoundType.Speech, "Speech", .25f)).ConfirmedCoarse);

        // 강한 단서가 있는 위협음은 0.20 부터, 첫 프레임에 확정한다.
        Assert.Equal(SoundType.Speech, pp.Process(Frame(SoundType.Danger, "Siren", .1999f, strong: true)).ConfirmedCoarse);
        Assert.Equal(SoundType.Danger, pp.Process(Frame(SoundType.Danger, "Siren", .2f, strong: true)).ConfirmedCoarse);

        // 위협음에서 벗어날 때 0.27 미만이면 두 프레임, 0.27 이상이면 한 프레임.
        Assert.Equal(SoundType.Danger, pp.Process(Frame(SoundType.Ambient, "Wind", .2699f)).ConfirmedCoarse);
        Assert.Equal(SoundType.Ambient, pp.Process(Frame(SoundType.Ambient, "Wind", .2699f)).ConfirmedCoarse);
        Assert.Equal(SoundType.Danger, pp.Process(Frame(SoundType.Danger, "Gunshot, gunfire", .4f, critical: true)).ConfirmedCoarse);
        Assert.Equal(SoundType.Ambient, pp.Process(Frame(SoundType.Ambient, "Wind", .27f)).ConfirmedCoarse);
    }

    [Fact]
    public void Reset_은_처음_상태로_돌린다()
    {
        var pp = new AiPostProcessor();
        pp.Process(Frame(SoundType.Speech, "Speech", .4f));
        pp.Process(Frame(SoundType.Speech, "Speech", .4f));
        var pending = pp.Process(Frame(SoundType.Ambient, "Wind", .4f));
        Assert.Equal(SoundType.Speech, pending.ConfirmedCoarse);
        Assert.Equal(SoundType.Ambient, pending.CandidateCoarse);

        pp.Reset();
        var afterReset = pp.Process(Frame(SoundType.Speech, "Speech", 0f));

        Assert.Equal(SoundType.Ambient, afterReset.ConfirmedCoarse);
        Assert.Equal("", afterReset.ConfirmedDisplay);
        Assert.Equal(0f, afterReset.ConfirmedConfidence);
        Assert.Null(afterReset.CandidateCoarse);
        Assert.Equal(0, afterReset.CandidateStreak);
    }

    [Fact]
    public void 이름_없이_만든_입력도_확정_이름은_null_이_되지_않는다()
    {
        // record struct 의 매개변수 없는 생성자는 이름을 null 로 둔다. 화면이 확정 이름을 그대로 쓰므로 빈 문자열로 받는다.
        var result = new AiPostProcessor().Process(default(AiPostProcessor.FrameInput) with { Confidence = 1f });

        Assert.True(result.MeetsThreshold);
        Assert.Equal("", result.ConfirmedDisplay);
    }
}
