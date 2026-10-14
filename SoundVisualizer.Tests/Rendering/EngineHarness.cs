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

    /// <summary>마지막으로 흘린 프레임의 시각.</summary>
    public double Now { get; private set; }

    public EngineHarness(double width = Width, double height = Height)
    {
        Engine.SetSurfaceSize(width, height);
    }

    /// <summary>모든 채널을 같은 값으로.</summary>
    public void All(float value) => Array.Fill(Peaks, value);

    public void Silence() => Array.Clear(Peaks);

    /// <summary><paramref name="stepMs"/> 뒤의 프레임 하나.</summary>
    public void Tick(double stepMs = Frame60)
    {
        Now += stepMs;
        Engine.Tick(Now, Buffers ? Peaks : ReadOnlySpan<float>.Empty, Input);
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
