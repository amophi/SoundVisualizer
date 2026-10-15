// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>
/// 쉬는 오버레이를 깨우는 소리 신호(Android SoundWakeSignalTest). 오버레이는 확인 직전에 <see cref="SoundWakeSignal.Arm"/> 하고,
/// 캡처 스레드는 버퍼마다 <see cref="SoundWakeSignal.OnBuffer"/> 를 부른다. true 를 받은 쪽이 오버레이의 확인을 앞당긴다.
/// </summary>
public class SoundWakeSignalTests
{
    private readonly SoundWakeSignal _signal = new(threshold: 0.01f);

    /// <summary>캡처 스레드처럼 다른 스레드에서 버퍼 하나를 넘긴다.</summary>
    private bool BufferFromCaptureThread(float level)
    {
        bool woke = false;
        var thread = new Thread(() => woke = _signal.OnBuffer(level));
        thread.Start();
        thread.Join();
        return woke;
    }

    [Fact]
    public void 조용한_버퍼로는_깨지_않는다()
    {
        _signal.Arm();
        Assert.False(BufferFromCaptureThread(0.005f));
        Assert.False(BufferFromCaptureThread(0.01f)); // 문턱과 같으면 조용한 것이다
        Assert.True(_signal.IsArmed);
    }

    [Fact]
    public void 큰_소리_버퍼가_오면_한_번만_깨운다()
    {
        _signal.Arm();
        Assert.True(BufferFromCaptureThread(0.5f));
        // 이미 알렸다. 오버레이가 다시 무장할 때까지 버퍼마다 깨우지 않는다.
        Assert.False(BufferFromCaptureThread(0.5f));
        Assert.False(_signal.IsArmed);
    }

    [Fact]
    public void 무장하기_전에_온_소리로는_깨지_않는다()
    {
        // 큰 소리 뒤 캡처가 멈춰 마지막 소리 크기가 큰 채로 남았다. 새 버퍼가 없으니 깨우면 안 된다.
        Assert.False(BufferFromCaptureThread(0.5f));
        _signal.Arm();
        Assert.True(_signal.IsArmed);
    }

    [Fact]
    public void 다시_무장하면_다음_큰_소리로_또_깨운다()
    {
        _signal.Arm();
        Assert.True(BufferFromCaptureThread(0.5f));
        _signal.Arm();
        Assert.True(BufferFromCaptureThread(0.5f));
    }

    [Fact]
    public void 무장을_풀면_큰_소리로도_깨우지_않는다()
    {
        // 표시를 꺼 둔 소리가 나는 동안에는 33ms 확인만 한다.
        _signal.Arm();
        _signal.Disarm();
        Assert.False(BufferFromCaptureThread(0.9f));
    }

    [Fact]
    public void 여러_스레드가_동시에_큰_소리를_넘겨도_한_번만_깨운다()
    {
        for (int round = 0; round < 50; round++)
        {
            _signal.Arm();
            int woke = 0;
            using var start = new ManualResetEventSlim();
            var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
            {
                start.Wait();
                if (_signal.OnBuffer(0.5f)) Interlocked.Increment(ref woke);
            })).ToList();
            threads.ForEach(t => t.Start());
            start.Set();
            threads.ForEach(t => t.Join());
            Assert.Equal(1, woke);
        }
    }
}
