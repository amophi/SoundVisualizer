// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>테스트가 정하는 단조 밀리초 시계. 여러 스레드에서 읽고 움직여도 된다.</summary>
internal sealed class ManualClock
{
    private long _nowMs;

    public ManualClock(long startMs = 0)
    {
        _nowMs = startMs;
    }

    public long Now() => Interlocked.Read(ref _nowMs);

    public void Set(long nowMs) => Interlocked.Exchange(ref _nowMs, nowMs);

    public long Advance(long ms) => Interlocked.Add(ref _nowMs, ms);
}

/// <summary>
/// 모델 자리에 끼우는 가짜. 정한 클래스에 확률을 몰아 주고, 추론마다 <see cref="OnInfer"/> 를 불러 테스트가 시계를 움직이거나
/// 추론을 붙잡아 두거나 실패시킨다. 닫힌 뒤에 불렸는지, 추론 중에 닫혔는지를 기록해 세션 해제 경합을 잡는다.
/// </summary>
internal sealed class FakeYamnetModel : IYamnetModel
{
    private int _inferCalls;
    private int _disposeCalls;
    private int _inferring;
    private int _disposed;
    private int _usedAfterDispose;
    private int _disposedDuringInference;

    /// <summary>확률을 몰아 줄 클래스 번호. 0 은 "Speech" 다.</summary>
    public int TopClass { get; set; }

    /// <summary>추론마다 몇 번째 호출인지(1부터)와 함께 부른다. 예외를 던지면 그 추론이 실패한다.</summary>
    public Action<int>? OnInfer { get; set; }

    public int InferCalls => Volatile.Read(ref _inferCalls);

    public int DisposeCalls => Volatile.Read(ref _disposeCalls);

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public bool UsedAfterDispose => Volatile.Read(ref _usedAfterDispose) != 0;

    public bool DisposedDuringInference => Volatile.Read(ref _disposedDuringInference) != 0;

    public void Infer(ReadOnlySpan<float> logMel, Span<float> logits, Span<float> probabilities)
    {
        // 추론 중 표시를 먼저 세우고 닫힘을 본다. Dispose 는 반대 순서라 둘 중 하나는 반드시 상대를 본다.
        Interlocked.Increment(ref _inferring);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                Interlocked.Exchange(ref _usedAfterDispose, 1);
                throw new ObjectDisposedException(nameof(FakeYamnetModel));
            }
            int call = Interlocked.Increment(ref _inferCalls);
            OnInfer?.Invoke(call);
            logits.Fill(-10f);
            logits[TopClass] = 10f;
            YamnetInference.Softmax(logits, probabilities);
        }
        finally
        {
            Interlocked.Decrement(ref _inferring);
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
        if (Volatile.Read(ref _inferring) != 0) Interlocked.Exchange(ref _disposedDuringInference, 1);
        Interlocked.Increment(ref _disposeCalls);
    }
}

/// <summary>파이프라인 테스트에 넣는 소리.</summary>
internal static class PipelineTestAudio
{
    /// <summary>모바일 계측 테스트의 사인(sin(i × 0.031) × 0.4, 16kHz 에서 약 79Hz)을 모든 채널에 같게 넣은 인터리브 PCM.</summary>
    public static float[] Sine(int frames, int channels = 1)
    {
        var pcm = new float[frames * channels];
        for (int frame = 0; frame < frames; frame++)
        {
            float value = (float)Math.Sin(frame * .031) * .4f;
            for (int channel = 0; channel < channels; channel++) pcm[frame * channels + channel] = value;
        }
        return pcm;
    }

    /// <summary>-1~1 균등 잡음. 모바일 닫기 경합 테스트의 CHUNK 와 같은 쓰임이다.</summary>
    public static float[] Noise(int seed, int count)
    {
        var random = new Random(seed);
        var pcm = new float[count];
        for (int i = 0; i < pcm.Length; i++) pcm[i] = random.NextSingle() * 2f - 1f;
        return pcm;
    }

    /// <summary>캡처 레이트에서 YAMNet 한 창을 채우는 프레임 수.</summary>
    public static int WindowFrames(int sampleRate) => CaptureAudioMath.CaptureSamplesForOneYamnetWindow(sampleRate);

    /// <summary>캡처처럼 float 1,024개씩 나눠 넣는다.</summary>
    public static void IngestInChunks(RealtimeAiPipeline pipeline, float[] interleaved, int chunk = 1024)
    {
        for (int offset = 0; offset < interleaved.Length; offset += chunk)
        {
            pipeline.Ingest(interleaved.AsSpan(offset, Math.Min(chunk, interleaved.Length - offset)));
        }
    }
}
