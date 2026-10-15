// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text;
using SoundVisualizer.Ai;
using SoundVisualizer.Diagnostics;
using TopClassHit = SoundVisualizer.Ai.YamnetCoarseClassifier.TopClassHit;

namespace SoundVisualizer.Tests.Diagnostics;

/// <summary>
/// 개발자 모드 기록의 CSV 한 줄(모바일 AiDebugCsvTest, #193).
///
/// 고정하는 것은 표가 어긋나는 경우들이다. 이름에 쉼표가 들어가는 것(YAMNet 은 "Gunshot, gunfire" 처럼 쉼표가 든 이름을 쓴다),
/// 로캘이 바뀌어 숫자가 달라지는 것, 값이 없는데 NaN 이 찍히는 것. PC 는 두 판의 기록을 같은 스크립트로 읽도록 머리글과 한 줄 전체를
/// 모바일 규칙으로 만든 글자와 맞춰 본다. capture_source 는 늘 Loopback 이다.
/// </summary>
public class AiDebugCsvTests
{
    private static AiClassificationResult Result(
        SoundType coarse = SoundType.Danger,
        string display = "Gunshot, gunfire",
        float confidence = 0.54321f,
        IReadOnlyList<TopClassHit>? top5 = null,
        long timestampMs = 1_000L,
        double preprocessMs = 3.21,
        double yamnetMs = 41.0,
        double totalMs = 46.21) => new()
        {
            Coarse = coarse,
            Display = display,
            Confidence = confidence,
            Top5 = top5 ?? [new TopClassHit(1, "Gunshot, gunfire", 0.54321f), new TopClassHit(2, "Speech", 0.1f)],
            DangerCuePromoted = false,
            MeetsThreshold = true,
            TimestampMs = timestampMs,
            PreprocessMs = preprocessMs,
            YamnetMs = yamnetMs,
            TotalMs = totalMs,
        };

