// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using SoundVisualizer.Ai;
using Xunit.Abstractions;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 닫기·멈추기와 추론의 경합(#38, #49). 모바일 계측 테스트 세 개를 옮겼다.
/// - CloseContract: 순서를 정해 경계를 고정한다. 모바일은 리플렉션으로 추론 잠금을 직접 쥐었지만, 여기서는 추론 안에서 멈춰 서는
///   가짜 모델로 "오래 걸리는 추론"을 만든다. 틱을 닫힘 검사와 잠금 사이에 세울 때만 모바일처럼 링의 잠금을 리플렉션으로 쥔다.
/// - CloseRace: 실제 모델로 무작위 위상에서 두들긴다. 해제한 세션에 들어가면 예외가 실패 알림으로 오거나 테스트 프로세스가 죽는다.
/// - SchedulerClose: 실제 스케줄러 경로에서 같은 경합을 친다.
/// 시간 단언은 CI 에서도 버티도록 하한과 넉넉한 상한만 둔다.
/// </summary>
public class RealtimeAiPipelineCloseTests(ITestOutputHelper output)
{
    /// <summary>타이머 해상도(Windows 15.6ms)와 마감 계산의 반올림을 넉넉히 덮는다.</summary>
    private const int TimerSlackMs = 100;

    /// <summary>한도 없이 기다리는 회귀만 잡으면 된다. CI 의 지연을 생각해 넉넉히 둔다.</summary>
    private const int UpperSlackMs = 2000;

    /// <summary>닫기가 무한정 기다리도록 바뀌어도 테스트가 멈추지 않게, 가짜 추론이 붙잡는 시간에도 한도를 둔다.</summary>
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(10);

    private static readonly YamnetClassMap Names = AiTestData.ClassMap;

    [Fact] // closeOnMainThread_waitsForRunningInference_butGivesUpAtTimeout
    public void 닫기는_돌고_있는_추론을_기다리되_1초에서_포기하고_세션은_그_추론이_끝난_뒤에_닫힌다()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new FakeYamnetModel
        {
            OnInfer = _ =>
            {
                entered.Set();
                release.Wait(HoldLimit);
            },
        };
        var pipeline = new RealtimeAiPipeline(16000, 1, model, Names, clock: new ManualClock().Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        AiTickOutcome? outcome = null;
        var ticker = new Thread(() => outcome = pipeline.RunTickOnce(0)) { IsBackground = true };
        ticker.Start();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "inference did not start");

        long started = Stopwatch.GetTimestamp();
        pipeline.Dispose();
        double elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        bool disposedWhileRunning = model.IsDisposed;
        release.Set();
        Assert.True(ticker.Join(TimeSpan.FromSeconds(5)), "tick did not finish");
        output.WriteLine($"Dispose while a tick is inside the model: {elapsedMs:F0} ms (limit {RealtimeAiPipeline.CloseWaitMs} ms)");

        // 기다리지 않고 닫으면 쓰는 중인 세션을 해제한다(use-after-free). 한도 없이 기다리면 UI 스레드가 멈춘다.
        Assert.True(elapsedMs >= RealtimeAiPipeline.CloseWaitMs - TimerSlackMs, $"Dispose returned after {elapsedMs} ms without waiting");
        Assert.True(elapsedMs < RealtimeAiPipeline.CloseWaitMs + UpperSlackMs, $"Dispose blocked for {elapsedMs} ms");
        Assert.False(disposedWhileRunning, "the session was released while the inference was still using it");

        // 닫기는 틱을 죽이지 않았고, 그 틱이 끝나면서 세션을 닫았다(모바일은 이때 세션을 버린다).
        Assert.Equal(AiTickOutcome.Completed, outcome);
        Assert.Equal(1, model.DisposeCalls);
        Assert.False(model.DisposedDuringInference);
        Assert.Null(pipeline.LastClassification()); // 닫힌 뒤 끝난 추론의 결과는 보이지 않는다.
    }

    [Fact] // closeWhileIdle_closesSessionsRightAway
    public void 한가할_때_닫으면_세션을_바로_닫는다()
    {
        var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
        var pipeline = new RealtimeAiPipeline(16000, 1, yamnet, Names, clock: new ManualClock().Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(0)); // 세션을 데워 둔다.

        long started = Stopwatch.GetTimestamp();
        pipeline.Dispose();
        double elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        output.WriteLine($"idle Dispose: {elapsedMs:F1} ms");

        Assert.True(elapsedMs < RealtimeAiPipeline.CloseWaitMs, $"idle Dispose took {elapsedMs} ms");
        // 닫힌 세션은 네이티브로 들어가기 전에 막힌다(모바일의 "Trying to score a closed OrtSession." 확인과 같다).
        Assert.Throws<ObjectDisposedException>(() =>
            yamnet.Infer(new float[YamnetInference.LogMelSize], new float[YamnetInference.NumClasses], new float[YamnetInference.NumClasses]));
    }

    [Fact] // tickEnteredBeforeClose_doesNotInferOnceClosedIsSet
    public void 닫기_전에_들어온_틱도_잠금을_잡은_뒤_닫힌_것을_보면_추론하지_않는다()
    {
        var failures = new ConcurrentQueue<Exception>();
        var model = new FakeYamnetModel();
        var pipeline = new RealtimeAiPipeline(
            16000, 1, model, Names, tickFailed: failures.Enqueue, clock: new ManualClock().Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(0));
        int before = model.InferCalls;

        // 링의 잠금을 쥐고 있으면 틱은 닫힘 검사를 지난 뒤 링 검사에서 멈춘다. 그사이 실제 Dispose 가 닫힘을 세우고 모델을 닫는다.
        // Dispose 는 링을 건드리지 않으므로 여기서 막히지 않는다. 잠금 안의 재확인이 없으면 틱이 닫힌 모델로 추론한다.
        object ringLock = RingLock(pipeline.AudioBufferForTest);
        AiTickOutcome? outcome = null;
        var ticker = new Thread(() => outcome = pipeline.RunTickOnce(250)) { IsBackground = true };
        lock (ringLock)
        {
            ticker.Start();
            AwaitBlocked(ticker);
            pipeline.Dispose();
            Assert.Equal(1, model.DisposeCalls);
        }

        Assert.True(ticker.Join(TimeSpan.FromSeconds(10)), "tick did not finish");
        Assert.Equal(AiTickOutcome.Closed, outcome);
        Assert.Equal(before, model.InferCalls);
        Assert.False(model.UsedAfterDispose);
        Assert.Empty(failures);
    }

    [Fact] // closeDuringInference_neverCrashesOrThrows
    public void 실제_모델로_추론하는_중에_닫아도_예외나_크래시가_없다()
    {
        const int iterations = 25;
        var random = new Random(1234);
        float[] chunk = PipelineTestAudio.Noise(99, 2 * 44100);
        long totalExecuted = 0;
        double maxCloseMs = 0;

        for (int i = 0; i < iterations; i++)
        {
            var failures = new ConcurrentQueue<Exception>();
            var yamnet = YamnetInference.Create(AiTestData.ModelDirectory);
            // 프로덕션의 틱 경로(RunTickOnce)를 테스트 스레드에서 쉬지 않고 돌려, 닫기가 추론 한복판에 걸리게 한다.
            var pipeline = new RealtimeAiPipeline(44100, 2, yamnet, Names, tickFailed: failures.Enqueue, runScheduler: false);
            pipeline.Start();
            pipeline.Ingest(chunk);
            Exception? tickerFailure = null;
            using var stop = new CancellationTokenSource();
            var ticker = new Thread(() =>
            {
                try
                {
                    while (!stop.IsCancellationRequested) pipeline.RunTickOnce(Environment.TickCount64);
                }
                catch (Exception ex)
                {
                    tickerFailure = ex;
                }
            }) { IsBackground = true };
            ticker.Start();

            // 추론이 실제로 시작된 뒤에 친다. 그냥 몇 ms 자고 닫으면 빈 잠금만 거듭 확인하게 된다(#49).
            Assert.True(AwaitFirstInference(pipeline, TimeSpan.FromSeconds(5)), $"iteration {i}: inference did not start");
            int jitterMs = random.Next(0, 4);
            if (jitterMs > 0) Thread.Sleep(jitterMs);

            long started = Stopwatch.GetTimestamp();
            pipeline.Dispose();
            maxCloseMs = Math.Max(maxCloseMs, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            stop.Cancel();
            Assert.True(ticker.Join(TimeSpan.FromSeconds(10)), $"iteration {i}: ticker did not stop");

            Assert.Null(tickerFailure);
            Assert.Empty(failures); // 해제한 세션에 들어간 틱이 있었다면 ObjectDisposedException 이 여기 온다.
            var stats = pipeline.Stats;
            Assert.Equal(stats.Executed, stats.Completed); // 시작한 추론은 모두 결과를 남기고 끝났다.
            Assert.Throws<ObjectDisposedException>(() =>
                yamnet.Infer(new float[YamnetInference.LogMelSize], new float[YamnetInference.NumClasses], new float[YamnetInference.NumClasses]));
            totalExecuted += stats.Executed;
        }

        output.WriteLine($"close race: executed={totalExecuted} in {iterations} iterations, max Dispose {maxCloseMs:F1} ms");
        Assert.True(totalExecuted >= iterations);
        Assert.True(maxCloseMs < RealtimeAiPipeline.CloseWaitMs + UpperSlackMs, $"Dispose blocked for {maxCloseMs} ms");
    }

    [Fact] // schedulerPath_closeDuringInference_neverLosesATick
    public void 스케줄러가_추론하는_중에_닫아도_틱이_도중에_죽지_않는다()
    {
        const int iterations = 8;
        var random = new Random(4321);
        float[] chunk = PipelineTestAudio.Noise(7, 2 * 44100);
        long totalExecuted = 0;
        int iterationsWithInference = 0;

        for (int i = 0; i < iterations; i++)
        {
            var failures = new ConcurrentQueue<Exception>();
            var pipeline = RealtimeAiPipeline.Create(44100, 2, AiTestData.ModelDirectory, Names, tickFailed: failures.Enqueue);
            pipeline.Start();
            pipeline.Ingest(chunk); // 시작한 뒤에만 받는다(캡처 스레드와 같다).

            if (AwaitFirstInference(pipeline, TimeSpan.FromSeconds(3))) iterationsWithInference++;
            int jitterMs = random.Next(0, 4);
            if (jitterMs > 0) Thread.Sleep(jitterMs);
            pipeline.Dispose();

            // 1초에서 포기한 닫기 뒤에도 돌던 추론은 끝난다. 그 완료만 잠깐 기다린다.
            long executed = pipeline.Stats.Executed;
            AwaitCompleted(pipeline, executed, TimeSpan.FromSeconds(2));
            Assert.Equal(executed, pipeline.Stats.Completed);
            Assert.Empty(failures);
            totalExecuted += executed;
        }

        output.WriteLine($"scheduler close: executed={totalExecuted} in {iterationsWithInference}/{iterations} iterations");
        Assert.True(totalExecuted > 0, "no inference ran during the race; the test would prove nothing");
        Assert.True(iterationsWithInference >= iterations / 2, $"only {iterationsWithInference}/{iterations} iterations reached inference");
    }

    [Fact]
    public void 다시_시작하면_앞선_추론이_끝나기를_기다렸다_비우므로_그_결과가_되살아나지_않는다()
    {
        // 모바일은 멈출 때 돌던 추론을 기다리지 않아, 그 결과가 다시 시작한 뒤에 되살아날 수 있었다(서비스가 재시작을 미뤄 피했다).
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new FakeYamnetModel
        {
            OnInfer = call =>
            {
                if (call != 1) return;
                entered.Set();
                release.Wait(HoldLimit);
            },
        };
        using var pipeline = new RealtimeAiPipeline(16000, 1, model, Names, clock: new ManualClock().Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        AiTickOutcome? outcome = null;
        var ticker = new Thread(() => outcome = pipeline.RunTickOnce(0)) { IsBackground = true };
        ticker.Start();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "inference did not start");

        pipeline.Stop();
        // 다시 시작하는 스레드가 추론 잠금에서 기다리기 시작한 뒤에 앞선 추론을 끝낸다.
        var starter = Thread.CurrentThread;
        var releaser = new Thread(() =>
        {
            AwaitBlocked(starter);
            release.Set();
        }) { IsBackground = true };
        releaser.Start();
        pipeline.Start();

        Assert.True(ticker.Join(TimeSpan.FromSeconds(5)), "tick did not finish");
        Assert.Equal(AiTickOutcome.Completed, outcome); // 앞선 추론은 결과를 게시하고 끝났지만
        Assert.Null(pipeline.LastClassification()); // 다시 시작하며 비웠으므로 보이지 않는다.
        Assert.Equal(new RealtimeAiPipeline.InferenceStats(0, 0, 0), pipeline.Stats);
        Assert.Equal(0, pipeline.AudioBufferForTest.AvailableSamples);
    }

    [Fact]
    public void 앞선_추론이_1초_넘게_끝나지_않으면_다시_시작하지_않고_알린다()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new FakeYamnetModel
        {
            OnInfer = call =>
            {
                if (call != 1) return;
                entered.Set();
                release.Wait(HoldLimit);
            },
        };
        using var pipeline = new RealtimeAiPipeline(16000, 1, model, Names, clock: new ManualClock().Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        var ticker = new Thread(() => pipeline.RunTickOnce(0)) { IsBackground = true };
        ticker.Start();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "inference did not start");
        pipeline.Stop();

        long started = Stopwatch.GetTimestamp();
        Assert.Throws<TimeoutException>(() => pipeline.Start());
        double elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Assert.True(elapsedMs >= RealtimeAiPipeline.CloseWaitMs - TimerSlackMs, $"Start gave up after {elapsedMs} ms");
        Assert.False(pipeline.IsRunning);

        release.Set();
        Assert.True(ticker.Join(TimeSpan.FromSeconds(5)), "tick did not finish");
        pipeline.Start(); // 추론이 끝났으니 이제 시작한다.
        Assert.True(pipeline.IsRunning);
    }

    [Fact]
    public void 멈추면_돌고_있는_틱을_기다리고_그_뒤로는_틱이_돌지_않는다()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var model = new FakeYamnetModel
        {
            OnInfer = call =>
            {
                if (call != 1) return;
                entered.Set();
                release.Wait(HoldLimit);
            },
        };
        // 실제 스케줄러와 시계로 돈다. 첫 틱은 소리가 모이기 전이고, 250ms 뒤의 틱이 추론에 들어가 멈춰 선다.
        using var pipeline = new RealtimeAiPipeline(16000, 1, model, Names);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "inference did not start");

        var releaser = new Thread(() =>
        {
            Thread.Sleep(200);
            release.Set();
        }) { IsBackground = true };
        releaser.Start();
        long started = Stopwatch.GetTimestamp();
        pipeline.Stop();
        double elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        // Stop 은 돌던 추론이 끝날 때까지 기다렸다(약 200ms).
        Assert.True(elapsedMs >= 150, $"Stop returned after {elapsedMs} ms without waiting for the running tick");
        Assert.True(elapsedMs < RealtimeAiPipeline.CloseWaitMs + UpperSlackMs, $"Stop blocked for {elapsedMs} ms");
        Assert.Equal(1, model.InferCalls);
        // 틱 두 번 넘게 지나도 더는 추론하지 않는다.
        Thread.Sleep(600);
        Assert.Equal(1, model.InferCalls);
        Assert.Null(pipeline.LastClassification());
    }

    /// <summary>링 버퍼의 내부 잠금. 틱을 "닫힘 검사 직후 ~ 추론 잠금 직전" 에 세울 수 있는 유일한 자리다(모바일 PipelineInternals.ringMonitor).</summary>
    private static object RingLock(AiAudioBuffer buffer)
    {
        var field = typeof(AiAudioBuffer).GetField("_lock", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field); // 이름이 바뀌면 여기서 바로 드러난다.
        return field.GetValue(buffer)!;
    }

    private static void AwaitBlocked(Thread thread)
    {
        long deadline = Environment.TickCount64 + 5_000;
        while (Environment.TickCount64 < deadline)
        {
            if ((thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0) return;
            Thread.Sleep(1);
        }
        throw new TimeoutException($"thread did not block (state={thread.ThreadState})");
    }

    /// <summary>Executed 가 오르면 그 추론은 지금 추론 잠금 안에서 돌고 있다.</summary>
    private static bool AwaitFirstInference(RealtimeAiPipeline pipeline, TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (pipeline.Stats.Executed > 0) return true;
            Thread.Sleep(1);
        }
        return false;
    }

    private static void AwaitCompleted(RealtimeAiPipeline pipeline, long expected, TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline && pipeline.Stats.Completed < expected)
        {
            Thread.Sleep(1);
        }
    }
}
