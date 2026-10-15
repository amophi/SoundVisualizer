// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using SoundVisualizer.Ai;
using SoundVisualizer.Diagnostics;
using SoundVisualizer.Tests.Ai;
using TopClassHit = SoundVisualizer.Ai.YamnetCoarseClassifier.TopClassHit;

namespace SoundVisualizer.Tests.Diagnostics;

/// <summary>
/// 개발자 모드 HUD 의 글자. 모바일 AiDebugTextTest 다섯 개를 옮기고, 모바일 문서(ARCHITECTURE 4장)가 고정한다고 적은 것
/// (모델 없음, 빈 확정 이름, 뒤로 간 시계, 오래된 나이의 짧은 표기, 한 줄 58자)과 PC 오버레이가 기대는 것(값 비교, 글자 종류)을 더했다.
/// </summary>
public class AiDebugTextTests
{
    private static AiClassificationResult Result(
        SoundType coarse = SoundType.Danger,
        string display = "Gunshot, gunfire",
        float confidence = 0.41f,
        bool meetsThreshold = true,
        long timestampMs = 1_000L,
        bool dangerCuePromoted = false,
        IReadOnlyList<TopClassHit>? top5 = null,
        double preprocessMs = 12.0,
        double yamnetMs = 48.0,
        double totalMs = 65.0) => new()
        {
            Coarse = coarse,
            Display = display,
            Confidence = confidence,
            Top5 = top5 ?? [],
            DangerCuePromoted = dangerCuePromoted,
            MeetsThreshold = meetsThreshold,
            TimestampMs = timestampMs,
            PreprocessMs = preprocessMs,
            YamnetMs = yamnetMs,
            TotalMs = totalMs,
        };

    private static AiDebugLines Format(AiClassificationResult? result, long nowMs = 1_200L) =>
        AiDebugText.Format(result, nowMs, level: 0.14f, shown: true, aiAvailable: true);

    [Fact] // 종류와 원래 이름과 확신도를 보여준다
    public void 종류와_원래_이름과_확신도를_보여준다()
    {
        var lines = Format(Result());
        Assert.Equal("DANGER", lines.Coarse);
        Assert.Equal("Gunshot, gunfire", lines.Label);
        Assert.Equal("0.41", lines.Confidence);
        Assert.Equal(SoundType.Danger, lines.ColorType);
    }

    [Fact] // 임계값 나이와 YAMNet safety cue를 보여준다
    public void 임계값_나이와_YAMNet_위험_단서를_보여준다()
    {
        var lines = Format(Result(meetsThreshold: false, dangerCuePromoted: true));
        Assert.Equal("thr N   age 0.2s", lines.Detail);
        Assert.Equal("cue Y", lines.Verdict);
        Assert.Equal("lvl 0.14   shown Y   65ms (12/48)", lines.Timing);
    }

    [Fact] // top5는 순위와 고정폭 확률로 보인다
    public void top5_는_순위와_고정폭_확률로_보인다()
    {
        var hit = new TopClassHit(0, "Vehicle horn, car horn, honking", 0.1f);
        string line = Assert.Single(Format(Result(top5: [hit])).Top5);
        Assert.Contains("Vehicle horn, car horn, h" + AiDebugText.Ellipsis, line);
        Assert.EndsWith(" 0.10", line);
    }

    [Fact] // 결과 없음을 구분한다
    public void 결과_없음을_구분한다()
    {
        var waiting = Format(null);
        Assert.Equal(AiDebugText.CoarseWaiting, waiting.Coarse);
        Assert.Null(waiting.ColorType);
    }

    [Fact] // 로캘과 무관하게 숫자를 표기한다
    public void 로캘과_무관하게_숫자를_표기한다()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar");
            Assert.NotEqual("0.41", 0.41f.ToString("F2", CultureInfo.CurrentCulture));
            Assert.Equal("0.41", Format(Result()).Confidence);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void 모델을_못_불러오면_AI_OFF_로_적고_소리_크기만_남긴다()
    {
        var off = AiDebugText.Format(null, nowMs: 0L, level: 0.5f, shown: false, aiAvailable: false);
        Assert.Equal(AiDebugText.CoarseAiOff, off.Coarse);
        Assert.Equal("model load failed", off.Label);
        Assert.Equal(AiDebugText.None, off.Confidence);
        Assert.Equal(AiDebugText.None, off.Detail);
        Assert.Equal(AiDebugText.None, off.Verdict);
        Assert.Equal("lvl 0.50", off.Timing);
        Assert.Empty(off.Top5);
        Assert.Equal("no result yet", Format(null).Label);
    }

    [Fact]
    public void 확정된_이름이_없으면_none_으로_적는다()
    {
        // 임계값을 한 번도 넘지 못하면 확정 이름이 빈 문자열이다. 빈 칸으로 두면 줄이 비어 보인다.
        Assert.Equal("(none)", Format(Result(display: "")).Label);
    }

    [Theory]
    [InlineData(5_000L, 1_000L, "0.0s")]   // 벽시계가 뒤로 갔다
    [InlineData(1_000L, 1_000L, "0.0s")]
    [InlineData(1_000L, 1_150L, "0.2s")]   // 모바일처럼 5 에서 올린다(.NET "F1" 은 0.1s)
    [InlineData(1_000L, 1_250L, "0.3s")]   // .NET "F1" 은 짝수 쪽 0.2s
    [InlineData(0L, 99_949L, "99.9s")]
    [InlineData(0L, 99_950L, "100s")]
    [InlineData(0L, 999_499L, "999s")]
    [InlineData(0L, 999_500L, "17m")]
    [InlineData(0L, 3_600_000L, "60m")]
    public void 나이는_오래될수록_짧게_쓰고_음수로_적지_않는다(long timestampMs, long nowMs, string age)
    {
        Assert.Equal($"thr Y   age {age}", Format(Result(timestampMs: timestampMs), nowMs).Detail);
    }

