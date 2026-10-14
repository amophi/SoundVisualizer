// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SoundVisualizer.Audio;
using SoundVisualizer.Diagnostics;

namespace SoundVisualizer
{
    // 개발자 모드 HUD(안드로이드 AiDebugOverlay): 오버레이 왼쪽 위에 AI 가 말한 것을 그대로 띄우고, 켜 두었으면 결과를 CSV 로 남긴다.
    // 렌더 루프와 따로 100ms 타이머로 돈다. 그래서 보이는 것이 없어 오버레이가 쉬는 동안에도 HUD 는 멈추지 않는다(나이가 자라는 것이
    // "지금 추론이 돌지 않는다" 는 신호다). 언제 읽고 언제 기록할지는 Core 의 AiDebugHud 가 정하고, 여기는 화면과 타이머만 맡는다.
    public partial class MainWindow
    {
        // 지금 소리 크기. 캡처 스레드가 버퍼마다 넣고(HandleAudioDataAsync) HUD 가 읽는다.
        private readonly CaptureLevel _level = new();
        private readonly AiDebugHud _debugHud = new();
        private readonly DispatcherTimer _debugHudTimer = new() { Interval = TimeSpan.FromMilliseconds(AiDebugHud.PollIntervalMs) };
        // 지금 HUD 에 넣어 둔 글자. 같으면 다시 넣지 않는다.
        private AiDebugLines? _debugHudLines;

        /// <summary>HUD 타이머를 잇고 지금 설정으로 한 번 맞춘다. 생성자에서 부른다.</summary>
        private void InitDebugHud()
        {
            _debugHudTimer.Tick += (s, e) => PollDebugHud();
            Closed += (s, e) =>
            {
                _debugHudTimer.Stop();
                // 기록 파일을 닫는다. 남은 줄을 쓰기를 1초까지 기다린다. 런처를 닫을 때도 이 창을 먼저 닫으므로, 앱이 끝나며 받아 둔 줄을 잃지 않는다.
                _debugHud.Dispose();
            };
            SyncDebugHud();
        }

        /// <summary>
        /// 개발자 모드·결과 기록 설정을 HUD 에 알린다. 프레임마다(쉬는 동안에는 확인할 때마다) 부르므로 런처·편집 패널·초기화 어디서 바꿔도
        /// 곧바로 따른다. 기록은 AiDebugHud 가 켜고 끄고, 여기서는 HUD 가 보이게 되거나 숨게 될 때만 판과 타이머를 다룬다.
        /// </summary>
        private void SyncDebugHud()
        {
            if (!_debugHud.Update(AppSettings.IsAdminMode, AppSettings.DeveloperRecord)) return;
            if (_debugHud.IsPolling)
            {
                PollDebugHud(); // 첫 타이머를 기다리지 않고 지금 값으로 채워 빈 판이 잠깐 보이지 않게 한다
                DebugHudBorder.Visibility = Visibility.Visible;
                _debugHudTimer.Start();
            }
            else
            {
                _debugHudTimer.Stop();
                DebugHudBorder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// HUD 를 한 번 읽는다(100ms 마다). 결과·소리 크기·표시 여부·AI 상태를 이 순간 한 번씩만 읽어 화면과 기록이 같은 값을 쓴다.
        /// </summary>
        private void PollDebugHud()
        {
            if (_isClosed) return;
            var result = Volatile.Read(ref _aiPipeline)?.LastClassification();
            // 결과의 시각(TimestampMs)과 같은 벽시계다. 소리 크기는 캡처 스레드와 같은 단조 시계로 잰다.
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float level = _level.Read(Environment.TickCount64);
            // 그 종류 표시를 꺼 두면 오버레이가 아무것도 그리지 않는다. 함께 보이지 않으면 설정 상태를 "AI 가 못 잡았다" 로 오독한다.
            bool shown = result != null && IsTypeVisible(result.Coarse);
            var lines = _debugHud.Poll(result, nowMs, level, shown, _aiAvailable);

            if (!lines.Equals(_debugHudLines))
            {
                _debugHudLines = lines;
                ShowDebugHudLines(lines);
            }

            // 점은 그 종류에 사용자가 고른 실제 색이다. 색은 글자와 따로 바뀔 수 있어(편집 패널에서 고르는 중) 읽을 때마다 맞춘다.
            // 붓은 색이 바뀔 때만 새로 만들므로 참조로 비교한다.
            Brush? dot = lines.ColorType is { } type ? GetBrushForType(type) : null;
            if (!ReferenceEquals(DebugHudDot.Fill, dot)) DebugHudDot.Fill = dot;
        }

        /// <summary>
        /// HUD 의 줄을 넣는다. 첫 줄은 종류·점·이름·확신도이고, 그 아래로 임계값과 나이, 위험 단서, 소리 크기·표시 여부·소요시간, top-5 다.
        /// 글자는 AiDebugText 가 만든 그대로다(번역하지 않는 영어, 숫자는 늘 같은 꼴).
        /// </summary>
        private void ShowDebugHudLines(AiDebugLines lines)
        {
            DebugHudCoarse.Text = lines.Coarse;
            DebugHudDot.Visibility = lines.ColorType is null ? Visibility.Collapsed : Visibility.Visible;
            DebugHudLabel.Text = lines.Label;
            DebugHudConfidence.Text = lines.Confidence;
            DebugHudDetail.Text = lines.Detail;
            DebugHudVerdict.Text = lines.Verdict;
            DebugHudTiming.Text = lines.Timing;
            DebugHudTop5.Text = string.Join('\n', lines.Top5);
            DebugHudTop5.Visibility = lines.Top5.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
