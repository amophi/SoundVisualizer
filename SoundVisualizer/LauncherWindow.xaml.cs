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

        public LauncherWindow()
        {
            InitializeComponent();
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
            if (isRunning)
            {
                TxtStatus.Text = Loc.Get("launcher.status.running");
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 130, 246)); // #3182F6
                StatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 130, 246));
            }
            else
            {
                TxtStatus.Text = Loc.Get("launcher.status.idle");
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 149, 161)); // #8B95A1
                StatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 149, 161));
            }
        }

        private void LoadSettingsToUI()
        {
            // 1. Wave Mode Load
            SldIntensityWave.Value = AppSettings.WaveMode.Intensity;
            ChkIntensityAsOpacityWave.IsChecked = AppSettings.WaveMode.IntensityAsOpacity;
            SldOpacityFixedSizeWave.Value = AppSettings.WaveMode.OpacityFixedSize;
            SldOpacityFixedMaxOpacityWave.Value = 100.0 - AppSettings.WaveMode.OpacityFixedMaxOpacity;
            
            bool waveOpacity = AppSettings.WaveMode.IntensityAsOpacity;
            SldOpacityFixedSizeWave.IsEnabled = waveOpacity;
            SldOpacityFixedSizeWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtOpacityFixedSizeWave.Opacity = waveOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeLabelWave != null) TxtOpacityFixedSizeLabelWave.Opacity = waveOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeDescWave != null) TxtOpacityFixedSizeDescWave.Opacity = waveOpacity ? 1.0 : 0.4;
            SldOpacityFixedMaxOpacityWave.IsEnabled = waveOpacity;
            SldOpacityFixedMaxOpacityWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityLabelWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityLabelWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityDescWave.Opacity = waveOpacity ? 1.0 : 0.4;
            
            SldIntensityWave.IsEnabled = !waveOpacity;
            SldIntensityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            TxtIntensityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            TxtIntensityLabelWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            TxtIntensityDescWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            
            SldOpacityWave.IsEnabled = !waveOpacity;
            SldOpacityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            TxtOpacityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            TxtOpacityLabelWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            TxtOpacityDescWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            SldSpeedWave.Value = AppSettings.WaveMode.PositionSpeed;
            SldSensitivityWave.Value = AppSettings.WaveMode.Sensitivity * 4.0;
            SldOpacityWave.Value = 100 - AppSettings.WaveMode.VisualOpacity;
            ChkGlowModeWave.IsChecked = AppSettings.WaveMode.IsGlowMode;
            SldGlowIntensityWave.Value = AppSettings.WaveMode.GlowIntensity;
            
            SldGlowIntensityWave.IsEnabled = AppSettings.WaveMode.IsGlowMode;
            SldGlowIntensityWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowIntensityWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeLabelWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeDescWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;

            // 2. Pad Mode Load
            SldIntensityPad.Value = AppSettings.PadMode.Intensity;
            ChkIntensityAsOpacityPad.IsChecked = AppSettings.PadMode.IntensityAsOpacity;
            SldOpacityFixedSizePad.Value = AppSettings.PadMode.OpacityFixedSize;
            SldOpacityFixedMaxOpacityPad.Value = 100.0 - AppSettings.PadMode.OpacityFixedMaxOpacity;
            
            bool padOpacity = AppSettings.PadMode.IntensityAsOpacity;
            SldOpacityFixedSizePad.IsEnabled = padOpacity;
            SldOpacityFixedSizePad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtOpacityFixedSizePad.Opacity = padOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeLabelPad != null) TxtOpacityFixedSizeLabelPad.Opacity = padOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeDescPad != null) TxtOpacityFixedSizeDescPad.Opacity = padOpacity ? 1.0 : 0.4;
            SldOpacityFixedMaxOpacityPad.IsEnabled = padOpacity;
            SldOpacityFixedMaxOpacityPad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityLabelPad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescPad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityPad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityLabelPad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityDescPad.Opacity = padOpacity ? 1.0 : 0.4;
            
            SldIntensityPad.IsEnabled = !padOpacity;
            SldIntensityPad.Opacity = !padOpacity ? 1.0 : 0.4;
            TxtIntensityPad.Opacity = !padOpacity ? 1.0 : 0.4;
            TxtIntensityLabelPad.Opacity = !padOpacity ? 1.0 : 0.4;
            TxtIntensityDescPad.Opacity = !padOpacity ? 1.0 : 0.4;
            
            SldOpacityPad.IsEnabled = !padOpacity;
            SldOpacityPad.Opacity = !padOpacity ? 1.0 : 0.4;
            TxtOpacityPad.Opacity = !padOpacity ? 1.0 : 0.4;
            TxtOpacityLabelPad.Opacity = !padOpacity ? 1.0 : 0.4;
            TxtOpacityDescPad.Opacity = !padOpacity ? 1.0 : 0.4;
            SldSpeedPad.Value = AppSettings.PadMode.PositionSpeed;
            SldSensitivityPad.Value = AppSettings.PadMode.Sensitivity * 4.0;
            SldOpacityPad.Value = 100 - AppSettings.PadMode.VisualOpacity;
            ChkGlowModePad.IsChecked = AppSettings.PadMode.IsGlowMode;
            SldGlowIntensityPad.Value = AppSettings.PadMode.GlowIntensity;
            
            SldGlowIntensityPad.IsEnabled = AppSettings.PadMode.IsGlowMode;
            SldGlowIntensityPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowIntensityPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeLabelPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeDescPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;

            // 3. Circle Mode Load
            SldIntensityCircle.Value = AppSettings.CircleMode.Intensity;
            ChkIntensityAsOpacityCircle.IsChecked = AppSettings.CircleMode.IntensityAsOpacity;
            SldOpacityFixedSizeCircle.Value = AppSettings.CircleMode.OpacityFixedSize;
            SldOpacityFixedMaxOpacityCircle.Value = 100.0 - AppSettings.CircleMode.OpacityFixedMaxOpacity;
            
            bool circleOpacity = AppSettings.CircleMode.IntensityAsOpacity;
            SldOpacityFixedSizeCircle.IsEnabled = circleOpacity;
            SldOpacityFixedSizeCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtOpacityFixedSizeCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeLabelCircle != null) TxtOpacityFixedSizeLabelCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeDescCircle != null) TxtOpacityFixedSizeDescCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            SldOpacityFixedMaxOpacityCircle.IsEnabled = circleOpacity;
            SldOpacityFixedMaxOpacityCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityLabelCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityLabelCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityDescCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            
            SldIntensityCircle.IsEnabled = !circleOpacity;
            SldIntensityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            TxtIntensityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            TxtIntensityLabelCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            TxtIntensityDescCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            
            SldOpacityCircle.IsEnabled = !circleOpacity;
            SldOpacityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            TxtOpacityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            TxtOpacityLabelCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            TxtOpacityDescCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            SldSpeedCircle.Value = AppSettings.CircleMode.PositionSpeed;
            SldSensitivityCircle.Value = AppSettings.CircleMode.Sensitivity * 4.0;
            SldOpacityCircle.Value = 100 - AppSettings.CircleMode.VisualOpacity;
            ChkGlowModeCircle.IsChecked = AppSettings.CircleMode.IsGlowMode;
            SldGlowIntensityCircle.Value = AppSettings.CircleMode.GlowIntensity;
            SldCircleRadius.Value = AppSettings.CircleMode.CircleRadius;
            
            SldGlowIntensityCircle.IsEnabled = AppSettings.CircleMode.IsGlowMode;
            SldGlowIntensityCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowIntensityCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeLabelCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeDescCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;

            // 3.5. Outline Mode Load
            SldIntensityOutline.Value = AppSettings.OutlineMode.Intensity;
            ChkIntensityAsOpacityOutline.IsChecked = AppSettings.OutlineMode.IntensityAsOpacity;
            SldOpacityFixedSizeOutline.Value = AppSettings.OutlineMode.OpacityFixedSize;
            SldOpacityFixedMaxOpacityOutline.Value = 100.0 - AppSettings.OutlineMode.OpacityFixedMaxOpacity;
            
            bool outlineOpacity = AppSettings.OutlineMode.IntensityAsOpacity;
            SldOpacityFixedSizeOutline.IsEnabled = outlineOpacity;
            SldOpacityFixedSizeOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtOpacityFixedSizeOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeLabelOutline != null) TxtOpacityFixedSizeLabelOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            if (TxtOpacityFixedSizeDescOutline != null) TxtOpacityFixedSizeDescOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            SldOpacityFixedMaxOpacityOutline.IsEnabled = outlineOpacity;
            SldOpacityFixedMaxOpacityOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityLabelOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityLabelOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtIntensityAsOpacityDescOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            
            SldIntensityOutline.IsEnabled = !outlineOpacity;
            SldIntensityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            TxtIntensityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            TxtIntensityLabelOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            TxtIntensityDescOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            
            SldOpacityOutline.IsEnabled = !outlineOpacity;
            SldOpacityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            TxtOpacityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            TxtOpacityLabelOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            TxtOpacityDescOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            SldSpeedOutline.Value = AppSettings.OutlineMode.PositionSpeed;
            SldSensitivityOutline.Value = AppSettings.OutlineMode.Sensitivity * 4.0;
            SldOpacityOutline.Value = 100 - AppSettings.OutlineMode.VisualOpacity;
            ChkGlowModeOutline.IsChecked = AppSettings.OutlineMode.IsGlowMode;
            SldGlowIntensityOutline.Value = AppSettings.OutlineMode.GlowIntensity;
            
            SldGlowIntensityOutline.IsEnabled = AppSettings.OutlineMode.IsGlowMode;
            SldGlowIntensityOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowIntensityOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeLabelOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeDescOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;

            // 4. Common settings
            CmbVisualMode.SelectedIndex = AppSettings.VisualMode;
            CmbSoundMode.SelectedIndex = AppSettings.SoundMode;
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

            // 5. Expander states update
            UpdateExpanderStates();
        }

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

            // Wave
            if (sender == SldIntensityWave) AppSettings.WaveMode.Intensity = SldIntensityWave.Value;
            else if (sender == ChkIntensityAsOpacityWave)
            {
                AppSettings.WaveMode.IntensityAsOpacity = ChkIntensityAsOpacityWave.IsChecked ?? false;
                bool waveOpacity = AppSettings.WaveMode.IntensityAsOpacity;
                SldOpacityFixedSizeWave.IsEnabled = waveOpacity;
                SldOpacityFixedSizeWave.Opacity = waveOpacity ? 1.0 : 0.4;
                TxtOpacityFixedSizeWave.Opacity = waveOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeLabelWave != null) TxtOpacityFixedSizeLabelWave.Opacity = waveOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeDescWave != null) TxtOpacityFixedSizeDescWave.Opacity = waveOpacity ? 1.0 : 0.4;
                SldOpacityFixedMaxOpacityWave.IsEnabled = waveOpacity;
                SldOpacityFixedMaxOpacityWave.Opacity = waveOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityLabelWave.Opacity = waveOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescWave.Opacity = waveOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityWave.Opacity = waveOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityLabelWave.Opacity = waveOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityDescWave.Opacity = waveOpacity ? 1.0 : 0.4;
                
                SldIntensityWave.IsEnabled = !waveOpacity;
                SldIntensityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                TxtIntensityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                TxtIntensityLabelWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                TxtIntensityDescWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                
                SldOpacityWave.IsEnabled = !waveOpacity;
                SldOpacityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                TxtOpacityWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                TxtOpacityLabelWave.Opacity = !waveOpacity ? 1.0 : 0.4;
                TxtOpacityDescWave.Opacity = !waveOpacity ? 1.0 : 0.4;
            }
            else if (sender == SldOpacityFixedSizeWave) AppSettings.WaveMode.OpacityFixedSize = SldOpacityFixedSizeWave.Value;
            else if (sender == SldOpacityFixedMaxOpacityWave) AppSettings.WaveMode.OpacityFixedMaxOpacity = 100.0 - SldOpacityFixedMaxOpacityWave.Value;
            else if (sender == SldSpeedWave) AppSettings.WaveMode.PositionSpeed = SldSpeedWave.Value;
            else if (sender == SldSensitivityWave) AppSettings.WaveMode.Sensitivity = SldSensitivityWave.Value / 4.0;
            else if (sender == SldOpacityWave) AppSettings.WaveMode.VisualOpacity = 100 - SldOpacityWave.Value;
            else if (sender == ChkGlowModeWave)
            {
                AppSettings.WaveMode.IsGlowMode = ChkGlowModeWave.IsChecked ?? false;
                SldGlowIntensityWave.IsEnabled = AppSettings.WaveMode.IsGlowMode;
                SldGlowIntensityWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowIntensityWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeLabelWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeDescWave.Opacity = AppSettings.WaveMode.IsGlowMode ? 1.0 : 0.4;
            }
            else if (sender == SldGlowIntensityWave) AppSettings.WaveMode.GlowIntensity = SldGlowIntensityWave.Value;

            // Pad
            else if (sender == SldIntensityPad) AppSettings.PadMode.Intensity = SldIntensityPad.Value;
            else if (sender == ChkIntensityAsOpacityPad)
            {
                AppSettings.PadMode.IntensityAsOpacity = ChkIntensityAsOpacityPad.IsChecked ?? false;
                bool padOpacity = AppSettings.PadMode.IntensityAsOpacity;
                SldOpacityFixedSizePad.IsEnabled = padOpacity;
                SldOpacityFixedSizePad.Opacity = padOpacity ? 1.0 : 0.4;
                TxtOpacityFixedSizePad.Opacity = padOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeLabelPad != null) TxtOpacityFixedSizeLabelPad.Opacity = padOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeDescPad != null) TxtOpacityFixedSizeDescPad.Opacity = padOpacity ? 1.0 : 0.4;
                SldOpacityFixedMaxOpacityPad.IsEnabled = padOpacity;
                SldOpacityFixedMaxOpacityPad.Opacity = padOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityLabelPad.Opacity = padOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescPad.Opacity = padOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityPad.Opacity = padOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityLabelPad.Opacity = padOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityDescPad.Opacity = padOpacity ? 1.0 : 0.4;
                
                SldIntensityPad.IsEnabled = !padOpacity;
                SldIntensityPad.Opacity = !padOpacity ? 1.0 : 0.4;
                TxtIntensityPad.Opacity = !padOpacity ? 1.0 : 0.4;
                TxtIntensityLabelPad.Opacity = !padOpacity ? 1.0 : 0.4;
                TxtIntensityDescPad.Opacity = !padOpacity ? 1.0 : 0.4;
                
                SldOpacityPad.IsEnabled = !padOpacity;
                SldOpacityPad.Opacity = !padOpacity ? 1.0 : 0.4;
                TxtOpacityPad.Opacity = !padOpacity ? 1.0 : 0.4;
                TxtOpacityLabelPad.Opacity = !padOpacity ? 1.0 : 0.4;
                TxtOpacityDescPad.Opacity = !padOpacity ? 1.0 : 0.4;
            }
            else if (sender == SldOpacityFixedSizePad) AppSettings.PadMode.OpacityFixedSize = SldOpacityFixedSizePad.Value;
            else if (sender == SldOpacityFixedMaxOpacityPad) AppSettings.PadMode.OpacityFixedMaxOpacity = 100.0 - SldOpacityFixedMaxOpacityPad.Value;
            else if (sender == SldSpeedPad) AppSettings.PadMode.PositionSpeed = SldSpeedPad.Value;
            else if (sender == SldSensitivityPad) AppSettings.PadMode.Sensitivity = SldSensitivityPad.Value / 4.0;
            else if (sender == SldOpacityPad) AppSettings.PadMode.VisualOpacity = 100 - SldOpacityPad.Value;
            else if (sender == ChkGlowModePad)
            {
                AppSettings.PadMode.IsGlowMode = ChkGlowModePad.IsChecked ?? false;
                SldGlowIntensityPad.IsEnabled = AppSettings.PadMode.IsGlowMode;
                SldGlowIntensityPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowIntensityPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeLabelPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeDescPad.Opacity = AppSettings.PadMode.IsGlowMode ? 1.0 : 0.4;
            }
            else if (sender == SldGlowIntensityPad) AppSettings.PadMode.GlowIntensity = SldGlowIntensityPad.Value;

            // Circle
            else if (sender == SldIntensityCircle) AppSettings.CircleMode.Intensity = SldIntensityCircle.Value;
            else if (sender == ChkIntensityAsOpacityCircle)
            {
                AppSettings.CircleMode.IntensityAsOpacity = ChkIntensityAsOpacityCircle.IsChecked ?? false;
                bool circleOpacity = AppSettings.CircleMode.IntensityAsOpacity;
                SldOpacityFixedSizeCircle.IsEnabled = circleOpacity;
                SldOpacityFixedSizeCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                TxtOpacityFixedSizeCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeLabelCircle != null) TxtOpacityFixedSizeLabelCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeDescCircle != null) TxtOpacityFixedSizeDescCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                SldOpacityFixedMaxOpacityCircle.IsEnabled = circleOpacity;
                SldOpacityFixedMaxOpacityCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityLabelCircle.Opacity = circleOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityLabelCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityDescCircle.Opacity = circleOpacity ? 1.0 : 0.4;
                
                SldIntensityCircle.IsEnabled = !circleOpacity;
                SldIntensityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                TxtIntensityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                TxtIntensityLabelCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                TxtIntensityDescCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                
                SldOpacityCircle.IsEnabled = !circleOpacity;
                SldOpacityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                TxtOpacityCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                TxtOpacityLabelCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
                TxtOpacityDescCircle.Opacity = !circleOpacity ? 1.0 : 0.4;
            }
            else if (sender == SldOpacityFixedSizeCircle) AppSettings.CircleMode.OpacityFixedSize = SldOpacityFixedSizeCircle.Value;
            else if (sender == SldOpacityFixedMaxOpacityCircle) AppSettings.CircleMode.OpacityFixedMaxOpacity = 100.0 - SldOpacityFixedMaxOpacityCircle.Value;
            else if (sender == SldSpeedCircle) AppSettings.CircleMode.PositionSpeed = SldSpeedCircle.Value;
            else if (sender == SldSensitivityCircle) AppSettings.CircleMode.Sensitivity = SldSensitivityCircle.Value / 4.0;
            else if (sender == SldOpacityCircle) AppSettings.CircleMode.VisualOpacity = 100 - SldOpacityCircle.Value;
            else if (sender == ChkGlowModeCircle)
            {
                AppSettings.CircleMode.IsGlowMode = ChkGlowModeCircle.IsChecked ?? false;
                SldGlowIntensityCircle.IsEnabled = AppSettings.CircleMode.IsGlowMode;
                SldGlowIntensityCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowIntensityCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeLabelCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeDescCircle.Opacity = AppSettings.CircleMode.IsGlowMode ? 1.0 : 0.4;
            }
            else if (sender == SldGlowIntensityCircle) AppSettings.CircleMode.GlowIntensity = SldGlowIntensityCircle.Value;
            else if (sender == SldCircleRadius) AppSettings.CircleMode.CircleRadius = SldCircleRadius.Value;

            // Outline
            else if (sender == SldIntensityOutline) AppSettings.OutlineMode.Intensity = SldIntensityOutline.Value;
            else if (sender == ChkIntensityAsOpacityOutline)
            {
                AppSettings.OutlineMode.IntensityAsOpacity = ChkIntensityAsOpacityOutline.IsChecked ?? false;
                bool outlineOpacity = AppSettings.OutlineMode.IntensityAsOpacity;
                SldOpacityFixedSizeOutline.IsEnabled = outlineOpacity;
                SldOpacityFixedSizeOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                TxtOpacityFixedSizeOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeLabelOutline != null) TxtOpacityFixedSizeLabelOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                if (TxtOpacityFixedSizeDescOutline != null) TxtOpacityFixedSizeDescOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                SldOpacityFixedMaxOpacityOutline.IsEnabled = outlineOpacity;
                SldOpacityFixedMaxOpacityOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityLabelOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
            TxtOpacityFixedMaxOpacityDescOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                TxtOpacityFixedMaxOpacityOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityLabelOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                TxtIntensityAsOpacityDescOutline.Opacity = outlineOpacity ? 1.0 : 0.4;
                
                SldIntensityOutline.IsEnabled = !outlineOpacity;
                SldIntensityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                TxtIntensityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                TxtIntensityLabelOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                TxtIntensityDescOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                
                SldOpacityOutline.IsEnabled = !outlineOpacity;
                SldOpacityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                TxtOpacityOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                TxtOpacityLabelOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
                TxtOpacityDescOutline.Opacity = !outlineOpacity ? 1.0 : 0.4;
            }
            else if (sender == SldOpacityFixedSizeOutline) AppSettings.OutlineMode.OpacityFixedSize = SldOpacityFixedSizeOutline.Value;
            else if (sender == SldOpacityFixedMaxOpacityOutline) AppSettings.OutlineMode.OpacityFixedMaxOpacity = 100.0 - SldOpacityFixedMaxOpacityOutline.Value;
            else if (sender == SldSpeedOutline) AppSettings.OutlineMode.PositionSpeed = SldSpeedOutline.Value;
            else if (sender == SldSensitivityOutline) AppSettings.OutlineMode.Sensitivity = SldSensitivityOutline.Value / 4.0;
            else if (sender == SldOpacityOutline) AppSettings.OutlineMode.VisualOpacity = 100 - SldOpacityOutline.Value;
            else if (sender == ChkGlowModeOutline)
            {
                AppSettings.OutlineMode.IsGlowMode = ChkGlowModeOutline.IsChecked ?? false;
                SldGlowIntensityOutline.IsEnabled = AppSettings.OutlineMode.IsGlowMode;
                SldGlowIntensityOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowIntensityOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeLabelOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeDescOutline.Opacity = AppSettings.OutlineMode.IsGlowMode ? 1.0 : 0.4;
            }
            else if (sender == SldGlowIntensityOutline) AppSettings.OutlineMode.GlowIntensity = SldGlowIntensityOutline.Value;

            // Common
            else if (sender == CmbVisualMode)
            {
                AppSettings.VisualMode = CmbVisualMode.SelectedIndex;
                UpdateExpanderStates();
            }
            else if (sender == CmbSoundMode) AppSettings.SoundMode = CmbSoundMode.SelectedIndex;
            else if (sender == SldTargetFps) AppSettings.TargetFps = SldTargetFps.Value;
            else if (sender == ChkAdminMode) AppSettings.IsAdminMode = ChkAdminMode.IsChecked ?? false;
            else if (sender == ChkAutoMinimizeOnLaunch) AppSettings.AutoMinimizeOnLaunch = ChkAutoMinimizeOnLaunch.IsChecked ?? false;
            // Removed old hotkey combo logic
            else if (sender == ChkShowAmbient) AppSettings.ShowAmbient = ChkShowAmbient.IsChecked ?? true;
            else if (sender == ChkShowSpeech) AppSettings.ShowSpeech = ChkShowSpeech.IsChecked ?? true;
            else if (sender == ChkShowDanger) AppSettings.ShowDanger = ChkShowDanger.IsChecked ?? true;

            AppSettings.Save();
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

                            // 콤보박스 선택 변경 반영
                            CmbSoundMode.SelectedIndex = newSoundMode;

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



