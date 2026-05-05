using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SoundVisualizer
{
    public partial class LauncherWindow : Window
    {
        private bool _isInitializing = true;
        private MainWindow? _overlayWindow = null;

        private readonly Dictionary<string, int> _hotkeys = new Dictionary<string, int>
        {
            {"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73},
            {"F5", 0x74}, {"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77},
            {"F9", 0x78}, {"F10", 0x79}, {"F11", 0x7A}, {"F12", 0x7B}
        };

        public LauncherWindow()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            foreach (var key in _hotkeys.Keys)
            {
                CmbVisualHotkey.Items.Add(key);
                CmbStereoHotkey.Items.Add(key);
            }

            if (AppSettings.Language == "ENG")
                CmbLanguage.SelectedIndex = 1;
            else
                CmbLanguage.SelectedIndex = 0;

            LoadSettingsToUI();
            _isInitializing = false;
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbLanguage.SelectedIndex == 0)
                SetLanguage("KOR");
            else
                SetLanguage("ENG");
        }

        private void SetLanguage(string lang)
        {
            AppSettings.Language = lang;
            if (lang == "KOR")
            {
                TabHome.Header = "홈";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "보이지 않던 소리를 화면에 그려냅니다.\n게이밍부터 음악 감상까지 새로운 경험을 시작하세요.";
                if (_overlayWindow == null) BtnLaunch.Content = "시작하기";
                TabSettings.Header = "설정";
                TxtScreenSettings.Text = "화면 설정";
                TxtIntensityLabel.Text = "크기";
                TxtIntensityDesc.Text = "그래픽이 화면을 덮는 전체적인 크기와 길이를 조절합니다.";
                TxtSpeedLabel.Text = "속도";
                TxtSpeedDesc.Text = "파동이 화면의 끝을 향해 퍼져나가는 속도를 조절합니다.";
                TxtSensitivityLabel.Text = "민감도";
                TxtSensitivityDesc.Text = "작은 소리에도 그래픽이 얼마나 민감하게 반응할지 결정합니다.";
                TxtAdvSensitivityLabel.Text = "민감도 (고급)";
                TxtAdvSensitivityDesc.Text = "극한의 반응성을 위해 내부 수치 제한을 해제합니다.";
                TxtOpacityLabel.Text = "투명도";
                TxtOpacityDesc.Text = "그래픽의 투명도를 조절하여 배경의 비침 정도를 결정합니다.";
                TxtModeSettings.Text = "모드 설정";
                TxtVisualModeLabel.Text = "표현 모드";
                TxtVisualModeDesc.Text = "화면에 그려질 그래픽의 기본 형태를 선택합니다.";
                CmbVisualModeWave.Content = "Wave (물결)";
                CmbVisualModePad.Content = "Pad (점)";
                TxtStereoModeLabel.Text = "스테레오 모드";
                TxtStereoModeDesc.Text = "2채널을 기반으로 좌, 우측 소리만을 표현합니다";
                ChkStereoUpmix.Content = "켜기";
                TxtHotkeySettings.Text = "단축키";
                TxtVisualHotkeyLabel.Text = "표현 모드 전환";
                TxtVisualHotkeyDesc.Text = "실행 중 형태를 실시간으로 변경할 단축키입니다.";
                TxtStereoHotkeyLabel.Text = "스테레오 모드 전환";
                TxtStereoHotkeyDesc.Text = "실행 중 모드를 실시간으로 변경할 단축키입니다.";
                BtnReset.Content = "기본값으로 되돌리기";
                TabHelp.Header = "도움말";
                TxtHelp1Title.Text = "7.1 서라운드 환경";
                TxtHelp1Desc.Text = "정상적인 방향성(레이더) 작동을 위해서는 윈도우 소리 설정에서 출력 장치가 '7.1 서라운드' (8채널)로 구성되어 있어야 합니다. 일반 스테레오(2채널) 환경인 경우 시각화 그래픽이 좌/우에만 나타날 수 있으며, 이를 보완하려면 설정 탭에서 '스테레오 모드' 기능을 켜주세요.";
                TxtHelp2Title.Text = "실시간 단축키 제어";
                TxtHelp2Desc.Text = "오버레이가 화면에 떠 있는 상태에서도, 백그라운드에서 지정된 단축키(기본 F2, F3)를 누르면 실시간으로 형태와 모드가 즉시 전환됩니다.";
                TxtHelp3Title.Text = "오버레이 종료 방법";
                TxtHelp3Desc.Text = "실행된 그래픽은 마우스 클릭을 방해하지 않도록 뒤로 투과됩니다. 종료하시려면 윈도우 작업 표시줄의 아이콘을 우클릭하여 '창 닫기'를 누르시거나, 이 런처 창 상단의 ✕ 버튼을 클릭하시면 함께 종료됩니다.";
            }
            else
            {
                TabHome.Header = "Home";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "Visualize the unseen sounds.\nStart a new experience from gaming to movies.";
                if (_overlayWindow == null) BtnLaunch.Content = "Start";
                TabSettings.Header = "Settings";
                TxtScreenSettings.Text = "Screen Settings";
                TxtIntensityLabel.Text = "Intensity";
                TxtIntensityDesc.Text = "Adjusts the overall size and length of the graphics on the screen.";
                TxtSpeedLabel.Text = "Speed";
                TxtSpeedDesc.Text = "Adjusts the speed at which the waves spread across the screen.";
                TxtSensitivityLabel.Text = "Sensitivity";
                TxtSensitivityDesc.Text = "Determines how sensitively the graphics react to soft sounds.";
                TxtAdvSensitivityLabel.Text = "Sensitivity (Adv)";
                TxtAdvSensitivityDesc.Text = "Removes internal limits for extreme responsiveness.";
                TxtOpacityLabel.Text = "Opacity";
                TxtOpacityDesc.Text = "Adjusts the graphic transparency to reveal the background behind it.";
                TxtModeSettings.Text = "Mode Settings";
                TxtVisualModeLabel.Text = "Visual Mode";
                TxtVisualModeDesc.Text = "Selects the basic shape of the graphics drawn on the screen.";
                CmbVisualModeWave.Content = "Wave";
                CmbVisualModePad.Content = "Pad (Dots)";
                TxtStereoModeLabel.Text = "Stereo Mode";
                TxtStereoModeDesc.Text = "Uses 2 channels to only represent left and right sounds.";
                ChkStereoUpmix.Content = "Enable";
                TxtHotkeySettings.Text = "Hotkeys";
                TxtVisualHotkeyLabel.Text = "Visual Mode Toggle";
                TxtVisualHotkeyDesc.Text = "Hotkey to change the visual shape in real-time.";
                TxtStereoHotkeyLabel.Text = "Stereo Mode Toggle";
                TxtStereoHotkeyDesc.Text = "Hotkey to toggle stereo mode in real-time.";
                BtnReset.Content = "Reset to Defaults";
                TabHelp.Header = "Help";
                TxtHelp1Title.Text = "7.1 Surround Environment";
                TxtHelp1Desc.Text = "For proper directional (radar) operation, your Windows sound output device must be configured as '7.1 Surround' (8 channels). In a standard stereo (2-channel) environment, the visualization may only appear on the left/right. To compensate for this, enable 'Stereo Mode' in the settings.";
                TxtHelp2Title.Text = "Real-time Hotkeys";
                TxtHelp2Desc.Text = "Even while the overlay is on screen, you can press the designated hotkeys (Default F2, F3) in the background to switch shapes and modes in real-time.";
                TxtHelp3Title.Text = "How to Close Overlay";
                TxtHelp3Desc.Text = "The running graphics allow mouse clicks to pass through. To close it, right-click the icon on the Windows taskbar and select 'Close window', or click the ✕ button at the top of this launcher window.";
            }
        }

        private void LoadSettingsToUI()
        {
            SldIntensity.Value = AppSettings.WaveIntensity;
            SldSpeed.Value = AppSettings.WavePositionSpeed;
            
            ChkAdvancedMode.IsChecked = AppSettings.IsAdvancedSensitivity;
            if (AppSettings.IsAdvancedSensitivity)
            {
                SldAdvSensitivity.Value = AppSettings.WaveSensitivity;
                SldSensitivity.Value = Math.Min(100.0, AppSettings.WaveSensitivity * 4.0);
                
                PanelNormalSensitivity.IsEnabled = false;
                PanelNormalSensitivity.Opacity = 0.4;

                SldAdvSensitivity.IsEnabled = true;
                SldAdvSensitivity.Opacity = 1.0;
                TxtAdvSensitivityLabel.Opacity = 1.0;
                TxtAdvSensitivityDesc.Opacity = 1.0;
                TxtAdvSensitivity.Opacity = 1.0;
            }
            else
            {
                SldSensitivity.Value = AppSettings.WaveSensitivity * 4.0; // 0~25 범위를 0~100으로 확장
                SldAdvSensitivity.Value = AppSettings.WaveSensitivity;
                
                PanelNormalSensitivity.IsEnabled = true;
                PanelNormalSensitivity.Opacity = 1.0;

                SldAdvSensitivity.IsEnabled = false;
                SldAdvSensitivity.Opacity = 0.4;
                TxtAdvSensitivityLabel.Opacity = 0.4;
                TxtAdvSensitivityDesc.Opacity = 0.4;
                TxtAdvSensitivity.Opacity = 0.4;
            }

            SldOpacity.Value = 100 - AppSettings.VisualOpacity; // 투명도 수치 반전 (100=완전 투명)
            CmbVisualMode.SelectedIndex = AppSettings.VisualMode;
            ChkStereoUpmix.IsChecked = AppSettings.IsStereoUpmixMode;

            CmbVisualHotkey.SelectedItem = GetKeyName(AppSettings.VisualModeHotkey) ?? "F3";
            CmbStereoHotkey.SelectedItem = GetKeyName(AppSettings.StereoUpmixHotkey) ?? "F2";
        }

        private string? GetKeyName(int code)
        {
            foreach (var pair in _hotkeys)
            {
                if (pair.Value == code) return pair.Key;
            }
            return null;
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            AppSettings.WaveIntensity = SldIntensity.Value;
            AppSettings.WavePositionSpeed = SldSpeed.Value;
            
            if (AppSettings.IsAdvancedSensitivity)
                AppSettings.WaveSensitivity = SldAdvSensitivity.Value;
            else
                AppSettings.WaveSensitivity = SldSensitivity.Value / 4.0; // 0~100 화면 수치를 0~25로 축소

            AppSettings.VisualOpacity = 100 - SldOpacity.Value; // 투명도 수치 반전 적용
            AppSettings.VisualMode = CmbVisualMode.SelectedIndex;
            AppSettings.IsStereoUpmixMode = ChkStereoUpmix.IsChecked ?? false;

            if (CmbVisualHotkey.SelectedItem is string vKey && _hotkeys.TryGetValue(vKey, out int vCode))
            {
                AppSettings.VisualModeHotkey = vCode;
            }

            if (CmbStereoHotkey.SelectedItem is string sKey && _hotkeys.TryGetValue(sKey, out int sCode))
            {
                AppSettings.StereoUpmixHotkey = sCode;
            }
        }

        private void ChkAdvancedMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            
            bool isAdvanced = ChkAdvancedMode.IsChecked ?? false;
            AppSettings.IsAdvancedSensitivity = isAdvanced;
            
            _isInitializing = true;
            if (!isAdvanced && AppSettings.WaveSensitivity > 25.0)
            {
                // 고급 모드에서 일반 모드로 내려올 때, 값이 25를 초과하면 25로 제한
                AppSettings.WaveSensitivity = 25.0;
            }
            
            LoadSettingsToUI();
            _isInitializing = false;
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow != null)
            {
                MessageBox.Show("오버레이가 이미 실행 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _overlayWindow = new MainWindow();
            _overlayWindow.Closed += (s, args) => 
            {
                _overlayWindow = null;
                BtnLaunch.Content = AppSettings.Language == "KOR" ? "시작하기" : "Start";
                BtnLaunch.IsEnabled = true;
                
                // 오버레이 동작 중 핫키로 변경된 사항(모드 등)을 다시 UI에 반영
                LoadSettingsToUI();
            };
            _overlayWindow.Show();
            
            BtnLaunch.Content = AppSettings.Language == "KOR" ? "실행 중..." : "Running...";
            BtnLaunch.IsEnabled = false;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;

            AppSettings.WaveIntensity = 33.3;
            AppSettings.WavePositionSpeed = 10.0;
            AppSettings.WaveSensitivity = 10.0;
            AppSettings.VisualOpacity = 60.0;
            AppSettings.VisualMode = 0;
            AppSettings.IsStereoUpmixMode = false;
            AppSettings.IsAdvancedSensitivity = false;
            AppSettings.VisualModeHotkey = 0x72; // F3
            AppSettings.StereoUpmixHotkey = 0x71; // F2

            LoadSettingsToUI();

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
    }
}
