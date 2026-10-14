// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Rendering
{
    /// <summary>
    /// 프레임마다 엔진(<see cref="VisualizerEngine"/>)에 넘기는 바깥 값(Android VisualizerInputs 가 읽어 오던 것).
    /// 설정과 AI 결과는 부르는 쪽이 프레임마다 한 번만 읽어 이것으로 넘긴다. 그래야 한 프레임의 판단이 모두 같은 결과로 된다.
    /// </summary>
    /// <param name="Settings">지금 표현 모드의 설정.</param>
    /// <param name="VisualMode">표현 모드(<see cref="VisualizerEngine.ModeWave"/> 등).</param>
    /// <param name="SoundMode">사운드 모드(0: 2.0, 1: 5.1, 2: 7.1).</param>
    /// <param name="Label">AI 가 확정한 소리 종류. 결과가 없으면 환경음이다.</param>
    /// <param name="Shown">그 종류를 화면에 보이게 해 두었는지.</param>
    public readonly record struct OverlayInput(VisualModeSettings Settings, int VisualMode, int SoundMode, SoundType Label, bool Shown);
}
