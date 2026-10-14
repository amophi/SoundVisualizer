// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Windows.Threading;
using SoundVisualizer.Status;

namespace SoundVisualizer
{
    // 오버레이가 런처에 알리는 상태(홈 안내·트레이). 렌더 루프와 섞이지 않게 이 파일에 따로 둔다.
    // 런처는 창을 만든 뒤 StartFailure 를 보고, 띄운 뒤에는 AiAvailabilityChanged 를 받고, 닫히면 ExitReason 을 읽는다.
    public partial class MainWindow
    {
        /// <summary>
        /// 생성자에서 캡처를 시작하지 못했으면 그 이유(출력 장치를 열지 못함 등). 그때 오버레이는 아무것도 그리지 못하므로
        /// 런처는 창을 띄우지 않고 닫은 뒤 홈에 이유를 남긴다.
        /// </summary>
        public StopReason? StartFailure { get; private set; }

        /// <summary>창이 닫힌 이유. 캡처가 되살아나지 않아 스스로 닫을 때만 UserRequested 가 아니다. 런처가 Closed 에서 읽는다.</summary>
        public StopReason ExitReason { get; private set; } = StopReason.UserRequested;

        /// <summary>
        /// AI(소리 종류 구분)를 쓸 수 있는지가 정해졌다. 캡처 형식에 맞는 파이프라인을 만들면 true, 만들지 못하면 false 다
        /// (지원하지 않는 샘플레이트, 모델 파일 이상). 장치가 바뀌어 다시 만들 때마다 다시 온다. UI 스레드에서 부른다.
        /// </summary>
        public event EventHandler<bool>? AiAvailabilityChanged;

        /// <summary>
        /// 마지막으로 <see cref="AiAvailabilityChanged"/> 로 알린 값. 개발자 모드 HUD 가 "AI OFF" 와 "WAIT" 를 가르는 데 쓴다. 런처와 같은 알림을
        /// 보므로 홈 안내와 HUD 가 어긋나지 않는다. 만드는 중에는 쓸 수 있다고 본다(런처의 RunStatus 와 같다). UI 스레드에서만 쓴다.
        /// </summary>
        private bool _aiAvailable = true;

        private DispatcherTimer? _captureCheckTimer;

        /// <summary>캡처 엔진의 오류를 받기 시작한다. 캡처를 시작하기 전에 부른다.</summary>
        private void WatchCaptureErrors()
        {
            _captureEngine!.OnCaptureError += OnCaptureError;
            Closed += (s, e) => _captureCheckTimer?.Stop();
        }

        /// <summary>캡처를 시작한 뒤 부른다. 장치를 열지 못했거나 녹음을 시작하지 못했으면 <see cref="StartFailure"/> 에 이유를 남긴다.</summary>
        private void CheckCaptureStarted() =>
            StartFailure = CaptureRecovery.Check(_captureEngine!.IsCapturing, _captureEngine.CaptureFormat != null);

        /// <summary>
        /// 캡처 엔진이 오류를 알렸다(장치를 열지 못함, 녹음이 오류로 멈춤). 캡처 스레드나 장치 알림 스레드에서 불린다.
        /// 출력 장치를 바꾸는 중이면 엔진이 곧 새 장치를 다시 연다. 그래서 바로 닫지 않고 <see cref="CaptureRecovery.GracePeriod"/> 만큼
        /// 기다려, 그때도 받지 못하면 창을 닫고 이유를 남긴다. 오류가 이어지면 마지막 오류부터 다시 센다.
        /// </summary>
        private void OnCaptureError(object? sender, string message)
        {
            AppLog.Warn("capture", $"캡처 엔진 오류: {message}");
            Dispatcher.InvokeAsync(() =>
            {
                // 생성자 안에서 시작하지 못한 경우는 런처가 StartFailure 를 보고 처리한다.
                if (_isClosed || StartFailure != null) return;
                if (_captureCheckTimer == null)
                {
                    _captureCheckTimer = new DispatcherTimer { Interval = CaptureRecovery.GracePeriod };
                    _captureCheckTimer.Tick += CaptureCheckTimer_Tick;
                }
                _captureCheckTimer.Stop();
                _captureCheckTimer.Start();
            });
        }

        private void CaptureCheckTimer_Tick(object? sender, EventArgs e)
        {
            _captureCheckTimer?.Stop();
            if (_isClosed || _captureEngine == null) return;

            var reason = CaptureRecovery.Check(_captureEngine.IsCapturing, _captureEngine.CaptureFormat != null);
            if (reason == null)
            {
                AppLog.Info("capture", "캡처가 다시 돌아 오버레이를 그대로 둡니다.");
                return;
            }

            // 캡처 없이 떠 있으면 아무것도 그리지 못하는데, 사용자는 그것을 조용한 장면과 구분할 수 없다. 닫고 런처가 알리게 한다.
            AppLog.Warn("capture", $"캡처가 {CaptureRecovery.GracePeriod.TotalSeconds:0}초 안에 되살아나지 않아 오버레이를 닫습니다({reason}).");
            ExitReason = reason.Value;
            Close();
        }

        /// <summary>AI 파이프라인을 만든 결과를 UI 스레드에서 알린다. 만드는 백그라운드 스레드에서 부른다.</summary>
        private void NotifyAiAvailability(bool available)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_isClosed) return;
                _aiAvailable = available;
                AiAvailabilityChanged?.Invoke(this, available);
            });
        }
    }
}
