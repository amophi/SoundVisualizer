// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Diagnostics;
using TopClassHit = SoundVisualizer.Ai.YamnetCoarseClassifier.TopClassHit;

namespace SoundVisualizer.Tests.Diagnostics;

/// <summary>
/// 개발자 모드 HUD 를 언제 읽고 언제 기록하는지(모바일 AiDebugOverlay 의 produceState·DisposableEffect).
///
/// 기록은 오버레이가 열려 있고, 개발자 모드가 켜져 있고, 기록 스위치가 켜져 있을 때만 돈다. 이 셋이 어긋나면 조용히 어긋난다.
/// 켜 둔 줄 알았는데 파일이 비어 있으면 그 구간을 "결과가 없었다" 로 읽고, 끈 줄 알았는데 계속 쓰면 디스크를 먹는다.
/// </summary>
public sealed class AiDebugHudTests : IDisposable
{
    private readonly FakeRecorder _recorder = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-aihud-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 임시 폴더라 남아도 된다.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void 처음에는_읽지도_기록하지도_않는다()
    {
        using var hud = new AiDebugHud(_recorder);

        Assert.False(hud.IsPolling);
        Assert.False(hud.IsRecording);
        Assert.Empty(_recorder.Calls);
    }

    [Fact]
    public void 개발자_모드를_켜면_읽기_시작하고_기록은_스위치를_따른다()
    {
        using var hud = new AiDebugHud(_recorder);

        Assert.True(hud.Update(developerMode: true, record: false));
        Assert.True(hud.IsPolling);
        Assert.False(hud.IsRecording);
        Assert.Empty(_recorder.Calls);

        // 읽는 중에 기록 스위치를 켜면 HUD 는 그대로 두고 기록만 켠다.
        Assert.False(hud.Update(developerMode: true, record: true));
        Assert.True(hud.IsRecording);
        Assert.Equal(new[] { "Start" }, _recorder.Calls);

        Assert.False(hud.Update(developerMode: true, record: false));
        Assert.False(hud.IsRecording);
        Assert.Equal(new[] { "Start", "Stop" }, _recorder.Calls);
    }

    [Fact]
    public void 기록_스위치만_켜져_있으면_기록하지_않는다()
    {
        // 모바일처럼 개발자 모드를 꺼도 기록 스위치의 값은 남는다. 그 값만으로 파일을 열면 안 된다.
        using var hud = new AiDebugHud(_recorder);

        Assert.False(hud.Update(developerMode: false, record: true));
        Assert.False(hud.IsPolling);
        Assert.False(hud.IsRecording);
        Assert.Empty(_recorder.Calls);

        // 개발자 모드를 켜는 순간 남아 있던 스위치대로 기록을 시작한다.
        Assert.True(hud.Update(developerMode: true, record: true));
        Assert.Equal(new[] { "Start" }, _recorder.Calls);
    }

    [Fact]
    public void 개발자_모드를_끄면_읽기와_기록을_함께_멈춘다()
    {
        using var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: true);

