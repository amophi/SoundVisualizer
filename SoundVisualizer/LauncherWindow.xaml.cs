using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

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
            
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _deviceEnumerator.RegisterEndpointNotificationCallback(this);
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
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
            // ComboBox initialization removed

            if (AppSettings.Language == "English" || AppSettings.Language == "ENG")
                CmbLanguage.SelectedIndex = 1;
            else if (AppSettings.Language == "日本語")
                CmbLanguage.SelectedIndex = 2;
            else if (AppSettings.Language == "中文")
                CmbLanguage.SelectedIndex = 3;
            else if (AppSettings.Language == "Español")
                CmbLanguage.SelectedIndex = 4;
            else if (AppSettings.Language == "Français")
                CmbLanguage.SelectedIndex = 5;
            else if (AppSettings.Language == "Deutsch")
                CmbLanguage.SelectedIndex = 6;
            else if (AppSettings.Language == "Русский")
                CmbLanguage.SelectedIndex = 7;
            else
                CmbLanguage.SelectedIndex = 0; // KOR

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

            // Ensure language-specific texts are applied on startup
            if (CmbLanguage.SelectedItem is ComboBoxItem initialItem)
            {
                SetLanguage(initialItem.Content.ToString());
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CmbLanguage.SelectedItem is ComboBoxItem item)
            {
                SetLanguage(item.Content.ToString());
            }
        }

        private void SetLanguage(string lang)
        {
            if (lang == "한국어") lang = "KOR";
            AppSettings.Language = lang;

            if (lang == "KOR")
            {
                TabHome.Header = "홈";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "보이지 않던 소리를 화면에 그려냅니다.\n게이밍부터 영화 감상까지 새로운 경험을 시작하세요.";
                if (BtnLaunch != null) BtnLaunch.Content = "실행";
                if (BtnStop != null) BtnStop.Content = "실행 종료";
                TabSettings.Header = "설정";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "파도 모드 설정";
                TxtIntensityLabelWave.Text = "크기";
                TxtIntensityDescWave.Text = "파도의 위아래 높이와 전체적인 볼륨감을 조절합니다.";
                TxtSpeedLabelWave.Text = "속도";
                TxtSpeedDescWave.Text = "파도가 흘러가며 일렁이는 속도를 조절합니다.";
                TxtSensitivityLabelWave.Text = "민감도";
                TxtSensitivityDescWave.Text = "작은 데시벨 소리에도 파도가 얼마나 민감하게 반응하여 출렁일지 조절합니다.";
                TxtOpacityLabelWave.Text = "투명도";
                TxtOpacityDescWave.Text = "파도의 투명도를 조절하여 오버레이 뒤의 게임이나 화면 비침 정도를 결정합니다.";
                TxtGlowModeLabelWave.Text = "광원";
                TxtGlowModeDescWave.Text = "파도 외곽선에 은은하게 빛나는 네온 광원을 입히고 그 강도를 조절합니다.";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "패드 모드 설정";
                TxtIntensityLabelPad.Text = "크기";
                TxtIntensityDescPad.Text = "사운드 반응 시 패드의 두께와 가로 영역 넓이를 조절합니다.";
                TxtSpeedLabelPad.Text = "속도";
                TxtSpeedDescPad.Text = "패드가 특정 방향의 사운드로 회전하며 반응하는 반응 속도를 조절합니다.";
                TxtSensitivityLabelPad.Text = "민감도";
                TxtSensitivityDescPad.Text = "패드가 소리를 더 잘 포착하여 더 큰 폭으로 도드라지게 조절합니다.";
                TxtOpacityLabelPad.Text = "투명도";
                TxtOpacityDescPad.Text = "패드 그래픽의 불투명도를 조정합니다.";
                TxtGlowModeLabelPad.Text = "광원";
                TxtGlowModeDescPad.Text = "패드 주변에 부드러운 아우라 형식의 광원 효과를 부여합니다.";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "원형 모드 설정";
                TxtIntensityLabelCircle.Text = "크기";
                TxtIntensityDescCircle.Text = "사운드가 울릴 때 원형 이퀄라이저의 돌출 진폭 스케일을 조절합니다.";
                TxtSpeedLabelCircle.Text = "속도";
                TxtSpeedDescCircle.Text = "원형 이퀄라이저의 물결이나 회전 반응 속도를 조절합니다.";
                TxtSensitivityLabelCircle.Text = "민감도";
                TxtSensitivityDescCircle.Text = "원형 진폭이 소리에 얼마나 섬세하게 쪼개지며 반응할지 조절합니다.";
                TxtOpacityLabelCircle.Text = "투명도";
                TxtOpacityDescCircle.Text = "원형 그래픽의 불투명도를 결정합니다.";
                TxtCircleRadiusLabel.Text = "원 크기";
                TxtCircleRadiusDesc.Text = "원형 모드에서 가운데 중심부의 지름(반지름) 크기를 개별 조절합니다.";
                TxtGlowModeLabelCircle.Text = "광원";
                TxtGlowModeDescCircle.Text = "원형 이퀄라이저의 테두리를 따라 반짝이는 글로우 후광을 만듭니다.";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "외곽선 모드 설정";
                TxtIntensityLabelOutline.Text = "크기";
                TxtIntensityDescOutline.Text = "외곽선 파도의 위아래 높이와 전체적인 볼륨감을 조절합니다.";
                TxtSpeedLabelOutline.Text = "속도";
                TxtSpeedDescOutline.Text = "외곽선 파도가 흘러가며 일렁이는 속도를 조절합니다.";
                TxtSensitivityLabelOutline.Text = "민감도";
                TxtSensitivityDescOutline.Text = "작은 데시벨 소리에도 외곽선 파도가 얼마나 민감하게 반응하여 출렁일지 조절합니다.";
                TxtOpacityLabelOutline.Text = "투명도";
                TxtOpacityDescOutline.Text = "외곽선 파도의 투명도를 조절합니다.";
                TxtGlowModeLabelOutline.Text = "광원";
                TxtGlowModeDescOutline.Text = "외곽선에 은은하게 빛나는 네온 광원을 입히고 그 강도를 조절합니다.";
                TxtModeSettings.Text = "모드 설정";
                TxtVisualModeLabel.Text = "표현 모드";
                TxtVisualModeDesc.Text = "화면에 그려질 그래픽의 형태를 선택합니다.";
                CmbVisualModeWave.Content = "파도";
                CmbVisualModePad.Content = "패드";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "원형";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "외곽선";
                TxtSoundModeLabel.Text = "사운드 모드";
                TxtSoundModeDesc.Text = "스피커 환경에 맞는 사운드 채널을 선택합니다.\n2채널 환경은 '2 채널', 서라운드 환경은 '5.1 채널' 또는 '7.1 채널'을 선택하세요.";
                TxtHotkeySettings.Text = "단축키";
                TxtVisualHotkeyLabel.Text = "표현 모드 전환";
                TxtVisualHotkeyDesc.Text = "실행 중 형태를 실시간으로 변경할 단축키입니다.";
                TxtSoundModeHotkeyLabel.Text = "사운드 모드 전환";
                TxtSoundModeHotkeyDesc.Text = "실행 중 사운드 모드를 실시간으로 변경할 단축키입니다.";
                TxtEditHotkeyLabel.Text = "오버레이 전환";
                TxtEditHotkeyDesc.Text = "오버레이 편집 화면을 표시하거나 닫는 단축키입니다.";
                TxtAdminSettings.Text = "고급 설정";
                TxtAdminModeLabel.Text = "개발자 모드";
                TxtAdminModeDesc.Text = "디버그용 정보 및 오디오 엔진 상태를 화면에 표시합니다.";
                ChkAdminMode.Content = "켜기";
                BtnReset.Content = "기본값으로 되돌리기";
                TabHelp.Header = "도움말";
                TxtHelp1Title.Text = "사운드 모드";
                TxtHelp1Desc.Text = "정상적인 방향성(레이더) 작동을 위해서는 윈도우 소리 설정에서 출력 장치가 '5.1 서라운드' (6채널) 또는 '7.1 서라운드' (8채널)로 구성되어 있어야 합니다. 일반 스테레오(2채널) 환경인 경우 시각화 그래픽이 좌/우에만 나타날 수 있으며, 이를 보완하려면 설정 탭에서 '사운드 모드'를 알맞게 설정해 주세요.";
                TxtHelp2Title.Text = "실시간 커스텀 단축키 제어";
                TxtHelp2Desc.Text = "설정 탭에서 핫키 버튼을 클릭한 뒤, 단일 키 또는 복합 키 조합(예: Ctrl + Shift + A)을 눌러 자유롭게 단축키를 변경할 수 있습니다. 백그라운드에서 지정된 단축키를 누르면 실시간으로 모드가 즉시 전환됩니다.";
                TxtHelp3Title.Text = "오버레이 실시간 편집 모드";
                TxtHelp3Desc.Text = "지정된 단축키(기본 F4)를 누르면 오버레이 편집 모드가 활성화됩니다. 화면에서 직접 마우스를 드래그하여 그래픽의 크기 한계선을 조절할 수 있으며, 팝업된 설정 패널에서 세부 조작이 가능합니다.";
                TxtHelp4Title.Text = "AI 소리 분석 및 색상";
                TxtHelp4Desc.Text = "AI가 실시간으로 소리의 종류를 분석하여 화면에 라벨과 색상으로 표시합니다. 설정에서 각 소리 종류(환경음, 말소리, 강조음)별로 고유한 색상을 지정하여 직관적으로 구분할 수 있습니다.";
                TxtHelp5Title.Text = "개발자 모드";
                TxtHelp5Desc.Text = "개발자 모드를 활성화하면 현재 감지되는 상세 AI 라벨, 오디오 엔진의 실시간 채널 상태, FPS 등 기술적인 정보를 오버레이 화면에 추가로 표시합니다. 시스템의 정상 작동 여부를 확인하고 싶을 때 유용합니다.";
                
                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "소리 분류 표시";
                    ChkShowAmbient.Content = "환경음 표시";
                    ChkShowSpeech.Content = "말소리 표시";
                    ChkShowDanger.Content = "강조음 표시";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "English")
            {
                TabHome.Header = "Home";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "Visualize the unseen sounds.\nStart a new experience from gaming to movies.";
                if (BtnLaunch != null) BtnLaunch.Content = "Start";
                if (BtnStop != null) BtnStop.Content = "Stop";
                TabSettings.Header = "Settings";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "Wave Mode Settings";
                TxtIntensityLabelWave.Text = "Intensity";
                TxtIntensityDescWave.Text = "Adjusts the vertical height and overall volume of the waves.";
                TxtSpeedLabelWave.Text = "Speed";
                TxtSpeedDescWave.Text = "Controls the speed at which the wave flows and ripples.";
                TxtSensitivityLabelWave.Text = "Sensitivity";
                TxtSensitivityDescWave.Text = "Determines how sensitively the waves react to low decibel sounds.";
                TxtOpacityLabelWave.Text = "Opacity";
                TxtOpacityDescWave.Text = "Adjusts wave opacity to determine transparency over background games or screens.";
                TxtIntensityAsOpacityLabelWave.Text = "Intensity as Opacity";
                TxtIntensityAsOpacityDescWave.Text = "Graphic size is fixed and opacity changes based on sound.";
                if (TxtOpacityFixedMaxOpacityLabelWave != null) TxtOpacityFixedMaxOpacityLabelWave.Text = "Max Opacity";
                TxtGlowModeLabelWave.Text = "Glow";
                TxtGlowModeDescWave.Text = "Applies a soft neon glow to the wave outline and adjusts its intensity.";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "Pad Mode Settings";
                TxtIntensityLabelPad.Text = "Size";
                TxtIntensityDescPad.Text = "Adjusts the pad thickness and horizontal width when responding to sound.";
                TxtSpeedLabelPad.Text = "Speed";
                TxtSpeedDescPad.Text = "Controls the speed at which the pad rotates in response to directional sounds.";
                TxtSensitivityLabelPad.Text = "Sensitivity";
                TxtSensitivityDescPad.Text = "Adjusts the pad to capture sounds better and stand out more.";
                TxtOpacityLabelPad.Text = "Opacity";
                TxtOpacityDescPad.Text = "Adjusts the opacity of the pad graphic.";
                TxtIntensityAsOpacityLabelPad.Text = "Intensity as Opacity";
                TxtIntensityAsOpacityDescPad.Text = "Graphic size is fixed and opacity changes based on sound.";
                if (TxtOpacityFixedMaxOpacityLabelPad != null) TxtOpacityFixedMaxOpacityLabelPad.Text = "Max Opacity";
                TxtGlowModeLabelPad.Text = "Glow";
                TxtGlowModeDescPad.Text = "Gives a soft aura-style glow effect around the pad.";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "Circle Mode Settings";
                TxtIntensityLabelCircle.Text = "Intensity";
                TxtIntensityDescCircle.Text = "Adjusts the protrusion amplitude scale of the circle equalizer.";
                TxtSpeedLabelCircle.Text = "Speed";
                TxtSpeedDescCircle.Text = "Adjusts the wave or rotation response speed of the circle equalizer.";
                TxtSensitivityLabelCircle.Text = "Sensitivity";
                TxtSensitivityDescCircle.Text = "Controls how delicately the circle amplitude splits and responds to sound.";
                TxtOpacityLabelCircle.Text = "Opacity";
                TxtOpacityDescCircle.Text = "Determines the opacity of the circle graphic.";
                TxtIntensityAsOpacityLabelCircle.Text = "Intensity as Opacity";
                TxtIntensityAsOpacityDescCircle.Text = "Graphic size is fixed and opacity changes based on sound.";
                if (TxtOpacityFixedMaxOpacityLabelCircle != null) TxtOpacityFixedMaxOpacityLabelCircle.Text = "Max Opacity";
                TxtCircleRadiusLabel.Text = "Circle Radius";
                TxtCircleRadiusDesc.Text = "Individually adjusts the radius of the center core in circle mode.";
                TxtGlowModeLabelCircle.Text = "Glow";
                TxtGlowModeDescCircle.Text = "Creates a shimmering glow aura along the border of the circle equalizer.";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "Outline Mode Settings";
                TxtIntensityLabelOutline.Text = "Intensity";
                TxtIntensityDescOutline.Text = "Adjusts the vertical height and overall volume of the outline waves.";
                TxtSpeedLabelOutline.Text = "Speed";
                TxtSpeedDescOutline.Text = "Controls the speed at which the outline wave flows and ripples.";
                TxtSensitivityLabelOutline.Text = "Sensitivity";
                TxtSensitivityDescOutline.Text = "Determines how sensitively the outline waves react to low decibel sounds.";
                TxtOpacityLabelOutline.Text = "Opacity";
                TxtOpacityDescOutline.Text = "Adjusts the opacity of the outline waves.";
                TxtIntensityAsOpacityLabelOutline.Text = "Intensity as Opacity";
                TxtIntensityAsOpacityDescOutline.Text = "Graphic size is fixed and opacity changes based on sound.";
                if (TxtOpacityFixedMaxOpacityLabelOutline != null) TxtOpacityFixedMaxOpacityLabelOutline.Text = "Max Opacity";
                TxtGlowModeLabelOutline.Text = "Glow";
                TxtGlowModeDescOutline.Text = "Applies a soft neon glow to the wave outline and adjusts its intensity.";
                TxtModeSettings.Text = "Mode Settings";
                TxtVisualModeLabel.Text = "Visual Mode";
                TxtVisualModeDesc.Text = "Selects the shape of the graphics drawn on the screen.";
                CmbVisualModeWave.Content = "Wave";
                CmbVisualModePad.Content = "Pad";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Circle";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "Outline";
                TxtSoundModeLabel.Text = "Sound Mode";
                TxtSoundModeDesc.Text = "Select the sound channel suitable for your speaker environment.\nFor 2-channel environments, select '2 Channels', and for surround environments, select '5.1 Channels' or '7.1 Channels'.";
                TxtHotkeySettings.Text = "Hotkeys";
                TxtVisualHotkeyLabel.Text = "Visual Mode Toggle";
                TxtVisualHotkeyDesc.Text = "Hotkey to change the visual shape in real-time.";
                TxtSoundModeHotkeyLabel.Text = "Sound Mode Toggle";
                TxtSoundModeHotkeyDesc.Text = "Hotkey to toggle sound mode in real-time.";
                TxtEditHotkeyLabel.Text = "Toggle Overlay Edit";
                TxtEditHotkeyDesc.Text = "Hotkey to open or close the overlay editor.";
                TxtAdminSettings.Text = "Advanced Settings";
                TxtAdminModeLabel.Text = "Developer Mode";
                TxtAdminModeDesc.Text = "Displays debug information and audio engine status on screen.";
                ChkAdminMode.Content = "Enable";
                BtnReset.Content = "Reset to Defaults";
                TabHelp.Header = "Help";
                TxtHelp1Title.Text = "Sound Mode";
                TxtHelp1Desc.Text = "For proper directional (radar) operation, your Windows sound output device must be configured as '5.1 Surround' (6 channels) or '7.1 Surround' (8 channels). In a standard stereo (2-channel) environment, the visualization may only appear on the left/right. To compensate for this, set the 'Sound Mode' appropriately in the settings.";
                TxtHelp2Title.Text = "Real-time Custom Hotkeys";
                TxtHelp2Desc.Text = "You can freely customize hotkeys by clicking the button in the settings and pressing a single key or combination (e.g., Ctrl + Shift + A). Pressing these hotkeys in the background will switch modes instantly.";
                TxtHelp3Title.Text = "Overlay Real-time Edit Mode";
                TxtHelp3Desc.Text = "Press the assigned hotkey (Default F4) to activate the overlay edit mode. You can drag your mouse on the screen to adjust the size limit of the graphics and use the pop-up panel for detailed settings.";
                TxtHelp4Title.Text = "AI Sound Analysis & Colors";
                TxtHelp4Desc.Text = "AI analyzes the type of sound in real-time and displays it with labels and colors. You can assign unique colors to each sound type (Ambient, Speech, Danger) in the settings for intuitive identification.";
                TxtHelp5Title.Text = "Developer Mode";
                TxtHelp5Desc.Text = "Enabling Developer Mode displays technical information such as detailed AI labels, real-time audio engine channel status, and FPS on the overlay. Useful for checking system operation.";
                
                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "AI Display Settings";
                    ChkShowAmbient.Content = "Show Ambient Sounds";
                    ChkShowSpeech.Content = "Show Speech Sounds";
                    ChkShowDanger.Content = "Show Danger Sounds";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "日本語")
            {
                TabHome.Header = "ホーム";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "見えない音を画面に描きます。\nゲームから映画鑑賞まで、新しい体験を始めましょう。";
                if (BtnLaunch != null) BtnLaunch.Content = "開始";
                if (BtnStop != null) BtnStop.Content = "停止";
                TabSettings.Header = "設定";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "波モード設定";
                TxtIntensityLabelWave.Text = "サイズ";
                TxtIntensityDescWave.Text = "波の上下の高さと全体的なボリューム感を調整します。";
                TxtSpeedLabelWave.Text = "速度";
                TxtSpeedDescWave.Text = "波が流れて揺れる速度を調整します。";
                TxtSensitivityLabelWave.Text = "感度";
                TxtSensitivityDescWave.Text = "小さなデシベルの音にも波がどれだけ敏感に反応して揺れるかを調整します。";
                TxtOpacityLabelWave.Text = "不透明度";
                TxtOpacityDescWave.Text = "波の透明度を調整し、オーバーレイの後ろのゲームや画面の透け具合を決定します。";
                TxtGlowModeLabelWave.Text = "発光";
                TxtGlowModeDescWave.Text = "波の輪郭にほのかなネオン光を適用し、その強度を調整します。";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "パッドモード設定";
                TxtIntensityLabelPad.Text = "サイズ";
                TxtIntensityDescPad.Text = "音に反応する際のパッドの厚さと横幅を調整します。";
                TxtSpeedLabelPad.Text = "速度";
                TxtSpeedDescPad.Text = "パッドが特定の方向の音に向かって回転して反応する速度を調整します。";
                TxtSensitivityLabelPad.Text = "感度";
                TxtSensitivityDescPad.Text = "パッドが音をよりよく捉え、より大きく目立つように調整します。";
                TxtOpacityLabelPad.Text = "不透明度";
                TxtOpacityDescPad.Text = "パッドグラフィックの不透明度を調整します。";
                TxtGlowModeLabelPad.Text = "発光";
                TxtGlowModeDescPad.Text = "パッドの周囲に柔らかいオーラ状の光エフェクトを付与します。";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "円形モード設定";
                TxtIntensityLabelCircle.Text = "サイズ";
                TxtIntensityDescCircle.Text = "音が鳴る際の円形イコライザーの突出振幅スケールを調整します。";
                TxtSpeedLabelCircle.Text = "速度";
                TxtSpeedDescCircle.Text = "円形イコライザーの波や回転の反応速度を調整します。";
                TxtSensitivityLabelCircle.Text = "感度";
                TxtSensitivityDescCircle.Text = "円形の振幅が音に対してどれだけ繊細に分割されて反応するかを調整します。";
                TxtOpacityLabelCircle.Text = "不透明度";
                TxtOpacityDescCircle.Text = "円形グラフィックの不透明度を決定します。";
                TxtCircleRadiusLabel.Text = "円のサイズ";
                TxtCircleRadiusDesc.Text = "円形モードでの中央中心部の半径サイズを個別に調整します。";
                TxtGlowModeLabelCircle.Text = "発光";
                TxtGlowModeDescCircle.Text = "円形イコライザーの縁に沿って輝くグロー後光を作成します。";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "外角線モード設定";
                TxtIntensityLabelOutline.Text = "サイズ";
                TxtIntensityDescOutline.Text = "外角線波の上下の高さと全体的なボリューム感を調整します。";
                TxtSpeedLabelOutline.Text = "速度";
                TxtSpeedDescOutline.Text = "外角線波が流れて揺れる速度を調整します。";
                TxtSensitivityLabelOutline.Text = "感度";
                TxtSensitivityDescOutline.Text = "小さなデシベルの音にも外角線波가どれだけ敏感に反応して揺れるかを調整します。";
                TxtOpacityLabelOutline.Text = "不透明度";
                TxtOpacityDescOutline.Text = "外角線波의透明度を調整します。";
                TxtGlowModeLabelOutline.Text = "発光";
                TxtGlowModeDescOutline.Text = "外角線にほのかなネオン光を適用し、その強度を調整します。";

                TxtModeSettings.Text = "モード設定";
                TxtVisualModeLabel.Text = "表現モード";
                TxtVisualModeDesc.Text = "画面に描画されるグラフィックの形状を選択します。";
                CmbVisualModeWave.Content = "波";
                CmbVisualModePad.Content = "パッド";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "円形 (Circle)";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "外角線 (Outline)";
                TxtSoundModeLabel.Text = "サウンドモード";
                TxtSoundModeDesc.Text = "スピーカー環境に合ったサウンドチャンネルを選択します。\n2チャンネル環境の場合は「2チャンネル」を、サラウンド環境の場合は「5.1チャンネル」または「7.1チャンネル」を選択してください。";
                TxtHotkeySettings.Text = "ショートカットキー";
                TxtVisualHotkeyLabel.Text = "表現モードの切り替え";
                TxtVisualHotkeyDesc.Text = "実行中に形状をリアルタイムで変更するショートカットです。";
                TxtSoundModeHotkeyLabel.Text = "サウンドモードの切り替え";
                TxtSoundModeHotkeyDesc.Text = "実行中にサウンドモードをリアルタイムで変更するショートカットです。";
                TxtEditHotkeyLabel.Text = "オーバーレイの切り替え";
                TxtEditHotkeyDesc.Text = "オーバーレイ編集画面を表示または閉じるショートカットキーです。";
                TxtAdminSettings.Text = "詳細設定";
                TxtAdminModeLabel.Text = "開発者モード";
                TxtAdminModeDesc.Text = "デバッグ情報とオーディオエンジンの状態を画面に表示します。";
                ChkAdminMode.Content = "オン";
                BtnReset.Content = "デフォルトに戻す";
                TabHelp.Header = "ヘルプ";
                TxtHelp1Title.Text = "サウンドモード";
                TxtHelp1Desc.Text = "正常な方向性(レーダー)動作のためには、Windowsのサウンド設定で出力デバイスが「5.1 サラウンド」(6チャンネル)または「7.1 サラウンド」(8チャンネル)に構成されている必要があります。通常のステレオ(2チャンネル)環境の場合、視覚化グラフィックが左右にのみ表示されることがあります。これを補完するには、設定タブで「サウンドモード」を適切に設定してください。";
                TxtHelp2Title.Text = "リアルタイムショートカットキー制御";
                TxtHelp2Desc.Text = "オーバーレイが画面に表示されている状態でも、バックグラウンドで指定されたショートカットキー（デフォルト F2、F3）を押すと、リアルタイムで形状とモードが即座に切り替わります。";
                TxtHelp3Title.Text = "オーバーレイの終了方法";
                TxtHelp3Desc.Text = "終了するには、ホームタブの「停止」ボタンを押すか、このランチャーウィンドウ上部の ボタンをクリックしてください。";
                TxtHelp4Title.Text = "AI音声分析と色";
                TxtHelp4Desc.Text = "AIがリアルタイムで音の種類を分析し、ラベルと色で表示します。設定で各音の種類（環境音、音声、強調音）ごとに固有の色を指定して、直感的に識別できます。";
                TxtHelp5Title.Text = "開発者モード";
                TxtHelp5Desc.Text = "開発者モードを有効にすると、詳細なAIラベル、オーディオエンジンのリアルタイムチャンネル状態、FPSなどの技術情報をオーバーレイに表示します。システムの動作確認に便利です。";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "AI音声分類の表示設定";
                    ChkShowAmbient.Content = "環境音の表示";
                    ChkShowSpeech.Content = "音声の表示";
                    ChkShowDanger.Content = "強調音の表示";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "中文")
            {
                TabHome.Header = "主页";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "将看不见的声音描绘在屏幕上。\n从游戏到观影，开始全新的体验。";
                if (BtnLaunch != null) BtnLaunch.Content = "开始";
                if (BtnStop != null) BtnStop.Content = "停止";
                TabSettings.Header = "设置";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "波浪模式设置";
                TxtIntensityLabelWave.Text = "大小";
                TxtIntensityDescWave.Text = "调整波浪的上下高度和整体体积感。";
                TxtSpeedLabelWave.Text = "速度";
                TxtSpeedDescWave.Text = "调整波浪流动和波动的速度。";
                TxtSensitivityLabelWave.Text = "灵敏度";
                TxtSensitivityDescWave.Text = "调整波浪对低分贝声音的敏感反应程度。";
                TxtOpacityLabelWave.Text = "不透明度";
                TxtOpacityDescWave.Text = "调整波浪的透明度，决定覆盖层后游戏或屏幕的可见度。";
                TxtGlowModeLabelWave.Text = "光源";
                TxtGlowModeDescWave.Text = "在波浪轮廓上应用柔和的霓虹光源并调整其强度。";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "面板模式设置";
                TxtIntensityLabelPad.Text = "大小";
                TxtIntensityDescPad.Text = "调整声音反应时面板的厚度和横向宽度。";
                TxtSpeedLabelPad.Text = "速度";
                TxtSpeedDescPad.Text = "调整面板在特定方向声音下的旋转反应速度。";
                TxtSensitivityLabelPad.Text = "灵敏度";
                TxtSensitivityDescPad.Text = "调整面板以更好地捕获声音并使其更加突出。";
                TxtOpacityLabelPad.Text = "不透明度";
                TxtOpacityDescPad.Text = "调整面板图形的不透明度。";
                TxtGlowModeLabelPad.Text = "光源";
                TxtGlowModeDescPad.Text = "在面板周围赋予柔和的光晕效果。";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "圆形模式设置";
                TxtIntensityLabelCircle.Text = "振幅";
                TxtIntensityDescCircle.Text = "调整声音响亮时圆形均衡器的突出振幅比例。";
                TxtSpeedLabelCircle.Text = "速度";
                TxtSpeedDescCircle.Text = "调整圆形均衡器的波动或旋转反应速度。";
                TxtSensitivityLabelCircle.Text = "灵敏度";
                TxtSensitivityDescCircle.Text = "调整圆形振幅对声音进行何种细腻划分并产生反应。";
                TxtOpacityLabelCircle.Text = "不透明度";
                TxtOpacityDescCircle.Text = "决定圆形图形的不透明度。";
                TxtCircleRadiusLabel.Text = "圆半径";
                TxtCircleRadiusDesc.Text = "单独调整圆形模式下中心核心的半径大小。";
                TxtGlowModeLabelCircle.Text = "光源";
                TxtGlowModeDescCircle.Text = "沿圆形均衡器的边缘创建闪烁的辉光后光。";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "轮廓线模式设置";
                TxtIntensityLabelOutline.Text = "大小";
                TxtIntensityDescOutline.Text = "调整轮廓线波浪的上下高度和整体体积感。";
                TxtSpeedLabelOutline.Text = "速度";
                TxtSpeedDescOutline.Text = "调整轮廓线波浪流动和波动的速度。";
                TxtSensitivityLabelOutline.Text = "灵敏度";
                TxtSensitivityDescOutline.Text = "调整轮廓线波浪对低分贝声音的敏感反应程度。";
                TxtOpacityLabelOutline.Text = "不透明度";
                TxtOpacityDescOutline.Text = "调整轮廓线波浪的透明度。";
                TxtGlowModeLabelOutline.Text = "光源";
                TxtGlowModeDescOutline.Text = "在轮廓线上应用柔和的霓虹光源并调整其强度。";
                TxtModeSettings.Text = "模式设置";
                TxtVisualModeLabel.Text = "表现模式";
                TxtVisualModeDesc.Text = "选择在屏幕上绘制的图形的形状。";
                CmbVisualModeWave.Content = "波浪";
                CmbVisualModePad.Content = "面板";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "圆形 (Circle)";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "轮廓线 (Outline)";
                TxtSoundModeLabel.Text = "声音模式";
                TxtSoundModeDesc.Text = "选择适合您扬声器环境的声音声道。\n对于双声道环境，请选择“2 声道”，对于环绕声环境，请选择“5.1 声道”或“7.1 声道”。";
                TxtHotkeySettings.Text = "快捷键";
                TxtVisualHotkeyLabel.Text = "切换表现模式";
                TxtVisualHotkeyDesc.Text = "在运行中实时改变形状的快捷键。";
                TxtSoundModeHotkeyLabel.Text = "切换声音模式";
                TxtSoundModeHotkeyDesc.Text = "在运行中实时改变声音模式的快捷键。";
                TxtEditHotkeyLabel.Text = "切换悬浮窗编辑";
                TxtEditHotkeyDesc.Text = "显示或关闭悬浮窗编辑窗口的快捷键。";
                TxtAdminSettings.Text = "高级设置";
                TxtAdminModeLabel.Text = "开发者模式";
                TxtAdminModeDesc.Text = "在屏幕上显示调试信息和音频引擎状态。";
                ChkAdminMode.Content = "开启";
                BtnReset.Content = "恢复默认值";
                TabHelp.Header = "帮助";
                TxtHelp1Title.Text = "声音模式";
                TxtHelp1Desc.Text = "为了使方向性(雷达)正常工作，在Windows声音设置中，输出设备必须配置为“5.1 环绕声”(6声道)或“7.1 环绕声”(8声道)。在普通立体声(2声道)环境中，可视化图形可能仅显示在左右两侧。要弥补这一点，请在设置选项卡中正确设置“声音模式”。";
                TxtHelp2Title.Text = "实时快捷键控制";
                TxtHelp2Desc.Text = "即使在屏幕上显示悬浮窗，在后台按下指定的快捷键（默认 F2、F3），也会实时立即切换形状和模式。";
                TxtHelp3Title.Text = "关闭悬浮窗的方法";
                TxtHelp3Desc.Text = "要关闭，请点击“主页”选项卡中的“停止”，或点击此启动器窗口顶部的 按钮即可一并关闭。";
                TxtHelp4Title.Text = "AI 声音分析与颜色";
                TxtHelp4Desc.Text = "AI 实时分析声音类型，并以标签和颜色显示。您可以在设置中为每种声音类型（环境音、语音、强调音）指定独特的颜色，以便直观区分。";
                TxtHelp5Title.Text = "开发者模式";
                TxtHelp5Desc.Text = "启用开发者模式后，将在悬浮窗上显示详细的 AI 标签、音频引擎实时通道状态和 FPS 等技术信息。适用于检查系统运行状态。";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "AI声音分类显示设置";
                    ChkShowAmbient.Content = "显示环境音";
                    ChkShowSpeech.Content = "显示语音";
                    ChkShowDanger.Content = "显示强调音";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "Español")
            {
                TabHome.Header = "Inicio";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "Visualiza los sonidos invisibles.\nComienza una nueva experiencia, desde los juegos hasta el cine.";
                if (BtnLaunch != null) BtnLaunch.Content = "Iniciar";
                if (BtnStop != null) BtnStop.Content = "Detener";
                TabSettings.Header = "Ajustes";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "Ajustes de Modo Ola";
                TxtIntensityLabelWave.Text = "Intensidad";
                TxtIntensityDescWave.Text = "Ajusta la altura vertical y el volumen general de las olas.";
                TxtSpeedLabelWave.Text = "Velocidad";
                TxtSpeedDescWave.Text = "Controla la velocidad a la que la ola fluye y ondula.";
                TxtSensitivityLabelWave.Text = "Sensibilidad";
                TxtSensitivityDescWave.Text = "Determina qué tan sensiblemente reaccionan las olas a sonidos de bajos decibelios.";
                TxtOpacityLabelWave.Text = "Opacidad";
                TxtOpacityDescWave.Text = "Ajusta la opacidad de la ola para determinar la transparencia sobre el fondo.";
                TxtGlowModeLabelWave.Text = "Brillo";
                TxtGlowModeDescWave.Text = "Aplica un brillo de neón suave al contorno de la ola y ajusta su intensidad.";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "Ajustes de Modo Pad";
                TxtIntensityLabelPad.Text = "Tamaño";
                TxtIntensityDescPad.Text = "Ajusta el grosor y el ancho del pad al responder al sonido.";
                TxtSpeedLabelPad.Text = "Velocidad";
                TxtSpeedDescPad.Text = "Controla la velocidad a la que el pad gira en respuesta a sonidos direccionales.";
                TxtSensitivityLabelPad.Text = "Sensibilidad";
                TxtSensitivityDescPad.Text = "Ajusta el pad para capturar mejor los sonidos y destacar más.";
                TxtOpacityLabelPad.Text = "Opacidad";
                TxtOpacityDescPad.Text = "Ajusta la opacidad del gráfico del pad.";
                TxtGlowModeLabelPad.Text = "Brillo";
                TxtGlowModeDescPad.Text = "Da un efecto de brillo suave estilo aura al pad.";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "Ajustes de Modo Círculo";
                TxtIntensityLabelCircle.Text = "Intensidad";
                TxtIntensityDescCircle.Text = "Ajusta la escala de amplitud de la protuberancia del ecualizador circular.";
                TxtSpeedLabelCircle.Text = "Velocidad";
                TxtSpeedDescCircle.Text = "Ajusta la velocidad de respuesta de ondulación o rotación del ecualizador circular.";
                TxtSensitivityLabelCircle.Text = "Sensibilidad";
                TxtSensitivityDescCircle.Text = "Controla qué tan delicadamente la amplitud del círculo se divide y responde al sonido.";
                TxtOpacityLabelCircle.Text = "Opacidad";
                TxtOpacityDescCircle.Text = "Determina la opacidad del gráfico circular.";
                TxtCircleRadiusLabel.Text = "Radio del Círculo";
                TxtCircleRadiusDesc.Text = "Ajusta individualmente el tamaño del radio del núcleo en el modo círculo.";
                TxtGlowModeLabelCircle.Text = "Brillo";
                TxtGlowModeDescCircle.Text = "Crea un halo de brillo parpadeante a lo largo del ecualizador circular.";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "Ajustes de Modo Contorno";
                TxtIntensityLabelOutline.Text = "Intensidad";
                TxtIntensityDescOutline.Text = "Ajusta la altura vertical y el volumen general de las olas de contorno.";
                TxtSpeedLabelOutline.Text = "Velocidad";
                TxtSpeedDescOutline.Text = "Controla la velocidad a la que la ola de contorno fluye y ondula.";
                TxtSensitivityLabelOutline.Text = "Sensibilidad";
                TxtSensitivityDescOutline.Text = "Determina qué tan sensiblemente reaccionan las olas de contorno a sonidos de bajos decibelios.";
                TxtOpacityLabelOutline.Text = "Opacidad";
                TxtOpacityDescOutline.Text = "Ajusta la opacidad de las olas de contorno.";
                TxtGlowModeLabelOutline.Text = "Brillo";
                TxtGlowModeDescOutline.Text = "Aplica un brillo de neón suave al contorno de la ola y ajusta su intensidad.";
                TxtModeSettings.Text = "Ajustes de Modo";
                TxtVisualModeLabel.Text = "Modo visual";
                TxtVisualModeDesc.Text = "Selecciona la forma de los gráficos en pantalla.";
                CmbVisualModeWave.Content = "Ola";
                CmbVisualModePad.Content = "Pad";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Círculo";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "Contorno (Outline)";
                TxtSoundModeLabel.Text = "Modo de Sonido";
                TxtSoundModeDesc.Text = "Seleccione el canal de sonido adecuado para su entorno de altavoces.\nPara entornos de 2 canales, seleccione '2 Canales', y para entornos envolventes, seleccione '5.1 Canales' o '7.1 Canales'.";
                TxtHotkeySettings.Text = "Atajos";
                TxtVisualHotkeyLabel.Text = "Cambiar modo visual";
                TxtVisualHotkeyDesc.Text = "Atajo para cambiar la forma en tiempo real durante la ejecución.";
                TxtSoundModeHotkeyLabel.Text = "Cambiar modo de sonido";
                TxtSoundModeHotkeyDesc.Text = "Atajo para cambiar el modo de sonido en tiempo real durante la ejecución.";
                TxtEditHotkeyLabel.Text = "Alternar editor";
                TxtEditHotkeyDesc.Text = "Atajo para abrir o cerrar el editor de superposición.";
                TxtAdminSettings.Text = "Configuración avanzada";
                TxtAdminModeLabel.Text = "Modo desarrollador";
                TxtAdminModeDesc.Text = "Muestra información de depuración y el estado del motor de audio en pantalla.";
                ChkAdminMode.Content = "Activar";
                BtnReset.Content = "Restablecer por defecto";
                TabHelp.Header = "Ayuda";
                TxtHelp1Title.Text = "Modo de Sonido";
                TxtHelp1Desc.Text = "Para que la direccionalidad (radar) funcione correctamente, tu dispositivo de salida de sonido de Windows debe estar configurado como 'Envolvente 5.1' (6 canales) o 'Envolvente 7.1' (8 canales). En un entorno estéreo normal (2 canales), la visualización gráfica puede aparecer solo a la izquierda/derecha. Para compensarlo, ajuste el 'Modo de Sonido' correctamente en la configuración.";
                TxtHelp2Title.Text = "Control de atajos en tiempo real";
                TxtHelp2Desc.Text = "Incluso con la superposición en pantalla, si presionas los atajos asignados (por defecto F2, F3) en segundo plano, la forma y el modo cambiarán instantáneamente en tiempo real.";
                TxtHelp3Title.Text = "Cómo cerrar la superposición";
                TxtHelp3Desc.Text = "Para cerrar, haga clic en 'Detener' en la pestaña Inicio, o haga clic en el botón en la parte superior de esta ventana.";
                TxtHelp4Title.Text = "Análisis de Sonido AI y Colores";
                TxtHelp4Desc.Text = "La IA analiza el tipo de sonido en tiempo real y lo muestra con etiquetas y colores. Puede asignar colores únicos a cada tipo de sonido (Ambiental, Voz, Peligro) en los ajustes para una identificación intuitiva.";
                TxtHelp5Title.Text = "Modo Desarrollador";
                TxtHelp5Desc.Text = "Al activar el Modo Desarrollador, se muestra información técnica como etiquetas de IA detalladas, estado de los canales del motor de audio en tiempo real y FPS en la superposición. Útil para verificar el funcionamiento del sistema.";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "Configuración de pantalla AI";
                    ChkShowAmbient.Content = "Mostrar sonido ambiental";
                    ChkShowSpeech.Content = "Mostrar voz";
                    ChkShowDanger.Content = "Mostrar sonido de peligro";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "Français")
            {
                TabHome.Header = "Accueil";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "Visualisez les sons invisibles.\nCommencez une nouvelle expérience, des jeux aux films.";
                if (BtnLaunch != null) BtnLaunch.Content = "Démarrer";
                if (BtnStop != null) BtnStop.Content = "Arrêter";
                TabSettings.Header = "Paramètres";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "Paramètres du Mode Vague";
                TxtIntensityLabelWave.Text = "Intensité";
                TxtIntensityDescWave.Text = "Ajuste la hauteur verticale et le volume général des vagues.";
                TxtSpeedLabelWave.Text = "Vitesse";
                TxtSpeedDescWave.Text = "Contrôle la vitesse à laquelle la vague s'écoule et ondule.";
                TxtSensitivityLabelWave.Text = "Sensibilité";
                TxtSensitivityDescWave.Text = "Détermine la sensibilité de réaction des vagues aux sons de faible décibel.";
                TxtOpacityLabelWave.Text = "Opacité";
                TxtOpacityDescWave.Text = "Ajuste l'opacité de la vague pour déterminer la transparence sur le fond.";
                TxtGlowModeLabelWave.Text = "Halo";
                TxtGlowModeDescWave.Text = "Applique une lueur néon douce au contour de la vague et ajuste son intensité.";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "Paramètres du Mode Pad";
                TxtIntensityLabelPad.Text = "Taille";
                TxtIntensityDescPad.Text = "Ajuste l'épaisseur et la largeur du pad lors de la réponse au son.";
                TxtSpeedLabelPad.Text = "Vitesse";
                TxtSpeedDescPad.Text = "Contrôle la vitesse à laquelle le pad tourne en réponse aux sons directionnels.";
                TxtSensitivityLabelPad.Text = "Sensibilité";
                TxtSensitivityDescPad.Text = "Ajuste le pad pour mieux capturer les sons et se démarquer davantage.";
                TxtOpacityLabelPad.Text = "Opacité";
                TxtOpacityDescPad.Text = "Ajuste l'opacité du graphique du pad.";
                TxtGlowModeLabelPad.Text = "Halo";
                TxtGlowModeDescPad.Text = "Donne un effet de lueur douce de type aura autour du pad.";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "Paramètres du Mode Cercle";
                TxtIntensityLabelCircle.Text = "Intensité";
                TxtIntensityDescCircle.Text = "Ajuste l'échelle d'amplitude de protrusion de l'égaliseur circulaire.";
                TxtSpeedLabelCircle.Text = "Vitesse";
                TxtSpeedDescCircle.Text = "Ajuste la vitesse de réponse d'ondulation ou de rotation de l'égaliseur circulaire.";
                TxtSensitivityLabelCircle.Text = "Sensibilité";
                TxtSensitivityDescCircle.Text = "Contrôle la finesse avec laquelle l'amplitude du cercle se divise et répond au son.";
                TxtOpacityLabelCircle.Text = "Opacité";
                TxtOpacityDescCircle.Text = "Détermine l'opacité du graphique circulaire.";
                TxtCircleRadiusLabel.Text = "Rayon du Cercle";
                TxtCircleRadiusDesc.Text = "Ajuste individuellement la taille du rayon du noyau dans le mode cercle.";
                TxtGlowModeLabelCircle.Text = "Halo";
                TxtGlowModeDescCircle.Text = "Crée une lueur scintillante le long de la bordure de l'égaliseur circulaire.";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "Paramètres du Mode Contour";
                TxtIntensityLabelOutline.Text = "Taille";
                TxtIntensityDescOutline.Text = "Ajuste la hauteur et le volume général de la vague de contour.";
                TxtSpeedLabelOutline.Text = "Vitesse";
                TxtSpeedDescOutline.Text = "Ajuste la vitesse à laquelle la vague de contour s'écoule et ondule.";
                TxtSensitivityLabelOutline.Text = "Sensibilité";
                TxtSensitivityDescOutline.Text = "Ajuste la sensibilité avec laquelle la vague de contour réagit aux sons de faible décibel.";
                TxtOpacityLabelOutline.Text = "Opacité";
                TxtOpacityDescOutline.Text = "Ajuste la transparence de la vague de contour.";
                TxtGlowModeLabelOutline.Text = "Lueur";
                TxtGlowModeDescOutline.Text = "Applique une lueur néon douce au contour de la vague et ajuste son intensité.";
                TxtModeSettings.Text = "Paramètres de Mode";
                TxtVisualModeLabel.Text = "Mode visuel";
                TxtVisualModeDesc.Text = "Sélectionne la forme des graphiques dessinés à l'écran.";
                CmbVisualModeWave.Content = "Vague";
                CmbVisualModePad.Content = "Pad";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Cercle";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "Contour";
                TxtSoundModeLabel.Text = "Mode Sonore";
                TxtSoundModeDesc.Text = "Sélectionnez le canal audio adapté à votre environnement de haut-parleurs.\nPour les environnements à 2 canaux, sélectionnez '2 Canaux', et pour les environnements surround, sélectionnez '5.1 Canaux' ou '7.1 Canaux'.";
                TxtHotkeySettings.Text = "Raccourcis";
                TxtVisualHotkeyLabel.Text = "Basculer le mode visuel";
                TxtVisualHotkeyDesc.Text = "Raccourci pour changer la forme en temps réel pendant l'exécution.";
                TxtSoundModeHotkeyLabel.Text = "Basculer le mode sonore";
                TxtSoundModeHotkeyDesc.Text = "Raccourci pour changer le mode sonore en temps réel pendant l'exécution.";
                TxtEditHotkeyLabel.Text = "Basculer l'éditeur";
                TxtEditHotkeyDesc.Text = "Raccourci pour ouvrir ou fermer l'éditeur de superposition.";
                TxtAdminSettings.Text = "Paramètres avancés";
                TxtAdminModeLabel.Text = "Mode Développeur";
                TxtAdminModeDesc.Text = "Affiche les informations de débogage et l'état du moteur audio à l'écran.";
                ChkAdminMode.Content = "Activer";
                BtnReset.Content = "Réinitialiser";
                TabHelp.Header = "Aide";
                TxtHelp1Title.Text = "Mode Sonore";
                TxtHelp1Desc.Text = "Pour que la directivité (radar) fonctionne correctement, votre périphérique de sortie audio Windows doit être configuré en 'Surround 5.1' (6 canaux) ou 'Surround 7.1' (8 canaux). Dans un environnement stéréo standard (2 canaux), la visualisation graphique peut n'apparaître qu'à gauche/droite. Pour compenser, réglez correctement le 'Mode Sonore' dans les paramètres.";
                TxtHelp2Title.Text = "Contrôle par raccourci en temps réel";
                TxtHelp2Desc.Text = "Même avec la superposition à l'écran, si vous appuyez sur les raccourcis définis (par défaut F2, F3) en arrière-plan, la forme et le mode changeront instantanément en temps réel.";
                TxtHelp3Title.Text = "Comment fermer la superposition";
                TxtHelp3Desc.Text = "Pour fermer, cliquez sur 'Arrêter' dans l'onglet Accueil, ou cliquez sur le bouton en haut de cette fenêtre.";
                TxtHelp4Title.Text = "Analyse Sonore IA & Couleurs";
                TxtHelp4Desc.Text = "L'IA analyse le type de son en temps réel et l'affiche avec des étiquettes et des couleurs. Vous pouvez attribuer des couleurs uniques à chaque type de son (Ambiance, Voix, Danger) dans les paramètres pour une identification intuitive.";
                TxtHelp5Title.Text = "Mode Développeur";
                TxtHelp5Desc.Text = "L'activation du mode Développeur affiche des informations techniques telles que des étiquettes IA détaillées, l'état des canaux du moteur audio en temps réel et le FPS sur la superposition. Utile pour vérifier le fonctionnement du système.";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "Paramètres d'affichage IA";
                    ChkShowAmbient.Content = "Afficher les sons ambiants";
                    ChkShowSpeech.Content = "Afficher la voix";
                    ChkShowDanger.Content = "Afficher les sons de danger";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "Deutsch")
            {
                TabHome.Header = "Startseite";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "Machen Sie unsichtbare Klänge sichtbar.\nStarten Sie ein neues Erlebnis, von Spielen bis hin zu Filmen.";
                if (BtnLaunch != null) BtnLaunch.Content = "Starten";
                if (BtnStop != null) BtnStop.Content = "Stoppen";
                TabSettings.Header = "Einstellungen";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "Wellenmodus-Einstellungen";
                TxtIntensityLabelWave.Text = "Intensität";
                TxtIntensityDescWave.Text = "Passt die vertikale Höhe und das Gesamtvolumen der Wellen an.";
                TxtSpeedLabelWave.Text = "Geschwindigkeit";
                TxtSpeedDescWave.Text = "Steuert die Geschwindigkeit, mit der die Welle fließt und wogt.";
                TxtSensitivityLabelWave.Text = "Empfindlichkeit";
                TxtSensitivityDescWave.Text = "Bestimmt, wie empfindlich die Wellen auf leise Geräusche reagieren.";
                TxtOpacityLabelWave.Text = "Deckkraft";
                TxtOpacityDescWave.Text = "Passt die Wellendeckkraft an, um die Transparenz über dem Hintergrund zu bestimmen.";
                TxtGlowModeLabelWave.Text = "Leuchten";
                TxtGlowModeDescWave.Text = "Trägt ein sanftes Neonleuchten auf die Wellenkontur auf und passt die Intensität an.";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "Padmodus-Einstellungen";
                TxtIntensityLabelPad.Text = "Größe";
                TxtIntensityDescPad.Text = "Passt die Dicke und Breite des Pads bei der Tonreaktion an.";
                TxtSpeedLabelPad.Text = "Geschwindigkeit";
                TxtSpeedDescPad.Text = "Steuert die Geschwindigkeit, mit der sich das Pad als Reaktion auf Richtungstöne dreht.";
                TxtSensitivityLabelPad.Text = "Empfindlichkeit";
                TxtSensitivityDescPad.Text = "Passt das Pad an, um Töne besser zu erfassen und sich deutlicher abzuheben.";
                TxtOpacityLabelPad.Text = "Deckkraft";
                TxtOpacityDescPad.Text = "Passt die Deckkraft der Pad-Grafik an.";
                TxtGlowModeLabelPad.Text = "Leuchten";
                TxtGlowModeDescPad.Text = "Erzeugt einen sanften Aura-Lichteffekt um das Pad.";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "Kreismus-Einstellungen";
                TxtIntensityLabelCircle.Text = "Intensität";
                TxtIntensityDescCircle.Text = "Passt die Skala der Amplitudenauslenkung des Kreis-Equalizers an.";
                TxtSpeedLabelCircle.Text = "Geschwindigkeit";
                TxtSpeedDescCircle.Text = "Passt die Wellen- oder Rotationsgeschwindigkeit des Kreis-Equalizers an.";
                TxtSensitivityLabelCircle.Text = "Empfindlichkeit";
                TxtSensitivityDescCircle.Text = "Steuert, wie fein die Kreisamplitude aufgeteilt wird und auf Töne reagiert.";
                TxtOpacityLabelCircle.Text = "Deckkraft";
                TxtOpacityDescCircle.Text = "Bestimmt die Deckkraft der Kreis-Grafik.";
                TxtCircleRadiusLabel.Text = "Kreisradius";
                TxtCircleRadiusDesc.Text = "Passt den Radius des inneren Kerns im Kreis-Modus individuell an.";
                TxtGlowModeLabelCircle.Text = "Leuchten";
                TxtGlowModeDescCircle.Text = "Erzeugt eine schimmernde Aura entlang des Rands des Kreis-Equalizers.";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "Umrissmodus-Einstellungen";
                TxtIntensityLabelOutline.Text = "Größe";
                TxtIntensityDescOutline.Text = "Passt die Höhe und das Gesamtvolumen der Umrisswelle an.";
                TxtSpeedLabelOutline.Text = "Geschwindigkeit";
                TxtSpeedDescOutline.Text = "Passt die Geschwindigkeit an, mit der die Umrisswelle fließt und sich kräuselt.";
                TxtSensitivityLabelOutline.Text = "Empfindlichkeit";
                TxtSensitivityDescOutline.Text = "Passt an, wie empfindlich die Umrisswelle auf leise Dezibel-Geräusche reagiert.";
                TxtOpacityLabelOutline.Text = "Deckkraft";
                TxtOpacityDescOutline.Text = "Passt die Transparenz der Umrisswelle an.";
                TxtGlowModeLabelOutline.Text = "Leuchten";
                TxtGlowModeDescOutline.Text = "Wendet ein sanftes Neonleuchten auf den Wellenumriss an und passt dessen Intensität an.";
                TxtModeSettings.Text = "Modus-Einstellungen";
                TxtVisualModeLabel.Text = "Visueller Modus";
                TxtVisualModeDesc.Text = "Wählt die Form der auf dem Bildschirm gezeichneten Grafiken aus.";
                CmbVisualModeWave.Content = "Welle";
                CmbVisualModePad.Content = "Pad";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Kreis";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "Umriss";
                TxtSoundModeLabel.Text = "Sound-Modus";
                TxtSoundModeDesc.Text = "Wählen Sie den für Ihre Lautsprecherumgebung geeigneten Audiokanal.\nWählen Sie für 2-Kanal-Umgebungen '2 Kanäle' und für Surround-Umgebungen '5.1 Kanäle' oder '7.1 Kanäle'.";
                TxtHotkeySettings.Text = "Tastenkombinationen";
                TxtVisualHotkeyLabel.Text = "Visuellen Modus umschalten";
                TxtVisualHotkeyDesc.Text = "Tastenkombination zum Ändern der Form in Echtzeit während der Ausführung.";
                TxtSoundModeHotkeyLabel.Text = "Sound-Modus umschalten";
                TxtSoundModeHotkeyDesc.Text = "Tastenkombination zum Ändern des Sound-Modus in Echtzeit während der Ausführung.";
                TxtEditHotkeyLabel.Text = "Overlay-Editor umschalten";
                TxtEditHotkeyDesc.Text = "Tastenkombination zum Öffnen oder Schließen des Overlay-Editors.";
                TxtAdminSettings.Text = "Erweiterte Einstellungen";
                TxtAdminModeLabel.Text = "Entwicklermodus";
                TxtAdminModeDesc.Text = "Zeigt Debug-Informationen und den Status der Audio-Engine auf dem Bildschirm an.";
                ChkAdminMode.Content = "Aktivieren";
                BtnReset.Content = "Auf Standard zurücksetzen";
                TabHelp.Header = "Hilfe";
                TxtHelp1Title.Text = "Sound-Modus";
                TxtHelp1Desc.Text = "Damit die Richtwirkung (Radar) richtig funktioniert, muss Ihr Windows-Audioausgabegerät als '5.1 Surround' (6 Kanäle) oder '7.1 Surround' (8 Kanäle) konfiguriert sein. In einer Standard-Stereoumgebung (2 Kanäle) wird die Grafikvisualisierung möglicherweise nur links/rechts angezeigt. Um dies auszugleichen, stellen Sie den 'Sound-Modus' in den Einstellungen richtig ein.";
                TxtHelp2Title.Text = "Echtzeit-Tastenkombinationssteuerung";
                TxtHelp2Desc.Text = "Selbst wenn das Overlay auf dem Bildschirm angezeigt wird, ändern sich Form und Modus sofort in Echtzeit, wenn Sie im Hintergrund die zugewiesenen Tastenkombinationen (Standard F2, F3) drücken.";
                TxtHelp3Title.Text = "So schließen Sie das Overlay";
                TxtHelp3Desc.Text = "Zum Schließen klicken Sie auf der Registerkarte 'Startseite' auf 'Stoppen' oder klicken Sie auf die Schaltfläche oben in diesem Fenster.";
                TxtHelp4Title.Text = "KI-Soundanalyse & Farben";
                TxtHelp4Desc.Text = "Die KI analysiert die Art des Geräusches in Echtzeit und zeigt sie mit Beschriftungen und Farben an. Sie können jedem Geräuschtyp (Umgebung, Sprache, Gefahr) in den Einstellungen eindeutige Farben zur intuitiven Identifizierung zuweisen.";
                TxtHelp5Title.Text = "Entwicklermodus";
                TxtHelp5Desc.Text = "Das Aktivieren des Entwicklermodus zeigt technische Informationen wie detaillierte KI-Labels, Echtzeit-Audiokanäle-Status und FPS auf dem Overlay an. Nützlich zur Überprüfung des Systembetriebs.";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "KI-Anzeigeeinstellungen";
                    ChkShowAmbient.Content = "Umgebungsgeräusche anzeigen";
                    ChkShowSpeech.Content = "Sprache anzeigen";
                    ChkShowDanger.Content = "Gefahrentöne anzeigen";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "Русский")
            {
                TabHome.Header = "Главная";
                TxtHomeTitle.Text = "SoundVisualizer";
                TxtHomeDesc.Text = "Визуализируйте невидимые звуки.\nНачните новый опыт, от игр до кино.";
                if (BtnLaunch != null) BtnLaunch.Content = "Запустить";
                if (BtnStop != null) BtnStop.Content = "Остановить";
                TabSettings.Header = "Настройки";
                
                // Wave Mode Settings
                TxtExpanderWaveTitle.Text = "Настройки режима Волна";
                TxtIntensityLabelWave.Text = "Размер";
                TxtIntensityDescWave.Text = "Настраивает высоту волны и общий объем.";
                TxtSpeedLabelWave.Text = "Скорость";
                TxtSpeedDescWave.Text = "Управляет скоростью течения и колебания волны.";
                TxtSensitivityLabelWave.Text = "Чувствительность";
                TxtSensitivityDescWave.Text = "Определяет, насколько чувствительно волны реагируют на тихие звуки.";
                TxtOpacityLabelWave.Text = "Непрозрачность";
                TxtOpacityDescWave.Text = "Настраивает прозрачность волны для определения видимости фона.";
                TxtGlowModeLabelWave.Text = "Свечение";
                TxtGlowModeDescWave.Text = "Применяет мягкое неоновое свечение к контуру волны и регулирует его интенсивность.";

                // Pad Mode Settings
                TxtExpanderPadTitle.Text = "Настройки режима Панель";
                TxtIntensityLabelPad.Text = "Размер";
                TxtIntensityDescPad.Text = "Регулирует толщину и ширину панели при реагировании на звук.";
                TxtSpeedLabelPad.Text = "Скорость";
                TxtSpeedDescPad.Text = "Управляет скоростью вращения панели в ответ на направленные звуки.";
                TxtSensitivityLabelPad.Text = "Чувствительность";
                TxtSensitivityDescPad.Text = "Настраивает панель для лучшего захвата звука и большей выразительности.";
                TxtOpacityLabelPad.Text = "Непрозрачность";
                TxtOpacityDescPad.Text = "Регулирует непрозрачность графики панели.";
                TxtGlowModeLabelPad.Text = "Свечение";
                TxtGlowModeDescPad.Text = "Создает мягкое свечение в стиле ауры вокруг панели.";

                // Circle Mode Settings
                TxtExpanderCircleTitle.Text = "Настройки режима Круг";
                TxtIntensityLabelCircle.Text = "Размер";
                TxtIntensityDescCircle.Text = "Регулирует масштаб амплитуды выступов кругового эквалайзера при звуке.";
                TxtSpeedLabelCircle.Text = "Скорость";
                TxtSpeedDescCircle.Text = "Настраивает скорость волны или вращения кругового эквалайзера.";
                TxtSensitivityLabelCircle.Text = "Чувствительность";
                TxtSensitivityDescCircle.Text = "Определяет, насколько тонко круговая амплитуда разделяется и реагирует на звук.";
                TxtOpacityLabelCircle.Text = "Непрозрачность";
                TxtOpacityDescCircle.Text = "Определяет непрозрачность графики круга.";
                TxtCircleRadiusLabel.Text = "Радиус круга";
                TxtCircleRadiusDesc.Text = "Индивидуально настраивает радиус центрального ядра в режиме круга.";
                TxtGlowModeLabelCircle.Text = "Свечение";
                TxtGlowModeDescCircle.Text = "Создает мерцающее свечение по краю кругового эквалайзера.";
                // Outline Mode Settings
                TxtExpanderOutlineTitle.Text = "Настройки режима Контур";
                TxtIntensityLabelOutline.Text = "Размер";
                TxtIntensityDescOutline.Text = "Регулирует высоту и общий объем контурной волны.";
                TxtSpeedLabelOutline.Text = "Скорость";
                TxtSpeedDescOutline.Text = "Регулирует скорость протекания и ряби контурной волны.";
                TxtSensitivityLabelOutline.Text = "Чувствительность";
                TxtSensitivityDescOutline.Text = "Регулирует чувствительность контурной волны к тихим звукам децибел.";
                TxtOpacityLabelOutline.Text = "Прозрачность";
                TxtOpacityDescOutline.Text = "Регулирует прозрачность контурной волны.";
                TxtGlowModeLabelOutline.Text = "Свечение";
                TxtGlowModeDescOutline.Text = "Применяет мягкое неоновое свечение к контуру волны и регулирует его интенсивность.";
                TxtModeSettings.Text = "Настройки режима";
                TxtVisualModeLabel.Text = "Визуальный режим";
                TxtVisualModeDesc.Text = "Выбирает форму графики, отображаемой на экране.";
                CmbVisualModeWave.Content = "Волна";
                CmbVisualModePad.Content = "Панель";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Круг";
                if (CmbVisualModeOutline != null) CmbVisualModeOutline.Content = "Контур (Outline)";
                TxtSoundModeLabel.Text = "Режим звука";
                TxtSoundModeDesc.Text = "Выберите звуковой канал, подходящий для среды ваших динамиков.\nДля 2-канальной среды выберите «2 канала», а для объемного звука выберите «5.1 каналов» или «7.1 каналов».";
                TxtHotkeySettings.Text = "Горячие клавиши";
                TxtVisualHotkeyLabel.Text = "Смена виз. режима";
                TxtVisualHotkeyDesc.Text = "Горячая клавиша для изменения визуальной формы в реальном времени.";
                TxtSoundModeHotkeyLabel.Text = "Смена режима звука";
                TxtSoundModeHotkeyDesc.Text = "Горячая клавиша для переключения звукового режима в реальном времени.";
                TxtEditHotkeyLabel.Text = "Переключение редактора";
                TxtEditHotkeyDesc.Text = "Горячая клавиша для открытия или закрытия редактора оверлея.";
                TxtAdminSettings.Text = "Дополнительные настройки";
                TxtAdminModeLabel.Text = "Режим разработчика";
                TxtAdminModeDesc.Text = "Отображает отладочную информацию и состояние аудиосистемы на экране.";
                ChkAdminMode.Content = "Включить";
                BtnReset.Content = "Сброс по умолчанию";
                TabHelp.Header = "Помощь";
                TxtHelp1Title.Text = "Режим звука";
                TxtHelp1Desc.Text = "Для правильной работы направления (радара) устройство вывода звука Windows должно быть настроено как «5.1 Surround» (6 каналов) или «7.1 Surround» (8 каналов). В стандартной стереосреде (2 канала) визуализация может отображаться только слева/справа. Чтобы компенсировать это, установите «Режим звука» должным образом в настройках.";
                TxtHelp2Title.Text = "Горячие клавиши в реальном времени";
                TxtHelp2Desc.Text = "Даже когда оверлей находится на экране, вы можете нажимать назначенные горячие клавиши (по умолчанию F2, F3) в фоновом режиме, чтобы мгновенно переключать формы и режимы.";
                TxtHelp3Title.Text = "Как закрыть оверлей";
                TxtHelp3Desc.Text = "Чтобы закрыть, нажмите «Остановить» на вкладке Главная или нажмите кнопку в верхней части этого окна запуска.";
                TxtHelp4Title.Text = "Анализ звука ИИ и цвета";
                TxtHelp4Desc.Text = "ИИ в реальном времени анализирует тип звука и отображает его с помощью меток и цветов. Вы можете назначить уникальные цвета каждому типу звука (Фон, Речь, Опасность) в настройках для интуитивной идентификации.";
                TxtHelp5Title.Text = "Режим разработчика";
                TxtHelp5Desc.Text = "Включение режима разработчика отображает на оверлее техническую информацию, такую как подробные метки ИИ, состояние каналов аудиосистемы в реальном времени и FPS. Полезно для проверки работы системы.";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "Настройки отображения ИИ";
                    ChkShowAmbient.Content = "Показывать фоновые звуки";
                    ChkShowSpeech.Content = "Показывать речь";
                    ChkShowDanger.Content = "Показывать звуки опасности";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
        }

        private void SetStatusUI(bool isRunning)
        {
            string runningText = "상태: 실행 중";
            string waitText = "상태: 실행 대기 중";

            if (AppSettings.Language == "English") { runningText = "Status: Running"; waitText = "Status: Waiting to start"; }
            else if (AppSettings.Language == "日本語") { runningText = "状態: 実行中"; waitText = "状態: 実行待機中"; }
            else if (AppSettings.Language == "中文") { runningText = "状态: 运行中"; waitText = "状态: 等待启动"; }
            else if (AppSettings.Language == "Español") { runningText = "Estado: En ejecución"; waitText = "Estado: Esperando inicio"; }
            else if (AppSettings.Language == "Français") { runningText = "Statut: En cours"; waitText = "Statut: En attente de démarrage"; }
            else if (AppSettings.Language == "Deutsch") { runningText = "Status: Wird ausgeführt"; waitText = "Status: Wartet auf Start"; }
            else if (AppSettings.Language == "Русский") { runningText = "Статус: Запущено"; waitText = "Статус: Ожидание запуска"; }

            if (isRunning)
            {
                TxtStatus.Text = runningText;
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 130, 246)); // #3182F6
                StatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 130, 246));
            }
            else
            {
                TxtStatus.Text = waitText;
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
            if (codes == null || codes.Count == 0) return "없음";
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
            string msg = AppSettings.Language == "KOR" ? "키 누르기.. (ESC 취소)" : "Press key.. (ESC cancel)";
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
                MessageBox.Show("오버레이가 이미 실행 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            string message = "정말로 모든 설정을 기본값으로 되돌리시겠습니까?";
            string title = "기본값 초기화";
            if (AppSettings.Language == "English") { message = "Are you sure you want to reset all settings to defaults?"; title = "Reset Settings"; }
            else if (AppSettings.Language == "日本語") { message = "すべての設定をデフォルトに戻してもよろしいですか？"; title = "設定のリセット"; }
            else if (AppSettings.Language == "中文") { message = "您确定要将所有设置恢复为默认值吗？"; title = "重置设置"; }
            else if (AppSettings.Language == "Español") { message = "¿Estás seguro de que deseas restablecer todas las configuraciones a los valores predeterminados?"; title = "Restablecer configuración"; }
            else if (AppSettings.Language == "Français") { message = "Êtes-vous sûr de vouloir réinitialiser tous les paramètres aux valeurs par défaut ?"; title = "Réinitialiser les paramètres"; }
            else if (AppSettings.Language == "Deutsch") { message = "Möchten Sie wirklich alle Einstellungen auf die Standardwerte zurücksetzen?"; title = "Einstellungen zurücksetzen"; }

            MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _isInitializing = true;

            // 모드별 개별 설정을 모두 기본값으로 안전하게 초기화
            AppSettings.WaveMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            AppSettings.PadMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            AppSettings.CircleMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
            AppSettings.OutlineMode = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };

            AppSettings.VisualMode = 0;
            AppSettings.SoundMode = 2;
            
            AppSettings.IsAdminMode = false;
            AppSettings.VisualModeHotkey = 0x72; // F3
            AppSettings.StereoUpmixHotkey = 0x71; // F2

            AppSettings.ShowAmbient = true;
            AppSettings.ShowSpeech = true;
            AppSettings.ShowDanger = true;
            AppSettings.ColorAmbient = "#FFFFFFFF";
            AppSettings.ColorSpeech = "#FFFFFF00";
            AppSettings.ColorDanger = "#FFFF0000";

            LoadSettingsToUI();
            AppSettings.Save();

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
    }
}


