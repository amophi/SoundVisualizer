// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Diagnostics;
using SoundVisualizer.Ai;
using SoundVisualizer.Diagnostics;
using TopClassHit = SoundVisualizer.Ai.YamnetCoarseClassifier.TopClassHit;

namespace SoundVisualizer.Tests.Diagnostics;

/// <summary>
/// 개발자 모드 기록이 실제로 파일에 닿는지(모바일 AiDebugRecorderInstrumentedTest, #193, #195, #199, #208).
///
/// 줄을 만드는 규칙은 AiDebugCsvTests·AiDebugLogWriterTests 가 지킨다. 여기서 보는 것은 파일이 생기는지, 쓰는 스레드가 실제로 쓰는지,
/// 끄면 닫히고 다시 켜면 새 파일이 열리는지다. 이 부분이 깨지면 조용히 깨진다. 파일이 안 열려도 앱은 멀쩡히 돌고 기록만 비어 있어서,
/// 채점할 때 "이 구간에는 결과가 없었다" 로 읽힌다.
///
/// 모바일 일곱 개를 옮기고, PC 의 폴더 고르기(실행 파일 폴더, 안 되면 LocalAppData), 상한, 끌 때 받아 둔 줄 비우기, 큐가 찰 때 오래된
/// 줄 버리기, 같은 초에 다시 켜기, 닫기를 더했다. 폴더는 테스트마다 임시 폴더를 준다.
/// </summary>
public sealed class AiDebugRecorderTests : IDisposable
{
    /// <summary>쓰는 스레드를 기다리는 한도. 빨리 끝나는 일이라 느린 CI 에서도 넉넉하다.</summary>
    private const int PatienceMs = 10_000;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "sv-ailog-" + Guid.NewGuid().ToString("N"));
    private readonly List<AiDebugRecorder> _recorders = [];

    private string PortableBase => Path.Combine(_root, "exe");

    private string FallbackBase => Path.Combine(_root, "local");

    private string PortableLog => Path.Combine(PortableBase, AiDebugRecorder.DirectoryName);

    private string FallbackLog => Path.Combine(FallbackBase, AiDebugRecorder.DirectoryName);

    public void Dispose()
    {
        // 쓰는 스레드를 먼저 끝내야 파일을 지울 수 있다.
        foreach (var recorder in _recorders) recorder.Dispose();
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

    [Fact] // 켜지_않으면_폴더도_만들지_않는다
    public void 켜지_않으면_폴더도_만들지_않는다()
    {
        // 기본이 꺼짐이라, 기록을 쓰지 않는 사용자의 PC 에는 아무 흔적도 남지 않아야 한다.
        var recorder = NewRecorder();
        recorder.Offer(Result(1_000L), 1_000L, 0.5f, true);

        Assert.False(recorder.IsRecording);
        Assert.Null(recorder.CurrentFile);
        Assert.False(Directory.Exists(PortableLog));
        Assert.False(Directory.Exists(FallbackLog));
    }

    [Fact] // 켜면_파일이_생기고_넘긴_줄이_바로_들어간다
    public void 켜면_첫_줄과_함께_파일이_생기고_끄지_않아도_줄이_바로_보인다()
    {
        var recorder = NewRecorder();
        recorder.Start();
        Assert.True(recorder.IsRecording);
        // 폴더와 파일은 첫 줄이 올 때 쓰는 스레드가 만든다.
        Assert.Null(recorder.CurrentFile);

        recorder.Offer(Result(2_000L), 2_050L, 0.42f, true);
        string file = WaitForCurrentFile(recorder);
        Assert.Equal(PortableLog, Path.GetDirectoryName(file));
        Assert.Matches(@"^ai-\d{8}-\d{6}\.csv$", Path.GetFileName(file));
        Assert.Equal("ai-20261014-093000.csv", Path.GetFileName(file));

        // 줄마다 밀어 넣으므로 끄지 않고도 파일에서 보여야 한다. 모아 두면 기록 중에 연 파일에 최근 몇 초가 비어,
        // 그 구간을 "결과가 없었다" 로 읽게 된다.
        WaitFor("넘긴 줄이 파일에 나타남", () => Lines(file).Length >= 2);
        var lines = Lines(file);
        Assert.Equal(AiDebugCsv.Header, lines[0]);
        Assert.Equal(AiDebugCsv.Row(Result(2_000L), 2_050L, 0.42f, true), lines[1]);
        Assert.Contains("\"Loopback\"", lines[1]);
    }

    [Fact] // 켰다_바로_끄면_빈_파일이_남지_않는다
    public void 켰다_바로_끄면_빈_파일도_폴더도_남지_않는다()
    {
        // 켜 보고 바로 끈 사용자의 PC 에 빈 파일이 쌓이지 않는 편이 낫다. PC 는 폴더도 첫 줄이 올 때 만든다.
        var recorder = NewRecorder();
        recorder.Start();
        recorder.Stop();

        Assert.True(recorder.WaitForWriters(PatienceMs), "쓰는 스레드가 끝나지 않았다");
        Assert.False(Directory.Exists(PortableLog));
        Assert.False(Directory.Exists(FallbackLog));
    }

    [Fact] // 같은_결과를_두_번_넘겨도_한_줄만_남는다
    public void 같은_결과를_여러_번_넘겨도_처음_본_값으로_한_줄만_남는다()
    {
        // 렌더 루프는 프레임마다 같은 결과를 넘긴다. 추론은 250ms 마다라 한 결과를 수십 번 본다.
        var recorder = NewRecorder();
        recorder.Start();
        var same = Result(3_000L);
        recorder.Offer(same, 3_000L, 0.1f, true);
        recorder.Offer(same, 3_100L, 0.2f, false);
        recorder.Offer(Result(3_000L), 3_150L, 0.3f, true); // 시각이 같으면 다른 객체도 같은 결과다
        recorder.Offer(Result(3_250L), 3_250L, 0.1f, true);
        recorder.Stop();

        Assert.True(recorder.WaitForWriters(PatienceMs));
        Assert.Equal(
            new[]
            {
                AiDebugCsv.Header,
                AiDebugCsv.Row(same, 3_000L, 0.1f, true),
                AiDebugCsv.Row(Result(3_250L), 3_250L, 0.1f, true),
            },
            Lines(Assert.Single(Files(PortableLog))));
    }

    [Fact] // 끄면_닫히고_다시_켜면_새_파일에_쓴다
    public void 끄면_닫히고_다시_켜면_새_파일에_쓴다()
    {
        var recorder = NewRecorder();
        recorder.Start();
        recorder.Offer(Result(4_000L), 4_000L, 0.2f, true);
        string first = WaitForCurrentFile(recorder);
        recorder.Stop();
        // 끈 뒤에도 파일이 남아 보이면 켠 상태와 끈 상태를 가릴 수 없다.
        Assert.Null(recorder.CurrentFile);
        Assert.False(recorder.IsRecording);

        recorder.Start();
        recorder.Offer(Result(5_000L), 5_000L, 0.3f, false);
        string second = WaitForCurrentFile(recorder);
        Assert.NotEqual(first, second); // 다시 켰는데 같은 파일에 이어 쓴다
        recorder.Stop();

        // 앞 파일은 그대로 남아 있어야 한다. 회차마다 파일이 갈려야 채점이 섞이지 않는다.
        Assert.True(recorder.WaitForWriters(PatienceMs));
        Assert.Equal(2, Files(PortableLog).Length);
        Assert.Equal(new[] { AiDebugCsv.Header, AiDebugCsv.Row(Result(4_000L), 4_000L, 0.2f, true) }, Lines(first));
        Assert.Equal(new[] { AiDebugCsv.Header, AiDebugCsv.Row(Result(5_000L), 5_000L, 0.3f, false) }, Lines(second));
    }

    [Fact] // 껐다_켜기를_되풀이해도_파일이_새지_않는다
    public void 껐다_켜기를_되풀이해도_스레드와_열린_파일이_남지_않는다()
    {
        // 설정 스위치를 빠르게 누르는 경우. 스레드나 열린 파일이 쌓이면 여기서 드러난다.
        var recorder = NewRecorder();
        for (int i = 0; i < 5; i++)
        {
            recorder.Start();
            recorder.Offer(Result(6_000L + i), 6_000L + i, 0.1f, true);
            recorder.Stop();
        }

        Assert.Null(recorder.CurrentFile);
        Assert.True(recorder.WaitForWriters(PatienceMs), "기록 스레드가 남았다");
        var files = Files(PortableLog);
        Assert.Equal(5, files.Length);
        Assert.All(files, file =>
        {
            Assert.Equal(2, Lines(file).Length);
            // 아무도 열고 있지 않아야 혼자 열 수 있다.
            using var exclusive = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });
    }

    [Fact] // 껐다_곧바로_켜면_줄이_새_파일에만_들어간다
    public void 껐다_곧바로_켜면_줄이_새_파일에만_들어간다()
    {
        // Stop 은 쓰는 스레드를 기다리지 않는다. 옛 스레드가 큐를 기다리는 동안 다시 켜면 두 스레드가 한 큐를 나눠 먹을 수 있었다(#208).
        var recorder = NewRecorder();
        recorder.Start();
        recorder.Offer(Result(7_000L), 7_000L, 0.1f, true);
        string first = WaitForCurrentFile(recorder);
        WaitFor("첫 파일에 한 줄", () => Lines(first).Length == 2);

        recorder.Stop();
        recorder.Start(); // 기다리지 않고 곧바로 켠다
        for (int i = 0; i < 6; i++) recorder.Offer(Result(8_000L + i), 8_000L + i, 0.2f, true);
        string second = WaitForCurrentFile(recorder);
        Assert.NotEqual(first, second); // 같은 파일을 다시 열었다
        WaitFor("새 파일에 여섯 줄", () => Lines(second).Length == 7);
        recorder.Stop();

        Assert.True(recorder.WaitForWriters(PatienceMs));
        Assert.Equal(2, Lines(first).Length); // 새 회차의 줄이 옛 파일로 새어 들어갔다
        Assert.Equal(7, Lines(second).Length);
    }

    [Fact]
    public void 같은_초에_다시_켜도_앞_회차를_덮어쓰지_않는다()
    {
        // 모바일은 같은 이름을 덮어쓴다. 여기서는 번호를 붙인다. 앞 회차가 이미 파일을 닫은 경우(덮어쓰기)와 옛 스레드가 아직 열고 있는
        // 경우(Windows 에서는 다시 열 수도 없다)를 함께 본다.
        var sameSecond = new DateTime(2026, 10, 14, 9, 30, 0, DateTimeKind.Local);
        var recorder = NewRecorder(clock: () => sameSecond);
        recorder.Start();
        recorder.Offer(Result(9_000L), 9_000L, 0.1f, true);
        recorder.Stop();
        Assert.True(recorder.WaitForWriters(PatienceMs));

        recorder.Start();
        recorder.Offer(Result(9_250L), 9_250L, 0.1f, true);
        recorder.Stop();
        recorder.Start(); // 앞 스레드를 기다리지 않는다
        recorder.Offer(Result(9_500L), 9_500L, 0.1f, true);
        recorder.Stop();

        Assert.True(recorder.WaitForWriters(PatienceMs));
        var files = Files(PortableLog);
        Assert.Equal(
            new[] { "ai-20261014-093000-2.csv", "ai-20261014-093000-3.csv", "ai-20261014-093000.csv" },
            files.Select(Path.GetFileName));
        Assert.Equal(new[] { AiDebugCsv.Header, AiDebugCsv.Row(Result(9_000L), 9_000L, 0.1f, true) }, Lines(files[2]));
        // 뒤의 두 회차는 쓰는 스레드가 함께 돌아 어느 쪽이 먼저 번호를 가질지 정해져 있지 않다. 내용만 본다.
        var rows = files[..2].Select(file => Lines(file)).ToArray();
        Assert.All(rows, lines => Assert.Equal(2, lines.Length));
        Assert.Equal(
            new[] { AiDebugCsv.Row(Result(9_250L), 9_250L, 0.1f, true), AiDebugCsv.Row(Result(9_500L), 9_500L, 0.1f, true) },
            rows.Select(lines => lines[1]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void 프레임마다_같은_결과를_넘겨도_큐가_차지_않는다()
    {
        // PC 렌더 루프는 프레임마다(60~144Hz) 부른다. 같은 결과로 큐가 차면 디스크가 잠깐 밀릴 때 새 결과가 밀려난다.
        using var opening = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var recorder = NewRecorder(createFile: path =>
        {
            opening.Set();
            release.Wait(PatienceMs);
            return CreateNew(path);
        });
        var shown = Result(17_000L);
        try
        {
            recorder.Start();
            recorder.Offer(shown, 17_000L, 0.1f, true);
            Assert.True(opening.Wait(PatienceMs), "쓰는 스레드가 파일을 열러 가지 않았다");
            for (int frame = 1; frame <= 3 * AiDebugRecorder.QueueCapacity; frame++)
            {
                recorder.Offer(shown, 17_000L + frame, 0.2f, false);
            }
            Assert.Equal(0, recorder.Dropped);
            recorder.Offer(Result(17_250L), 17_250L, 0.3f, true);
            recorder.Stop();
        }
        finally
        {
            release.Set();
        }

        Assert.True(recorder.WaitForWriters(PatienceMs));
        Assert.Equal(
            new[]
            {
                AiDebugCsv.Header,
                AiDebugCsv.Row(shown, 17_000L, 0.1f, true),
                AiDebugCsv.Row(Result(17_250L), 17_250L, 0.3f, true),
            },
            Lines(Assert.Single(Files(PortableLog))));
    }

    [Fact]
    public void 끄기_전에_받은_줄은_끈_뒤에도_모두_쓰고_끈_뒤에_온_줄은_받지_않는다()
    {
        using var opening = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var recorder = NewRecorder(createFile: path =>
        {
            // 쓰는 스레드를 파일을 여는 자리에 세워, 나머지 줄이 큐에 남은 채로 끄게 한다.
            opening.Set();
            release.Wait(PatienceMs);
            return CreateNew(path);
        });
        try
        {
            recorder.Start();
            recorder.Offer(Result(10_000L), 10_000L, 0.1f, true);
            Assert.True(opening.Wait(PatienceMs), "쓰는 스레드가 파일을 열러 가지 않았다");
            for (int i = 1; i <= 20; i++) recorder.Offer(Result(10_000L + i), 10_000L + i, 0.1f, true);
            recorder.Stop();
            recorder.Offer(Result(10_999L), 10_999L, 0.1f, true);
        }
        finally
        {
            release.Set();
        }

        Assert.True(recorder.WaitForWriters(PatienceMs));
        var lines = Lines(Assert.Single(Files(PortableLog)));
        Assert.Equal(22, lines.Length); // 머리글 + 끄기 전에 받은 21줄
        Assert.Equal(AiDebugCsv.Row(Result(10_000L), 10_000L, 0.1f, true), lines[1]);
        Assert.Equal(AiDebugCsv.Row(Result(10_020L), 10_020L, 0.1f, true), lines[^1]);
    }

    [Fact]
    public void 큐가_차면_가장_오래된_줄을_버리고_버린_수를_센다()
    {
        // 기록이 밀리는 것보다 화면이 밀리는 것이 나쁘다. Offer 는 기다리지 않고, 최근 것이 궁금한 도구라 뒤를 살린다.
        using var opening = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var recorder = NewRecorder(createFile: path =>
        {
            opening.Set();
            release.Wait(PatienceMs);
            return CreateNew(path);
        });
        const int extra = 5;
        const int last = AiDebugRecorder.QueueCapacity + extra;
        try
        {
            recorder.Start();
            // 첫 줄은 쓰는 스레드가 꺼내 들고 파일을 열러 간다. 그 뒤의 줄이 큐에 쌓인다.
            recorder.Offer(Result(20_000L), 20_000L, 0.1f, true);
            Assert.True(opening.Wait(PatienceMs), "쓰는 스레드가 파일을 열러 가지 않았다");
            for (int i = 1; i <= last; i++) recorder.Offer(Result(20_000L + i), 20_000L + i, 0.1f, true);
            Assert.Equal(extra, recorder.Dropped);
            recorder.Stop();
        }
        finally
        {
            release.Set();
        }

        Assert.True(recorder.WaitForWriters(PatienceMs));
        var lines = Lines(Assert.Single(Files(PortableLog)));
        Assert.Equal(1 + 1 + AiDebugRecorder.QueueCapacity, lines.Length);
        Assert.Equal(AiDebugCsv.Row(Result(20_000L), 20_000L, 0.1f, true), lines[1]);
        Assert.Equal(AiDebugCsv.Row(Result(20_000L + extra + 1), 20_000L + extra + 1, 0.1f, true), lines[2]);
        Assert.Equal(AiDebugCsv.Row(Result(20_000L + last), 20_000L + last, 0.1f, true), lines[^1]);
    }

    [Fact]
    public void 실행_파일_폴더에_폴더를_만들_수_없으면_LocalAppData_에_쓴다()
    {
        // 폴더를 만들 자리 위에 같은 이름의 파일이 있으면 만들 수 없다. 쓸 수 없는 실행 파일 폴더를 실제 파일 시스템으로 흉내 낸다.
        Directory.CreateDirectory(_root);
        string blocked = Path.Combine(_root, "exe-is-a-file");
        File.WriteAllText(blocked, "");
        var recorder = NewRecorder(portableBase: blocked);
        recorder.Start();
        recorder.Offer(Result(11_000L), 11_000L, 0.1f, true);

        string file = WaitForCurrentFile(recorder);
        Assert.Equal(FallbackLog, Path.GetDirectoryName(file));
        WaitFor("넘긴 줄이 파일에 나타남", () => Lines(file).Length == 2);
    }

    [Fact]
    public void 실행_파일_폴더에_쓸_권한이_없으면_LocalAppData_에_쓴다()
    {
        // Program Files 에 풀어 둔 경우. 일반 권한으로는 그 아래에 파일을 만들 수 없다.
        var recorder = NewRecorder(createFile: path =>
            path.StartsWith(PortableLog, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("Access to the path is denied.")
                : CreateNew(path));
        recorder.Start();
        recorder.Offer(Result(12_000L), 12_000L, 0.1f, true);
        recorder.Stop();

        Assert.True(recorder.WaitForWriters(PatienceMs));
        Assert.Empty(Files(PortableLog));
        Assert.Equal(
            new[] { AiDebugCsv.Header, AiDebugCsv.Row(Result(12_000L), 12_000L, 0.1f, true) },
            Lines(Assert.Single(Files(FallbackLog))));
    }

    [Fact]
    public void 어디에도_쓸_수_없으면_스스로_꺼지고_더_받지_않는다()
    {
        // 기록이 안 되는 것이 앱이 멈추는 것보다 낫다. 쓰는 스레드의 예외가 새면 프로세스가 끝난다.
        var recorder = NewRecorder(createFile: _ => throw new UnauthorizedAccessException("read-only"));
        recorder.Start();
        recorder.Offer(Result(13_000L), 13_000L, 0.1f, true);

        WaitFor("회차가 스스로 닫힘", () => !recorder.IsRecording);
        Assert.Null(recorder.CurrentFile);
        recorder.Offer(Result(13_250L), 13_250L, 0.1f, true); // 아무도 비우지 않는 큐에 넣지 않는다
        Assert.True(recorder.WaitForWriters(PatienceMs));
        Assert.Empty(Files(PortableLog));
        Assert.Empty(Files(FallbackLog));
    }

    [Fact]
    public void 상한에_닿으면_그_줄까지_쓰고_스스로_멈추고_다시_켜면_새_파일에_쓴다()
    {
        // 켜 둔 것을 잊은 PC 의 디스크를 먹지 않게 한다. 기본 상한은 20MB 라 테스트는 머리글과 세 줄 크기로 준다.
        long threeRows = AiDebugCsv.Header.Length + 1 + 3 * (AiDebugCsv.Row(Result(14_000L), 14_000L, 0.1f, true).Length + 1L);
        var recorder = NewRecorder(maxBytes: threeRows);
        recorder.Start();
        for (int i = 0; i < 5; i++) recorder.Offer(Result(14_000L + i), 14_000L + i, 0.1f, true);

        WaitFor("상한에 닿아 스스로 닫힘", () => !recorder.IsRecording);
        Assert.Null(recorder.CurrentFile);
        recorder.Offer(Result(14_500L), 14_500L, 0.1f, true); // 꺼진 뒤에는 받지 않는다
        Assert.True(recorder.WaitForWriters(PatienceMs));
        string first = Assert.Single(Files(PortableLog));
        var lines = Lines(first);
        Assert.Equal(4, lines.Length);
        Assert.Equal(AiDebugCsv.Row(Result(14_002L), 14_002L, 0.1f, true), lines[^1]);

        recorder.Start();
        recorder.Offer(Result(15_000L), 15_000L, 0.1f, true);
        string second = WaitForCurrentFile(recorder);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void 닫으면_남은_줄을_쓰고_파일을_닫은_뒤에_돌아오고_다시_켜지지_않는다()
    {
        // 앱을 닫을 때. 쓰는 스레드는 배경 스레드라 기다리지 않으면 프로세스가 끝나면서 받아 둔 줄을 잃는다.
        var recorder = NewRecorder();
        recorder.Start();
        recorder.Offer(Result(16_000L), 16_000L, 0.1f, true);
        recorder.Offer(Result(16_250L), 16_250L, 0.1f, true);
        recorder.Dispose();

        Assert.False(recorder.IsRecording);
        string file = Assert.Single(Files(PortableLog));
        Assert.Equal(3, Lines(file).Length);
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // 아무도 열고 있지 않다.
        }

        recorder.Start();
        Assert.False(recorder.IsRecording);
        recorder.Dispose(); // 여러 번 불러도 된다
    }

    private AiDebugRecorder NewRecorder(
        string? portableBase = null,
        long maxBytes = AiDebugLogWriter.DefaultMaxBytes,
        Func<DateTime>? clock = null,
        Func<string, Stream>? createFile = null)
    {
        var recorder = new AiDebugRecorder(portableBase ?? PortableBase, FallbackBase, maxBytes, clock ?? new SteppingClock().Next, createFile);
        _recorders.Add(recorder);
        return recorder;
    }

    private static AiClassificationResult Result(long timestampMs) => new()
    {
        Coarse = SoundType.Danger,
        Display = "Gunshot, gunfire",
        Confidence = 0.5f,
        Top5 = [new TopClassHit(1, "Gunshot, gunfire", 0.5f)],
        DangerCuePromoted = false,
        MeetsThreshold = true,
        TimestampMs = timestampMs,
        PreprocessMs = 1.0,
        YamnetMs = 2.0,
        TotalMs = 3.5,
    };

    private static Stream CreateNew(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

    /// <summary>조건이 될 때까지 짧게 기다린다. 쓰는 스레드가 따로 돌기 때문이다. 고정 sleep 은 느린 PC 에서 깜빡이고 빠른 PC 에서는 시간을 버린다.</summary>
    private static void WaitFor(string what, Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > PatienceMs) Assert.Fail($"{what}: {PatienceMs}ms 안에 일어나지 않았다");
            Thread.Sleep(10);
        }
    }

    /// <summary>켠 회차가 쓰는 파일. 첫 줄이 와서 파일이 생겨야 정해진다.</summary>
    private static string WaitForCurrentFile(AiDebugRecorder recorder)
    {
        WaitFor("기록 파일이 생김", () => recorder.CurrentFile is not null);
        return recorder.CurrentFile!;
    }

    private static string[] Files(string directory) =>
        Directory.Exists(directory) ? [.. Directory.GetFiles(directory).Order(StringComparer.Ordinal)] : [];

    /// <summary>쓰는 스레드가 아직 열고 있어도 읽는다. 파일은 첫 줄이 올 때 생기므로 없으면 빈 목록.</summary>
    private static string[] Lines(string path)
    {
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>켤 때마다 1초씩 가는 시계. 모바일 테스트는 파일 이름이 갈리도록 1.1초를 쉬었다.</summary>
    private sealed class SteppingClock
    {
        private DateTime _now = new(2026, 10, 14, 9, 30, 0, DateTimeKind.Local);

        public DateTime Next()
        {
            var now = _now;
            _now = _now.AddSeconds(1);
            return now;
        }
    }
}
