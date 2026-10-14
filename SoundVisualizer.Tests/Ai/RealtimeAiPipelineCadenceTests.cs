// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Concurrent;
using SoundVisualizer.Ai;
using Xunit.Abstractions;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 분석 주기(#379). 앞의 세 테스트는 모바일 RealtimeAiPipelineCadenceTest 를 옮긴 것이고, 나머지는 스케줄러 루프가 그 계산을 실제로
/// 쓰는지를 가짜 시계로 한 걸음씩 돌려 본다. 벽시계를 쓰는 마지막 테스트는 느슨한 하한만 본다.
/// </summary>
public class RealtimeAiPipelineCadenceTests(ITestOutputHelper output)
{
    [Fact] // inferenceTimeIsSubtractedFromTickInterval
    public void 추론에_걸린_시간은_틱_간격에서_뺀다()
    {
        Assert.Equal(220L, RealtimeAiPipeline.RemainingTickDelayMs(250L, 1_000L, 1_030L, RealtimeAiPipeline.MinTickRestMs));
    }

    [Fact] // overrunKeepsMinimumRestInsteadOfStartingContinuousInference
    public void 틱이_간격보다_길어져도_쉬지_않고_잇지_않고_최소_휴식은_쉰다()
    {
        Assert.Equal(100L, RealtimeAiPipeline.RemainingTickDelayMs(250L, 1_000L, 1_300L, RealtimeAiPipeline.MinTickRestMs));
    }

    [Fact] // monotonicClockAnomalyDoesNotLengthenTheConfiguredInterval
    public void 끝이_시작보다_앞서_보여도_간격을_늘리지_않는다()
    {
        Assert.Equal(250L, RealtimeAiPipeline.RemainingTickDelayMs(250L, 1_001L, 1_000L, RealtimeAiPipeline.MinTickRestMs));
    }

    [Fact]
    public void 스케줄러는_틱_시작에서_다음_시작까지_250ms를_지키고_최소_100ms는_쉰다()
    {
        var scheduler = new SteppedScheduler(new ManualClock(1_000));
        var durations = new Queue<long>(new[] { 30L, 300L });
        var model = new FakeYamnetModel
        {
            // 추론이 걸린 시간만큼 가짜 시계를 움직인다. 이 콜백은 틱 스레드에서만 돈다.
            OnInfer = _ => scheduler.Clock.Advance(durations.Count > 0 ? durations.Dequeue() : 0),
        };
        using var pipeline = new RealtimeAiPipeline(
            16000, 1, model, AiTestData.ClassMap, clock: scheduler.Clock.Now, waitForNextTick: scheduler.Wait);

        pipeline.Start();
        Assert.Equal(250L, scheduler.NextDelay()); // 1,000ms: 소리가 모이기 전이라 바로 끝났다.
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        scheduler.Step();
        Assert.Equal(220L, scheduler.NextDelay()); // 1,250ms: 추론 30ms → 1,500ms 에 다음 틱
        scheduler.Step();
        Assert.Equal(100L, scheduler.NextDelay()); // 1,500ms: 추론 300ms → 쉬지 않고 잇지 않는다
        scheduler.Step();
        Assert.Equal(250L, scheduler.NextDelay()); // 1,900ms: 추론 0ms

        Assert.Equal(3, model.InferCalls);
        pipeline.Stop();
    }

    [Fact]
    public void 틱이_실패해도_스케줄러는_멈추지_않고_다음_틱을_돌린다()
    {
        var scheduler = new SteppedScheduler(new ManualClock(1_000));
        var failures = new ConcurrentQueue<Exception>();
        var model = new FakeYamnetModel
        {
            OnInfer = call =>
            {
                if (call <= 2) throw new InvalidOperationException($"inference {call} failed");
            },
        };
        using var pipeline = new RealtimeAiPipeline(
            16000,
            1,
            model,
            AiTestData.ClassMap,
            tickFailed: failure =>
            {
                failures.Enqueue(failure);
                // 실패를 받는 쪽이 던져도 루프는 살아 있어야 한다.
                if (failures.Count == 1) throw new InvalidOperationException("listener failed");
            },
            clock: scheduler.Clock.Now,
            waitForNextTick: scheduler.Wait);

        pipeline.Start();
        scheduler.NextDelay();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        for (int tick = 0; tick < 3; tick++)
        {
            scheduler.Step();
            scheduler.NextDelay();
        }

        Assert.Equal(new[] { "inference 1 failed", "inference 2 failed" }, failures.Select(failure => failure.Message));
        Assert.Equal(3, model.InferCalls);
        Assert.NotNull(pipeline.LastClassification()); // 세 번째 추론은 결과를 냈다.
        Assert.Equal(new RealtimeAiPipeline.InferenceStats(3, 1, 0), pipeline.Stats);
        pipeline.Stop();
    }

    [Fact]
    public void 실제_시계에서도_결과_사이가_최소_휴식보다_짧지_않고_틱은_배경_스레드에서_돈다()
    {
        string? tickThread = null;
        bool? background = null;
        var model = new FakeYamnetModel
        {
            OnInfer = _ =>
            {
                tickThread = Thread.CurrentThread.Name;
                background = Thread.CurrentThread.IsBackground;
            },
        };
        using var pipeline = new RealtimeAiPipeline(48000, 2, model, AiTestData.ClassMap);
        pipeline.Start();

        // 한 창은 미리 채워 둔다. 10ms 버퍼로만 채우면 Windows 의 Sleep 해상도(15.6ms) 때문에 한 창에 1.5초가 걸린다.
        pipeline.Ingest(PipelineTestAudio.Noise(2, PipelineTestAudio.WindowFrames(48000) * 2));
        // 그 뒤로는 캡처처럼 다른 스레드가 10ms 버퍼를 계속 넣는다(잡음이라 게이트가 열려 있다).
        float[] buffer = PipelineTestAudio.Noise(3, 480 * 2);
        using var stopFeeding = new CancellationTokenSource();
        var feeder = new Thread(() =>
        {
            while (!stopFeeding.IsCancellationRequested)
            {
                pipeline.Ingest(buffer);
                Thread.Sleep(10);
            }
        }) { IsBackground = true };
        feeder.Start();

        var timestamps = new List<long>();
        AiClassificationResult? seen = null;
        long deadline = Environment.TickCount64 + 1_600;
        while (Environment.TickCount64 < deadline)
        {
            var result = pipeline.LastClassification();
            if (result is not null && !ReferenceEquals(result, seen))
            {
                seen = result;
                timestamps.Add(result.TimestampMs);
            }
            Thread.Sleep(5);
        }
        stopFeeding.Cancel();
        feeder.Join();
        // 틱 스레드가 쓴 값은 그 스레드가 끝난 뒤에 읽는다(Stop 이 기다린다).
        pipeline.Stop();

        var intervals = timestamps.Zip(timestamps.Skip(1), (a, b) => b - a).ToList();
        output.WriteLine($"results={timestamps.Count} intervals=[{string.Join(", ", intervals)}] ms");
        Assert.True(timestamps.Count >= 3, $"only {timestamps.Count} results in 1.6 s");
        // 최소 휴식은 100ms 다. 쉬지 않고 연달아 돌면 몇 ms 간격이 된다. CI 의 지연을 생각해 하한만 느슨하게 본다.
        Assert.All(intervals, interval => Assert.True(interval >= 50, $"interval {interval} ms"));
        Assert.Equal("SV-AiTick", tickThread);
        Assert.True(background);
    }

    /// <summary>
    /// 스케줄러의 대기 자리. 틱이 끝나면 쉴 시간을 기록하고 테스트가 <see cref="Step"/> 할 때까지 멈춘 뒤 가짜 시계를 그만큼 움직인다.
    /// 그래서 틱 사이에 테스트가 소리를 넣거나 상태를 볼 수 있고, 실제로는 기다리지 않는다.
    /// </summary>
    private sealed class SteppedScheduler
    {
        private readonly BlockingCollection<long> _delays = new();
        private readonly SemaphoreSlim _step = new(0);

        public SteppedScheduler(ManualClock clock)
        {
            Clock = clock;
        }

        public ManualClock Clock { get; }

        public bool Wait(long delayMs, CancellationToken stop)
        {
            _delays.Add(delayMs);
            try
            {
                _step.Wait(stop);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            Clock.Advance(delayMs);
            return true;
        }

        public long NextDelay()
        {
            Assert.True(_delays.TryTake(out long delay, TimeSpan.FromSeconds(10)), "the scheduler did not finish a tick");
            return delay;
        }

        public void Step() => _step.Release();
    }
}
