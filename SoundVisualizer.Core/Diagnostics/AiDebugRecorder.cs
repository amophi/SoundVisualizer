// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Diagnostics
{
    /// <summary>
    /// 개발자 모드의 결과 기록을 파일로 남긴다(모바일 AiDebugRecorder.kt, #193). 줄을 만드는 규칙은 <see cref="AiDebugLogWriter"/> 에 있다.
    ///
    /// 켜면 <c>&lt;실행 파일 폴더&gt;/ai-log/ai-yyyyMMdd-HHmmss.csv</c> 를 새로 열고, 끄면 닫는다. 앱이 설정 파일도 실행 파일 옆에 두는
    /// 포터블 배포라 기록도 옆에 둔다. 그 폴더에 쓸 수 없으면(Program Files 에 풀어 둔 경우 등) <c>%LOCALAPPDATA%/SoundVisualizer/ai-log/</c> 에 둔다.
    ///
    /// 디스크는 쓰는 스레드에서만 만진다. 부르는 곳은 오버레이의 렌더 루프(UI 스레드)라, 거기서 폴더를 확인하거나 파일을 열거나 쓰면
    /// 그리는 프레임이 밀린다. 그래서 <see cref="Offer"/> 는 큐에 넣고 바로 돌아오고, 폴더를 고르는 일까지 쓰는 스레드가 한다(#199, #208).
    ///
    /// 켠 회차마다 큐와 쓰는 스레드를 따로 둔다. <see cref="Stop"/> 은 쓰는 스레드를 기다리지 않으므로 껐다 곧바로 켜면 옛 스레드가 아직
    /// 살아 있다. 큐가 하나면 두 스레드가 나눠 먹어 줄이 두 파일로 갈렸다(#208). 회차마다 따로면 옛 스레드는 자기 줄만 비우고 나가고,
    /// 새 줄은 새 스레드만 본다.
    ///
    /// 스레드: <see cref="Start"/>·<see cref="Stop"/>·<see cref="Offer"/> 는 UI 스레드에서 부른다(어느 스레드여도 되고, 디스크를 기다리지 않는다).
    /// <see cref="Dispose"/> 는 앱을 닫을 때 부르며, 남은 줄을 쓰고 파일을 닫기를 1초까지 기다린다.
    ///
    /// 모바일과 다른 점:
    /// - 폴더를 고르려면 실제로 만들어 봐야 해서 폴더와 파일을 첫 줄이 올 때 함께 정한다. 그래서 <see cref="CurrentFile"/> 은 파일이
    ///   생긴 뒤에 채워진다(모바일은 쓰는 스레드가 시작할 때 경로만 정한다).
    /// - 같은 초에 다시 켜면 모바일은 같은 이름의 파일을 덮어쓴다. 여기서는 앞 회차를 지우지 않게 <c>ai-…-2.csv</c> 처럼 번호를 붙인다.
    ///   Windows 에서는 옛 스레드가 아직 연 파일을 다시 열 수도 없다.
    /// - <see cref="Offer"/> 가 같은 결과를 큐에 거듭 넣지 않는다. 모바일 HUD 는 100ms 마다 읽지만 PC 렌더 루프는 프레임마다 부른다.
    ///   남는 줄은 모바일과 같다(그 결과를 처음 본 때의 값이 남는다).
    /// - 끈 뒤에 들어온 줄은 받지 않고, 끄기 전에 받은 줄은 모두 쓴다. 모바일은 끄는 순간과 겹친 줄이 아무도 비우지 않는 큐에 남을 수 있다.
    /// - 쓰다 실패해도 회차를 닫는다. 모바일은 상한과 열기 실패에서만 닫아, 쓰기 실패 뒤에도 오버레이가 아무도 비우지 않는 큐에 줄을 넣는다.
    /// </summary>
    public sealed class AiDebugRecorder : IDisposable
    {
        /// <summary>기록을 두는 폴더 이름. 모바일과 같다.</summary>
        public const string DirectoryName = "ai-log";

        /// <summary>
        /// 한 회차의 큐에 담아 두는 줄 수. 넘치면 오래된 것을 버린다. 초당 네 줄이니 50초 분량이다. 디스크가 그만큼 밀리는 일은 없고,
        /// 있다면 그 PC 에서는 기록 자체가 뜻이 없다.
        /// </summary>
        internal const int QueueCapacity = 200;

        /// <summary><see cref="Dispose"/> 가 쓰는 스레드를 기다리는 한도. 앱을 닫는 UI 스레드를 오래 잡지 않는다.</summary>
        internal const int CloseWaitMs = 1000;

        /// <summary>같은 초에 다시 켠 회차가 붙이는 번호의 끝. 사람이 스위치를 누르는 간격이라 실제로는 2 를 넘지 않는다.</summary>
        private const int MaxNameAttempts = 100;

        private const string Tag = nameof(AiDebugRecorder);

        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly string? _portableBase;
        private readonly string? _fallbackBase;
        private readonly long _maxBytes;
        private readonly Func<DateTime> _clock;
        private readonly Func<string, Stream> _createFile;

        // 켜기·끄기·닫기와, 쓰는 스레드가 스스로 회차를 닫는 일의 순서를 지킨다. 디스크를 기다리는 동안에는 잡지 않는다.
        private readonly object _lifecycleLock = new();
        // 아직 끝나지 않은 쓰는 스레드들. 끈 회차가 남은 줄을 쓰는 중이면 그 스레드도 여기 있다.
        private readonly List<Thread> _writers = [];
        private Session? _session;
        private string? _currentFile;
        private bool _disposed;

        /// <summary>앱이 쓰는 기록기. 폴더는 실행 파일 폴더, 거기에 쓸 수 없으면 %LOCALAPPDATA%/SoundVisualizer 다.</summary>
        public AiDebugRecorder()
            : this(portableBase: null, fallbackBase: null)
        {
        }

        /// <param name="portableBase">먼저 쓰는 폴더의 부모. null 이면 실행 파일 폴더(<see cref="AppContext.BaseDirectory"/>).</param>
        /// <param name="fallbackBase">거기에 쓸 수 없을 때의 부모. null 이면 %LOCALAPPDATA%/SoundVisualizer.</param>
        /// <param name="maxBytes">파일 하나의 상한. 테스트가 작게 준다.</param>
        /// <param name="clock">파일 이름에 쓰는 지역 시각. 테스트가 정한다.</param>
        /// <param name="createFile">
        /// 새 파일을 만든다. 같은 이름이 있으면 <see cref="IOException"/> 을 던진다. 테스트가 쓰는 스레드를 세워 두거나 권한 없음을 흉내 낸다.
        /// </param>
        internal AiDebugRecorder(
            string? portableBase,
            string? fallbackBase,
            long maxBytes = AiDebugLogWriter.DefaultMaxBytes,
            Func<DateTime>? clock = null,
            Func<string, Stream>? createFile = null)
        {
            _portableBase = portableBase;
            _fallbackBase = fallbackBase;
            _maxBytes = maxBytes;
            _clock = clock ?? (static () => DateTime.Now);
            _createFile = createFile ?? CreateNewFile;
        }

        /// <summary>켜져 있는지. 상한에 닿았거나 파일을 만들거나 쓰지 못해 쓰는 쪽이 스스로 닫으면 false 가 된다.</summary>
        public bool IsRecording => Volatile.Read(ref _session) is not null;

        /// <summary>지금 쓰고 있는 파일의 전체 경로. 꺼져 있거나, 켰지만 첫 줄이 와서 파일이 생기기 전이면 null.</summary>
        public string? CurrentFile => Volatile.Read(ref _currentFile);

        /// <summary>지금 회차에서 큐가 차서 버린 줄 수. 0 이 아니면 디스크가 따라오지 못한 것이다. 꺼져 있으면 0.</summary>
        public int Dropped => Volatile.Read(ref _session)?.Dropped ?? 0;

        /// <summary>
        /// 기록을 시작한다. 이미 켜져 있거나 닫은 뒤면 아무것도 하지 않는다(설정을 두 번 켜도 파일이 갈리지 않게).
        ///
        /// 여기서는 디스크를 만지지 않는다. 파일 이름에 쓸 시각만 정하고 쓰는 스레드를 띄운다. 줄이 한 번도 오지 않으면 폴더도 파일도
        /// 만들지 않는다. 켜 보고 바로 끈 PC 에 빈 파일이 남지 않는 편이 낫다. 파일을 만들지 못하면 쓰는 스레드가 로그를 남기고 그 회차를
        /// 닫는다. 기록이 안 되는 것이 앱이 멈추는 것보다 낫다.
        /// </summary>
        public void Start()
        {
            string stamp;
            lock (_lifecycleLock)
            {
                if (_disposed || _session is not null) return;
                stamp = _clock().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                var mine = new Session(stamp);
                var writer = new Thread(() => Run(mine))
                {
                    IsBackground = true,
                    Name = "SV-AiDebugRecord",
                };
                writer.Start();
                // 쓰는 스레드는 끝날 때 이 잠금 안에서 자기를 뺀다. 잠금을 쥔 채 넣으므로 빼기보다 넣기가 먼저다.
                _writers.Add(writer);
                Volatile.Write(ref _currentFile, null);
                Volatile.Write(ref _session, mine);
            }
            AppLog.Info(Tag, $"기록 시작: ai-{stamp}.csv (폴더는 첫 줄이 올 때 쓰는 스레드가 정한다)");
        }

        /// <summary>
        /// 기록을 멈춘다. 꺼져 있으면 아무것도 하지 않는다. 쓰는 스레드를 기다리지 않는다. 그 스레드는 끄기 전에 받은 줄을 모두 쓰고
        /// 파일을 닫은 뒤 스스로 나간다.
        /// </summary>
        public void Stop()
        {
            Session? leaving;
            lock (_lifecycleLock)
            {
                leaving = _session;
                if (leaving is null) return;
                Volatile.Write(ref _session, null);
                // 지금 쓰고 있는 파일이다. 끈 뒤에도 남겨 두면 켠 상태와 끈 상태를 가릴 수 없다.
                Volatile.Write(ref _currentFile, null);
            }
            leaving.Deactivate();
        }

        /// <summary>
        /// 결과 하나를 지금 회차의 큐에 넣고 바로 돌아온다. 꺼져 있으면 아무것도 하지 않는다. 렌더 루프에서 프레임마다 불러도 된다.
        /// 같은 결과(<see cref="AiClassificationResult.TimestampMs"/> 가 같은 것)를 거듭 넘기면 처음 넘긴 것만 남는다.
        /// </summary>
        /// <param name="result">마지막 결과.</param>
        /// <param name="nowMs">지금 벽시계 시각(유닉스 밀리초).</param>
        /// <param name="level">지금 소리 크기(0..1).</param>
        /// <param name="shown">그 종류의 표시가 켜져 있는지.</param>
        public void Offer(AiClassificationResult result, long nowMs, float level, bool shown)
        {
            ArgumentNullException.ThrowIfNull(result);
            Volatile.Read(ref _session)?.Offer(new Row(result, nowMs, level, shown));
        }

        /// <summary>
        /// 기록을 멈추고, 쓰는 스레드들이 남은 줄을 쓰고 파일을 닫기를 1초까지 기다린다. 앱을 닫을 때 부른다. 쓰는 스레드는 배경 스레드라
        /// 기다리지 않으면 프로세스가 끝나면서 받아 둔 줄을 잃는다. 닫은 뒤에는 <see cref="Start"/> 가 아무것도 하지 않는다. 여러 번 불러도 된다.
        /// </summary>
        public void Dispose()
        {
            lock (_lifecycleLock)
            {
                if (_disposed) return;
                _disposed = true;
            }
            Stop();
            if (!WaitForWriters(CloseWaitMs))
                AppLog.Warn(Tag, "쓰는 스레드가 1초 안에 끝나지 않았다. 프로세스가 끝나면 남은 줄을 잃을 수 있다");
        }

        /// <summary>지금 살아 있는 쓰는 스레드가 모두 끝나기를 기다린다. 모두 끝났으면 true. 끄지 않은 회차의 스레드는 끝나지 않는다.</summary>
        internal bool WaitForWriters(int timeoutMs)
        {
            Thread[] writers;
            lock (_lifecycleLock) writers = [.. _writers];
            long deadline = Environment.TickCount64 + timeoutMs;
            foreach (var writer in writers)
            {
                if (!writer.Join((int)Math.Max(0L, deadline - Environment.TickCount64))) return false;
            }
            return true;
        }

        /// <summary>파일 이름. 같은 초에 다시 켠 회차만 번호가 붙는다.</summary>
        internal static string FileName(string stamp, int attempt) =>
            attempt == 1 ? $"ai-{stamp}.csv" : string.Create(CultureInfo.InvariantCulture, $"ai-{stamp}-{attempt}.csv");

        /// <summary>쓰는 스레드의 본체. 자기 회차의 큐만 본다.</summary>
        private void Run(Session mine)
        {
            TextWriter? writer = null;
            AiDebugLogWriter? log = null;
            string? path = null;
            try
            {
                while (mine.TryTake(out var row))
                {
                    if (writer is null || log is null)
                    {
                        // 파일은 첫 줄이 올 때 만든다. 어느 폴더에 둘지도 이때 실제로 만들어 보고 정한다.
                        if (!TryOpen(mine.Stamp, out writer, out path))
                        {
                            // 회차를 닫아, 오버레이가 아무도 비우지 않는 큐에 줄을 넣지 않게 한다.
                            CloseSession(mine);
                            return;
                        }
                        log = new AiDebugLogWriter(writer, _maxBytes);
                        PublishCurrentFile(mine, path);
                    }

                    // 줄마다 디스크에 밀어 넣는다. 모아 두면 기록 중에 연 파일에 최근 몇 초가 비어, 그 구간을 "결과가 없었다" 로 읽는다.
                    // 초당 네 번 쓰는 비용은 프레임마다 그리는 것 옆에서 없는 셈이다.
                    if (log.Write(row.Result, row.NowMs, row.Level, row.Shown)) writer.Flush();
                    if (log.Stopped)
                    {
                        // 상태를 사실에 맞춘다. 닫지 않으면 오버레이가 아무도 비우지 않는 큐에 계속 줄을 넣는다.
                        AppLog.Warn(Tag, $"기록 상한에 닿아 멈춘다: {log.Rows}줄, {Path.GetFileName(path)}");
                        CloseSession(mine);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // 이 스레드에서 새어 나간 예외는 앱을 끝낸다. 디스크가 가득 찼거나 쓰던 파일에 쓸 수 없게 된 경우가 여기로 온다.
                AppLog.Warn(Tag, "기록을 쓰다 실패해 멈춘다", ex);
                CloseSession(mine);
            }
            finally
            {
                try
                {
                    writer?.Dispose();
                }
                catch (Exception ex)
                {
                    AppLog.Warn(Tag, "기록 파일을 닫다 실패했다", ex);
                }
                string what = path is null ? "파일을 만들지 않았다" : $"{log?.Rows ?? 0}줄, {Path.GetFileName(path)}";
                AppLog.Info(Tag, $"기록 끝: {what}, 버린 줄 {mine.Dropped}");
                lock (_lifecycleLock) _writers.Remove(Thread.CurrentThread);
            }
        }

        /// <summary>폴더를 고르고 새 파일을 만든다. 쓰는 스레드에서만 부른다. 실행 파일 폴더가 먼저고, 거기에 쓸 수 없으면 LocalAppData 다.</summary>
        private bool TryOpen(string stamp, [NotNullWhen(true)] out TextWriter? writer, [NotNullWhen(true)] out string? path)
        {
            foreach (string directory in CandidateDirectories())
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    for (int attempt = 1; attempt <= MaxNameAttempts; attempt++)
                    {
                        string candidate = Path.Combine(directory, FileName(stamp, attempt));
                        Stream stream;
                        try
                        {
                            stream = _createFile(candidate);
                        }
                        catch (IOException) when (File.Exists(candidate))
                        {
                            // 같은 초에 켠 앞 회차의 파일이다. 덮어쓰지 않고 다음 번호로 간다.
                            continue;
                        }
                        writer = new StreamWriter(stream, Utf8NoBom);
                        path = candidate;
                        AppLog.Info(Tag, $"기록 파일: {candidate}");
                        return true;
                    }
                    AppLog.Warn(Tag, $"같은 시각의 기록 파일이 너무 많다: {directory}");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
                {
                    // 쓸 수 없는 폴더다(Program Files, 읽기 전용 매체, 폴더 자리에 같은 이름의 파일 등). 다음 후보로 간다.
                    AppLog.Warn(Tag, $"기록 폴더에 쓸 수 없다: {directory}", ex);
                }
            }
            writer = null;
            path = null;
            return false;
        }

        /// <summary>기록 폴더 후보. 포터블 배포라 실행 파일 옆이 먼저다(설정 파일도 그 옆에 둔다).</summary>
        private IEnumerable<string> CandidateDirectories()
        {
            yield return Path.Combine(_portableBase ?? AppContext.BaseDirectory, DirectoryName);
            // 알려진 폴더를 찾는 일도 쓰는 스레드에서 한다.
            string fallbackBase = _fallbackBase ?? DefaultFallbackBase();
            if (fallbackBase.Length > 0) yield return Path.Combine(fallbackBase, DirectoryName);
        }

        private static string DefaultFallbackBase()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return localAppData.Length == 0 ? "" : Path.Combine(localAppData, "SoundVisualizer");
        }

        /// <summary>새 파일을 만든다. 같은 이름이 있으면 <see cref="IOException"/>. 쓰는 동안에도 다른 프로그램이 읽을 수 있게 연다.</summary>
        private static Stream CreateNewFile(string path) =>
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

        private void PublishCurrentFile(Session mine, string path)
        {
            lock (_lifecycleLock)
            {
                // 그사이 꺼졌으면 남기지 않는다. 끈 상태에서 파일이 보이면 켜져 있는 것으로 읽힌다.
                if (_session == mine) Volatile.Write(ref _currentFile, path);
            }
        }

        /// <summary>
        /// 쓰는 쪽이 스스로 그만둘 때(상한, 열기·쓰기 실패) 자기 회차를 닫는다. 이미 다른 회차가 켜져 있으면 건드리지 않는다.
        /// 남의 회차를 끄면 켜 둔 사람의 기록이 조용히 멈춘다.
        /// </summary>
        private void CloseSession(Session mine)
        {
            mine.Deactivate();
            lock (_lifecycleLock)
            {
                if (_session != mine) return;
                Volatile.Write(ref _session, null);
                Volatile.Write(ref _currentFile, null);
            }
        }

        /// <summary>오버레이가 결과를 본 순간의 값. 쓰는 스레드가 나중에 상태를 읽지 않도록 그때 고정한다.</summary>
        private readonly record struct Row(AiClassificationResult Result, long NowMs, float Level, bool Shown);

        /// <summary>켠 한 회차. 큐와 "아직 받는지" 를 함께 들고 있어 회차끼리 섞이지 않는다.</summary>
        private sealed class Session(string stamp)
        {
            private readonly object _gate = new();
            private readonly Queue<Row> _queue = new(QueueCapacity);
            private bool _active = true;
            private bool _hasOffered;
            private long _lastOfferedMs;
            private int _dropped;

            /// <summary>파일 이름에 쓰는 켠 시각.</summary>
            public string Stamp { get; } = stamp;

            public int Dropped
            {
                get
                {
                    lock (_gate) return _dropped;
                }
            }

            public void Offer(in Row row)
            {
                lock (_gate)
                {
                    // 끈 뒤에 들어온 줄은 받지 않는다. 받은 줄은 쓰는 스레드가 모두 쓴다.
                    if (!_active) return;
                    // 렌더 루프는 프레임마다 같은 결과를 넘긴다. 큐가 같은 결과로 차서 새 결과를 밀어내지 않게 여기서 거른다.
                    long timestampMs = row.Result.TimestampMs;
                    if (_hasOffered && _lastOfferedMs == timestampMs) return;
                    _hasOffered = true;
                    _lastOfferedMs = timestampMs;
                    if (_queue.Count >= QueueCapacity)
                    {
                        // 가장 오래된 것을 버리고 새것을 넣는다. 최근 것이 궁금한 도구라 뒤를 살린다.
                        _queue.Dequeue();
                        _dropped++;
                    }
                    _queue.Enqueue(row);
                    Monitor.Pulse(_gate);
                }
            }

            /// <summary>다음 줄을 기다려 꺼낸다. 꺼졌고 남은 줄도 없으면 false.</summary>
            public bool TryTake(out Row row)
            {
                lock (_gate)
                {
                    while (_queue.Count == 0 && _active) Monitor.Wait(_gate);
                    return _queue.TryDequeue(out row);
                }
            }

            /// <summary>더 받지 않는다. 기다리던 쓰는 스레드를 깨워 남은 줄을 쓰고 나가게 한다.</summary>
            public void Deactivate()
            {
                lock (_gate)
                {
                    _active = false;
                    Monitor.PulseAll(_gate);
                }
            }
        }
    }
}
