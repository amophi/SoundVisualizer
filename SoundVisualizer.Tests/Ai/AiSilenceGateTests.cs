// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>Android 의 AiSilenceGateTest.kt 를 옮겼다.</summary>
public class AiSilenceGateTests
{
    /// <summary>Android 의 추론 간격(RealtimeAiPipeline.AI_PREDICT_INTERVAL_MS = 250ms)보다 100ms 느린 틱.</summary>
    private const long SlowTickMs = 250 + 100;

    [Fact]
    public void 긴_무음과_기준값_크기의_잡음은_게이트를_열지_않는다()
    {
        var gate = new AiSilenceGate();
        float[] quiet = { 0.01f, -0.01f, 0.005f, -0.005f };

        gate.OnInterleavedPcm(quiet, nowMs: 0);

        Assert.False(gate.IsOpen(0));
        Assert.False(gate.IsOpen(10_000));
    }

    [Fact]
    public void 느린_추론에서도_완전히_조용한_추론이_두_번_끝날_때까지_열려_있다()
    {
        var gate = new AiSilenceGate();
        gate.OnInterleavedPcm(new[] { 0.0101f, 0f }, nowMs: 100);

        Assert.True(gate.IsOpen(100));
        long firstSilentSnapshot = 100 + AiSilenceGate.YamnetWindowMs;
        Assert.True(gate.IsOpen(firstSilentSnapshot + 10_000));

        gate.OnInferenceCompleted(firstSilentSnapshot);
        Assert.True(gate.IsOpen(firstSilentSnapshot + SlowTickMs));

        gate.OnInferenceCompleted(firstSilentSnapshot + SlowTickMs);
        Assert.False(gate.IsOpen(firstSilentSnapshot + SlowTickMs));
    }

    [Fact]
    public void 소리가_창을_빠져나가기_전에_끝난_추론은_게이트를_닫지_않는다()
    {
        var gate = new AiSilenceGate();
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: 0);

        for (int i = 0; i < AiSilenceGate.RequiredSilentInferenceCompletions; i++)
        {
            gate.OnInferenceCompleted(AiSilenceGate.YamnetWindowMs - 1);
        }

        Assert.True(gate.IsOpen(AiSilenceGate.YamnetWindowMs + 10_000));
    }

    [Fact]
    public void 위상이_반대인_짧은_소리도_원래_채널로_게이트를_연다()
    {
        var gate = new AiSilenceGate();
        // 섞으면 0 이지만, 어느 한 채널의 최고값으로 추론을 다시 연다.
        gate.OnInterleavedPcm(new[] { 0.02f, -0.02f }, nowMs: 200);

        Assert.True(gate.IsOpen(200));
    }

    [Fact]
    public void 닫힌_게이트는_새_소리가_오면_바로_다시_열린다()
    {
        var gate = new AiSilenceGate();

        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: 0);
        for (int index = 0; index < AiSilenceGate.RequiredSilentInferenceCompletions; index++)
        {
            gate.OnInferenceCompleted(AiSilenceGate.YamnetWindowMs + index * SlowTickMs);
        }
        Assert.False(gate.IsOpen(AiSilenceGate.YamnetWindowMs + SlowTickMs));

        long newActiveMs = AiSilenceGate.YamnetWindowMs + SlowTickMs + 1;
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: newActiveMs);

        Assert.True(gate.IsOpen(newActiveMs));
        Assert.True(gate.IsOpen(newActiveMs + AiSilenceGate.YamnetWindowMs + 10_000));

        for (int index = 0; index < AiSilenceGate.RequiredSilentInferenceCompletions; index++)
        {
            gate.OnInferenceCompleted(newActiveMs + AiSilenceGate.YamnetWindowMs + index * SlowTickMs);
        }
        Assert.False(gate.IsOpen(newActiveMs + AiSilenceGate.YamnetWindowMs + SlowTickMs));
    }

    [Fact]
    public void 추론이_계속_실패하면_결국_닫힌다()
    {
        var gate = new AiSilenceGate();
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: 0);
        long firstFailedSnapshot = AiSilenceGate.YamnetWindowMs;

        gate.OnInferenceFailed(firstFailedSnapshot);

        Assert.True(gate.IsOpen(firstFailedSnapshot + AiSilenceGate.FailureFallbackMs - 1));
        gate.OnInferenceFailed(firstFailedSnapshot + AiSilenceGate.FailureFallbackMs - 1);
        Assert.False(gate.IsOpen(firstFailedSnapshot + AiSilenceGate.FailureFallbackMs));

        long newActiveMs = firstFailedSnapshot + AiSilenceGate.FailureFallbackMs + 1;
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: newActiveMs);
        Assert.True(gate.IsOpen(newActiveMs + AiSilenceGate.YamnetWindowMs + 10_000));
    }

    [Fact]
    public void 성공한_추론은_실패_대비를_지우되_정리를_줄이지_않는다()
    {
        var gate = new AiSilenceGate();
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: 0);
        long firstSilentSnapshot = AiSilenceGate.YamnetWindowMs;

        gate.OnInferenceFailed(firstSilentSnapshot);
        gate.OnInferenceCompleted(firstSilentSnapshot + SlowTickMs);

        Assert.True(gate.IsOpen(firstSilentSnapshot + AiSilenceGate.FailureFallbackMs + 10_000));
        long secondSuccessfulSnapshot = firstSilentSnapshot + AiSilenceGate.FailureFallbackMs + 10_001;
        gate.OnInferenceCompleted(secondSuccessfulSnapshot);
        Assert.False(gate.IsOpen(secondSuccessfulSnapshot));
    }

    [Fact]
    public void Reset_은_소리_완료_실패_상태를_모두_지운다()
    {
        var gate = new AiSilenceGate();
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: 0);
        gate.OnInferenceFailed(AiSilenceGate.YamnetWindowMs);

        gate.Reset();

        Assert.False(gate.IsOpen(AiSilenceGate.YamnetWindowMs + 10_000));
        long newActiveMs = AiSilenceGate.YamnetWindowMs + 10_001;
        gate.OnInterleavedPcm(new[] { 0.02f, 0f }, nowMs: newActiveMs);
        Assert.True(gate.IsOpen(newActiveMs + AiSilenceGate.YamnetWindowMs + 10_000));
    }

    [Fact]
    public void 정리_기준은_YAMNet_창과_종류_히스테리시스_상수를_따른다()
    {
        // 15,600샘플 ÷ 16kHz = 975ms. Android 는 AudioPreprocessor 의 상수로 같은 값을 만든다.
        Assert.Equal(15600, CaptureAudioMath.RequiredMono16kSamples);
        Assert.Equal(16000, CaptureAudioMath.TargetSampleRate);
        Assert.Equal(975L, AiSilenceGate.YamnetWindowMs);
        // Android 의 AiPostProcessor.COARSE_HYSTERESIS_THRESHOLD. 후처리를 옮기면 그 상수와 직접 비교한다.
        Assert.Equal(2, AiSilenceGate.RequiredSilentInferenceCompletions);
    }
}