        Assert.True(hud.Update(developerMode: false, record: true));
        Assert.False(hud.IsPolling);
        Assert.False(hud.IsRecording);
        Assert.Equal(new[] { "Start", "Stop" }, _recorder.Calls);
    }

    [Fact]
    public void 같은_설정을_거듭_알려도_한_번만_켜고_끈다()
    {
        // 오버레이는 프레임마다 지금 설정을 알린다. 그때마다 켜면 기록 파일이 프레임마다 새로 생긴다.
        using var hud = new AiDebugHud(_recorder);

        Assert.True(hud.Update(developerMode: true, record: true));
        Assert.False(hud.Update(developerMode: true, record: true));
        Assert.False(hud.Update(developerMode: true, record: true));
        Assert.False(hud.Update(developerMode: true, record: false));
        Assert.False(hud.Update(developerMode: true, record: false));

        Assert.Equal(new[] { "Start", "Stop" }, _recorder.Calls);
    }

    [Fact]
    public void 기록을_껐다_켜면_다시_시작한다()
    {
        // 기록기가 상한에 닿아 스스로 멈췄을 때도 스위치를 껐다 켜면 새 파일로 다시 시작한다.
        using var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: true);
        hud.Update(developerMode: true, record: false);
        hud.Update(developerMode: true, record: true);

        Assert.Equal(new[] { "Start", "Stop", "Start" }, _recorder.Calls);
    }

    [Fact]
    public void 기록_중이면_읽을_때마다_그_값을_그대로_넘긴다()
    {
        using var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: true);
        var result = Result(timestampMs: 5_000L);

        hud.Poll(result, nowMs: 5_040L, level: 0.42f, shown: false, aiAvailable: true);
        hud.Poll(result, nowMs: 5_140L, level: 0.40f, shown: false, aiAvailable: true);

        // 같은 결과도 읽을 때마다 넘긴다. 한 줄만 남기는 일은 기록기가 한다(그 결과를 처음 본 때의 값이 남는다).
        Assert.Equal(new[] { (result, 5_040L, 0.42f, false), (result, 5_140L, 0.40f, false) }, _recorder.Offers);
    }

    [Fact]
    public void 결과가_없으면_기록기에_넘기지_않는다()
    {
        using var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: true);

        hud.Poll(null, nowMs: 1_000L, level: 0.3f, shown: false, aiAvailable: true);

        Assert.Empty(_recorder.Offers);
    }

    [Fact]
    public void 기록하지_않을_때는_읽기만_한다()
    {
        using var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: false);

        hud.Poll(Result(timestampMs: 1_000L), nowMs: 1_050L, level: 0.3f, shown: true, aiAvailable: true);

        Assert.Empty(_recorder.Offers);
    }

    [Fact]
    public void 읽은_글자는_AiDebugText_와_같다()
    {
        using var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: false);
        var result = Result(timestampMs: 2_000L);

        Assert.Equal(AiDebugText.Format(result, 2_300L, 0.5f, true, true), hud.Poll(result, 2_300L, 0.5f, true, true));

        // 결과가 없을 때 AI 를 만들지 못했으면 AI OFF, 만드는 중이거나 첫 결과를 기다리면 WAIT 다.
        Assert.Equal(AiDebugText.CoarseAiOff, hud.Poll(null, 2_400L, 0f, false, aiAvailable: false).Coarse);
        Assert.Equal(AiDebugText.CoarseWaiting, hud.Poll(null, 2_500L, 0f, false, aiAvailable: true).Coarse);
    }

    [Fact]
    public void 닫으면_기록기를_닫고_다시_켜지_않는다()
    {
        var hud = new AiDebugHud(_recorder);
        hud.Update(developerMode: true, record: true);

        hud.Dispose();
        Assert.False(hud.IsPolling);
        Assert.False(hud.IsRecording);
        Assert.Equal(new[] { "Start", "Dispose" }, _recorder.Calls);

        // 닫힌 오버레이에 늦게 온 설정이나 읽기는 아무것도 하지 않는다.
        Assert.False(hud.Update(developerMode: false, record: false));
        Assert.False(hud.Update(developerMode: true, record: true));
        hud.Poll(Result(timestampMs: 3_000L), 3_050L, 0.5f, true, true);
        hud.Dispose();

        Assert.Equal(new[] { "Start", "Dispose" }, _recorder.Calls);
        Assert.Empty(_recorder.Offers);
    }

    [Fact]
    public void 실제_기록기로_켜면_파일이_생기고_개발자_모드를_끄면_닫힌다()
    {
        string portable = Path.Combine(_root, "exe");
        var recorder = new AiDebugRecorder(portable, Path.Combine(_root, "local"));
        using var hud = new AiDebugHud(recorder);
        var first = Result(timestampMs: 10_000L);
        var second = Result(timestampMs: 10_250L);

        hud.Update(developerMode: true, record: true);
        hud.Poll(first, 10_050L, 0.5f, true, true);
        hud.Poll(first, 10_150L, 0.6f, true, true); // 같은 결과를 또 읽었다
        hud.Poll(second, 10_300L, 0.4f, false, true);
        hud.Update(developerMode: false, record: true);

        // 끄면 쓰는 스레드가 받아 둔 줄을 모두 쓰고 파일을 닫는다.
        Assert.True(recorder.WaitForWriters(10_000), "쓰는 스레드가 끝나지 않았다");
        Assert.False(recorder.IsRecording);
        string file = Assert.Single(Directory.GetFiles(Path.Combine(portable, AiDebugRecorder.DirectoryName)));
        string[] lines = File.ReadAllLines(file);
        Assert.Equal(3, lines.Length); // 머리글과 두 결과. 같은 결과는 처음 본 때의 한 줄만 남는다.
        Assert.Equal(AiDebugCsv.Header, lines[0]);
        Assert.Equal(AiDebugCsv.Row(first, 10_050L, 0.5f, true), lines[1]);
        Assert.Equal(AiDebugCsv.Row(second, 10_300L, 0.4f, false), lines[2]);

        // 닫힌 파일이라 다른 프로그램이 바로 옮기거나 지울 수 있다.
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }
    }

    private static AiClassificationResult Result(long timestampMs) => new()
    {
        Coarse = SoundType.Speech,
        Display = "Speech",
        Confidence = 0.8f,
        Top5 = [new TopClassHit(0, "Speech", 0.8f), new TopClassHit(1, "Conversation", 0.1f)],
        MeetsThreshold = true,
        TimestampMs = timestampMs,
        PreprocessMs = 1.0,
        YamnetMs = 2.0,
        TotalMs = 3.5,
    };

    /// <summary>켜고 끈 순서와 넘긴 값을 적어 두는 기록기.</summary>
    private sealed class FakeRecorder : IAiDebugRecorder
    {
        public List<string> Calls { get; } = [];

        public List<(AiClassificationResult Result, long NowMs, float Level, bool Shown)> Offers { get; } = [];

        public void Start() => Calls.Add("Start");

        public void Stop() => Calls.Add("Stop");

        public void Offer(AiClassificationResult result, long nowMs, float level, bool shown) => Offers.Add((result, nowMs, level, shown));

        public void Dispose() => Calls.Add("Dispose");
    }
}