    /// <summary>칸 수를 맞춰 세려면 따옴표 안의 쉼표를 빼고 세야 한다. 아주 작은 CSV 파서.</summary>
    private static List<string> Cells(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                cell.Append('"');
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }
        cells.Add(cell.ToString());
        return cells;
    }

    private static int Column(string name) => AiDebugCsv.Columns.ToList().IndexOf(name);

    [Fact] // 머리글과 줄의 칸 수가 같다
    public void 머리글과_줄의_칸_수가_같다()
    {
        var header = Cells(AiDebugCsv.Header);
        var row = Cells(AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0.4f, shown: true));
        Assert.Equal(header.Count, row.Count);
        Assert.Equal(AiDebugCsv.Columns, header);
    }

    [Fact]
    public void 머리글은_모바일과_칸_이름과_순서가_같다()
    {
        // 모바일 AiDebugCsv.COLUMNS 를 그대로 적었다. 두 판의 기록을 같은 스크립트로 읽는다.
        Assert.Equal(
            "time_ms,result_ms,age_ms,capture_source,coarse,display,confidence,meets_threshold,danger_cue_promoted," +
            "top1_name,top1_prob,top2_name,top2_prob,top3_name,top3_prob,top4_name,top4_prob,top5_name,top5_prob," +
            "level,shown,preprocess_ms,yamnet_ms,total_ms,mapping_override_count,mapping_signature",
            AiDebugCsv.Header);
    }

    [Fact]
    public void 한_줄의_모든_칸이_모바일_규칙의_글자와_같다()
    {
        // 모든 칸을 따옴표로 감싸고, 참거짓은 소문자, 종류는 저장 문자열, 확률은 다섯 자리, 소요시간은 한 자리다.
        Assert.Equal(
            "\"1500\",\"1000\",\"500\",\"Loopback\",\"danger\",\"Gunshot, gunfire\",\"0.54321\",\"true\",\"false\"," +
            "\"Gunshot, gunfire\",\"0.54321\",\"Speech\",\"0.10000\",\"\",\"\",\"\",\"\",\"\",\"\"," +
            "\"0.40000\",\"true\",\"3.2\",\"41.0\",\"46.2\",\"0\",\"default\"",
            AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0.4f, shown: true));
    }

    [Fact] // 쉼표가 든 이름이 칸을 밀지 않는다
    public void 쉼표가_든_이름이_칸을_밀지_않는다()
    {
        var parsed = Cells(AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0.4f, shown: true));
        Assert.Equal("Gunshot, gunfire", parsed[Column("display")]);
        Assert.Equal("Gunshot, gunfire", parsed[Column("top1_name")]);
        Assert.Equal(Cells(AiDebugCsv.Header).Count, parsed.Count);
    }

    [Fact] // 이름에 든 따옴표도 살아남는다
    public void 이름에_든_따옴표도_살아남는다()
    {
        var parsed = Cells(AiDebugCsv.Row(Result(display: "he said \"stop\""), nowMs: 1_500L, level: 0.4f, shown: true));
        Assert.Equal("he said \"stop\"", parsed[Column("display")]);
        Assert.Equal(Cells(AiDebugCsv.Header).Count, parsed.Count);
    }

    [Fact] // 모델이 덜 내놓으면 남은 top 칸은 비운다
    public void 모델이_덜_내놓으면_남은_top_칸은_비운다()
    {
        var parsed = Cells(AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0f, shown: false));
        Assert.Equal("Speech", parsed[Column("top2_name")]);
        Assert.Equal(AiDebugCsv.None, parsed[Column("top3_name")]);
        Assert.Equal(AiDebugCsv.None, parsed[Column("top5_prob")]);
    }

    [Fact] // 언어를 바꿔도 숫자 서식이 같다
    public void 언어를_바꿔도_숫자_서식이_같다()
    {
        // 아랍어 로캘은 소수점(٫)과 음수 표기가 다르다. 기본 서식을 쓰면 표가 읽히지 않는다.
        string us = AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0.4f, shown: true);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-EG");
            Assert.NotEqual("0.54321", 0.54321f.ToString("F5", CultureInfo.CurrentCulture));
            string arabic = AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0.4f, shown: true);
            Assert.Equal(us, arabic);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
        Assert.Contains("0.54321", us); // 확신도가 0.54321 로 적히지 않았다
    }

    [Fact] // 나이는 음수로 적지 않는다
    public void 나이는_음수로_적지_않는다()
    {
        // 벽시계는 뒤로 튈 수 있다.
        var parsed = Cells(AiDebugCsv.Row(Result(timestampMs: 5_000L), nowMs: 1_000L, level: 0f, shown: false));
        Assert.Equal("0", parsed[Column("age_ms")]);
    }

    [Fact] // 캡처 소스를 행에 기록한다
    public void 캡처_소스는_PC_에서_늘_Loopback_이다()
    {
        // 모바일은 실행마다 InternalPlayback·Microphone 을 적는다. PC 는 시스템 소리(루프백)만 받는다.
        var parsed = Cells(AiDebugCsv.Row(Result(), nowMs: 1_500L, level: 0.4f, shown: true));
        Assert.Equal("Loopback", parsed[Column("capture_source")]);
        Assert.Equal(AiDebugCsv.CaptureSource, parsed[Column("capture_source")]);
    }

    [Fact] // 매핑 설정은 추론 결과 스냅샷으로 기록한다
    public void 매핑_설정은_추론_결과_스냅샷으로_기록한다()
    {
        var baseline = Result();
        var mapped = new AiClassificationResult
        {
            Coarse = baseline.Coarse,
            Display = baseline.Display,
            Confidence = baseline.Confidence,
            Top5 = baseline.Top5,
            MeetsThreshold = baseline.MeetsThreshold,
            TimestampMs = baseline.TimestampMs,
            PreprocessMs = baseline.PreprocessMs,
            YamnetMs = baseline.YamnetMs,
            TotalMs = baseline.TotalMs,
            MappingOverrideCount = 1,
            MappingSignature = "Siren=ambient",
        };

        var parsed = Cells(AiDebugCsv.Row(mapped, 1_500L, .4f, true));
        Assert.Equal("1", parsed[Column("mapping_override_count")]);
        Assert.Equal("Siren=ambient", parsed[Column("mapping_signature")]);
        var plain = Cells(AiDebugCsv.Row(baseline, 1_500L, .4f, true));
        Assert.Equal("0", plain[Column("mapping_override_count")]);
        Assert.Equal("default", plain[Column("mapping_signature")]);
    }

    [Fact]
    public void 소요시간은_모바일처럼_5에서_올린다()
    {
        // .NET "F1" 이면 0.2·0.1·12.3 이 된다. 같은 결과를 두 판이 다르게 적지 않게 한다.
        var parsed = Cells(AiDebugCsv.Row(Result(preprocessMs: 0.25, yamnetMs: 0.15, totalMs: 12.35), 1_500L, 0.4f, true));
        Assert.Equal("0.3", parsed[Column("preprocess_ms")]);
        Assert.Equal("0.2", parsed[Column("yamnet_ms")]);
        Assert.Equal("12.4", parsed[Column("total_ms")]);
    }

    [Fact]
    public void 값이_없는_숫자는_빈_칸으로_둔다()
    {
        var parsed = Cells(AiDebugCsv.Row(Result(confidence: float.NaN, totalMs: double.NaN), 1_500L, float.NaN, true));
        Assert.Equal(AiDebugCsv.None, parsed[Column("confidence")]);
        Assert.Equal(AiDebugCsv.None, parsed[Column("level")]);
        Assert.Equal(AiDebugCsv.None, parsed[Column("total_ms")]);
        Assert.Equal("3.2", parsed[Column("preprocess_ms")]);
    }
}
