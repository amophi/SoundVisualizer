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
            foreach (var key in _hotkeys.Keys)
            {
                CmbVisualHotkey.Items.Add(key);
                CmbSoundModeHotkey.Items.Add(key);
            }

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
                if (BtnStop != null) BtnStop.Content = "실행 중단";
                TabSettings.Header = "설정";
                TxtScreenSettings.Text = "화면 설정";
                TxtIntensityLabel.Text = "크기";
                TxtIntensityDesc.Text = "그래픽이 화면을 덮는 전체적인 크기와 길이를 조절합니다.";
                TxtSpeedLabel.Text = "속도";
                TxtSpeedDesc.Text = "오버레이가 소리의 방향을 따라가는 속도를 조절합니다.";
                TxtSensitivityLabel.Text = "민감도";
                TxtSensitivityDesc.Text = "작은 소리에도 그래픽이 얼마나 민감하게 반응할지 결정합니다.";
                TxtOpacityLabel.Text = "투명도";
                TxtOpacityDesc.Text = "그래픽의 투명도를 조절하여 배경의 비침 정도를 결정합니다.";
                TxtModeSettings.Text = "모드 설정";
                TxtVisualModeLabel.Text = "표현 모드";
                TxtVisualModeDesc.Text = "화면에 그려질 그래픽의 형태를 선택합니다.";
                CmbVisualModeWave.Content = "파도";
                CmbVisualModePad.Content = "패드";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "원형";
                TxtSoundModeLabel.Text = "사운드 모드";
                TxtSoundModeDesc.Text = "스피커 환경에 맞는 사운드 채널을 선택합니다.\n2채널 환경은 '2 채널', 서라운드 환경은 '5.1 채널' 또는 '7.1 채널'을 선택하세요.";
                // ChkStereoUpmix.Content = "켜기";
                TxtHotkeySettings.Text = "단축키";
                TxtVisualHotkeyLabel.Text = "표현 모드 전환";
                TxtVisualHotkeyDesc.Text = "실행 중 형태를 실시간으로 변경할 단축키입니다.";
                TxtSoundModeHotkeyLabel.Text = "사운드 모드 전환";
                TxtSoundModeHotkeyDesc.Text = "실행 중 사운드 모드를 실시간으로 변경할 단축키입니다.";
                TxtAdminSettings.Text = "고급 설정";
                TxtAdminModeLabel.Text = "개발자 모드";
                TxtAdminModeDesc.Text = "디버그용 정보 및 오디오 엔진 상태를 화면에 표시합니다.";
                ChkAdminMode.Content = "켜기";
                BtnReset.Content = "기본값으로 되돌리기";
                TabHelp.Header = "도움말";
                TxtHelp1Title.Text = "사운드 모드";
                TxtHelp1Desc.Text = "정상적인 방향성(레이더) 작동을 위해서는 윈도우 소리 설정에서 출력 장치가 '5.1 서라운드' (6채널) 또는 '7.1 서라운드' (8채널)로 구성되어 있어야 합니다. 일반 스테레오(2채널) 환경인 경우 시각화 그래픽이 좌/우에만 나타날 수 있으며, 이를 보완하려면 설정 탭에서 '사운드 모드'를 알맞게 설정해 주세요.";
                TxtHelp2Title.Text = "실시간 단축키 제어";
                TxtHelp2Desc.Text = "오버레이가 화면에 떠 있는 상태에서도, 백그라운드에서 지정된 단축키(기본 F2, F3)를 누르면 실시간으로 형태와 모드가 즉시 전환됩니다.";
                TxtHelp3Title.Text = "오버레이 종료 방법";
                TxtHelp3Desc.Text = "종료하려면 홈 탭의 '실행 중단' 버튼을 누르거나, 이 런처 창 상단의 ✕ 버튼을 클릭하세요.";
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
                TxtScreenSettings.Text = "Screen Settings";
                TxtIntensityLabel.Text = "Intensity";
                TxtIntensityDesc.Text = "Adjusts the overall size and length of the graphics on the screen.";
                TxtSpeedLabel.Text = "Speed";
                TxtSpeedDesc.Text = "Controls the speed at which the overlay follows the direction of the sound.";
                TxtSensitivityLabel.Text = "Sensitivity";
                TxtSensitivityDesc.Text = "Determines how sensitively the graphics react to soft sounds.";
                TxtOpacityLabel.Text = "Opacity";
                TxtOpacityDesc.Text = "Adjusts the graphic transparency to reveal the background behind it.";
                TxtModeSettings.Text = "Mode Settings";
                TxtVisualModeLabel.Text = "Visual Mode";
                TxtVisualModeDesc.Text = "Selects the shape of the graphics drawn on the screen.";
                CmbVisualModeWave.Content = "Wave";
                CmbVisualModePad.Content = "Pad";                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Cercle";                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Circle";
                TxtSoundModeLabel.Text = "Sound Mode";
                TxtSoundModeDesc.Text = "Select the sound channel suitable for your speaker environment.\nFor 2-channel environments, select '2 Channels', and for surround environments, select '5.1 Channels' or '7.1 Channels'.";
                // ChkStereoUpmix.Content = "Enable";
                TxtHotkeySettings.Text = "Hotkeys";
                TxtVisualHotkeyLabel.Text = "Visual Mode Toggle";
                TxtVisualHotkeyDesc.Text = "Hotkey to change the visual shape in real-time.";
                TxtSoundModeHotkeyLabel.Text = "Sound Mode Toggle";
                TxtSoundModeHotkeyDesc.Text = "Hotkey to toggle sound mode in real-time.";
                TxtAdminSettings.Text = "Advanced Settings";
                TxtAdminModeLabel.Text = "Developer Mode";
                TxtAdminModeDesc.Text = "Displays debug information and audio engine status on screen.";
                ChkAdminMode.Content = "Enable";
                BtnReset.Content = "Reset to Defaults";
                TabHelp.Header = "Help";
                TxtHelp1Title.Text = "Sound Mode";
                TxtHelp1Desc.Text = "For proper directional (radar) operation, your Windows sound output device must be configured as '5.1 Surround' (6 channels) or '7.1 Surround' (8 channels). In a standard stereo (2-channel) environment, the visualization may only appear on the left/right. To compensate for this, set the 'Sound Mode' appropriately in the settings.";
                TxtHelp2Title.Text = "Real-time Hotkeys";
                TxtHelp2Desc.Text = "Even while the overlay is on screen, you can press the designated hotkeys (Default F2, F3) in the background to switch shapes and modes in real-time.";
                TxtHelp3Title.Text = "How to Close Overlay";
                TxtHelp3Desc.Text = "To close, click 'Stop' on the Home tab, or click the ✕ button at the top of this launcher window.";
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
                TxtScreenSettings.Text = "画面設定";
                TxtIntensityLabel.Text = "サイズ";
                TxtIntensityDesc.Text = "画面を覆うグラフィックの全体的なサイズと長さを調整します。";
                TxtSpeedLabel.Text = "速度";
                TxtSpeedDesc.Text = "オーバーレイが音の方向を追う速度を調整します。";
                TxtSensitivityLabel.Text = "感度";
                TxtSensitivityDesc.Text = "小さな音に対してグラフィックがどれだけ敏感に反応するかを決定します。";
                TxtOpacityLabel.Text = "不透明度";
                TxtOpacityDesc.Text = "グラフィックの透明度を調整して背景の透け具合を決定します。";
                TxtModeSettings.Text = "モード設定";
                TxtVisualModeLabel.Text = "表現モード";
                TxtVisualModeDesc.Text = "画面に描画されるグラフィックの形状を選択します。";
                CmbVisualModeWave.Content = "波";
                CmbVisualModePad.Content = "パッド";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "円形 (Circle)";
                TxtSoundModeLabel.Text = "サウンドモード";
                TxtSoundModeDesc.Text = "スピーカー環境に合ったサウンドチャンネルを選択します。\n2チャンネル環境の場合は「2チャンネル」を、サラウンド環境の場合は「5.1チャンネル」または「7.1チャンネル」を選択してください。";
                // ChkStereoUpmix.Content = "オン";
                TxtHotkeySettings.Text = "ショートカットキー";
                TxtVisualHotkeyLabel.Text = "表現モードの切り替え";
                TxtVisualHotkeyDesc.Text = "実行中に形状をリアルタイムで変更するショートカットです。";
                TxtSoundModeHotkeyLabel.Text = "サウンドモードの切り替え";
                TxtSoundModeHotkeyDesc.Text = "実行中にサウンドモードをリアルタイムで変更するショートカットです。";
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
                TxtHelp3Desc.Text = "終了するには、ホームタブの「停止」ボタンを押すか、このランチャーウィンドウ上部の ✕ ボタンをクリックしてください。";
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
                TxtScreenSettings.Text = "画面设置";
                TxtIntensityLabel.Text = "大小";
                TxtIntensityDesc.Text = "调整覆盖屏幕的图形的整体大小和长度。";
                TxtSpeedLabel.Text = "速度";
                TxtSpeedDesc.Text = "调整覆盖层跟随声音方向的速度。";
                TxtSensitivityLabel.Text = "灵敏度";
                TxtSensitivityDesc.Text = "决定图形对微小声音的反应敏锐程度。";
                TxtOpacityLabel.Text = "不透明度";
                TxtOpacityDesc.Text = "调整图形的透明度以决定背景的可见程度。";
                TxtModeSettings.Text = "模式设置";
                TxtVisualModeLabel.Text = "表现模式";
                TxtVisualModeDesc.Text = "选择在屏幕上绘制的图形的形状。";
                CmbVisualModeWave.Content = "波浪";
                CmbVisualModePad.Content = "面板";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "圆形 (Circle)";
                TxtSoundModeLabel.Text = "声音模式";
                TxtSoundModeDesc.Text = "选择适合您扬声器环境的声音声道。\n对于双声道环境，请选择“2 声道”，对于环绕声环境，请选择“5.1 声道”或“7.1 声道”。";
                // ChkStereoUpmix.Content = "开启";
                TxtHotkeySettings.Text = "快捷键";
                TxtVisualHotkeyLabel.Text = "切换表现模式";
                TxtVisualHotkeyDesc.Text = "在运行中实时改变形状的快捷键。";
                TxtSoundModeHotkeyLabel.Text = "切换声音模式";
                TxtSoundModeHotkeyDesc.Text = "在运行中实时改变声音模式的快捷键。";
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
                TxtHelp3Desc.Text = "要关闭，请点击“主页”选项卡中的“停止”，或点击此启动器窗口顶部的 ✕ 按钮即可一并关闭。";
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
                TxtScreenSettings.Text = "Ajustes de pantalla";
                TxtIntensityLabel.Text = "Tamaño";
                TxtIntensityDesc.Text = "Ajusta el tamaño y longitud general de los gráficos en la pantalla.";
                TxtSpeedLabel.Text = "Velocidad";
                TxtSpeedDesc.Text = "Controla la velocidad a la que la superposición sigue la dirección del sonido.";
                TxtSensitivityLabel.Text = "Sensibilidad";
                TxtSensitivityDesc.Text = "Determina la sensibilidad de los gráficos ante pequeños sonidos.";
                TxtOpacityLabel.Text = "Opacidad";
                TxtOpacityDesc.Text = "Ajusta la transparencia de los gráficos para determinar la visibilidad del fondo.";
                TxtModeSettings.Text = "Ajustes de modo";
                TxtVisualModeLabel.Text = "Modo visual";
                TxtVisualModeDesc.Text = "Selecciona la forma de los gráficos en pantalla.";
                CmbVisualModeWave.Content = "Ola";
                CmbVisualModePad.Content = "Pad";                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Círculo";                TxtSoundModeLabel.Text = "Modo de Sonido";
                TxtSoundModeDesc.Text = "Seleccione el canal de sonido adecuado para su entorno de altavoces.\nPara entornos de 2 canales, seleccione '2 Canales', y para entornos envolventes, seleccione '5.1 Canales' o '7.1 Canales'.";
                // ChkStereoUpmix.Content = "Activar";
                TxtHotkeySettings.Text = "Atajos";
                TxtVisualHotkeyLabel.Text = "Cambiar modo visual";
                TxtVisualHotkeyDesc.Text = "Atajo para cambiar la forma en tiempo real durante la ejecución.";
                TxtSoundModeHotkeyLabel.Text = "Cambiar modo de sonido";
                TxtSoundModeHotkeyDesc.Text = "Atajo para cambiar el modo de sonido en tiempo real durante la ejecución.";
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
                TxtHelp3Desc.Text = "Para cerrar, haga clic en 'Detener' en la pestaña Inicio, o haga clic en el botón ✕ en la parte superior de esta ventana.";
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
                TxtScreenSettings.Text = "Paramètres d'écran";
                TxtIntensityLabel.Text = "Taille";
                TxtIntensityDesc.Text = "Ajuste la taille et la longueur globales des graphiques à l'écran.";
                TxtSpeedLabel.Text = "Vitesse";
                TxtSpeedDesc.Text = "Contrôle la vitesse à laquelle la superposition suit la direction du son.";
                TxtSensitivityLabel.Text = "Sensibilité";
                TxtSensitivityDesc.Text = "Détermine la sensibilité de réaction des graphiques aux petits sons.";
                TxtOpacityLabel.Text = "Opacité";
                TxtOpacityDesc.Text = "Ajuste la transparence des graphiques pour déterminer la visibilité de l'arrière-plan.";
                TxtModeSettings.Text = "Paramètres de mode";
                TxtVisualModeLabel.Text = "Mode visuel";
                TxtVisualModeDesc.Text = "Sélectionne la forme des graphiques dessinés à l'écran.";
                CmbVisualModeWave.Content = "Vague";
                CmbVisualModePad.Content = "Pad";
                TxtSoundModeLabel.Text = "Mode Sonore";
                TxtSoundModeDesc.Text = "Sélectionnez le canal audio adapté à votre environnement de haut-parleurs.\nPour les environnements à 2 canaux, sélectionnez '2 Canaux', et pour les environnements surround, sélectionnez '5.1 Canaux' ou '7.1 Canaux'.";
                // ChkStereoUpmix.Content = "Activer";
                TxtHotkeySettings.Text = "Raccourcis";
                TxtVisualHotkeyLabel.Text = "Basculer le mode visuel";
                TxtVisualHotkeyDesc.Text = "Raccourci pour changer la forme en temps réel pendant l'exécution.";
                TxtSoundModeHotkeyLabel.Text = "Basculer le mode sonore";
                TxtSoundModeHotkeyDesc.Text = "Raccourci pour changer le mode sonore en temps réel pendant l'exécution.";
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
                TxtHelp3Desc.Text = "Pour fermer, cliquez sur 'Arrêter' dans l'onglet Accueil, ou cliquez sur le bouton ✕ en haut de cette fenêtre.";
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
                TxtScreenSettings.Text = "Bildeinstellungen";
                TxtIntensityLabel.Text = "Größe";
                TxtIntensityDesc.Text = "Passt die Gesamtgröße und Länge der Grafiken auf dem Bildschirm an.";
                TxtSpeedLabel.Text = "Geschwindigkeit";
                TxtSpeedDesc.Text = "Steuert die Geschwindigkeit, mit der das Overlay der Schallrichtung folgt.";
                TxtSensitivityLabel.Text = "Empfindlichkeit";
                TxtSensitivityDesc.Text = "Bestimmt, wie empfindlich die Grafiken auf leise Töne reagieren.";
                TxtOpacityLabel.Text = "Deckkraft";
                TxtOpacityDesc.Text = "Passt die Transparenz der Grafiken an, um die Sichtbarkeit des Hintergrunds zu bestimmen.";
                TxtModeSettings.Text = "Moduseinstellungen";
                TxtVisualModeLabel.Text = "Visueller Modus";
                TxtVisualModeDesc.Text = "Wählt die Form der auf dem Bildschirm gezeichneten Grafiken aus.";
                CmbVisualModeWave.Content = "Welle";
                CmbVisualModePad.Content = "Pad";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Kreis";
                TxtSoundModeLabel.Text = "Sound-Modus";
                TxtSoundModeDesc.Text = "Wählen Sie den für Ihre Lautsprecherumgebung geeigneten Audiokanal.\nWählen Sie für 2-Kanal-Umgebungen '2 Kanäle' und für Surround-Umgebungen '5.1 Kanäle' oder '7.1 Kanäle'.";
                // ChkStereoUpmix.Content = "Aktivieren";
                TxtHotkeySettings.Text = "Tastenkombinationen";
                TxtVisualHotkeyLabel.Text = "Visuellen Modus umschalten";
                TxtVisualHotkeyDesc.Text = "Tastenkombination zum Ändern der Form in Echtzeit während der Ausführung.";
                TxtSoundModeHotkeyLabel.Text = "Sound-Modus umschalten";
                TxtSoundModeHotkeyDesc.Text = "Tastenkombination zum Ändern des Sound-Modus in Echtzeit während der Ausführung.";
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
                TxtHelp3Desc.Text = "Zum Schließen klicken Sie auf der Registerkarte 'Startseite' auf 'Stoppen' oder klicken Sie auf die Schaltfläche ✕ oben in diesem Fenster.";
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
                TxtScreenSettings.Text = "Настройки экрана";
                TxtIntensityLabel.Text = "Размер";
                TxtIntensityDesc.Text = "Настраивает общий размер и длину графики на экране.";
                TxtSpeedLabel.Text = "Скорость";
                TxtSpeedDesc.Text = "Управляет скоростью, с которой оверлей следует за направлением звука.";
                TxtSensitivityLabel.Text = "Чувствительность";
                TxtSensitivityDesc.Text = "Определяет, насколько чувствительно графика реагирует на тихие звуки.";
                TxtOpacityLabel.Text = "Непрозрачность";
                TxtOpacityDesc.Text = "Настраивает прозрачность графики, чтобы определить видимость фона.";
                TxtModeSettings.Text = "Настройки режима";
                TxtVisualModeLabel.Text = "Визуальный режим";
                TxtVisualModeDesc.Text = "Выбирает форму графики, отображаемой на экране.";
                CmbVisualModeWave.Content = "Волна";
                CmbVisualModePad.Content = "Панель";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Круг";
                TxtSoundModeLabel.Text = "Режим звука";
                TxtSoundModeDesc.Text = "Выберите звуковой канал, подходящий для среды ваших динамиков.\nДля 2-канальной среды выберите «2 канала», а для объемного звука выберите «5.1 каналов» или «7.1 каналов».";
                TxtHotkeySettings.Text = "Горячие клавиши";
                TxtVisualHotkeyLabel.Text = "Смена виз. режима";
                TxtVisualHotkeyDesc.Text = "Горячая клавиша для изменения визуальной формы в реальном времени.";
                TxtSoundModeHotkeyLabel.Text = "Смена режима звука";
                TxtSoundModeHotkeyDesc.Text = "Горячая клавиша для переключения звукового режима в реальном времени.";
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
                TxtHelp3Desc.Text = "Чтобы закрыть, нажмите «Остановить» на вкладке Главная или нажмите кнопку ✕ в верхней части этого окна запуска.";
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
            SldIntensity.Value = AppSettings.WaveIntensity;
            SldSpeed.Value = AppSettings.WavePositionSpeed;
            SldSensitivity.Value = AppSettings.WaveSensitivity * 4.0; // 0~25 범위를 0~100으로 확장
            
            SldOpacity.Value = 100 - AppSettings.VisualOpacity; // 투명도 수치 반전 (100=완전 투명)
            SldGlowIntensity.Value = AppSettings.GlowIntensity;
            SldGlowIntensity.IsEnabled = AppSettings.IsGlowMode;
            SldGlowIntensity.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            TxtGlowIntensity.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeLabel.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeDesc.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;

            CmbVisualMode.SelectedIndex = AppSettings.VisualMode;
            if (SldCircleRadius != null && PanelCircleRadius != null)
            {
                SldCircleRadius.Value = AppSettings.CircleRadius;
                bool isCircle = AppSettings.VisualMode == 2;
                PanelCircleRadius.IsEnabled = isCircle;
                PanelCircleRadius.Opacity = isCircle ? 1.0 : 0.4;
            }

            CmbSoundMode.SelectedIndex = AppSettings.SoundMode;
            ChkGlowMode.IsChecked = AppSettings.IsGlowMode;
            ChkAdminMode.IsChecked = AppSettings.IsAdminMode;

            CmbVisualHotkey.SelectedItem = GetKeyName(AppSettings.VisualModeHotkey) ?? "F3";
            CmbSoundModeHotkey.SelectedItem = GetKeyName(AppSettings.StereoUpmixHotkey) ?? "F2";

            if (ChkShowAmbient != null)
            {
                ChkShowAmbient.IsChecked = AppSettings.ShowAmbient;
                ChkShowSpeech.IsChecked = AppSettings.ShowSpeech;
                ChkShowDanger.IsChecked = AppSettings.ShowDanger;

                SetColorButton(BtnColorAmbient, AppSettings.ColorAmbient);
                SetColorButton(BtnColorSpeech, AppSettings.ColorSpeech);
                SetColorButton(BtnColorDanger, AppSettings.ColorDanger);
            }
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

            if (sender == SldIntensity) AppSettings.WaveIntensity = SldIntensity.Value;
            else if (sender == SldSpeed) AppSettings.WavePositionSpeed = SldSpeed.Value;
            else if (sender == SldSensitivity) AppSettings.WaveSensitivity = SldSensitivity.Value / 4.0;
            else if (sender == SldOpacity) AppSettings.VisualOpacity = 100 - SldOpacity.Value;
            else if (sender == SldGlowIntensity) AppSettings.GlowIntensity = SldGlowIntensity.Value;
            else if (sender == SldCircleRadius) AppSettings.CircleRadius = SldCircleRadius.Value;
            else if (sender == CmbVisualMode) 
            {
                AppSettings.VisualMode = CmbVisualMode.SelectedIndex;
                if (PanelCircleRadius != null)
                {
                    bool isCircle = AppSettings.VisualMode == 2;
                    PanelCircleRadius.IsEnabled = isCircle;
                    PanelCircleRadius.Opacity = isCircle ? 1.0 : 0.4;
                }
            }
            else if (sender == CmbSoundMode) AppSettings.SoundMode = CmbSoundMode.SelectedIndex;
            else if (sender == ChkGlowMode) 
            {
                AppSettings.IsGlowMode = ChkGlowMode.IsChecked ?? false;
                SldGlowIntensity.IsEnabled = AppSettings.IsGlowMode;
                SldGlowIntensity.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
                TxtGlowIntensity.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeLabel.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
                TxtGlowModeDesc.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            }
            else if (sender == ChkAdminMode) AppSettings.IsAdminMode = ChkAdminMode.IsChecked ?? false;
            else if (sender == CmbVisualHotkey && CmbVisualHotkey.SelectedItem is string vKey && _hotkeys.TryGetValue(vKey, out int vCode)) AppSettings.VisualModeHotkey = vCode;
            else if (sender == CmbSoundModeHotkey && CmbSoundModeHotkey.SelectedItem is string sKey && _hotkeys.TryGetValue(sKey, out int sCode)) AppSettings.StereoUpmixHotkey = sCode;
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

                System.Windows.Media.Color currentMediaColor = System.Windows.Media.Colors.White;
                try { currentMediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColorHex); } catch { }

                var currentDrawingColor = System.Drawing.Color.FromArgb(currentMediaColor.A, currentMediaColor.R, currentMediaColor.G, currentMediaColor.B);

                using (var dialog = new System.Windows.Forms.ColorDialog())
                {
                    dialog.Color = currentDrawingColor;
                    dialog.FullOpen = true;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var newDrawingColor = dialog.Color;
                        var newMediaColor = System.Windows.Media.Color.FromArgb(newDrawingColor.A, newDrawingColor.R, newDrawingColor.G, newDrawingColor.B);
                        string hex = newMediaColor.ToString();

                        if (type == "Ambient") AppSettings.ColorAmbient = hex;
                        else if (type == "Speech") AppSettings.ColorSpeech = hex;
                        else if (type == "Danger") AppSettings.ColorDanger = hex;

                        LoadSettingsToUI();
                        AppSettings.Save();
                    }
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

            AppSettings.WaveIntensity = 50.0;
            AppSettings.WavePositionSpeed = 20.0;
            AppSettings.WaveSensitivity = 3.75;
            AppSettings.VisualOpacity = 50.0;
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


