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
            else
                CmbLanguage.SelectedIndex = 0; // KOR

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
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "언어";
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
                CmbVisualModeWave.Content = "파도";
                CmbVisualModePad.Content = "패드";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "원형";
                TxtStereoModeLabel.Text = "스테레오 모드";
                TxtStereoModeDesc.Text = "2채널을 기반으로 좌, 우측 소리만을 표현합니다.\n유튜브 영상, 음악 감상 등, 2채널 소스를 청취할 때 사용해 주세요.";
                ChkStereoUpmix.Content = "켜기";
                TxtHotkeySettings.Text = "단축키";
                TxtVisualHotkeyLabel.Text = "표현 모드 전환";
                TxtVisualHotkeyDesc.Text = "실행 중 형태를 실시간으로 변경할 단축키입니다.";
                TxtStereoHotkeyLabel.Text = "스테레오 모드 전환";
                TxtStereoHotkeyDesc.Text = "실행 중 모드를 실시간으로 변경할 단축키입니다.";
                TxtAdminSettings.Text = "고급 설정";
                TxtAdminModeLabel.Text = "관리자 모드";
                TxtAdminModeDesc.Text = "디버그용 정보 및 오디오 엔진 상태를 화면에 표시합니다.";
                ChkAdminMode.Content = "켜기";
                BtnReset.Content = "기본값으로 되돌리기";
                TabHelp.Header = "도움말";
                TxtHelp1Title.Text = "7.1 서라운드 환경";
                TxtHelp1Desc.Text = "정상적인 방향성(레이더) 작동을 위해서는 윈도우 소리 설정에서 출력 장치가 '7.1 서라운드' (8채널)로 구성되어 있어야 합니다. 일반 스테레오(2채널) 환경인 경우 시각화 그래픽이 좌/우에만 나타날 수 있으며, 이를 보완하려면 설정 탭에서 '스테레오 모드' 기능을 켜주세요.";
                TxtHelp2Title.Text = "실시간 단축키 제어";
                TxtHelp2Desc.Text = "오버레이가 화면에 떠 있는 상태에서도, 백그라운드에서 지정된 단축키(기본 F2, F3)를 누르면 실시간으로 형태와 모드가 즉시 전환됩니다.";
                TxtHelp3Title.Text = "오버레이 종료 방법";
                TxtHelp3Desc.Text = "종료하려면 홈 탭의 '실행 중단' 버튼을 누르거나, 이 런처 창 상단의 ✕ 버튼을 클릭하세요.";
                TxtHelp4Title.Text = "AI 소리 분석 및 색상";
                TxtHelp4Desc.Text = "AI가 실시간으로 소리의 종류를 분석하여 화면에 라벨과 색상으로 표시합니다. 설정에서 각 소리 종류(환경음, 말소리, 강조음)별로 고유한 색상을 지정하여 직관적으로 구분할 수 있습니다.";
                TxtHelp5Title.Text = "관리자 모드와 디버깅";
                TxtHelp5Desc.Text = "관리자 모드를 활성화하면 현재 감지되는 상세 AI 라벨, 오디오 엔진의 실시간 채널 상태, FPS 등 기술적인 정보를 오버레이 화면에 추가로 표시합니다. 시스템의 정상 작동 여부를 확인하고 싶을 때 유용합니다.";
                
                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "AI 소리 분류 표시 설정";
                    ChkShowAmbient.Content = "환경음 표시";
                    ChkShowSpeech.Content = "말소리 표시";
                    ChkShowDanger.Content = "강조음 표시";
                }
                if (TxtStatus != null) SetStatusUI(_overlayWindow != null);
            }
            else if (lang == "English")
            {
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "Language";
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
                CmbVisualModePad.Content = "Pad";                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Cercle";                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Circle";
                TxtStereoModeLabel.Text = "Stereo Mode";
                TxtStereoModeDesc.Text = "Represents only left and right sounds based on 2 channels.\nPlease use this when listening to 2-channel sources such as YouTube videos or music.";
                ChkStereoUpmix.Content = "Enable";
                TxtHotkeySettings.Text = "Hotkeys";
                TxtVisualHotkeyLabel.Text = "Visual Mode Toggle";
                TxtVisualHotkeyDesc.Text = "Hotkey to change the visual shape in real-time.";
                TxtStereoHotkeyLabel.Text = "Stereo Mode Toggle";
                TxtStereoHotkeyDesc.Text = "Hotkey to toggle stereo mode in real-time.";
                TxtAdminSettings.Text = "Advanced Settings";
                TxtAdminModeLabel.Text = "Admin Mode";
                TxtAdminModeDesc.Text = "Displays debug information and audio engine status on screen.";
                ChkAdminMode.Content = "Enable";
                BtnReset.Content = "Reset to Defaults";
                TabHelp.Header = "Help";
                TxtHelp1Title.Text = "7.1 Surround Environment";
                TxtHelp1Desc.Text = "For proper directional (radar) operation, your Windows sound output device must be configured as '7.1 Surround' (8 channels). In a standard stereo (2-channel) environment, the visualization may only appear on the left/right. To compensate for this, enable 'Stereo Mode' in the settings.";
                TxtHelp2Title.Text = "Real-time Hotkeys";
                TxtHelp2Desc.Text = "Even while the overlay is on screen, you can press the designated hotkeys (Default F2, F3) in the background to switch shapes and modes in real-time.";
                TxtHelp3Title.Text = "How to Close Overlay";
                TxtHelp3Desc.Text = "To close, click 'Stop' on the Home tab, or click the ✕ button at the top of this launcher window.";
                TxtHelp4Title.Text = "AI Sound Analysis & Colors";
                TxtHelp4Desc.Text = "AI analyzes the type of sound in real-time and displays it with labels and colors. You can assign unique colors to each sound type (Ambient, Speech, Danger) in the settings for intuitive identification.";
                TxtHelp5Title.Text = "Admin Mode & Debugging";
                TxtHelp5Desc.Text = "Enabling Admin Mode displays technical information such as detailed AI labels, real-time audio engine channel status, and FPS on the overlay. Useful for checking system operation.";
                
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
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "言語";
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
                TxtSpeedDesc.Text = "波が画面の端に向かって広がる速度を調整します。";
                TxtSensitivityLabel.Text = "感度";
                TxtSensitivityDesc.Text = "小さな音に対してグラフィックがどれだけ敏感に反応するかを決定します。";
                TxtAdvSensitivityLabel.Text = "感度 (高度)";
                TxtAdvSensitivityDesc.Text = "極限の応答性のために内部数値制限を解除します。";
                TxtOpacityLabel.Text = "不透明度";
                TxtOpacityDesc.Text = "グラフィックの透明度を調整して背景の透け具合を決定します。";
                TxtModeSettings.Text = "モード設定";
                TxtVisualModeLabel.Text = "表現モード";
                TxtVisualModeDesc.Text = "画面に描画されるグラフィックの基本形状を選択します。";
                CmbVisualModeWave.Content = "波";
                CmbVisualModePad.Content = "パッド";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "円形 (Circle)";
                TxtStereoModeLabel.Text = "ステレオモード";
                TxtStereoModeDesc.Text = "2チャンネルに基づいて左右の音のみを表現します。\nYouTube動画や音楽鑑賞など、2チャンネルソースを聴く際に使用してください。";
                ChkStereoUpmix.Content = "オン";
                TxtHotkeySettings.Text = "ショートカットキー";
                TxtVisualHotkeyLabel.Text = "表現モードの切り替え";
                TxtVisualHotkeyDesc.Text = "実行中に形状をリアルタイムで変更するショートカットです。";
                TxtStereoHotkeyLabel.Text = "ステレオモードの切り替え";
                TxtStereoHotkeyDesc.Text = "実行中にモードをリアルタイムで変更するショートカットです。";
                TxtAdminSettings.Text = "詳細設定";
                TxtAdminModeLabel.Text = "管理者モード";
                TxtAdminModeDesc.Text = "デバッグ情報とオーディオエンジンの状態を画面に表示します。";
                ChkAdminMode.Content = "オン";
                BtnReset.Content = "デフォルトに戻す";
                TabHelp.Header = "ヘルプ";
                TxtHelp1Title.Text = "7.1 サラウンド環境";
                TxtHelp1Desc.Text = "正常な方向性（レーダー）動作のためには、Windowsのサウンド設定で出力デバイスが「7.1 サラウンド」（8チャンネル）に構成されている必要があります。通常のステレオ（2チャンネル）環境の場合、視覚化グラフィックが左右にのみ表示されることがあります。これを補完するには、設定タブで「ステレオモード」機能をオンにしてください。";
                TxtHelp2Title.Text = "リアルタイムショートカットキー制御";
                TxtHelp2Desc.Text = "オーバーレイが画面に表示されている状態でも、バックグラウンドで指定されたショートカットキー（デフォルト F2、F3）を押すと、リアルタイムで形状とモードが即座に切り替わります。";
                TxtHelp3Title.Text = "オーバーレイの終了方法";
                TxtHelp3Desc.Text = "終了するには、ホームタブの「停止」ボタンを押すか、このランチャーウィンドウ上部の ✕ ボタンをクリックしてください。";
                TxtHelp4Title.Text = "AI音声分析と色";
                TxtHelp4Desc.Text = "AIがリアルタイムで音の種類を分析し、ラベルと色で表示します。設定で各音の種類（環境音、音声、強調音）ごとに固有の色を指定して、直感的に識別できます。";
                TxtHelp5Title.Text = "管理者モードとデバッグ";
                TxtHelp5Desc.Text = "管理者モードを有効にすると、詳細なAIラベル、オーディオエンジンのリアルタイムチャンネル状態、FPSなどの技術情報をオーバーレイに表示します。システムの動作確認に便利です。";

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
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "语言";
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
                TxtSpeedDesc.Text = "调整波纹向屏幕边缘扩散的速度。";
                TxtSensitivityLabel.Text = "灵敏度";
                TxtSensitivityDesc.Text = "决定图形对微小声音的反应敏锐程度。";
                TxtAdvSensitivityLabel.Text = "灵敏度 (高级)";
                TxtAdvSensitivityDesc.Text = "为了达到极限响应，解除内部数值限制。";
                TxtOpacityLabel.Text = "不透明度";
                TxtOpacityDesc.Text = "调整图形的透明度以决定背景的可见程度。";
                TxtModeSettings.Text = "模式设置";
                TxtVisualModeLabel.Text = "表现模式";
                TxtVisualModeDesc.Text = "选择在屏幕上绘制的图形的基本形状。";
                CmbVisualModeWave.Content = "波浪";
                CmbVisualModePad.Content = "面板";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "圆形 (Circle)";
                TxtStereoModeLabel.Text = "立体声模式";
                TxtStereoModeDesc.Text = "基于双声道仅表现左右两侧的声音。\n在观看 YouTube 视频、欣赏音乐等收听双声道音源时请使用。";
                ChkStereoUpmix.Content = "开启";
                TxtHotkeySettings.Text = "快捷键";
                TxtVisualHotkeyLabel.Text = "切换表现模式";
                TxtVisualHotkeyDesc.Text = "在运行中实时改变形状的快捷键。";
                TxtStereoHotkeyLabel.Text = "切换立体声模式";
                TxtStereoHotkeyDesc.Text = "在运行中实时改变模式的快捷键。";
                TxtAdminSettings.Text = "高级设置";
                TxtAdminModeLabel.Text = "管理员模式";
                TxtAdminModeDesc.Text = "在屏幕上显示调试信息和音频引擎状态。";
                ChkAdminMode.Content = "开启";
                BtnReset.Content = "恢复默认值";
                TabHelp.Header = "帮助";
                TxtHelp1Title.Text = "7.1 环绕声环境";
                TxtHelp1Desc.Text = "为了使方向性（雷达）正常工作，在Windows声音设置中，输出设备必须配置为“7.1 环绕声”（8声道）。在普通立体声（2声道）环境中，可视化图形可能仅显示在左右两侧。要弥补这一点，请在设置选项卡中开启“立体声模式”功能。";
                TxtHelp2Title.Text = "实时快捷键控制";
                TxtHelp2Desc.Text = "即使在屏幕上显示悬浮窗，在后台按下指定的快捷键（默认 F2、F3），也会实时立即切换形状和模式。";
                TxtHelp3Title.Text = "关闭悬浮窗的方法";
                TxtHelp3Desc.Text = "要关闭，请点击“主页”选项卡中的“停止”，或点击此启动器窗口顶部的 ✕ 按钮即可一并关闭。";
                TxtHelp4Title.Text = "AI 声音分析与颜色";
                TxtHelp4Desc.Text = "AI 实时分析声音类型，并以标签和颜色显示。您可以在设置中为每种声音类型（环境音、语音、强调音）指定独特的颜色，以便直观区分。";
                TxtHelp5Title.Text = "管理员模式与调试";
                TxtHelp5Desc.Text = "启用管理员模式后，将在悬浮窗上显示详细的 AI 标签、音频引擎实时通道状态和 FPS 等技术信息。适用于检查系统运行状态。";

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
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "Idioma";
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
                TxtSpeedDesc.Text = "Ajusta la velocidad a la que las ondas se propagan hacia el borde de la pantalla.";
                TxtSensitivityLabel.Text = "Sensibilidad";
                TxtSensitivityDesc.Text = "Determina la sensibilidad de los gráficos ante pequeños sonidos.";
                TxtAdvSensitivityLabel.Text = "Sensibilidad (Avanzado)";
                TxtAdvSensitivityDesc.Text = "Elimina los límites internos para una reactividad extrema.";
                TxtOpacityLabel.Text = "Opacidad";
                TxtOpacityDesc.Text = "Ajusta la transparencia de los gráficos para determinar la visibilidad del fondo.";
                TxtModeSettings.Text = "Ajustes de modo";
                TxtVisualModeLabel.Text = "Modo visual";
                TxtVisualModeDesc.Text = "Selecciona la forma básica de los gráficos en pantalla.";
                CmbVisualModeWave.Content = "Ola";
                CmbVisualModePad.Content = "Pad";                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Círculo";                TxtStereoModeLabel.Text = "Modo Estéreo";
                TxtStereoModeDesc.Text = "Representa solo los sonidos izquierdo y derecho en base a 2 canales.\nÚselo al escuchar fuentes de 2 canales, como videos de YouTube o música.";
                ChkStereoUpmix.Content = "Activar";
                TxtHotkeySettings.Text = "Atajos";
                TxtVisualHotkeyLabel.Text = "Cambiar modo visual";
                TxtVisualHotkeyDesc.Text = "Atajo para cambiar la forma en tiempo real durante la ejecución.";
                TxtStereoHotkeyLabel.Text = "Cambiar modo estéreo";
                TxtStereoHotkeyDesc.Text = "Atajo para cambiar de modo en tiempo real durante la ejecución.";
                TxtAdminSettings.Text = "Configuración avanzada";
                TxtAdminModeLabel.Text = "Modo administrador";
                TxtAdminModeDesc.Text = "Muestra información de depuración y el estado del motor de audio en pantalla.";
                ChkAdminMode.Content = "Activar";
                BtnReset.Content = "Restablecer por defecto";
                TabHelp.Header = "Ayuda";
                TxtHelp1Title.Text = "Entorno envolvente 7.1";
                TxtHelp1Desc.Text = "Para que la direccionalidad (radar) funcione correctamente, tu dispositivo de salida de sonido de Windows debe estar configurado como 'Envolvente 7.1' (8 canales). En un entorno estéreo normal (2 canales), la visualización gráfica puede aparecer solo a la izquierda/derecha. Para compensarlo, activa la función 'Modo Estéreo' en los ajustes.";
                TxtHelp2Title.Text = "Control de atajos en tiempo real";
                TxtHelp2Desc.Text = "Incluso con la superposición en pantalla, si presionas los atajos asignados (por defecto F2, F3) en segundo plano, la forma y el modo cambiarán instantáneamente en tiempo real.";
                TxtHelp3Title.Text = "Cómo cerrar la superposición";
                TxtHelp3Desc.Text = "Para cerrar, haga clic en 'Detener' en la pestaña Inicio, o haga clic en el botón ✕ en la parte superior de esta ventana.";
                TxtHelp4Title.Text = "Análisis de Sonido AI y Colores";
                TxtHelp4Desc.Text = "La IA analiza el tipo de sonido en tiempo real y lo muestra con etiquetas y colores. Puede asignar colores únicos a cada tipo de sonido (Ambiental, Voz, Peligro) en los ajustes para una identificación intuitiva.";
                TxtHelp5Title.Text = "Modo Admin y Depuración";
                TxtHelp5Desc.Text = "Al activar el Modo Admin, se muestra información técnica como etiquetas de IA detalladas, estado de los canales del motor de audio en tiempo real y FPS en la superposición. Útil para verificar el funcionamiento del sistema.";

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
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "Langue";
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
                TxtSpeedDesc.Text = "Ajuste la vitesse à laquelle les ondes se propagent vers le bord de l'écran.";
                TxtSensitivityLabel.Text = "Sensibilité";
                TxtSensitivityDesc.Text = "Détermine la sensibilité de réaction des graphiques aux petits sons.";
                TxtAdvSensitivityLabel.Text = "Sensibilité (Avancé)";
                TxtAdvSensitivityDesc.Text = "Supprime les limites internes pour une réactivité extrême.";
                TxtOpacityLabel.Text = "Opacité";
                TxtOpacityDesc.Text = "Ajuste la transparence des graphiques pour déterminer la visibilité de l'arrière-plan.";
                TxtModeSettings.Text = "Paramètres de mode";
                TxtVisualModeLabel.Text = "Mode visuel";
                TxtVisualModeDesc.Text = "Sélectionne la forme de base des graphiques dessinés à l'écran.";
                CmbVisualModeWave.Content = "Vague";
                CmbVisualModePad.Content = "Pad";
                TxtStereoModeLabel.Text = "Mode Stéréo";
                TxtStereoModeDesc.Text = "Représente uniquement les sons gauche et droit basés sur 2 canaux.\nVeuillez l'utiliser lors de l'écoute de sources à 2 canaux telles que des vidéos YouTube ou de la musique.";
                ChkStereoUpmix.Content = "Activer";
                TxtHotkeySettings.Text = "Raccourcis";
                TxtVisualHotkeyLabel.Text = "Basculer le mode visuel";
                TxtVisualHotkeyDesc.Text = "Raccourci pour changer la forme en temps réel pendant l'exécution.";
                TxtStereoHotkeyLabel.Text = "Basculer le mode stéréo";
                TxtStereoHotkeyDesc.Text = "Raccourci pour changer de mode en temps réel pendant l'exécution.";
                TxtAdminSettings.Text = "Paramètres avancés";
                TxtAdminModeLabel.Text = "Mode Administrateur";
                TxtAdminModeDesc.Text = "Affiche les informations de débogage et l'état du moteur audio à l'écran.";
                ChkAdminMode.Content = "Activer";
                BtnReset.Content = "Réinitialiser";
                TabHelp.Header = "Aide";
                TxtHelp1Title.Text = "Environnement Surround 7.1";
                TxtHelp1Desc.Text = "Pour que la directivité (radar) fonctionne correctement, votre périphérique de sortie audio Windows doit être configuré en 'Surround 7.1' (8 canaux). Dans un environnement stéréo standard (2 canaux), la visualisation graphique peut n'apparaître qu'à gauche/droite. Pour compenser, activez la fonction 'Mode Stéréo' dans les paramètres.";
                TxtHelp2Title.Text = "Contrôle par raccourci en temps réel";
                TxtHelp2Desc.Text = "Même avec la superposition à l'écran, si vous appuyez sur les raccourcis définis (par défaut F2, F3) en arrière-plan, la forme et le mode changeront instantanément en temps réel.";
                TxtHelp3Title.Text = "Comment fermer la superposition";
                TxtHelp3Desc.Text = "Pour fermer, cliquez sur 'Arrêter' dans l'onglet Accueil, ou cliquez sur le bouton ✕ en haut de cette fenêtre.";
                TxtHelp4Title.Text = "Analyse Sonore IA & Couleurs";
                TxtHelp4Desc.Text = "L'IA analyse le type de son en temps réel et l'affiche avec des étiquettes et des couleurs. Vous pouvez attribuer des couleurs uniques à chaque type de son (Ambiance, Voix, Danger) dans les paramètres pour une identification intuitive.";
                TxtHelp5Title.Text = "Mode Admin & Débogage";
                TxtHelp5Desc.Text = "L'activation du mode Admin affiche des informations techniques telles que des étiquettes IA détaillées, l'état des canaux du moteur audio en temps réel et le FPS sur la superposition. Utile pour vérifier le fonctionnement du système.";

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
                if (TxtLanguageLabel != null) TxtLanguageLabel.Text = "Sprache";
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
                TxtSpeedDesc.Text = "Passt die Geschwindigkeit an, mit der sich die Wellen zum Bildschirmrand ausbreiten.";
                TxtSensitivityLabel.Text = "Empfindlichkeit";
                TxtSensitivityDesc.Text = "Bestimmt, wie empfindlich die Grafiken auf leise Töne reagieren.";
                TxtAdvSensitivityLabel.Text = "Empfindlichkeit (Erweitert)";
                TxtAdvSensitivityDesc.Text = "Hebt interne Beschränkungen für extreme Reaktionsfähigkeit auf.";
                TxtOpacityLabel.Text = "Deckkraft";
                TxtOpacityDesc.Text = "Passt die Transparenz der Grafiken an, um die Sichtbarkeit des Hintergrunds zu bestimmen.";
                TxtModeSettings.Text = "Moduseinstellungen";
                TxtVisualModeLabel.Text = "Visueller Modus";
                TxtVisualModeDesc.Text = "Wählt die Grundform der auf dem Bildschirm gezeichneten Grafiken aus.";
                CmbVisualModeWave.Content = "Welle";
                CmbVisualModePad.Content = "Pad";
                if (CmbVisualModeCircle != null) CmbVisualModeCircle.Content = "Kreis";
                TxtStereoModeLabel.Text = "Stereo-Modus";
                TxtStereoModeDesc.Text = "Stellt basierend auf 2 Kanälen nur den linken und rechten Ton dar.\nBitte verwenden Sie dies beim Hören von 2-Kanal-Quellen wie YouTube-Videos oder Musik.";
                ChkStereoUpmix.Content = "Aktivieren";
                TxtHotkeySettings.Text = "Tastenkombinationen";
                TxtVisualHotkeyLabel.Text = "Visuellen Modus umschalten";
                TxtVisualHotkeyDesc.Text = "Tastenkombination zum Ändern der Form in Echtzeit während der Ausführung.";
                TxtStereoHotkeyLabel.Text = "Stereo-Modus umschalten";
                TxtStereoHotkeyDesc.Text = "Tastenkombination zum Ändern des Modus in Echtzeit während der Ausführung.";
                TxtAdminSettings.Text = "Erweiterte Einstellungen";
                TxtAdminModeLabel.Text = "Administratormodus";
                TxtAdminModeDesc.Text = "Zeigt Debug-Informationen und den Status der Audio-Engine auf dem Bildschirm an.";
                ChkAdminMode.Content = "Aktivieren";
                BtnReset.Content = "Auf Standard zurücksetzen";
                TabHelp.Header = "Hilfe";
                TxtHelp1Title.Text = "7.1 Surround-Umgebung";
                TxtHelp1Desc.Text = "Damit die Richtwirkung (Radar) richtig funktioniert, muss Ihr Windows-Audioausgabegerät als '7.1 Surround' (8 Kanäle) konfiguriert sein. In einer Standard-Stereoumgebung (2 Kanäle) wird die Grafikvisualisierung möglicherweise nur links/rechts angezeigt. Um dies auszugleichen, aktivieren Sie in den Einstellungen die Funktion 'Stereo-Modus'.";
                TxtHelp2Title.Text = "Echtzeit-Tastenkombinationssteuerung";
                TxtHelp2Desc.Text = "Selbst wenn das Overlay auf dem Bildschirm angezeigt wird, ändern sich Form und Modus sofort in Echtzeit, wenn Sie im Hintergrund die zugewiesenen Tastenkombinationen (Standard F2, F3) drücken.";
                TxtHelp3Title.Text = "So schließen Sie das Overlay";
                TxtHelp3Desc.Text = "Zum Schließen klicken Sie auf der Registerkarte 'Startseite' auf 'Stoppen' oder klicken Sie auf die Schaltfläche ✕ oben in diesem Fenster.";
                TxtHelp4Title.Text = "KI-Soundanalyse & Farben";
                TxtHelp4Desc.Text = "Die KI analysiert die Art des Geräusches in Echtzeit und zeigt sie mit Beschriftungen und Farben an. Sie können jedem Geräuschtyp (Umgebung, Sprache, Gefahr) in den Einstellungen eindeutige Farben zur intuitiven Identifizierung zuweisen.";
                TxtHelp5Title.Text = "Admin-Modus & Debugging";
                TxtHelp5Desc.Text = "Das Aktivieren des Admin-Modus zeigt technische Informationen wie detaillierte KI-Labels, Echtzeit-Audiokanäle-Status und FPS auf dem Overlay an. Nützlich zur Überprüfung des Systembetriebs.";

                if (TxtAIDisplaySettings != null)
                {
                    TxtAIDisplaySettings.Text = "KI-Anzeigeeinstellungen";
                    ChkShowAmbient.Content = "Umgebungsgeräusche anzeigen";
                    ChkShowSpeech.Content = "Sprache anzeigen";
                    ChkShowDanger.Content = "Gefahrentöne anzeigen";
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
            SldGlowIntensity.Value = AppSettings.GlowIntensity;
            SldGlowIntensity.IsEnabled = AppSettings.IsGlowMode;
            SldGlowIntensity.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            TxtGlowIntensity.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeLabel.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;
            TxtGlowModeDesc.Opacity = AppSettings.IsGlowMode ? 1.0 : 0.4;

            CmbVisualMode.SelectedIndex = AppSettings.VisualMode;
            ChkStereoUpmix.IsChecked = AppSettings.IsStereoUpmixMode;
            ChkGlowMode.IsChecked = AppSettings.IsGlowMode;
            ChkAdminMode.IsChecked = AppSettings.IsAdminMode;

            CmbVisualHotkey.SelectedItem = GetKeyName(AppSettings.VisualModeHotkey) ?? "F3";
            CmbStereoHotkey.SelectedItem = GetKeyName(AppSettings.StereoUpmixHotkey) ?? "F2";

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
            else if (sender == SldAdvSensitivity && AppSettings.IsAdvancedSensitivity) AppSettings.WaveSensitivity = SldAdvSensitivity.Value;
            else if (sender == SldSensitivity && !AppSettings.IsAdvancedSensitivity) AppSettings.WaveSensitivity = SldSensitivity.Value / 4.0;
            else if (sender == SldOpacity) AppSettings.VisualOpacity = 100 - SldOpacity.Value;
            else if (sender == SldGlowIntensity) AppSettings.GlowIntensity = SldGlowIntensity.Value;
            else if (sender == CmbVisualMode) AppSettings.VisualMode = CmbVisualMode.SelectedIndex;
            else if (sender == ChkStereoUpmix) AppSettings.IsStereoUpmixMode = ChkStereoUpmix.IsChecked ?? false;
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
            else if (sender == CmbStereoHotkey && CmbStereoHotkey.SelectedItem is string sKey && _hotkeys.TryGetValue(sKey, out int sCode)) AppSettings.StereoUpmixHotkey = sCode;
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
            AppSettings.Save();
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
            AppSettings.IsStereoUpmixMode = false;
            AppSettings.IsAdvancedSensitivity = false;
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
    }
}
