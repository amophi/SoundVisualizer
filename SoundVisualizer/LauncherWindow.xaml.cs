// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using SoundVisualizer.Localization;

namespace SoundVisualizer
{
    public partial class LauncherWindow : Window, IMMNotificationClient
    {
        private bool _isInitializing = true;
        private MainWindow? _overlayWindow = null;
        private MMDeviceEnumerator? _deviceEnumerator;

        private string? _bindingTarget = null;
        private System.Collections.Generic.HashSet<int> _currentlyHeldKeys = new System.Collections.Generic.HashSet<int>();
        private System.Collections.Generic.HashSet<int> _maxKeysInCurrentBinding = new System.Collections.Generic.HashSet<int>();

        // 네 모드 카드의 설정 칸. 카드마다 모양이 같아 이 표 하나로 읽고 쓴다(순서는 AppSettings.VisualMode 번호와 같다).
        private readonly ModeControls[] _modes;

        public LauncherWindow()
        {
            InitializeComponent();
            _modes = new[]
            {
                new ModeControls(() => AppSettings.WaveMode, ChkIntensityAsOpacityWave, SldOpacityFixedSizeWave, SldOpacityFixedMaxOpacityWave,
                    SldIntensityWave, SldOpacityWave, SldSpeedWave, SldSensitivityWave, ChkGlowModeWave, SldGlowIntensityWave, null),
                new ModeControls(() => AppSettings.PadMode, ChkIntensityAsOpacityPad, SldOpacityFixedSizePad, SldOpacityFixedMaxOpacityPad,
                    SldIntensityPad, SldOpacityPad, SldSpeedPad, SldSensitivityPad, ChkGlowModePad, SldGlowIntensityPad, null),
                new ModeControls(() => AppSettings.CircleMode, ChkIntensityAsOpacityCircle, SldOpacityFixedSizeCircle, SldOpacityFixedMaxOpacityCircle,
                    SldIntensityCircle, SldOpacityCircle, SldSpeedCircle, SldSensitivityCircle, ChkGlowModeCircle, SldGlowIntensityCircle, SldCircleRadius),
                new ModeControls(() => AppSettings.OutlineMode, ChkIntensityAsOpacityOutline, SldOpacityFixedSizeOutline, SldOpacityFixedMaxOpacityOutline,
                    SldIntensityOutline, SldOpacityOutline, SldSpeedOutline, SldSensitivityOutline, ChkGlowModeOutline, SldGlowIntensityOutline, null),
            };
            InitializeUI();
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _deviceEnumerator.RegisterEndpointNotificationCallback(this);
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            Loc.Instance.LanguageChanged -= OnLanguageChanged;

            // 1. 오버레이 창 종료 처리 (오류 발생 시에도 계속 진행)
            try
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.Close();
                    _overlayWindow = null;
                }
            }
            catch { }

            // 2. 오디오 기본 출력 장치 COM 콜백 해제 및 Dispose 처리 (COM 장치 이탈 시 등의 오류 원천 방어)
            try
            {
                if (_deviceEnumerator != null)
                {
                    try
                    {
                        _deviceEnumerator.UnregisterEndpointNotificationCallback(this);
                    }
                    catch { }
                    
                    try
                    {
                        _deviceEnumerator.Dispose();
                    }
                    catch { }
                    _deviceEnumerator = null;
                }
            }
            catch { }

            // 3. 부모 클래스 종료 이벤트 처리
            try
            {
                base.OnClosed(e);
            }
            catch { }

