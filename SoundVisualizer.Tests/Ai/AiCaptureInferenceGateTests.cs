// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// Android 의 AiCaptureInferenceGateTest.kt 를 옮겼다. 뒤의 두 테스트는 Android 에서 후처리(AiPostProcessor)와 함께 도는데,
/// 후처리는 이 묶음 밖이라 게이트 쪽 확인만 옮겼다. 후처리를 옮기면 같은 흐름에 라벨 확인을 붙인다.
/// </summary>
public class AiCaptureInferenceGateTests
{
    /// <summary>Android 의 추론 간격(RealtimeAiPipeline.AI_PREDICT_INTERVAL_MS = 250ms)보다 100ms 느린 틱.</summary>
    private const long SlowTickMs = 250 + 100;

    [Fact]
    public void 소리가_든_창은_게이트가_열린_것을_보기_전에_링에_들어가_있다()
    {
        var buffer = new AiAudioBuffer(captureSampleRate: 16000, channels: 2);
        var ingress = new AiCaptureInferenceGate(buffer);
        var activeWindow = new float[CaptureAudioMath.RequiredMono16kSamples * 2];
        for (int index = 0; index < activeWindow.Length; index++)
        {
            activeWindow[index] = index % 2 == 0 ? 0.02f : 0f;
        }

        ingress.IngestInterleaved(activeWindow, nowMs: 0);

        Assert.True(buffer.HasEnoughForYamnetWindow());
        Assert.True(ingress.IsInferenceOpen(0));
    }

    [Fact]
    public void 소리_뒤의_무음도_링에_쌓이고_정리_뒤_새_소리에_다시_열린다()
    {
        var buffer = new AiAudioBuffer(captureSampleRate: 16000, channels: 2);
        var ingress = new AiCaptureInferenceGate(buffer);
        float[] active = { 0.02f, 0f };
        var silence = new float[CaptureAudioMath.RequiredMono16kSamples * 2];

        ingress.IngestInterleaved(active, nowMs: 0);
        ingress.IngestInterleaved(silence, nowMs: 1);

        Assert.True(buffer.HasEnoughForYamnetWindow());
        long firstSilentSnapshot = AiSilenceGate.YamnetWindowMs;
        ingress.OnInferenceCompleted(firstSilentSnapshot);
        Assert.True(ingress.IsInferenceOpen(firstSilentSnapshot + SlowTickMs));
        ingress.OnInferenceCompleted(firstSilentSnapshot + SlowTickMs);
        Assert.False(ingress.IsInferenceOpen(firstSilentSnapshot + SlowTickMs));
        int samplesAfterLongSilence = buffer.AvailableSamples;

        long newActiveMs = firstSilentSnapshot + SlowTickMs + 1;
        ingress.IngestInterleaved(active, nowMs: newActiveMs);

        Assert.True(ingress.IsInferenceOpen(newActiveMs));
        Assert.Equal(samplesAfterLongSilence + 1, buffer.AvailableSamples);
    }

    [Fact]
    public void 추론이_계속_실패하면_대비_시간이_지나_닫힌다()
    {
        var buffer = new AiAudioBuffer(captureSampleRate: 16000, channels: 2);
        var ingress = new AiCaptureInferenceGate(buffer);
        float[] active = { 0.02f, 0f };
        ingress.IngestInterleaved(active, nowMs: 0);
        long firstFailedSnapshot = AiSilenceGate.YamnetWindowMs;

        ingress.OnInferenceFailed(firstFailedSnapshot);

        Assert.True(ingress.IsInferenceOpen(firstFailedSnapshot + AiSilenceGate.FailureFallbackMs - 1));
        Assert.False(ingress.IsInferenceOpen(firstFailedSnapshot + AiSilenceGate.FailureFallbackMs));
    }

    [Fact]
    public void 조용한_추론이_두_번_끝나야_닫히고_새_소리에_다시_열린다()
    {
        // Android 의 completedSilentInferences_clearConfirmedSpeechBeforeGateCloses 의 게이트 쪽.
        // Android 는 같은 흐름에서 확정된 대화음이 두 번째 조용한 추론에서 환경음으로 정리된 뒤에야 닫히는지 본다.
        var buffer = new AiAudioBuffer(captureSampleRate: 16000, channels: 2);
        var ingress = new AiCaptureInferenceGate(buffer);
        float[] active = { 0.02f, 0f };

        ingress.IngestInterleaved(active, nowMs: 0);

        long firstSilentSnapshot = AiSilenceGate.YamnetWindowMs;
        ingress.OnInferenceCompleted(firstSilentSnapshot);
        Assert.True(ingress.IsInferenceOpen(firstSilentSnapshot + SlowTickMs));

        ingress.OnInferenceCompleted(firstSilentSnapshot + SlowTickMs);
        Assert.False(ingress.IsInferenceOpen(firstSilentSnapshot + SlowTickMs));

        long newActiveMs = firstSilentSnapshot + SlowTickMs + 1;
        ingress.IngestInterleaved(active, nowMs: newActiveMs);
        Assert.True(ingress.IsInferenceOpen(newActiveMs));
    }

    [Fact]
    public void 정리가_끝나_닫힌_게이트는_새_소리의_창을_따로_연다()
    {
        // Android 의 speechCandidatesSeparatedBySilentCleanup_doNotCombineAcrossClosedGate 의 게이트 쪽.
        // Android 는 닫히기 전과 뒤의 대화음 후보가 이어 세어지지 않는지(후보 연속 1회)도 본다.
        var buffer = new AiAudioBuffer(captureSampleRate: 16000, channels: 2);
        var ingress = new AiCaptureInferenceGate(buffer);
        float[] active = { 0.02f, 0f };

        ingress.IngestInterleaved(active, nowMs: 0);

        long firstSilentSnapshot = AiSilenceGate.YamnetWindowMs;
        for (int index = 0; index < AiSilenceGate.RequiredSilentInferenceCompletions; index++)
        {
            ingress.OnInferenceCompleted(firstSilentSnapshot + index * SlowTickMs);
        }
        Assert.False(ingress.IsInferenceOpen(firstSilentSnapshot + SlowTickMs));

        long newActiveMs = firstSilentSnapshot + SlowTickMs + 1;
        ingress.IngestInterleaved(active, nowMs: newActiveMs);

        Assert.True(ingress.IsInferenceOpen(newActiveMs));
        // 새 소리가 창을 빠져나간 뒤에도 조용한 추론 두 번을 처음부터 다시 센다.
        ingress.OnInferenceCompleted(newActiveMs + AiSilenceGate.YamnetWindowMs);
        Assert.True(ingress.IsInferenceOpen(newActiveMs + AiSilenceGate.YamnetWindowMs + SlowTickMs));
    }
}
