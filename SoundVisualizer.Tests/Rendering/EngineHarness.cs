// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tests.Rendering;

/// <summary>
/// 엔진에 넣을 바깥 값을 들고 프레임을 흘린다(Android VisualizerEngineTest 의 FakeInputs 와 advance).
/// 채널 값은 캡처 순서 FL, FR, FC, BL, BR, SL, SR, LFE 다.
/// </summary>
internal sealed class EngineHarness
{
    /// <summary>60fps 한 프레임. 엔진의 시간 정규화 기준값과 같다.</summary>
    public const double Frame60 = FrameStep.ReferenceFrameMs;

    public const double Width = 1920;
    public const double Height = 1080;

    /// <summary>크기 100% 의 깊이: 짧은 변의 절반 - 10.</summary>
    public const double MaxBase = 530;

    public readonly VisualizerEngine Engine = new();
    public VisualModeSettings Settings = new();
    public int VisualMode = VisualizerEngine.ModeWave;
    public int SoundMode = 2;

    /// <summary>AI 가 확정한 종류와, 그 종류를 보이게 해 두었는지.</summary>
    public SoundType Label = SoundType.Ambient;
    public bool Shown = true;

    public OverlayInput Input => new(Settings, VisualMode, SoundMode, Label, Shown);

    /// <summary>다음 프레임에 들어올 채널별 피크.</summary>
    public readonly float[] Peaks = new float[VisualizerEngine.Channels];

    /// <summary>false 면 지난 프레임 뒤로 버퍼가 하나도 오지 않은 것으로 넘긴다(엔진이 이전 값을 줄인다).</summary>
    public bool Buffers = true;

    /// <summary>오버레이 편집 중처럼, 보이는 것이 없어도 쉬지 않게 한다.</summary>
    public bool KeepAwake;

    /// <summary>쉬는 동안의 확인 간격. 33ms(60fps 두 프레임)다.</summary>
    public const double PollMs = 2 * Frame60;

    /// <summary>마지막으로 흘린 프레임의 시각.</summary>
    public double Now { get; private set; }

    public EngineHarness(double width = Width, double height = Height)
    {
        Engine.SetSurfaceSize(width, height);
    }

    /// <summary>모든 채널을 같은 값으로.</summary>
    public void All(float value) => Array.Fill(Peaks, value);

    public void Silence() => Array.Clear(Peaks);

    /// <summary>앞 좌우(FL, FR)만 울리는 소리. Android 테스트의 좌우 피크에 해당한다.</summary>
    public void Stereo(float left, float right)
    {
        Silence();
        Peaks[0] = left;
        Peaks[1] = right;
    }

    /// <summary>가장 깊은 자리의 깊이.</summary>
    public double DeepestDepth => Engine.Depths.ToArray().Max();

    /// <summary>
    /// 위협음을 표시 중인 엔진이 같은 소리를 제때 받았을 때 평소 경로가 도달하는 가장 큰 전체 크기.
    /// <paramref name="loudFrames"/> 프레임 동안 <paramref name="loud"/>, 이어서 <paramref name="tailFrames"/> 프레임 동안 <paramref name="tail"/> 을
    /// 앞 좌우에 같게 넣는다.
    /// </summary>
    public static double OnTimePeakSmoothTotal(float loud, int loudFrames, float tail, int tailFrames)
    {
        var run = new EngineHarness { Label = SoundType.Danger, Shown = true };
        double peak = 0;
        for (int n = 0; n < loudFrames + tailFrames; n++)
        {
            float v = n < loudFrames ? loud : tail;
            run.Stereo(v, v);
            run.Tick();
            peak = Math.Max(peak, run.Engine.SmoothTotal);
        }
        return peak;
    }

    /// <summary><paramref name="stepMs"/> 뒤의 프레임 하나.</summary>
    public void Tick(double stepMs = Frame60)
    {
        // 오버레이는 쉬는 동안 프레임을 흘리지 않고 PollWake 로 확인한다. 테스트도 그 약속을 지킨다.
        Assert.False(Engine.IsIdle, "쉬는 엔진에 프레임을 흘렸다");
        Now += stepMs;
        Engine.Tick(Now, Buffers ? Peaks : ReadOnlySpan<float>.Empty, Input, KeepAwake);
    }

    /// <summary>쉬는 동안의 확인 한 번. 깨어났으면 true.</summary>
    public bool Poll(double stepMs = PollMs)
    {
        Now += stepMs;
        return Engine.PollWake(Now, Buffers ? Peaks : ReadOnlySpan<float>.Empty, Input);
    }

    /// <summary>확인을 <paramref name="times"/> 번.</summary>
    public void Polls(int times, double stepMs = PollMs)
    {
        for (int i = 0; i < times; i++) Poll(stepMs);
    }

    /// <summary>
    /// 쉴 때까지 프레임을 흘리고 흘린 프레임 수를 돌려준다. 오버레이처럼 쉬기 시작하면 프레임을 멈춘다.
    /// </summary>
    public int TickUntilIdle(double stepMs = Frame60, int maxFrames = 2000)
    {
        int frames = 0;
        while (!Engine.IsIdle && frames < maxFrames)
        {
            Tick(stepMs);
            frames++;
        }
        Assert.True(Engine.IsIdle, $"쉬기로 내려가지 않았다 (frames={frames})");
        return frames;
    }

    /// <summary>분류기가 위협음으로 판정했고, 위협음은 표시가 켜져 있다.</summary>
    public void BecomeDanger()
    {
        Label = SoundType.Danger;
        Shown = true;
    }

    public void Advance(int frames, double stepMs = Frame60)
    {
        for (int i = 0; i < frames; i++) Tick(stepMs);
    }
}
