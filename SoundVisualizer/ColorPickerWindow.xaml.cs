// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SoundVisualizer.Localization;

namespace SoundVisualizer
{
    /// <summary>
    /// 소리 종류의 색을 고르는 창. 안드로이드 ColorPickerDialog 와 같이 채도 x 밝기 사각형, 색상 막대, 미리 담긴 색 8개로 고르고,
    /// PC 에서는 #RRGGBB 를 직접 적을 수도 있다. 시각화는 늘 불투명하게 그리므로 투명도는 다루지 않는다.
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        // 미리 담긴 색(흰, 빨강, 주황, 노랑, 초록, 청록, 파랑, 자홍)과 그 이름. 안드로이드 색 선택 창과 같은 여덟 색, 같은 이름(cd_preset_*)이다.
        private static readonly (uint Rgb, string NameKey)[] PresetColors =
        {
            (0xFFFFFF, "color_picker.preset.white"),
            (0xFF0000, "color_picker.preset.red"),
            (0xFF7F00, "color_picker.preset.orange"),
            (0xFFFF00, "color_picker.preset.yellow"),
            (0x00FF00, "color_picker.preset.green"),
            (0x00FFFF, "color_picker.preset.cyan"),
            (0x0080FF, "color_picker.preset.blue"),
            (0xFF00FF, "color_picker.preset.magenta"),
        };

        // 색상 막대 값을 읽을 때 붙이는 도 기호. 앱 소스의 문자열에는 ASCII 만 쓰므로 글자 코드로 적는다.
        private const char Degree = (char)0xB0;

        // 사각형 손잡이(지름 22)와 색상 막대 손잡이(지름 22, 막대 높이 28 에서 3 씩 안쪽)의 반지름
        private const double ThumbRadius = 11;

        /// <summary>고른 색. 설정에 그대로 저장하는 "#FFRRGGBB" 꼴이다.</summary>
        public string SelectedHexColor { get; private set; }

        // 지금 고른 색. 내보내는 값은 이것이다. 미리 담긴 색과 적은 코드는 HSV 를 거치지 않고 그대로 둔다(반올림으로 1 씩 어긋나지 않게).
        private Color _color = Colors.Red;

        // 사각형과 막대 위치. 회색(채도 0)이나 검정(밝기 0)에서는 RGB 로 색상을 되살릴 수 없어, 끄는 동안은 이 값을 그대로 쓴다.
        private double _hue;
        private double _saturation = 1;
        private double _brightness = 1;

        private bool _writingHexText;
        private bool _syncingPresets;
        private readonly List<RadioButton> _presetButtons = new();

        public ColorPickerWindow(string initialHexColor)
        {
            InitializeComponent();
            BuildPresets();

            // 읽을 수 없는 값이면 빨강에서 시작한다(예전과 같다).
            Color start = TryParseHex(initialHexColor, out var parsed) ? parsed : Colors.Red;
            SelectedHexColor = ToSettingHex(start);
            SetColor(start, updateHsv: true, writeHexText: true);
        }

        private void BuildPresets()
        {
            foreach (var (rgb, nameKey) in PresetColors)
            {
                var color = FromRgb(rgb);
                var button = new RadioButton
                {
                    Style = (Style)FindResource("PresetChoice"),
                    Background = new SolidColorBrush(color),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Tag = color,
                };
                // 화면 읽어주기는 색 이름("빨간색")을 읽고, 16진 코드는 도움말로 덧붙인다. 이름은 툴팁으로도 보인다(PresetChoice).
                button.SetBinding(AutomationProperties.NameProperty, Loc.BindingFor(nameKey));
                AutomationProperties.SetHelpText(button, ToDisplayHex(color));
                // Click 이 아니라 Checked 로 받는다. 화면 읽어주기의 "선택"은 IsChecked 만 바꾸고 Click 을 일으키지 않는다.
                button.Checked += Preset_Checked;
                _presetButtons.Add(button);
                PresetGrid.Children.Add(button);
            }
        }

