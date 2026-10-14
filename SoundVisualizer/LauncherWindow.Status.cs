// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using SoundVisualizer.Localization;
using SoundVisualizer.Status;

namespace SoundVisualizer
{
    // 실행 상태: 오버레이를 켜고 끄기, 홈의 상태 줄 아래 안내.
    // 무엇을 보일지는 Core 의 RunStatus 가 정한다. 오버레이(MainWindow.Status.cs)는 시작 실패·AI 상태·닫힌 이유를 알려 온다.
    public partial class LauncherWindow
    {
        private readonly RunStatus _runStatus = new();

        // 화면 읽어주기에 마지막으로 알린 안내. 언어만 바뀐 때는 다시 읽지 않는다.
        private HomeNotice _announcedNotice = HomeNotice.None;
        private StopReason? _announcedStop;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // AI 를 쓸 수 없으면 모든 소리가 환경음이 되므로, 환경음 표시를 켜고 끄면 홈 안내도 달라진다.
            // 설정 탭·초기화 버튼·오버레이 편집 패널 어디서 바꿔도 이 스위치에 반영된다.
            ChkShowAmbient.Checked += (s, args) => UpdateNotices();
            ChkShowAmbient.Unchecked += (s, args) => UpdateNotices();
        }

        /// <summary>오버레이를 띄운다. 홈의 실행 버튼이 부른다.</summary>
        private void StartOverlay()
        {
            if (_overlayWindow != null)
            {
                Dialogs.Show(Loc.Get("dialog.already_running"), Loc.Get("dialog.notice_title"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var overlay = new MainWindow();
            if (overlay.StartFailure is { } failure)
            {
                // 출력 장치를 열지 못하면 오버레이는 아무것도 그리지 못한다. 띄우지 않고 닫은 뒤 홈에 이유를 남긴다.
                // 런처를 최소화하기 전이라 홈을 보던 사람은 바로 안내를 본다.
                overlay.Close();
                OnOverlayStopped(failure);
                return;
            }

            _overlayWindow = overlay;
            overlay.OnSettingsChangedFromHotkey = () =>
            {
                // UI 스레드에서 LoadSettingsToUI 호출 (MainWindow는 이미 UI 스레드에서 이벤트를 발생시킴)
                LoadSettingsToUI();
            };
            overlay.AiAvailabilityChanged += (s, available) =>
            {
                _runStatus.SetAiAvailable(available);
                UpdateNotices();
            };
            overlay.Closed += (s, args) =>
            {
                _overlayWindow = null;
                BtnLaunch.IsEnabled = true;
                BtnStop.IsEnabled = false;
                OnOverlayStopped(overlay.ExitReason);

                // 오버레이 동작 중 핫키로 변경된 사항(모드 등)을 다시 UI에 반영
                LoadSettingsToUI();
            };
            // AI 를 만든 결과는 띄운 뒤에 온다. 그 알림을 실행 중의 것으로 받도록 띄우기 전에 실행 상태로 둔다.
            _runStatus.Started();
            overlay.Show();

            BtnLaunch.IsEnabled = false;
            BtnStop.IsEnabled = true;
            SetStatusUI(true);
            Announce(TxtStatus);

            if (AppSettings.AutoMinimizeOnLaunch)
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        /// <summary>오버레이를 닫는다. 홈의 실행 종료 버튼이 부른다. 나머지는 오버레이의 Closed 가 한다.</summary>
        private void StopOverlay() => _overlayWindow?.Close();

        /// <summary>오버레이가 닫혔거나 시작하지 못했다. 사용자가 끈 게 아니면 홈에 이유를 남긴다.</summary>
        private void OnOverlayStopped(StopReason reason)
        {
            bool unexpected = _runStatus.Stopped(reason);
            SetStatusUI(false);
            if (!unexpected)
            {
                Announce(TxtStatus);
                return;
            }
            AppLog.Info("launcher", $"오버레이가 멈췄습니다({reason}).");
        }

        /// <summary>
        /// 상태 줄 아래 안내를 지금 상태와 언어로 다시 넣는다. 안내가 새로 생기거나 바뀌면 화면 읽어주기가 읽고 지나가게 한다
        /// (홈을 보는 중에 꺼질 수 있다).
        /// </summary>
        private void UpdateNotices()
        {
            var notice = _runStatus.Notice(AppSettings.ShowAmbient);
            var stop = notice == HomeNotice.Stopped ? _runStatus.LastUnexpectedStop : null;

            string aiText = notice switch
            {
                HomeNotice.AiUnavailable => Loc.Get("notice.ai_unavailable"),
                HomeNotice.AiUnavailableHidden => Loc.Format("notice.ai_unavailable_hidden", Loc.Get("settings.show_ambient")),
                _ => "",
            };
            TxtAiNotice.Text = aiText;
            TxtAiNotice.Visibility = aiText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            string stopText = stop is { } reason ? StopText(reason) : "";
            TxtStoppedText.Text = stopText;
            // 제목과 내용을 한 번에 읽는다. 닫기 버튼은 따로 누를 수 있게 묶지 않는다.
            AutomationProperties.SetName(TxtStoppedText, stopText.Length > 0 ? Loc.Get("notice.stopped.title") + "\n" + stopText : "");
            PanelStopped.Visibility = stopText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (notice != _announcedNotice || stop != _announcedStop)
            {
                _announcedNotice = notice;
                _announcedStop = stop;
                if (aiText.Length > 0) Announce(TxtAiNotice);
                else if (stopText.Length > 0) Announce(TxtStoppedText);
            }
        }

        /// <summary>무엇이 꺼졌는지 알리는 문구. 이유마다 문구가 다르다(엉뚱한 원인을 가리키지 않게).</summary>
        private static string StopText(StopReason reason) => reason switch
        {
            StopReason.NoPlaybackDevice => Loc.Get("notice.stopped.no_device"),
            _ => Loc.Get("notice.stopped.capture_error"),
        };

        private void BtnDismissNotice_Click(object sender, RoutedEventArgs e)
        {
            _runStatus.Dismiss();
            UpdateNotices();
            // 누른 버튼이 사라지므로 키보드 초점을 실행 버튼으로 옮긴다.
            BtnLaunch.Focus();
        }

        /// <summary>바뀐 글을 화면 읽어주기에 알린다(요소에 AutomationProperties.LiveSetting 이 있어야 읽는다).</summary>
        private static void Announce(UIElement element)
        {
            if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;
            UIElementAutomationPeer.CreatePeerForElement(element)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
}
