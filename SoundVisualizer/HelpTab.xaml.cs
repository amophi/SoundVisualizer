// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using SoundVisualizer.Help;
using SoundVisualizer.Localization;

namespace SoundVisualizer
{
    /// <summary>
    /// 런처의 도움말 탭. 글은 XAML 이 문자열 표에 바인딩하고, 여기서는 {0} 자리가 있는 글(버튼·설정 이름, 버전)과 제보·라이선스 버튼을 맡는다.
    /// 탭을 오갈 때마다 Loaded·Unloaded 가 오므로 그때 언어 바뀜 알림을 붙였다 뗀다.
    /// </summary>
    public partial class HelpTab : UserControl
    {
        /// <summary>제보에 넣는 환경 정보. 화면에 보이는 것과 보내는 것이 같다.</summary>
        private string _environment = "";

        public HelpTab()
        {
            InitializeComponent();
        }

        private void HelpTab_Loaded(object sender, RoutedEventArgs e)
        {
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            ApplyTexts();
        }

        private void HelpTab_Unloaded(object sender, RoutedEventArgs e) => Loc.Instance.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            ApplyTexts();
            // 지난 결과 문구는 바뀌기 전 언어다. 다음에 누를 때 다시 나온다.
            TxtReportResult.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 바인딩이 아니라 코드가 넣는 글. 버튼·설정 이름은 그 화면의 글자를 그대로 끼워 넣어, 번역이 바뀌어도 도움말과 화면이 어긋나지 않는다.
        /// </summary>
        private void ApplyTexts()
        {
            TxtStart2.Text = Loc.Format("help.start.2", Loc.Get("launcher.home.start"));
            TxtStart4.Text = Loc.Format("help.start.4", Loc.Get("launcher.home.stop"));
            TxtFaqPerformance.Text = Loc.Format("help.faq.performance_a", Loc.Get("settings.max_fps.title"), Loc.Get("settings.glow.title"));
            TxtFaqLanguage.Text = Loc.Format("help.faq.language_a", Loc.Get("common.language"), Loc.Get("language.system"));
            TxtVersion.Text = Loc.Format("help.about.version", AppVersionText.Display(InformationalVersion));

            // 링크가 문장 가운데에 오므로 번역문의 {0} 자리에서 나눠 앞뒤 글을 넣는다. 언어마다 어순이 달라 링크 위치를 번역문이 정한다.
            string tip = Loc.Get("help.sound_mode.tip");
            int at = tip.IndexOf("{0}", StringComparison.Ordinal);
            TxtTipPrefix.Text = at < 0 ? tip + " " : tip[..at];
            TxtTipSuffix.Text = at < 0 ? "" : tip[(at + 3)..];

            // 넘기는 값이 전부라 다른 정보(소리, 장치 이름, 사용자 경로)가 섞이지 않는다. 언어는 지금 화면 언어의 코드다.
            _environment = ReportLink.EnvironmentBlock(
                AppVersionText.ForReport(InformationalVersion),
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                Loc.Instance.Code);
            TxtEnvironment.Text = _environment;
        }

        /// <summary>실행 파일의 버전. .NET SDK 가 빌드한 커밋을 + 뒤에 붙인다.</summary>
        private static string? InformationalVersion =>
            typeof(HelpTab).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        private void BtnReportGithub_Click(object sender, RoutedEventArgs e)
        {
            string url = ReportLink.IssueUrl(Loc.Get("help.report.issue_title"), Loc.Get("help.report.issue_body"), _environment);
            if (TryOpen(url)) TxtReportResult.Visibility = Visibility.Collapsed;
            else ShowResult(Loc.Get("help.report.no_browser"));
        }

        private void BtnReportMail_Click(object sender, RoutedEventArgs e)
        {
            string uri = ReportLink.MailtoUri(ReportLink.ReportEmail, Loc.Get("help.report.issue_title"), Loc.Get("help.report.issue_body"), _environment);
            if (TryOpen(uri)) TxtReportResult.Visibility = Visibility.Collapsed;
            else ShowResult(Loc.Get("help.report.no_mail"));
        }

        private void BtnCopyEnvironment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 다른 앱에 붙여 넣기 좋게 Windows 줄바꿈으로 넣는다.
                Clipboard.SetText(_environment.Replace("\n", "\r\n", StringComparison.Ordinal));
                ShowResult(Loc.Get("help.report.copied"));
            }
            catch (ExternalException ex)
            {
                // 다른 앱이 클립보드를 붙잡고 있으면 WPF 가 몇 번 다시 해 보고도 실패한다.
                AppLog.Warn("help", "환경 정보를 클립보드에 넣지 못했습니다.", ex);
                ShowResult(Loc.Get("help.report.copy_failed"));
            }
        }

        private void BtnLicenses_Click(object sender, RoutedEventArgs e)
        {
            var window = new LicenseWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            e.Handled = true;
            if (!TryOpen(e.Uri.AbsoluteUri)) Dialogs.Show(Loc.Format("dialog.link_error", e.Uri.AbsoluteUri));
        }

        /// <summary>결과 문구를 보이고 화면 읽어주기에 알린다.</summary>
        private void ShowResult(string text)
        {
            TxtReportResult.Text = text;
            TxtReportResult.Visibility = Visibility.Visible;
            UIElementAutomationPeer.CreatePeerForElement(TxtReportResult)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        /// <summary>
        /// 주소를 Windows 가 정한 앱(브라우저, 메일 앱)으로 연다. 열 앱이 없거나 실행하지 못하면 false. 앱은 인터넷에 직접 닿지 않고 주소만 넘긴다.
        /// </summary>
        private static bool TryOpen(string target)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
            {
                AppLog.Warn("help", "링크를 열지 못했습니다.", ex);
                return false;
            }
        }
    }
}