        private void Preset_Checked(object sender, RoutedEventArgs e)
        {
            if (_syncingPresets) return;
            if (sender is RadioButton { Tag: Color color }) SetColor(color, updateHsv: true, writeHexText: true);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void BtnApply_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        // --- 색 코드 입력칸 ---

        private void TxtHexCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_writingHexText || !IsLoaded) return;
            // 적는 중에는 입력칸을 고쳐 쓰지 않는다. 고쳐 쓰면 글자 위치가 튄다.
            if (TryParseHex(TxtHexCode.Text, out var color)) SetColor(color, updateHsv: true, writeHexText: false);
        }

        private void TxtHexCode_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => WriteHexText();

        // --- 채도 x 밝기 사각형 ---

        private void SV_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SVBorder.Focus();
            SVBorder.CaptureMouse();
            PickSaturationBrightness(e.GetPosition(SVBorder));
        }

        private void SV_MouseMove(object sender, MouseEventArgs e)
        {
            if (SVBorder.IsMouseCaptured) PickSaturationBrightness(e.GetPosition(SVBorder));
        }

        private void SV_MouseUp(object sender, MouseButtonEventArgs e) => SVBorder.ReleaseMouseCapture();

        private void PickSaturationBrightness(Point p)
        {
            double width = SVBorder.ActualWidth, height = SVBorder.ActualHeight;
            if (width <= 0 || height <= 0) return;
            _saturation = Math.Clamp(p.X / width, 0, 1);
            _brightness = 1 - Math.Clamp(p.Y / height, 0, 1);
            SetColor(HsvToColor(_hue, _saturation, _brightness), updateHsv: false, writeHexText: true);
        }

        private void SV_KeyDown(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.1 : 0.01;
            switch (e.Key)
            {
                case Key.Left: _saturation -= step * Forward(SVBorder); break;
                case Key.Right: _saturation += step * Forward(SVBorder); break;
                case Key.Up: _brightness += step; break;
                case Key.Down: _brightness -= step; break;
                default: return;
            }
            _saturation = Math.Clamp(_saturation, 0, 1);
            _brightness = Math.Clamp(_brightness, 0, 1);
            SetColor(HsvToColor(_hue, _saturation, _brightness), updateHsv: false, writeHexText: true);
            e.Handled = true;
        }

        // --- 색상 막대 ---

        private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HueBorder.Focus();
            HueBorder.CaptureMouse();
            PickHue(e.GetPosition(HueBorder));
        }

        private void Hue_MouseMove(object sender, MouseEventArgs e)
        {
            if (HueBorder.IsMouseCaptured) PickHue(e.GetPosition(HueBorder));
        }

        private void Hue_MouseUp(object sender, MouseButtonEventArgs e) => HueBorder.ReleaseMouseCapture();

        private void PickHue(Point p)
        {
            double width = HueBorder.ActualWidth;
            if (width <= 0) return;
            _hue = Math.Clamp(p.X / width, 0, 1) * 360.0;
            SetColor(HsvToColor(_hue, _saturation, _brightness), updateHsv: false, writeHexText: true);
        }

        private void Hue_KeyDown(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            switch (e.Key)
            {
                case Key.Left: _hue -= step * Forward(HueBorder); break;
                case Key.Right: _hue += step * Forward(HueBorder); break;
                default: return;
            }
            _hue = Math.Clamp(_hue, 0, 360);
            SetColor(HsvToColor(_hue, _saturation, _brightness), updateHsv: false, writeHexText: true);
            e.Handled = true;
        }

        /// <summary>오른쪽 화살표가 값을 키우면 1. 오른쪽에서 왼쪽으로 쓰는 화면은 사각형·막대가 뒤집혀 있어 왼쪽 화살표가 키운다.</summary>
        private static int Forward(FrameworkElement element) => element.FlowDirection == FlowDirection.RightToLeft ? -1 : 1;

        private void Area_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisuals();

        // --- 색 반영 ---

        /// <summary>
        /// 고른 색을 바꾼다. 사각형·막대를 끄는 중이면 updateHsv 를 거짓으로 두어 그 위치를 지키고, 미리 담긴 색과 적은 코드는
        /// RGB 에서 위치를 다시 구한다. 적는 중인 입력칸은 고쳐 쓰지 않는다.
        /// </summary>
        private void SetColor(Color color, bool updateHsv, bool writeHexText)
        {
            _color = Color.FromRgb(color.R, color.G, color.B);
            SelectedHexColor = ToSettingHex(_color);
            if (updateHsv) ColorToHsv(_color, out _hue, out _saturation, out _brightness);

            UpdateVisuals();
            if (writeHexText) WriteHexText();

            // 지금 색과 같은 미리 담긴 색에 고리를 둔다. 여기서 바꾸는 IsChecked 는 고른 것으로 치지 않는다.
            _syncingPresets = true;
            foreach (var button in _presetButtons) button.IsChecked = button.Tag is Color preset && preset == _color;
            _syncingPresets = false;
        }

        private void WriteHexText()
        {
            _writingHexText = true;
            TxtHexCode.Text = ToDisplayHex(_color);
            _writingHexText = false;
        }

        private void UpdateVisuals()
        {
            SVHue.Background = new SolidColorBrush(HsvToColor(_hue, 1, 1));
            ColorPreview.Background = new SolidColorBrush(_color);

            // 화면 읽어주기가 읽는 값. 화살표로 옮길 때마다 바뀐 값을 다시 읽는다.
            SVBorder.ValueText = Loc.Format("color_picker.area_value", Percent(_saturation), Percent(_brightness));
            HueBorder.ValueText = Math.Round(_hue).ToString(CultureInfo.InvariantCulture) + Degree;

            double width = SVBorder.ActualWidth, height = SVBorder.ActualHeight;
            if (width > 0 && height > 0)
            {
                Canvas.SetLeft(SVThumb, _saturation * width - ThumbRadius);
                Canvas.SetTop(SVThumb, (1 - _brightness) * height - ThumbRadius);
            }

            // 막대 손잡이는 반지름만큼 안으로 물린다. 그러지 않으면 양 끝에서 반이 잘린다(안드로이드와 같다).
            double hueWidth = HueBorder.ActualWidth;
            if (hueWidth > 0)
            {
                double center = Math.Clamp(_hue / 360.0 * hueWidth, ThumbRadius, Math.Max(ThumbRadius, hueWidth - ThumbRadius));
                Canvas.SetLeft(HueThumb, center - ThumbRadius);
            }
        }

        // --- 변환 ---

        /// <summary>"#RRGGBB" 나 "#AARRGGBB"("#" 는 없어도 된다). 투명도는 버린다.</summary>
        private static bool TryParseHex(string? text, out Color color)
        {
            color = Colors.Red;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string hex = text.Trim().TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8) return false;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value)) return false;
            color = FromRgb(value & 0xFFFFFF);
            return true;
        }

        private static Color FromRgb(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        /// <summary>화면에 보이는 꼴. 안드로이드 색 선택 창과 같이 "#RRGGBB" 다.</summary>
        private static string ToDisplayHex(Color c) => string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}");

        /// <summary>설정에 저장하는 꼴. 예전과 같이 불투명 알파를 붙인 "#FFRRGGBB" 다.</summary>
        private static string ToSettingHex(Color c) => string.Create(CultureInfo.InvariantCulture, $"#FF{c.R:X2}{c.G:X2}{c.B:X2}");

        private static void ColorToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            h = 0;
            if (delta > 0)
            {
                if (max == r) h = 60 * (((g - b) / delta) % 6);
                else if (max == g) h = 60 * (((b - r) / delta) + 2);
                else h = 60 * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360;

            s = max == 0 ? 0 : delta / max;
            v = max;
        }

        private static Color HsvToColor(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
        }

        private static byte ToByte(double unit) => (byte)Math.Clamp(Math.Round(unit * 255, MidpointRounding.AwayFromZero), 0, 255);

        private static int Percent(double unit) => (int)Math.Round(unit * 100, MidpointRounding.AwayFromZero);
    }
}