    [Fact]
    public void 소요시간은_정수_밀리초로_적고_값이_없으면_자리_표시를_쓴다()
    {
        var lines = AiDebugText.Format(
            Result(confidence: float.NaN, preprocessMs: 0.5, yamnetMs: double.NaN, totalMs: 12.5),
            nowMs: 1_200L, level: float.NaN, shown: false, aiAvailable: true);
        Assert.Equal(AiDebugText.None, lines.Confidence);
        Assert.Equal("lvl -   shown N   13ms (1/-)", lines.Timing);
    }

    [Fact]
    public void top5_는_다섯까지만_적고_이름_칸을_채워_확률이_한_열에_선다()
    {
        string[] names = ["Speech", "Vehicle horn, car horn, honking", "Alarm", "Livestock, farm animals, working animals", "Siren", "Music"];
        float[] probabilities = [0.5f, 0.45f, 0.4f, 0.35f, 0.3f, 0.25f];
        var hits = names.Select((name, i) => new TopClassHit(i, name, probabilities[i])).ToArray();

        var top5 = Format(Result(top5: hits)).Top5;

        Assert.Equal(
            new[]
            {
                "1 " + "Speech".PadRight(AiDebugText.Top5NameWidth) + " 0.50",
                "2 Vehicle horn, car horn, h" + AiDebugText.Ellipsis + " 0.45",
                "3 " + "Alarm".PadRight(AiDebugText.Top5NameWidth) + " 0.40",
                "4 Livestock, farm animals, " + AiDebugText.Ellipsis + " 0.35",
                "5 " + "Siren".PadRight(AiDebugText.Top5NameWidth) + " 0.30",
            },
            top5);
        Assert.All(top5, line => Assert.Equal(top5[0].Length, line.Length));
    }

    [Fact]
    public void 모든_줄이_HUD_한_줄_58자_안에_든다()
    {
        foreach (var lines in WorstCases())
        {
            // 첫 줄은 오버레이가 "종류 ● 이름 확신도" 로 붙인다. 점과 그 양옆 여백을 세 칸으로 센다(모바일 6dp + 8dp + 6dp).
            int header = lines.Coarse.Length + 3 + lines.Label.Length + 1 + lines.Confidence.Length;
            Assert.True(header <= AiDebugText.HudMaxColumns, $"{header}: {lines.Coarse} ● {lines.Label} {lines.Confidence}");
            foreach (string line in new[] { lines.Detail, lines.Verdict, lines.Timing }.Concat(lines.Top5))
            {
                Assert.True(line.Length <= AiDebugText.HudMaxColumns, $"{line.Length}: {line}");
            }
        }
    }

    [Fact]
    public void HUD_글자는_아스키와_말줄임표뿐이다()
    {
        // 고정폭 글꼴에 없는 글자가 섞이면 그 글자만 다른 글꼴로 그려져 줄이 다시 흔들린다(모바일 #162).
        // 아스키와 말줄임표만 쓰면 Windows 의 고정폭 글꼴 어느 것으로 그려도 된다.
        string text = string.Concat(WorstCases().Select(lines =>
            lines.Coarse + lines.Label + lines.Confidence + lines.Detail + lines.Verdict + lines.Timing + string.Concat(lines.Top5)));

        Assert.Contains(AiDebugText.Ellipsis, text);
        Assert.Empty(text.Where(c => !(c is >= ' ' and <= '~' || c == '…')).Distinct());
    }

    [Fact]
    public void 같은_입력이면_같은_줄로_본다()
    {
        // 오버레이는 지난번과 같은 줄이면 글자를 다시 넣지 않는다. top-5 목록도 내용으로 비교해야 한다.
        var a = Format(Result(top5: [new TopClassHit(1, "Speech", 0.3f)]));
        var b = Format(Result(top5: [new TopClassHit(1, "Speech", 0.3f)]));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Format(Result(top5: [new TopClassHit(1, "Speech", 0.4f)])));
        Assert.NotEqual(a, Format(Result(top5: [new TopClassHit(1, "Speech", 0.3f)]), nowMs: 1_400L));
    }

    /// <summary>
    /// 가장 긴 줄이 나오는 입력들. 가장 긴 종류 이름(AMBIENT), YAMNet 이름 521개 전부를 이름과 top-5 에, 다섯 글자가 되는 나이,
    /// 다섯 자리 소요시간. 결과 없음과 모델 없음도 넣는다.
    /// </summary>
    private static IEnumerable<AiDebugLines> WorstCases()
    {
        yield return AiDebugText.Format(null, 0L, 0.99f, shown: false, aiAvailable: true);
        yield return AiDebugText.Format(null, 0L, 0.99f, shown: false, aiAvailable: false);
        foreach (string name in AiTestData.ClassMap)
        {
            var hits = Enumerable.Range(0, 5).Select(i => new TopClassHit(i, name, 0.99f)).ToArray();
            var result = Result(SoundType.Ambient, name, 0.99f, timestampMs: 0L, top5: hits,
                preprocessMs: 99_999, yamnetMs: 99_999, totalMs: 99_999);
            yield return AiDebugText.Format(result, nowMs: 99_949L, level: 0.99f, shown: true, aiAvailable: true);
        }
    }
}
