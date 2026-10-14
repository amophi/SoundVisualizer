// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using SoundVisualizer.Localization;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SoundVisualizer
{
    /// <summary>
    /// 작업 표시줄 알림 영역의 아이콘. 안드로이드의 실행 중 알림과 빠른 설정 타일에 해당한다. 앱이 도는 동안 늘 있고,
    /// 런처를 최소화하거나 다른 창에 가려도 상태를 보고 켜고 끄고 런처로 돌아올 수 있다.
    ///
    /// 툴팁은 앱 이름과 상태, 메뉴는 상태(누를 수 없음) · 실행/실행 종료 · 런처 열기 · 종료다. 왼쪽 더블클릭은 런처를 연다.
    /// 무엇을 할지는 런처가 정한다(이 클래스는 이벤트만 낸다). 앱을 끝낼 때 Dispose 해야 알림 영역에 유령 아이콘이 남지 않는다.
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        private const string AppName = "Sound Visualizer";

        // Windows 가 받는 길이(NOTIFYICONDATA): 툴팁 128, 풍선 제목 64, 풍선 글 256 (끝의 \0 포함)
        private const int TooltipLimit = 127;
        private const int BalloonTitleLimit = 63;
        private const int BalloonTextLimit = 255;
        private const int BalloonTimeoutMs = 10000;

        private readonly Forms.NotifyIcon _icon;
        private readonly Forms.ContextMenuStrip _menu;
        private readonly Forms.ToolStripMenuItem _statusItem;
        private readonly Forms.ToolStripMenuItem _toggleItem;
        private readonly Forms.ToolStripMenuItem _openItem;
        private readonly Forms.ToolStripMenuItem _exitItem;
        private readonly Drawing.Icon? _appIcon;
        private bool _isRunning;
        private string _status = "";
        private bool _disposed;

        /// <summary>런처를 열어 달라(메뉴의 런처 열기, 아이콘 더블클릭, 풍선 알림 누르기).</summary>
        public event EventHandler? OpenRequested;

        /// <summary>실행 중이면 끄고, 아니면 켜 달라.</summary>
        public event EventHandler? ToggleRequested;

        /// <summary>앱을 끝내 달라.</summary>
        public event EventHandler? ExitRequested;

        public TrayIcon()
        {
            _statusItem = new Forms.ToolStripMenuItem { Enabled = false };
            _toggleItem = new Forms.ToolStripMenuItem();
            _toggleItem.Click += (s, e) => ToggleRequested?.Invoke(this, EventArgs.Empty);
            _openItem = new Forms.ToolStripMenuItem();
            _openItem.Click += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);
            _exitItem = new Forms.ToolStripMenuItem();
            _exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

            _menu = new Forms.ContextMenuStrip();
            _menu.Items.AddRange(new Forms.ToolStripItem[]
            {
                _statusItem,
                new Forms.ToolStripSeparator(),
                _toggleItem,
                _openItem,
                new Forms.ToolStripSeparator(),
                _exitItem,
            });

            _appIcon = LoadAppIcon();
            _icon = new Forms.NotifyIcon
            {
                Icon = _appIcon ?? Drawing.SystemIcons.Application,
                ContextMenuStrip = _menu,
                Text = AppName,
            };
            _icon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) OpenRequested?.Invoke(this, EventArgs.Empty);
            };
            _icon.BalloonTipClicked += (s, e) => OpenRequested?.Invoke(this, EventArgs.Empty);

            ApplyTexts();
            _icon.Visible = true;
        }

        /// <summary>실행 상태가 바뀌었다. <paramref name="status"/> 는 홈의 상태 글("상태: 실행 중")이다.</summary>
        public void SetState(bool isRunning, string status)
        {
            _isRunning = isRunning;
            _status = status;
            ApplyTexts();
        }

        /// <summary>메뉴와 툴팁을 지금 화면 언어로 다시 적는다. 언어를 바꿀 때 런처가 부른다(상태 글은 SetState 로 온다).</summary>
        public void ApplyTexts()
        {
            if (_disposed) return;
            _statusItem.Text = _status;
            _statusItem.Visible = _status.Length > 0;
            _toggleItem.Text = Loc.Get(_isRunning ? "launcher.home.stop" : "launcher.home.start");
            _openItem.Text = Loc.Get("tray.open");
            _exitItem.Text = Loc.Get("tray.exit");
            _menu.RightToLeft = Loc.Instance.IsRightToLeft ? Forms.RightToLeft.Yes : Forms.RightToLeft.No;
            _icon.Text = Clip(_status.Length > 0 ? AppName + "\n" + _status : AppName, TooltipLimit);
        }

        /// <summary>
        /// Windows 알림(풍선)으로 알린다. 사용자가 끄지 않았는데 멈췄고 런처를 보고 있지 않을 때 쓴다(안드로이드의 "꺼졌습니다" 알림).
        /// 방해 금지나 집중 모드면 Windows 가 알림 센터에만 남긴다.
        /// </summary>
        public void ShowAlert(string title, string text)
        {
            if (_disposed) return;
            _icon.ShowBalloonTip(BalloonTimeoutMs, Clip(title, BalloonTitleLimit), Clip(text, BalloonTextLimit), Forms.ToolTipIcon.Warning);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // 숨기고 버려야 알림 영역에서 바로 사라진다. 버리지 않고 끝내면 마우스를 올릴 때까지 아이콘이 남는다.
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
            _appIcon?.Dispose();
        }

        /// <summary>
        /// 앱 아이콘(실행 파일에 넣은 SoundVisualizer.ico)을 알림 영역 크기로 만든다. 꺼내지 못하면 null(Windows 기본 앱 아이콘을 쓴다).
        ///
        /// 아이콘에는 어두운 바탕에 가는 고리를 그린 256px 하나만 있다. 셸에 작은 크기로 꺼내 달라고 하면 고리가 빠져
        /// 검은 네모만 남아서, 256px 을 꺼내 고품질로 줄인다(작업 표시줄 단추처럼 고리가 옅게 남는다).
        /// </summary>
        private static Drawing.Icon? LoadAppIcon()
        {
            try
            {
                string? exe = Environment.ProcessPath;
                if (exe == null) return null;
                using var large = Drawing.Icon.ExtractIcon(exe, 0, 256);
                if (large == null) return null;
                using var source = large.ToBitmap();

                int size = Forms.SystemInformation.SmallIconSize.Width;
                using var small = new Drawing.Bitmap(size, size, Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Drawing.Graphics.FromImage(small))
                {
                    g.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(source, 0, 0, size, size);
                }

                // GetHicon 의 핸들은 Icon 이 갖지 않는다. 복사본을 남기고 원래 핸들은 바로 돌려준다.
                IntPtr handle = small.GetHicon();
                try
                {
                    using var borrowed = Drawing.Icon.FromHandle(handle);
                    return (Drawing.Icon)borrowed.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("tray", "앱 아이콘을 읽지 못해 기본 아이콘을 씁니다.", ex);
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        // 잘랐으면 끝에 말줄임표(U+2026)를 단다. 앱 소스의 문자열에는 ASCII 만 쓰므로 글자 코드로 적는다.
        private const char Ellipsis = (char)0x2026;

        private static string Clip(string text, int limit) => text.Length <= limit ? text : text[..(limit - 1)] + Ellipsis;
    }
}
