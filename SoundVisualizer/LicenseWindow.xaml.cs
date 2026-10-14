// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SoundVisualizer.Help;

namespace SoundVisualizer
{
    /// <summary>
    /// 오픈소스 라이선스 창. THIRD_PARTY.md, Pretendard 글꼴 라이선스(OFL), 소리 이름 고지, 이 앱의 라이선스(AGPL)를 차례로 보인다.
    /// 고지는 실행 파일에 들어 있어(Core 의 LicenseNotices) 따로 파일을 찾지 않는다.
    /// </summary>
    public partial class LicenseWindow : Window
    {
        // 고지 네 개를 합쳐도 50KB 남짓이라 창을 열 때 읽는다.
        private readonly string _raw = LicenseNotices.ReadAll();
        private int _columns = -1;

        public LicenseWindow()
        {
            InitializeComponent();
            TxtLicenses.Text = _raw;
        }

        private void TxtLicenses_SizeChanged(object sender, SizeChangedEventArgs e) => FitRulers();

        // 글자판 폭(ViewportWidth)은 첫 배치가 끝나야 정해진다. 크기 알림 때 아직 0 이었으면 처음 그린 뒤에 맞춘다.
        private void Window_ContentRendered(object? sender, EventArgs e) => FitRulers();

        /// <summary>
        /// 글자판 폭에 들어가는 칸 수로 구분선을 맞춘다(안드로이드 #167). 고지는 80칸에 맞춰 적혀 있어 창이 좁으면 '=' 줄이 두세 줄로 꺾인다.
        /// 칸 수는 실제 글꼴로 '=' 100개의 폭을 재서 한 글자 폭을 구해 나눈다. 스크롤 막대는 늘 보여 폭이 흔들리지 않는다.
        /// </summary>
        private void FitRulers()
        {
            double available = TxtLicenses.ViewportWidth;
            if (available <= 0) return;

            var sample = new FormattedText(new string('=', 100), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(TxtLicenses.FontFamily, TxtLicenses.FontStyle, TxtLicenses.FontWeight, TxtLicenses.FontStretch),
                TxtLicenses.FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double charWidth = sample.WidthIncludingTrailingWhitespace / 100;
            // 글자판은 양옆에 몇 px 의 여백을 둔다. 한 칸을 덜 잡아 마지막 '=' 가 다음 줄로 넘어가지 않게 한다.
            int columns = charWidth > 0 ? Math.Max(1, (int)(available / charWidth) - 1) : 0;
            if (columns == _columns) return;

            _columns = columns;
            TxtLicenses.Text = LicenseNotices.DisplayText(_raw, columns);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