            // 4. 애플리케이션 정상 셧다운 및 OS 수준 강제 프로세스 종료 처리 (더블 보안 설계)
            try
            {
                Application.Current.Shutdown();
            }
            catch
            {
                try
                {
                    Environment.Exit(0);
                }
                catch { }
            }
        }

        private void InitializeUI()
        {
            // 언어 콤보: 시스템 기본값 + 18개 언어. 저장된 선택(system 또는 코드)을 Tag 로 찾아 고른다.
            LanguagePicker.Fill(CmbLanguage);
            LanguagePicker.Select(CmbLanguage, AppSettings.Language);

            // 현재 환경의 출력 장치 사운드 채널 자동 파악 및 설정
            try
            {
                using (var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
                {
                    var device = enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
                    int channels = device.AudioClient.MixFormat.Channels;
                    if (channels >= 8) AppSettings.SoundMode = 2; // 7.1채널
                    else if (channels >= 6) AppSettings.SoundMode = 1; // 5.1채널
                    else AppSettings.SoundMode = 0; // 2채널
                }
            }
            catch { }

            LoadSettingsToUI();
            _isInitializing = false;

            ApplyCodeTexts();
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (LanguagePicker.SelectedSetting(CmbLanguage) is { } setting && setting != AppSettings.Language)
            {
                // 저장하고 열린 창 모두에 적용한다. XAML 글자는 바인딩이, 코드가 넣은 글자는 OnLanguageChanged 가 바꾼다.
                Loc.Choose(setting);
            }
        }

        /// <summary>언어가 바뀌었다(이 창이나 오버레이 편집 패널에서). 언어 콤보를 맞추고 코드가 넣은 글자를 다시 넣는다.</summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            bool wasInitializing = _isInitializing;
            _isInitializing = true; // 콤보를 맞추는 동안 다시 언어를 바꾸지 않게
            LanguagePicker.Select(CmbLanguage, AppSettings.Language);
            _isInitializing = wasInitializing;

            ApplyCodeTexts();
        }

        /// <summary>바인딩이 아니라 코드가 넣는 글자: 실행 상태, 링크가 든 안내 문장, 단축키 이름(키가 없을 때의 표시도 언어를 따른다).</summary>
        private void ApplyCodeTexts()
        {
            SetStatusUI(_overlayWindow != null);
            UpdateHelpTip();
            if (_bindingTarget != "Visual") BtnVisualHotkey.Content = GetKeysName(AppSettings.VisualModeKeyBind);
            if (_bindingTarget != "Sound") BtnSoundModeHotkey.Content = GetKeysName(AppSettings.StereoUpmixKeyBind);
            if (_bindingTarget != "Edit") BtnEditHotkey.Content = GetKeysName(AppSettings.EditModeKeyBind);
        }

        /// <summary>
        /// 링크가 문장 가운데에 오므로 번역문의 {0} 자리에서 나눠 앞뒤 글을 넣는다. 언어마다 어순이 달라 링크 위치를 번역문이 정한다.
        /// </summary>
        private void UpdateHelpTip()
        {
            string tip = Loc.Get("help.sound_mode.tip");
            int at = tip.IndexOf("{0}", StringComparison.Ordinal);
            TxtHelp1TipPrefix.Text = at < 0 ? tip + " " : tip[..at];
            TxtHelp1TipSuffix.Text = at < 0 ? "" : tip[(at + 3)..];
        }

        private void ComboBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && !comboBox.IsDropDownOpen)
            {
                e.Handled = true;
            }
        }

        private void SetStatusUI(bool isRunning)
        {
            TxtStatus.Text = isRunning ? Loc.Get("launcher.status.running") : Loc.Get("launcher.status.idle");
            // 실행 중이면 상태 점만 강조색이 된다(안드로이드 홈과 같다). 글자는 늘 보조 글자색이다.
            StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, isRunning ? "AccentBrush" : "SecondaryTextBrush");
        }

        private void LoadSettingsToUI()
        {
            // 1. 모드별 설정 (파도, 패드, 원형, 외곽선)
            foreach (var mode in _modes) LoadMode(mode);

            // 2. 공통 설정
            ChipsVisualMode.SelectedIndex = AppSettings.VisualMode;
            ChipsSoundMode.SelectedIndex = AppSettings.SoundMode;
            SldTargetFps.Maximum = AppSettings.GetMonitorRefreshRate();
            SldTargetFps.Value = AppSettings.TargetFps;
            ChkAdminMode.IsChecked = AppSettings.IsAdminMode;
            if (ChkAutoMinimizeOnLaunch != null) ChkAutoMinimizeOnLaunch.IsChecked = AppSettings.AutoMinimizeOnLaunch;

            if (BtnVisualHotkey != null) BtnVisualHotkey.Content = GetKeysName(AppSettings.VisualModeKeyBind);
            if (BtnSoundModeHotkey != null) BtnSoundModeHotkey.Content = GetKeysName(AppSettings.StereoUpmixKeyBind);
            if (BtnEditHotkey != null) BtnEditHotkey.Content = GetKeysName(AppSettings.EditModeKeyBind);

            if (ChkShowAmbient != null)
            {
                ChkShowAmbient.IsChecked = AppSettings.ShowAmbient;
                ChkShowSpeech.IsChecked = AppSettings.ShowSpeech;
                ChkShowDanger.IsChecked = AppSettings.ShowDanger;

                SetColorButton(BtnColorAmbient, AppSettings.ColorAmbient);
                SetColorButton(BtnColorSpeech, AppSettings.ColorSpeech);
                SetColorButton(BtnColorDanger, AppSettings.ColorDanger);
            }

            // 3. 고른 모드의 카드를 펼쳐 맨 위로
            UpdateExpanderStates();

            // 4. 소리 탭: 종류 색(점·거르기 칸)과 바꾼 종류
            SoundsTabView?.ReloadSettings();
        }

        /// <summary>
        /// 모드 하나의 칸에 저장된 값을 넣는다. 진하기·최대 진하기는 저장값(불투명도 %)을 그대로 보인다.
        /// 예전에는 100 에서 뺀 투명도로 보였는데, 값이 클수록 옅어져 다른 슬라이더와 방향이 거꾸로였다.
        /// </summary>
        private static void LoadMode(ModeControls c)
        {
            var settings = c.Settings();
            c.Size.Value = settings.Intensity;
            c.SizeLock.IsChecked = settings.IntensityAsOpacity;
            c.FixedSize.Value = settings.OpacityFixedSize;
            c.MaxOpacity.Value = settings.OpacityFixedMaxOpacity;
            c.Speed.Value = settings.PositionSpeed;
            c.Sensitivity.Value = settings.Sensitivity * 4.0;
            c.Opacity.Value = settings.VisualOpacity;
            c.Glow.IsChecked = settings.IsGlowMode;
            c.GlowStrength.Value = settings.GlowIntensity;
            if (c.Radius != null) c.Radius.Value = settings.CircleRadius;
            ShowDependentRows(c, settings);
        }

        /// <summary>
        /// 스위치에 딸린 줄을 보이거나 접는다. 크기 고정을 켜면 고정 크기·최대 진하기를, 끄면 크기·진하기를 쓰고,
        /// 광원을 켜야 광원 강도를 쓴다. 지금 먹지 않는 줄은 흐리게 남기지 않고 접는다(안드로이드 DependentSettings).
        /// </summary>
        private static void ShowDependentRows(ModeControls c, VisualModeSettings settings)
        {
            bool sizeLocked = settings.IntensityAsOpacity;
            Show(c.FixedSize, sizeLocked);
            Show(c.MaxOpacity, sizeLocked);
            Show(c.Size, !sizeLocked);
            Show(c.Opacity, !sizeLocked);
            Show(c.GlowStrength, settings.IsGlowMode);
        }

        private static void Show(UIElement element, bool visible) => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>모드 카드 칸 하나가 바뀌었다. 그 칸이 이 모드의 것이면 설정에 쓰고 true.</summary>
        private static bool ApplyModeChange(ModeControls c, object sender)
        {
            var settings = c.Settings();
            if (sender == c.Size) settings.Intensity = c.Size.Value;
            else if (sender == c.SizeLock)
            {
                settings.IntensityAsOpacity = c.SizeLock.IsChecked ?? false;
                ShowDependentRows(c, settings);
            }
            else if (sender == c.FixedSize) settings.OpacityFixedSize = c.FixedSize.Value;
            else if (sender == c.MaxOpacity) settings.OpacityFixedMaxOpacity = c.MaxOpacity.Value;
            else if (sender == c.Speed) settings.PositionSpeed = c.Speed.Value;
            else if (sender == c.Sensitivity) settings.Sensitivity = c.Sensitivity.Value / 4.0;
            else if (sender == c.Opacity) settings.VisualOpacity = c.Opacity.Value;
            else if (sender == c.Glow)
            {
                settings.IsGlowMode = c.Glow.IsChecked ?? false;
                ShowDependentRows(c, settings);
            }
            else if (sender == c.GlowStrength) settings.GlowIntensity = c.GlowStrength.Value;
            else if (c.Radius != null && sender == c.Radius) settings.CircleRadius = c.Radius.Value;
            else return false;
            return true;
        }

        /// <summary>모드 카드 하나의 칸. 설정 객체는 초기화 버튼이 바꿔 끼우므로 쓸 때마다 Settings 로 꺼낸다.</summary>
        private sealed record ModeControls(
            Func<VisualModeSettings> Settings,
            CheckBox SizeLock,
            Slider FixedSize,
            Slider MaxOpacity,
            Slider Size,
            Slider Opacity,
            Slider Speed,
            Slider Sensitivity,
            CheckBox Glow,
            Slider GlowStrength,
            Slider? Radius);

        private void UpdateExpanderStates()
        {
            if (ExpanderWave == null || ExpanderPad == null || ExpanderCircle == null || ExpanderOutline == null || PanelModeExpanders == null) return;
            
            bool wasInitializing = _isInitializing;
            _isInitializing = true; // IsExpanded 변경이 이벤트를 유발하여 설정을 변경하는 악순환 원천 차단
            
            // 1. 상태 변경 (현재 선택된 모드는 펼치고 나머지는 접음)
            ExpanderWave.IsExpanded = (AppSettings.VisualMode == 0);
            ExpanderPad.IsExpanded = (AppSettings.VisualMode == 1);
            ExpanderCircle.IsExpanded = (AppSettings.VisualMode == 2);
            ExpanderOutline.IsExpanded = (AppSettings.VisualMode == 3);
            
            // 2. 동적 물리적 순서 정렬 (현재 활성화된 모드가 맨 첫 번째로 가도록 패널 재배치)
            PanelModeExpanders.Children.Remove(ExpanderWave);
            PanelModeExpanders.Children.Remove(ExpanderPad);
            PanelModeExpanders.Children.Remove(ExpanderCircle);
            PanelModeExpanders.Children.Remove(ExpanderOutline);
            
            if (AppSettings.VisualMode == 0)
            {
                PanelModeExpanders.Children.Add(ExpanderWave);
                PanelModeExpanders.Children.Add(ExpanderPad);
                PanelModeExpanders.Children.Add(ExpanderCircle);
                PanelModeExpanders.Children.Add(ExpanderOutline);
            }
            else if (AppSettings.VisualMode == 1)
            {
                PanelModeExpanders.Children.Add(ExpanderPad);
                PanelModeExpanders.Children.Add(ExpanderWave);
                PanelModeExpanders.Children.Add(ExpanderCircle);
                PanelModeExpanders.Children.Add(ExpanderOutline);
            }
            else if (AppSettings.VisualMode == 2)
            {
                PanelModeExpanders.Children.Add(ExpanderCircle);
                PanelModeExpanders.Children.Add(ExpanderWave);
                PanelModeExpanders.Children.Add(ExpanderPad);
                PanelModeExpanders.Children.Add(ExpanderOutline);
            }
            else if (AppSettings.VisualMode == 3)
            {
                PanelModeExpanders.Children.Add(ExpanderOutline);
                PanelModeExpanders.Children.Add(ExpanderWave);
                PanelModeExpanders.Children.Add(ExpanderPad);
                PanelModeExpanders.Children.Add(ExpanderCircle);
            }
            
            _isInitializing = wasInitializing;
        }

        private void SetColorButton(Button btn, string hex)
        {
            try
            {
                var mediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                btn.Background = new System.Windows.Media.SolidColorBrush(mediaColor);
            }
            catch { }
        }

        private string GetKeysName(List<int> codes)
        {
            if (codes == null || codes.Count == 0) return Loc.Get("hotkey.none");
            List<string> names = new List<string>();
            foreach (var code in codes)
            {
                var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(code);
                string keyName = key.ToString();
                if (keyName.Contains("System")) keyName = "Alt"; 
                else if (keyName.StartsWith("Left")) keyName = keyName.Substring(4);
                else if (keyName.StartsWith("Right")) keyName = "R" + keyName.Substring(5);
                names.Add(keyName);
            }
            return string.Join(" + ", names);
        }

        private void BtnVisualHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartBinding("Visual");
        }

        private void BtnSoundModeHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartBinding("Sound");
        }

        private void BtnEditHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartBinding("Edit");
        }

        private void StartBinding(string target)
        {
            _bindingTarget = target;
            _currentlyHeldKeys.Clear();
            _maxKeysInCurrentBinding.Clear();
            string msg = Loc.Get("hotkey.prompt");
            if (target == "Visual") BtnVisualHotkey.Content = msg;
            else if (target == "Sound") BtnSoundModeHotkey.Content = msg;
            else if (target == "Edit") BtnEditHotkey.Content = msg;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_bindingTarget != null)
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    _bindingTarget = null;
                    _currentlyHeldKeys.Clear();
                    _maxKeysInCurrentBinding.Clear();
                    LoadSettingsToUI();
                    e.Handled = true;
                    return;
                }

                int vKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key);
                _currentlyHeldKeys.Add(vKey);
                _maxKeysInCurrentBinding.Add(vKey);
                
                string currentStr = GetKeysName(new System.Collections.Generic.List<int>(_maxKeysInCurrentBinding));
                if (_bindingTarget == "Visual") BtnVisualHotkey.Content = currentStr;
                else if (_bindingTarget == "Sound") BtnSoundModeHotkey.Content = currentStr;
                else if (_bindingTarget == "Edit") BtnEditHotkey.Content = currentStr;

                e.Handled = true;
            }
        }

        private void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_bindingTarget != null)
            {
                int vKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key);
                _currentlyHeldKeys.Remove(vKey);

                if (_currentlyHeldKeys.Count == 0 && _maxKeysInCurrentBinding.Count > 0)
                {
                    var keysToSave = new System.Collections.Generic.List<int>(_maxKeysInCurrentBinding);
                    
                    if (_bindingTarget == "Visual") AppSettings.VisualModeKeyBind = keysToSave;
                    else if (_bindingTarget == "Sound") AppSettings.StereoUpmixKeyBind = keysToSave;
                    else if (_bindingTarget == "Edit") AppSettings.EditModeKeyBind = keysToSave;

                    AppSettings.Save();
                    _bindingTarget = null;
                    _currentlyHeldKeys.Clear();
                    _maxKeysInCurrentBinding.Clear();
                    LoadSettingsToUI();
                }
                e.Handled = true;
            }
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (!ApplyToModeCards(sender))
            {
                if (sender == ChipsVisualMode)
                {
                    AppSettings.VisualMode = ChipsVisualMode.SelectedIndex;
                    UpdateExpanderStates();
                }
                else if (sender == ChipsSoundMode) AppSettings.SoundMode = ChipsSoundMode.SelectedIndex;
                else if (sender == SldTargetFps) AppSettings.TargetFps = SldTargetFps.Value;
                else if (sender == ChkAdminMode) AppSettings.IsAdminMode = ChkAdminMode.IsChecked ?? false;
                else if (sender == ChkAutoMinimizeOnLaunch) AppSettings.AutoMinimizeOnLaunch = ChkAutoMinimizeOnLaunch.IsChecked ?? false;
                else if (sender == ChkShowAmbient) AppSettings.ShowAmbient = ChkShowAmbient.IsChecked ?? true;
                else if (sender == ChkShowSpeech) AppSettings.ShowSpeech = ChkShowSpeech.IsChecked ?? true;
                else if (sender == ChkShowDanger) AppSettings.ShowDanger = ChkShowDanger.IsChecked ?? true;
            }

            AppSettings.Save();
        }

        // 선택지 칸(ChoiceChipList)은 이 앱에 있는 컨트롤이라 XAML 이 이벤트를 실행 중에 이름으로 잇는다. 그때는 인자 형식이
        // 정확히 같아야 해서(RoutedEventArgs 로 받는 Setting_Changed 는 이어지지 않는다) 이 처리기를 거친다.
        private void ChoiceChips_SelectionChanged(object sender, SelectionChangedEventArgs e) => Setting_Changed(sender, e);

        /// <summary>바뀐 칸이 모드 카드의 것이면 그 모드 설정에 쓰고 true.</summary>
        private bool ApplyToModeCards(object sender)
        {
            foreach (var mode in _modes)
            {
                if (ApplyModeChange(mode, sender)) return true;
            }
            return false;
        }

        private void BtnColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string type = btn.Tag?.ToString() ?? "";
                string currentColorHex = "#FFFFFF";

                if (type == "Ambient") currentColorHex = AppSettings.ColorAmbient;
                else if (type == "Speech") currentColorHex = AppSettings.ColorSpeech;
                else if (type == "Danger") currentColorHex = AppSettings.ColorDanger;

                var dialog = new ColorPickerWindow(currentColorHex);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    string hex = dialog.SelectedHexColor;

                    if (type == "Ambient") AppSettings.ColorAmbient = hex;
                    else if (type == "Speech") AppSettings.ColorSpeech = hex;
                    else if (type == "Danger") AppSettings.ColorDanger = hex;

                    LoadSettingsToUI();
                    AppSettings.Save();
                }
            }
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow != null)
            {
                Dialogs.Show(Loc.Get("dialog.already_running"), Loc.Get("dialog.notice_title"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _overlayWindow = new MainWindow();
            _overlayWindow.OnSettingsChangedFromHotkey = () =>
            {
                // UI 스레드에서 LoadSettingsToUI 호출 (MainWindow는 이미 UI 스레드에서 이벤트를 발생시킴)
                LoadSettingsToUI();
            };
            _overlayWindow.Closed += (s, args) => 
            {
                _overlayWindow = null;
                BtnLaunch.IsEnabled = true;
                BtnStop.IsEnabled = false;
                SetStatusUI(false);
                
                // 오버레이 동작 중 핫키로 변경된 사항(모드 등)을 다시 UI에 반영
                LoadSettingsToUI();
            };
            _overlayWindow.Show();
            
            BtnLaunch.IsEnabled = false;
            BtnStop.IsEnabled = true;
            SetStatusUI(true);

            if (AppSettings.AutoMinimizeOnLaunch)
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
            }
        }
        private void BtnResetWave_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(Loc.Get("dialog.reset_wave"), Loc.Get("dialog.reset_title"))) return;
            AppSettings.WaveMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            _isInitializing = true;
            LoadSettingsToUI();
            _isInitializing = false;
            AppSettings.Save();
        }

        private void BtnResetPad_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(Loc.Get("dialog.reset_pad"), Loc.Get("dialog.reset_title"))) return;
            AppSettings.PadMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            _isInitializing = true;
            LoadSettingsToUI();
            _isInitializing = false;
            AppSettings.Save();
        }

        private void BtnResetCircle_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(Loc.Get("dialog.reset_circle"), Loc.Get("dialog.reset_title"))) return;
            AppSettings.CircleMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            _isInitializing = true;
            LoadSettingsToUI();
            _isInitializing = false;
            AppSettings.Save();
        }

        private void BtnResetOutline_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(Loc.Get("dialog.reset_outline"), Loc.Get("dialog.reset_title"))) return;
            AppSettings.OutlineMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            _isInitializing = true;
            LoadSettingsToUI();
            _isInitializing = false;
            AppSettings.Save();
        }

        private void BtnResetSoundClassification_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(Loc.Get("dialog.reset_sound_types"), Loc.Get("dialog.reset_title"))) return;
            AppSettings.ShowAmbient = true;
            AppSettings.ShowSpeech = true;
            AppSettings.ShowDanger = true;
            AppSettings.ColorAmbient = "#FFFFFFFF";
            AppSettings.ColorSpeech = "#FFFFFF00";
            AppSettings.ColorDanger = "#FFFF0000";
            _isInitializing = true;
            LoadSettingsToUI();
            _isInitializing = false;
            AppSettings.Save();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (!Dialogs.Confirm(Loc.Get("dialog.reset_all"), Loc.Get("dialog.reset_all_title"))) return;

            _isInitializing = true;

            // 언어를 뺀 모든 설정을 기본값으로 되돌린다. 단축키도 실제로 쓰는 키 목록까지 되돌린다.
            AppSettings.ResetToDefaults();

            LoadSettingsToUI();
            AppSettings.SaveNow();

            _isInitializing = false;
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
            }
            this.Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // --- IMMNotificationClient 구현 ---
        private async void CheckAndApplyDeviceChannels()
        {
            await System.Threading.Tasks.Task.Delay(500); // 장치 초기화 안정화 딜레이

            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_deviceEnumerator != null)
                    {
                        var device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        int channels = device.AudioClient.MixFormat.Channels;
                        int newSoundMode = channels >= 8 ? 2 : (channels >= 6 ? 1 : 0);

                        if (AppSettings.SoundMode != newSoundMode)
                        {
                            AppSettings.SoundMode = newSoundMode;
                            AppSettings.Save();

                            bool wasInitializing = _isInitializing;
                            _isInitializing = true;

                            // 사운드 모드 칸의 선택을 맞춘다
                            ChipsSoundMode.SelectedIndex = newSoundMode;

                            _isInitializing = wasInitializing;
                        }
                    }
                }
                catch { }
            });
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow != DataFlow.Render || role != Role.Multimedia) return;
            CheckAndApplyDeviceChannels();
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string pwstrDeviceId) { }
        
        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            CheckAndApplyDeviceChannels();
        }
        
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            CheckAndApplyDeviceChannels();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Dialogs.Show(Loc.Format("dialog.link_error", ex.Message));
            }
        }

        private void BtnMoreInfoGithub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/amophi/SoundVisualizer",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Dialogs.Show(Loc.Format("dialog.link_error", ex.Message));
            }
        }
    }
}



