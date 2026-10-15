// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using Xunit.Abstractions;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// 실제 YAMNet 으로 도는 파이프라인을 스케줄러 없이 틱 하나씩, 시각을 정해 돌린다. 모바일 계측 테스트
/// RealtimeAiPipelineInstrumentedTest 를 옮기고, 무음 게이트·실제 소리·실패·수명 경로를 더했다.
/// </summary>
public class RealtimeAiPipelineTests(ITestOutputHelper output)
{
    private const int Window44 = 42998;
    private static readonly YamnetClassMap Names = AiTestData.ClassMap;

    [Fact]
    public void 한_창이_모이기_전에는_추론하지_않는다()
    {
        var clock = new ManualClock();
        using var pipeline = NewPipeline(44100, 2, clock);
        pipeline.Start();
        float[] alarm = Alarm();

        PipelineTestAudio.IngestInChunks(pipeline, alarm[..^2]); // 한 프레임 모자란다.
        Assert.Equal(AiTickOutcome.NotEnoughAudio, pipeline.RunTickOnce(clock.Now()));
        Assert.Equal(0, pipeline.Stats.Executed);
        Assert.Null(pipeline.LastClassification());

        pipeline.Ingest(alarm.AsSpan(alarm.Length - 2));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));
        Assert.NotNull(pipeline.LastClassification());
    }

    [Fact]
    public void 소리가_그치면_창이_빠져나간_뒤_조용한_추론_두_번을_마치고_추론을_멈춘다()
    {
        var clock = new ManualClock(10_000);
        using var pipeline = NewPipeline(44100, 2, clock);
        pipeline.Start();

        PipelineTestAudio.IngestInChunks(pipeline, Alarm()); // 10,000ms 의 소리가 마지막 소리다.
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(10_000));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(10_500));
        clock.Set(10_900);
        PipelineTestAudio.IngestInChunks(pipeline, new float[Window44 * 2]); // 무음은 게이트를 다시 열지 않는다.
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(10_975)); // 창(975ms)이 지난 뒤 조용한 추론 1
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(11_225)); // 조용한 추론 2
        Assert.Equal(AiTickOutcome.SkippedForSilence, pipeline.RunTickOnce(11_475));
        Assert.Equal(AiTickOutcome.SkippedForSilence, pipeline.RunTickOnce(20_000));
        Assert.Equal(new RealtimeAiPipeline.InferenceStats(4, 4, 2), pipeline.Stats);

        // 조용한 추론으로 후처리를 정리했으므로 마지막 라벨은 소리가 그친 뒤의 무음이다.
        var last = pipeline.LastClassification();
        Assert.NotNull(last);
        Assert.Equal(SoundType.Ambient, last.Coarse);
        Assert.Equal("Silence", last.Display);

        // 소리가 다시 나면 바로 연다.
        clock.Set(20_000);
        PipelineTestAudio.IngestInChunks(pipeline, Alarm());
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(20_000));
    }

    [Fact]
    public void 조용한_창의_결과는_Python_e2e_기준과_같다()
    {
        // yamnet_e2e_meta.json 에서 무음 경우만은 지금 파이프라인에도 유효하다. 무음의 로그멜은 옛 전처리와 Qualcomm 전처리에서
        // 똑같이 ln(0.001) 이고(e2e_silence_logmel.bin 과 qualcomm_source_silence_logmel.bin 이 같은 파일이다), 옛 Booster 도
        // 이 경우를 받아들이지 않아 결과에 흔적이 없다.
        var meta = AiTestData.LoadJson("yamnet_e2e_meta.json").GetProperty("cases").GetProperty("silence");
        var clock = new ManualClock();
        using var pipeline = NewPipeline(44100, 2, clock);
        pipeline.Start();

        // 무음만으로는 게이트가 열리지 않는다. 짧은 소리로 연 뒤 한 창을 무음으로 채우면 창에는 무음만 남는다.
        pipeline.Ingest(Enumerable.Repeat(0.5f, 2048).ToArray());
        clock.Set(10);
        PipelineTestAudio.IngestInChunks(pipeline, new float[Window44 * 2]);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(10));

        Assert.True(pipeline.Mono16kForTest.IndexOfAnyExcept(0f) < 0, "window must be pure silence");
        Assert.True(MaxAbs(pipeline.LogMelForTest, Load("qualcomm_source_silence_logmel.bin", QualcommSourceAudioPreprocessor.LogMelSize)) < 1e-6);
        double probError = MaxAbs(pipeline.ProbabilitiesForTest, Load("e2e_silence_probs.bin", YamnetInference.NumClasses));
        output.WriteLine($"silence probs vs Python e2e maxAbs={probError:E3}");
        Assert.True(probError < 1e-4, $"silence probs maxAbs {probError}");

        var result = pipeline.LastClassification();
        Assert.NotNull(result);
        Assert.Equal(meta.GetProperty("ui_coarse").GetString(), result.Coarse.ToLabel());
        Assert.Equal(meta.GetProperty("ui_display").GetString(), result.Display);
        // 옛 이름 pre_booster_coarse 는 상위 3개 투표다. Booster 가 없어진 지금은 투표가 곧 결과의 바탕이다.
        var vote = new YamnetCoarseClassifier(Names).Classify(pipeline.ProbabilitiesForTest.ToArray());
        Assert.Equal(meta.GetProperty("pre_booster_coarse").GetString(), vote.Coarse.ToLabel());
    }

    [Fact]
    public void 실제_소리를_캡처처럼_넣으면_결과를_게시하고_소요시간과_상위_5개가_그럴듯하다()
    {
        // yamnet_e2e_meta.json 의 창 크기는 지금 파이프라인과 같다.
        var meta = AiTestData.LoadJson("yamnet_e2e_meta.json");
        Assert.Equal(44100, meta.GetProperty("capture_sample_rate").GetInt32());
        Assert.Equal(CaptureAudioMath.RequiredMono16kSamples, meta.GetProperty("required_mono_16k").GetInt32());
        Assert.Equal(PipelineTestAudio.WindowFrames(44100), meta.GetProperty("capture_window_samples").GetInt32());
        // 알람·총소리 경우의 pre_booster_coarse·gunshot_score·booster_accepted·ui_coarse·ui_display 는 확인하지 않는다.
        // 없앤 Gunshot Booster 와 옛 전처리(AudioPreprocessor, e2e_*_logmel.bin) 시절의 값이다. 지금의 Qualcomm 로그멜은
        // 이 두 소리에서 옛 로그멜과 최대 8~10 이나 다르고, 지금 파이프라인은 이 알람을 Music(환경음)으로 듣는다.
        // 메타의 위협음은 Booster 가 받아들여 바꾼 결과다(알람의 "Gunshot, gunfire" 표시도 그 오탐이다, #116).

        var clock = new ManualClock(5_000);
        using var pipeline = NewPipeline(44100, 2, clock);
        pipeline.Start();
        PipelineTestAudio.IngestInChunks(pipeline, Alarm());
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));
        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 링 → 16kHz 는 Python 기준과, 로그멜은 Qualcomm 원본 전처리의 Python 기준과 같다.
        double monoError = MaxAbs(pipeline.Mono16kForTest, Load("e2e_alarm_mono16k.bin", CaptureAudioMath.RequiredMono16kSamples));
        float[] qualcommLogMel = Load("qualcomm_source_alarm_logmel.bin", QualcommSourceAudioPreprocessor.LogMelSize);
        double logMelError = MaxAbs(pipeline.LogMelForTest, qualcommLogMel);
        // 확률은 그 Qualcomm 기준 로그멜을 모델에 바로 넣은 것과 같다.
        var expectedProbs = new float[YamnetInference.NumClasses];
        using (var direct = YamnetInference.Create(AiTestData.ModelDirectory))
        {
            direct.Infer(qualcommLogMel, new float[YamnetInference.NumClasses], expectedProbs);
        }
        double probError = MaxAbs(pipeline.ProbabilitiesForTest, expectedProbs);
        var result = pipeline.LastClassification();
        Assert.NotNull(result);
        output.WriteLine($"mono16k maxAbs={monoError:E3} logmel maxAbs={logMelError:E3} probs maxAbs={probError:E3}");
        output.WriteLine($"{result.Coarse}/{result.Display}/{result.Confidence:F4} pre={result.PreprocessMs:F2}ms yamnet={result.YamnetMs:F2}ms total={result.TotalMs:F2}ms");
        Assert.True(monoError < 1e-5, $"mono16k maxAbs {monoError}");
        Assert.True(logMelError < 1e-5, $"log-mel maxAbs {logMelError}");
        Assert.True(probError < 1e-4, $"probs maxAbs {probError}");

        // 상위 5개는 이번 확률에서 큰 순서로 고른 실제 클래스다.
        float[] probs = pipeline.ProbabilitiesForTest.ToArray();
        Assert.Equal(5, result.Top5.Count);
        Assert.Equal(Enumerable.Range(0, probs.Length).OrderByDescending(i => probs[i]).Take(5), result.Top5.Select(hit => hit.Index));
        foreach (var hit in result.Top5)
        {
            Assert.Equal(Names[hit.Index], hit.Name);
            Assert.Equal(probs[hit.Index], hit.Probability);
        }

        // 소요시간은 밀리초로, 전처리·모델이 전체 안에 든다. 상한은 단위를 잘못 넣었는지만 본다(틱 단위면 1만 배가 된다).
        Assert.True(result.PreprocessMs > 0);
        Assert.True(result.YamnetMs > 0);
        Assert.True(result.TotalMs >= result.PreprocessMs + result.YamnetMs - 1e-6);
        Assert.True(result.TotalMs < 10_000, $"total {result.TotalMs} ms");
        Assert.InRange(result.TimestampMs, before, after);
        Assert.Equal("default", result.MappingSignature);
        Assert.Equal(0, result.MappingOverrideCount);
    }

    [Fact] // realtimePipeline_logMelMatchesQualcommFrontend
    public void 파이프라인은_Qualcomm_로그멜을_쓴다()
    {
        float[] mono = PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples);
        var clock = new ManualClock();
        using var pipeline = NewPipeline(16000, 1, clock);
        pipeline.Start();

        // 캡처 쪽 입구로 넣어야 무음 게이트가 열려 틱이 링을 읽는다.
        pipeline.Ingest(mono);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));

        float[] expected = new QualcommSourceAudioPreprocessor().ComputeLogMelSpectrogram(mono);
        Assert.True(MaxAbs(pipeline.LogMelForTest, expected) < 1e-5, "pipeline did not use the Qualcomm frontend");
    }

    [Fact] // runtimeMappingChangeInvalidatesPreviousResultAndResetsConfirmation
    public void 실행_중_매핑이_바뀌면_옛_결과를_바로_숨기고_새_매핑으로_투표한다()
    {
        IReadOnlyDictionary<string, string> overrides = Names.ToDictionary(name => name, _ => "danger");
        float[] mono = PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples);
        var clock = new ManualClock();
        using var pipeline = NewPipeline(16000, 1, clock, () => overrides);
        pipeline.Start();

        pipeline.Ingest(mono);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));
        var danger = Vote(pipeline, overrides);
        Assert.True(danger.DangerScore > 0f);
        Assert.Equal(0f, danger.AmbientScore);
        Assert.NotNull(pipeline.LastClassification());

        overrides = Names.ToDictionary(name => name, _ => "ambient");
        Assert.Null(pipeline.LastClassification()); // 다음 추론을 기다리지 않고 바로 사라진다.
        pipeline.Ingest(mono);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));
        Assert.Equal(0f, Vote(pipeline, overrides).DangerScore);
        var ambient = pipeline.LastClassification();
        Assert.NotNull(ambient);
        Assert.Equal(SoundType.Ambient, ambient.Coarse);
        Assert.Equal(YamnetMappingPolicy.From(overrides, Names).Signature, ambient.MappingSignature);

        overrides = new Dictionary<string, string>();
        Assert.Null(pipeline.LastClassification());
        pipeline.Ingest(mono);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));
        Assert.Equal("default", pipeline.LastClassification()?.MappingSignature);
    }

    [Fact]
    public void 매핑이_바뀌면_확정을_처음부터_다시_해서_옛_위협음에서_곧바로_넘어가지_않는다()
    {
        // 모두 위협음이면 알람(Music .97)이 첫 프레임에 위협음으로 확정된다. 모두 대화음으로 바꾸면 히스테리시스가 처음(환경음)부터
        // 다시 시작해 대화음은 두 프레임이 이어져야 확정된다. 다시 시작하지 않았다면 위협음에서 확신도 0.27 이상으로 빠져나가는
        // 규칙 때문에 첫 프레임에 대화음이 됐을 것이다.
        IReadOnlyDictionary<string, string> overrides = Names.ToDictionary(name => name, _ => "danger");
        var clock = new ManualClock();
        using var pipeline = NewPipeline(44100, 2, clock, () => overrides);
        pipeline.Start();
        PipelineTestAudio.IngestInChunks(pipeline, Alarm());

        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(0));
        Assert.Equal(SoundType.Danger, pipeline.LastClassification()?.Coarse);

        overrides = Names.ToDictionary(name => name, _ => "speech");
        Assert.Null(pipeline.LastClassification());
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(250));
        var first = pipeline.LastClassification();
        Assert.NotNull(first);
        Assert.True(first.MeetsThreshold);
        Assert.Equal(SoundType.Ambient, first.Coarse);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(500));
        Assert.Equal(SoundType.Speech, pipeline.LastClassification()?.Coarse);
    }

    [Fact]
    public void 무음으로_건너뛴_틱에도_매핑을_반영해_옛_위협음이_되살아나지_않는다()
    {
        IReadOnlyDictionary<string, string> allDanger = Names.ToDictionary(name => name, _ => "danger");
        IReadOnlyDictionary<string, string> overrides = allDanger;
        var clock = new ManualClock();
        using var pipeline = NewPipeline(44100, 2, clock, () => overrides);
        pipeline.Start();
        PipelineTestAudio.IngestInChunks(pipeline, Alarm());
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(0));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(975));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(1_225));
        Assert.Equal(AiTickOutcome.SkippedForSilence, pipeline.RunTickOnce(1_475));
        Assert.Equal(SoundType.Danger, pipeline.LastClassification()?.Coarse);

        // 조용한 동안 사용자가 모두 환경음으로 바꿨다가 되돌린다. 바꾼 사이에 돈 틱이 무음으로 건너뛰어도 매핑을 반영해
        // 확정 상태와 마지막 결과를 비운다. 반영하지 않으면 되돌린 순간 옛 매핑의 위협음 결과가 다시 보인다.
        overrides = Names.ToDictionary(name => name, _ => "ambient");
        Assert.Null(pipeline.LastClassification());
        Assert.Equal(AiTickOutcome.SkippedForSilence, pipeline.RunTickOnce(1_725));
        overrides = allDanger;
        Assert.Null(pipeline.LastClassification());
    }

    [Fact]
    public void 시작_전과_멈춘_뒤에는_소리를_받지_않고_결과를_숨기며_다시_시작하면_처음부터_한다()
    {
        float[] mono = PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples);
        var clock = new ManualClock();
        using var pipeline = NewPipeline(16000, 1, clock);

        pipeline.Ingest(mono); // 시작 전에는 버린다.
        Assert.Equal(0, pipeline.AudioBufferForTest.AvailableSamples);
        Assert.False(pipeline.IsRunning);

        pipeline.Start();
        Assert.True(pipeline.IsRunning);
        pipeline.Ingest(mono);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(clock.Now()));
        var result = pipeline.LastClassification();
        Assert.NotNull(result);

        pipeline.Start(); // 이미 돌고 있으면 아무것도 하지 않는다(비우지 않는다).
        Assert.Same(result, pipeline.LastClassification());
        Assert.Equal(mono.Length, pipeline.AudioBufferForTest.AvailableSamples);

        pipeline.Stop();
        Assert.False(pipeline.IsRunning);
        Assert.Null(pipeline.LastClassification()); // 멈추면 마지막 결과를 바로 숨긴다.
        pipeline.Ingest(mono); // 멈춘 뒤에도 버린다.
        Assert.Equal(mono.Length, pipeline.AudioBufferForTest.AvailableSamples);
        pipeline.Stop(); // 여러 번 불러도 된다.

        pipeline.Start(); // 링·게이트·히스테리시스·결과·횟수를 비운다.
        Assert.Equal(0, pipeline.AudioBufferForTest.AvailableSamples);
        Assert.Equal(new RealtimeAiPipeline.InferenceStats(0, 0, 0), pipeline.Stats);
        Assert.Null(pipeline.LastClassification());
        Assert.Equal(AiTickOutcome.NotEnoughAudio, pipeline.RunTickOnce(clock.Now()));
    }

    [Fact]
    public void 추론이_실패하면_알리고_무음_게이트에_넘기며_되살아나면_다시_결과를_낸다()
    {
        var failures = new List<Exception>();
        var model = new FakeYamnetModel { OnInfer = _ => throw new InvalidOperationException("inference failed") };
        var clock = new ManualClock();
        using var pipeline = new RealtimeAiPipeline(
            16000, 1, model, Names, tickFailed: failures.Add, clock: clock.Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));

        Assert.Equal(AiTickOutcome.Failed, pipeline.RunTickOnce(0));
        Assert.IsType<InvalidOperationException>(Assert.Single(failures));
        Assert.Equal(new RealtimeAiPipeline.InferenceStats(1, 0, 0), pipeline.Stats);
        Assert.Null(pipeline.LastClassification());

        // 소리가 창을 빠져나간 뒤에도 계속 실패하면 무음 게이트가 창 네 개(3.9초)가 지나 추론을 멈춘다. 실패가 게이트에 닿았다는 뜻이다.
        Assert.Equal(AiTickOutcome.Failed, pipeline.RunTickOnce(1_000));
        Assert.Equal(AiTickOutcome.Failed, pipeline.RunTickOnce(1_000 + AiSilenceGate.FailureFallbackMs - 1));
        Assert.Equal(AiTickOutcome.SkippedForSilence, pipeline.RunTickOnce(1_000 + AiSilenceGate.FailureFallbackMs));
        Assert.Equal(3, failures.Count);

        model.OnInfer = null;
        clock.Set(6_000);
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(6_000));
        Assert.NotNull(pipeline.LastClassification());
    }

    [Fact]
    public void 사용자_지정을_읽다_실패하면_알리고_추론하지_않는다()
    {
        var failures = new List<Exception>();
        bool broken = true;
        var model = new FakeYamnetModel();
        var clock = new ManualClock();
        using var pipeline = new RealtimeAiPipeline(
            16000,
            1,
            model,
            Names,
            () => broken ? throw new InvalidOperationException("settings unavailable") : new Dictionary<string, string>(),
            failures.Add,
            clock.Now,
            runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));

        Assert.Equal(AiTickOutcome.Failed, pipeline.RunTickOnce(0));
        Assert.Equal("settings unavailable", Assert.Single(failures).Message);
        Assert.Equal(0, model.InferCalls);

        broken = false;
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(250));
    }

    [Fact]
    public void 실패를_받는_쪽이_던져도_틱은_예외를_내지_않는다()
    {
        var model = new FakeYamnetModel { OnInfer = _ => throw new InvalidOperationException("inference failed") };
        var clock = new ManualClock();
        using var pipeline = new RealtimeAiPipeline(
            16000, 1, model, Names, tickFailed: _ => throw new InvalidOperationException("listener failed"), clock: clock.Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples));

        Assert.Equal(AiTickOutcome.Failed, pipeline.RunTickOnce(0));
    }

    [Fact]
    public void 닫은_뒤에는_틱도_소리도_결과도_없고_두_번_닫아도_된다()
    {
        float[] mono = PipelineTestAudio.Sine(CaptureAudioMath.RequiredMono16kSamples);
        var model = new FakeYamnetModel();
        var clock = new ManualClock();
        var pipeline = new RealtimeAiPipeline(16000, 1, model, Names, clock: clock.Now, runScheduler: false);
        pipeline.Start();
        pipeline.Ingest(mono);
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(0));

        pipeline.Dispose();
        Assert.Equal(1, model.DisposeCalls);
        Assert.False(pipeline.IsRunning);
        Assert.Null(pipeline.LastClassification());
        Assert.Equal(AiTickOutcome.Closed, pipeline.RunTickOnce(250));
        pipeline.Start();
        Assert.False(pipeline.IsRunning);
        pipeline.Ingest(mono);
        Assert.Equal(mono.Length, pipeline.AudioBufferForTest.AvailableSamples);

        pipeline.Dispose();
        Assert.Equal(1, model.DisposeCalls);
        Assert.Equal(1, model.InferCalls);
        Assert.False(model.UsedAfterDispose);
    }

    [Theory]
    [InlineData(22050)]
    [InlineData(8000)]
    [InlineData(0)]
    public void AI_가_받지_않는_레이트면_모델을_읽기_전에_거절한다(int sampleRate)
    {
        // 없는 모델 폴더를 준다. 레이트에서 먼저 걸러야 파일을 찾지 못했다는 오류가 나지 않는다.
        string missing = Path.Combine(Path.GetTempPath(), "sv-no-model-" + Guid.NewGuid().ToString("N"));

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => RealtimeAiPipeline.Create(sampleRate, 2, missing, Names));

        Assert.Equal("captureSampleRate", error.ParamName);
    }

    [Fact]
    public void 채널이_없거나_클래스가_521개가_아니거나_모델이_없으면_만들지_않는다()
    {
        string missing = Path.Combine(Path.GetTempPath(), "sv-no-model-" + Guid.NewGuid().ToString("N"));

        Assert.Equal("channels", Assert.Throws<ArgumentOutOfRangeException>(() => RealtimeAiPipeline.Create(44100, 0, missing, Names)).ParamName);
        var threeNames = new YamnetClassMap(new[] { "Speech", "Music", "Silence" });
        Assert.Equal("classes", Assert.Throws<ArgumentException>(() => RealtimeAiPipeline.Create(44100, 2, missing, threeNames)).ParamName);
        Assert.Throws<FileNotFoundException>(() => RealtimeAiPipeline.Create(44100, 2, missing, Names));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(88200)]
    [InlineData(96000)]
    [InlineData(176400)]
    [InlineData(192000)]
    public void AI_가_받는_모든_레이트에서_한_창이_모이면_추론한다(int sampleRate)
    {
        int frames = PipelineTestAudio.WindowFrames(sampleRate);
        float[] stereo = PipelineTestAudio.Sine(frames, channels: 2);
        var model = new FakeYamnetModel();
        var clock = new ManualClock();
        using var pipeline = new RealtimeAiPipeline(sampleRate, 2, model, Names, clock: clock.Now, runScheduler: false);
        pipeline.Start();

        PipelineTestAudio.IngestInChunks(pipeline, stereo[..^2], chunk: 4096);
        Assert.Equal(AiTickOutcome.NotEnoughAudio, pipeline.RunTickOnce(0));
        pipeline.Ingest(stereo.AsSpan(stereo.Length - 2));
        Assert.Equal(AiTickOutcome.Completed, pipeline.RunTickOnce(0));

        Assert.Equal(1, model.InferCalls);
        Assert.NotNull(pipeline.LastClassification());
        Assert.Equal(sampleRate, pipeline.SampleRate);
        Assert.Equal(2, pipeline.ChannelCount);
        // 16kHz 로 바꾼 창 끝부분에 소리가 들어 있다(사인의 크기 0.4 안팎).
        float peak = 0f;
        foreach (float value in pipeline.Mono16kForTest[^1000..]) peak = Math.Max(peak, Math.Abs(value));
        Assert.InRange(peak, 0.3f, 0.5f);
    }

    private static RealtimeAiPipeline NewPipeline(
        int sampleRate,
        int channels,
        ManualClock clock,
        Func<IReadOnlyDictionary<string, string>>? overrides = null) =>
        new(sampleRate, channels, YamnetInference.Create(AiTestData.ModelDirectory), Names, overrides, clock: clock.Now, runScheduler: false);

    /// <summary>Python 기준 구현이 만든 실제 알람 소리(44.1kHz 스테레오, 한 창).</summary>
    private static float[] Alarm() => AiTestData.LoadFloats("e2e_alarm_stereo44100.bin", Window44 * 2);

    private static float[] Load(string fileName, int count) => AiTestData.LoadFloats(fileName, count);

    private static YamnetCoarseClassifier.Result Vote(RealtimeAiPipeline pipeline, IReadOnlyDictionary<string, string> overrides) =>
        new YamnetCoarseClassifier(Names).Classify(pipeline.ProbabilitiesForTest.ToArray(), YamnetMappingPolicy.From(overrides, Names));

    private static double MaxAbs(ReadOnlySpan<float> actual, ReadOnlySpan<float> expected)
    {
        Assert.Equal(expected.Length, actual.Length);
        double max = 0;
        for (int i = 0; i < actual.Length; i++) max = Math.Max(max, Math.Abs((double)actual[i] - expected[i]));
        return max;
    }
}
