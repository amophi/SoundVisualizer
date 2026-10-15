// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Diagnostics;

namespace SoundVisualizer.Tests.Diagnostics;

/// <summary>
/// 개발자 모드 기록의 쌓는 규칙(모바일 AiDebugLogWriterTest, #193).
///
/// 세 가지를 고정한다. 머리글이 한 번만 나가는 것, 같은 결과를 두 번 적지 않는 것(HUD 는 추론 250ms 보다 자주 읽는다), 상한에 닿으면
/// 멈추는 것. PC 는 줄 끝이 모바일 파일과 같은 \n 인 것과, 상한을 어떻게 세는지를 더했다.
/// </summary>
public class AiDebugLogWriterTests
{
    private static AiClassificationResult Result(long timestampMs, SoundType coarse = SoundType.Ambient) => new()
    {
        Coarse = coarse,
        Display = "Speech",
        Confidence = 0.5f,
        Top5 = [],
        DangerCuePromoted = false,
        MeetsThreshold = true,
        TimestampMs = timestampMs,
        PreprocessMs = 1.0,
        YamnetMs = 2.0,
        TotalMs = 3.0,
    };

    private static string[] Lines(StringWriter sink) => sink.ToString().Trim().Split('\n');

    [Fact] // 머리글은 첫 줄과 함께 한 번만 나간다
    public void 머리글은_첫_줄과_함께_한_번만_나간다()
    {
        var sink = new StringWriter();
        var writer = new AiDebugLogWriter(sink);
        Assert.True(writer.Write(Result(1_000L), 1_000L, 0.1f, true));
        Assert.True(writer.Write(Result(1_250L), 1_250L, 0.1f, true));

        var lines = Lines(sink);
        Assert.Equal(3, lines.Length);
        Assert.Equal(AiDebugCsv.Header, lines[0]);
        Assert.Single(lines, line => line == AiDebugCsv.Header);
        Assert.Equal(2, writer.Rows);
    }

    [Fact] // 결과가 없으면 아무것도 쓰지 않는다
    public void 결과가_없으면_아무것도_쓰지_않는다()
    {
        // 기록만 켜고 소리가 없으면 빈 파일이어야 한다. 머리글만 남은 파일은 "돌았는데 결과가 없었다" 와 "켜지지 않았다" 를 헷갈리게 한다.
        var sink = new StringWriter();
        _ = new AiDebugLogWriter(sink);
        Assert.Equal("", sink.ToString());
    }

    [Fact] // 같은 결과를 두 번 읽으면 한 번만 적는다
    public void 같은_결과를_두_번_읽으면_한_번만_적는다()
    {
        var sink = new StringWriter();
        var writer = new AiDebugLogWriter(sink);
        var same = Result(2_000L);
        Assert.True(writer.Write(same, 2_000L, 0.1f, true));
        Assert.False(writer.Write(same, 2_100L, 0.1f, true)); // 같은 추론 결과가 두 줄이 됐다
        Assert.False(writer.Write(Result(2_000L), 2_200L, 0.1f, true));
        Assert.True(writer.Write(Result(2_250L), 2_250L, 0.1f, true));
        Assert.Equal(2, writer.Rows);
    }

    [Fact] // 상한에 닿으면 멈추고 더 쓰지 않는다
    public void 상한에_닿으면_멈추고_더_쓰지_않는다()
    {
        var sink = new StringWriter();
        // 머리글 + 한 줄이면 이미 넘는 아주 작은 상한.
        var writer = new AiDebugLogWriter(sink, maxBytes: 10L);
        Assert.True(writer.Write(Result(1_000L), 1_000L, 0f, false));
        Assert.True(writer.Stopped); // 상한에 닿았는데 멈추지 않았다
        Assert.False(writer.Write(Result(1_250L), 1_250L, 0f, false));
        Assert.Equal(1, writer.Rows);
        Assert.Equal(2, Lines(sink).Length);
    }

    [Fact]
    public void 상한은_줄_끝까지_센_글자_수로_보고_그_줄까지_쓰고_멈춘다()
    {
        Assert.Equal(20L * 1024 * 1024, AiDebugLogWriter.DefaultMaxBytes);

        // 머리글과 두 줄이 딱 들어가는 상한. 세 번째 줄은 쓰지 않는다.
        long twoRows = AiDebugCsv.Header.Length + 1
            + AiDebugCsv.Row(Result(1_000L), 1_000L, 0f, false).Length + 1
            + AiDebugCsv.Row(Result(1_250L), 1_250L, 0f, false).Length + 1;
        var sink = new StringWriter();
        var writer = new AiDebugLogWriter(sink, maxBytes: twoRows);

        Assert.True(writer.Write(Result(1_000L), 1_000L, 0f, false));
        Assert.False(writer.Stopped);
        Assert.True(writer.Write(Result(1_250L), 1_250L, 0f, false));
        Assert.True(writer.Stopped);
        Assert.False(writer.Write(Result(1_500L), 1_500L, 0f, false));
        Assert.Equal(twoRows, sink.ToString().Length);
    }

    [Fact] // 쓴 줄에 그때의 값이 들어간다
    public void 쓴_줄에_그때의_값이_들어간다()
    {
        var sink = new StringWriter();
        var writer = new AiDebugLogWriter(sink);
        writer.Write(Result(3_000L, SoundType.Danger), 3_120L, 0.42f, false);

        string row = Lines(sink)[1];
        Assert.Equal(AiDebugCsv.Row(Result(3_000L, SoundType.Danger), 3_120L, 0.42f, false), row);
        Assert.Contains("\"Loopback\"", row); // 캡처 소스가 빠졌다
        Assert.Contains("0.42000", row); // 소리 크기가 빠졌다
        Assert.Contains("false", row); // 표시 여부가 빠졌다
    }

    [Fact]
    public void 줄_끝은_모바일_파일처럼_LF_하나다()
    {
        // TextWriter.WriteLine 은 Windows 에서 \r\n 을 쓴다. 두 판의 파일이 바이트까지 같은 모양이어야 같은 스크립트로 읽는다.
        var sink = new StringWriter();
        var writer = new AiDebugLogWriter(sink);
        writer.Write(Result(1_000L), 1_000L, 0.1f, true);

        string text = sink.ToString();
        Assert.DoesNotContain("\r", text);
        Assert.Equal(AiDebugCsv.Header + "\n" + AiDebugCsv.Row(Result(1_000L), 1_000L, 0.1f, true) + "\n", text);
    }
}
