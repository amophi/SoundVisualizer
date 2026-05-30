using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SoundVisualizer.CoreAudio; 
using SoundVisualizer.DSP;       
using SoundVisualizer.AIModel;   
using SoundVisualizer.Visualizers;

namespace SoundVisualizer
{
    public partial class MainWindow : Window
    {
        private bool _wasStereoHotkeyPressed = false;
        private bool _wasVisualHotkeyPressed = false;
        private bool _wasEditHotkeyPressed = false;
        private bool _isEditMode = false;

        private bool _isDraggingCircleRadius = false;
        private bool _isDraggingCircleIntensity = false;
        private bool _isDraggingRectIntensity = false;
        private Point _rectDragStartPos;
        private double _rectDragStartIntensity;


        private AudioCaptureEngine? _captureEngine;
        private AudioRouter? _audioRouter;
        private VectorCalculator? _vectorCalc;
        private SoundClassifier? _soundAI;

        // 전체 볼륨 및 위치 분포 관련 변수
        private double _smoothTotal;
        private double _distFL = 0.125, _distFR = 0.125, _distFC = 0.125, _distBL = 0.125, _distBR = 0.125, _distSL = 0.125, _distSR = 0.125, _distLFE = 0.125;
        private double _smoothFL, _smoothFR, _smoothFC, _smoothBL, _smoothBR, _smoothSL, _smoothSR, _smoothLFE;
        private float _targetFL, _targetFR, _targetFC, _targetBL, _targetBR, _targetSL, _targetSR, _targetLFE;
        private string _currentLabel = "";
        private double _animationTime = 0;
        private DateTime _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);
        private volatile int _lastSourceChannels = 0;  // 현재 소스 채널 수 (실시간)

        // WPF UI 속성 갱신 및 가비지 최소화용 캐시 필드
        private string? _cachedColorDangerHex;
        private string? _cachedColorSpeechHex;
        private string? _cachedColorAmbientHex;
        private SolidColorBrush? _dangerBrush;
        private SolidColorBrush? _speechBrush;
        private SolidColorBrush? _ambientBrush;
        private Color _dangerColor = Colors.Red;
        private Color _speechColor = Colors.Green;
        private Color _ambientColor = Colors.Blue;

        private Color _cachedActiveColor = Colors.Transparent;
        private int _cachedVisualModeForBrush = -1;

        private string _cachedVisualModeText = "";
        private string _cachedStereoModeText = "";
        private Brush? _cachedStereoModeForeground = null;
        private string _cachedFpsText = "";

        private Visibility _cachedAILabelBorderVisibility = Visibility.Collapsed;
        private string _cachedAILabelText = "";
        private Brush? _cachedAILabelForeground = null;
        private Visibility _cachedUnifiedWaveVisibility = Visibility.Collapsed;

        private Visibility _cachedStatusBorderVisibility = Visibility.Collapsed;
        private Visibility _cachedFpsBorderVisibility = Visibility.Collapsed;
        private Visibility _cachedModeUIStackVisibility = Visibility.Collapsed;
        
        // 다국어 라벨 템플릿 보관
        private string _rectModeLabelPrefix = "한계선";
        private string _rectSizeLabelPrefix = "크기: ";
        private string _circleRadiusLabelPrefix = "기본 반경: ";
        private string _circleIntensityLabelPrefix = "원형 크기: ";
        private string _visualModeUIPrefix = "시각화 모드: ";
        private string _stereoModeUIPrefix = "채널 모드: ";
        private string _editModeUIText = "오버레이 설정: ";
        private string[] _visualModeNames = { "파도", "패드", "원형", "외곽선" };
        private string[] _soundModeNames = { "2 채널", "5.1 채널", "7.1 채널" };
        private string[] _opacityFixedSizeLabels = { "파도 크기", "패드 크기", "원형 크기", "외곽선 두께" };

        // YAMNet 추론 스로틀링 제어 필드
        private readonly object _aiLock = new();
        private volatile bool _isPredicting = false;
        private long _lastPredictTimeTicks = 0;
        private const long AI_PREDICT_INTERVAL_MS = 250; // YAMNet 추론 주기 (250ms)

        // 시각화 모듈 (Strategy Pattern)
        private IVisualizerMode[] _visualizers = new IVisualizerMode[4];
        
        // 렌더링 최적화를 위한 재사용 객체
        private VisualizerContext _renderContext = new VisualizerContext();
        private double[] _channelDepths = new double[8];
        private readonly double[] _rmsBuffer = new double[8];
        private double[] _channelDists = new double[8];
        
        public Action? OnSettingsChangedFromHotkey;

        private readonly Stopwatch _renderStopwatch = new();
        private double _lastRenderTimeMs = 0;
        private long _lastChannelCountTick = 0;
        private long _lastHotkeyCheckTick = 0;
        private long _lastHudUpdateTick = 0;
        private bool _forceUpdateHUDTexts = true;
        
        private int _frameCount;
        private double _currentFps;
        private readonly Stopwatch _fpsStopwatch = new();

        private const int WAVE_SAMPLE_COUNT = 400;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettingsToEditPanel(); // UI 초기화 직후 설정값을 미리 반영하여, 지연된 레이아웃 패스에서 기본값(50)이 이벤트를 발생시켜도 올바른 값이 유지되도록 함
            _isUpdatingEditPanelSliders = false;
            ApplyClickThroughMagic();
            BootSequence();
            StartHighFpsRenderLoop();

            this.Closed += (s, e) =>
            {
                if (_topmostTimer != null)
                {
                    _topmostTimer.Stop();
                    _topmostTimer = null;
                }

                CompositionTarget.Rendering -= OnCompositionTargetRendering;
                
                if (_captureEngine != null)
                {
                    _captureEngine.OnAudioDataAvailable -= HandleAudioDataAsync;
                    _captureEngine.Dispose();
                }
                
                _audioRouter?.StopRouting();
                _soundAI?.Dispose();
            };
        }

        private void BootSequence()
        {
            _visualizers[0] = new WaveVisualizer();
            _visualizers[1] = new PadVisualizer();
            _visualizers[2] = new CircleRippleVisualizer();
            _visualizers[3] = new OutlineVisualizer();

            _vectorCalc = new VectorCalculator();
            _soundAI = new SoundClassifier();
            _captureEngine = new AudioCaptureEngine();
            _audioRouter = new AudioRouter();

            _captureEngine.OnChannelsChanged += (s, currentChannels) =>
            {
                Dispatcher.Invoke(() =>
                {
                    int autoSoundMode = currentChannels >= 8 ? 2 : (currentChannels >= 6 ? 1 : 0);
                    if (AppSettings.SoundMode != autoSoundMode)
                    {
                        AppSettings.SoundMode = autoSoundMode;
                        AppSettings.Save();
                        OnSettingsChangedFromHotkey?.Invoke(); // UI 업데이트 트리거
                    }
                });
            };

            _captureEngine.OnAudioDataAvailable += HandleAudioDataAsync;
            _captureEngine.StartCapture();

            if (_captureEngine.CaptureFormat != null)
            {
                int channels = _captureEngine.CaptureFormat.Channels;
                if (channels != 8)
                {
                    StatusText.Text = $"PC 오디오 설정: {channels}ch (스테레오)\n소스: 대기 중...";
                    StatusText.Foreground = Brushes.Orange;
                }
                else
                {
                    StatusText.Text = "PC 오디오 설정: 8ch (7.1)\n소스: 대기 중...";
                    StatusText.Foreground = Brushes.LimeGreen;
                }
                _audioRouter.StartRouting(_captureEngine.CaptureFormat);
            }
        }

        // ==========================================
        // 고프레임 렌더링 루프
        // ==========================================
        private void StartHighFpsRenderLoop()
        {
            _fpsStopwatch.Start();
            _renderStopwatch.Start();
            CompositionTarget.Rendering += OnCompositionTargetRendering;
        }

        private void OnCompositionTargetRendering(object? sender, EventArgs e)
        {
            double targetFps = Math.Max(30.0, AppSettings.TargetFps);
            double frameIntervalMs = 1000.0 / targetFps;

            double now = _renderStopwatch.Elapsed.TotalMilliseconds;
            double elapsed = now - _lastRenderTimeMs;

            if (elapsed >= frameIntervalMs)
            {
                _lastRenderTimeMs = now - (elapsed % frameIntervalMs);
                RenderFrame();
            }
        }

        private void RenderFrame()
        {
            long nowTicks = Environment.TickCount64;

            // 1. 핫키 체크 스로틀링 (약 30Hz - CPU 사용량 감소 및 P/Invoke 부하 최소화)
            if (nowTicks - _lastHotkeyCheckTick >= 33)
            {
                _lastHotkeyCheckTick = nowTicks;

                // 실시간 오버레이 편집 모드 토글
                bool isEditHotkeyPressed = IsHotkeyPressed(AppSettings.EditModeKeyBind);
                if (isEditHotkeyPressed && !_wasEditHotkeyPressed)
                {
                    ToggleEditMode(!_isEditMode);
                }
                _wasEditHotkeyPressed = isEditHotkeyPressed;

                // F3 키: 시각화 모드(Wave/Pad) 실시간 전환
                bool isVisualHotkeyPressed = IsHotkeyPressed(AppSettings.VisualModeKeyBind);
                if (isVisualHotkeyPressed && !_wasVisualHotkeyPressed)
                {
                    AppSettings.VisualMode = (AppSettings.VisualMode + 1) % _visualizers.Length;
                    AppSettings.Save();
                    OnSettingsChangedFromHotkey?.Invoke();
                    _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);
                    _forceUpdateHUDTexts = true;

                    if (_isEditMode)
                    {
                        LoadSettingsToEditPanel();
                        UpdateGuidelinePositions();
                    }
                }
                _wasVisualHotkeyPressed = isVisualHotkeyPressed;

                // F2 키: 스테레오 확장 모드 실시간 전환
                bool isStereoHotkeyPressed = IsHotkeyPressed(AppSettings.StereoUpmixKeyBind);
                if (isStereoHotkeyPressed && !_wasStereoHotkeyPressed)
                {
                    AppSettings.SoundMode = (AppSettings.SoundMode + 1) % 3;
                    AppSettings.Save();
                    OnSettingsChangedFromHotkey?.Invoke();
                    _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);
                    _forceUpdateHUDTexts = true;
                    
                    if (_isEditMode)
                    {
                        LoadSettingsToEditPanel();
                    }
                }
                _wasStereoHotkeyPressed = isStereoHotkeyPressed;
            }

            // 2. UI 텍스트 문자열 할당 최소화 (500ms마다 또는 강제 업데이트 필요 시에만 수행)
            if (nowTicks - _lastHudUpdateTick >= 500 || _forceUpdateHUDTexts)
            {
                _lastHudUpdateTick = nowTicks;
                _forceUpdateHUDTexts = false;

                string visualKeyName = GetKeysName(AppSettings.VisualModeKeyBind);
                string targetVisualModeText = $"{_visualModeUIPrefix}[{visualKeyName}] {_visualModeNames[AppSettings.VisualMode]}";
                if (_cachedVisualModeText != targetVisualModeText)
                {
                    _cachedVisualModeText = targetVisualModeText;
                    VisualModeText.Text = targetVisualModeText;
                }

                string stereoKeyName = GetKeysName(AppSettings.StereoUpmixKeyBind);
                string targetStereoModeText = $"{_stereoModeUIPrefix}[{stereoKeyName}] {_soundModeNames[AppSettings.SoundMode]}";
                Brush targetStereoForeground = AppSettings.SoundMode == 0 ? Brushes.Cyan : (AppSettings.SoundMode == 1 ? Brushes.Gold : Brushes.White);

                if (_cachedStereoModeText != targetStereoModeText)
                {
                    _cachedStereoModeText = targetStereoModeText;
                    StereoModeText.Text = targetStereoModeText;
                }
                if (_cachedStereoModeForeground != targetStereoForeground)
                {
                    _cachedStereoModeForeground = targetStereoForeground;
                    StereoModeText.Foreground = targetStereoForeground;
                }

                string editKeyName = GetKeysName(AppSettings.EditModeKeyBind);
                string targetEditModeText = $"{_editModeUIText}[{editKeyName}]";
                if (EditModeText != null && EditModeText.Text != targetEditModeText)
                {
                    EditModeText.Text = targetEditModeText;
                }
            }

            _frameCount++;
            double fpsElapsed = _fpsStopwatch.Elapsed.TotalSeconds;
            if (fpsElapsed >= 0.5)
            {
                _currentFps = _frameCount / fpsElapsed;
                _frameCount = 0;
                _fpsStopwatch.Restart();
                
                string targetFpsText = $"FPS: {_currentFps:F0}";
                if (_cachedFpsText != targetFpsText)
                {
                    _cachedFpsText = targetFpsText;
                    FpsText.Text = targetFpsText;
                }
            }

            _animationTime += 0.035;

            float totalTarget = _targetFL + _targetFR + _targetFC + _targetBL + _targetBR + _targetSL + _targetSR + _targetLFE;

            // 민감성(떨림): 전체 사운드 볼륨 크기를 따라가는 스무딩 팩터 (즉각적인 떨림)
            float sfTremor = (float)(Math.Max(0.1, AppSettings.WaveSensitivity) / 100.0);
            if (sfTremor > 1.0f) sfTremor = 1.0f;
            _smoothTotal += (totalTarget - _smoothTotal) * sfTremor;

            // 위치 변화 속도: 소리가 어디서 나는지 분포 채널 비율을 쫓아가는 팩터 (좌우 이동)
            float sfPosition = (float)(Math.Max(0.1, AppSettings.WavePositionSpeed) / 100.0);
            if (sfPosition > 1.0f) sfPosition = 1.0f;

            if (totalTarget > 0.0001f)
            {
                _distFL += ((_targetFL / totalTarget) - _distFL) * sfPosition;
                _distFR += ((_targetFR / totalTarget) - _distFR) * sfPosition;
                _distFC += ((_targetFC / totalTarget) - _distFC) * sfPosition;
                _distBL += ((_targetBL / totalTarget) - _distBL) * sfPosition;
                _distBR += ((_targetBR / totalTarget) - _distBR) * sfPosition;
                _distSL += ((_targetSL / totalTarget) - _distSL) * sfPosition;
                _distSR += ((_targetSR / totalTarget) - _distSR) * sfPosition;
                _distLFE += ((_targetLFE / totalTarget) - _distLFE) * sfPosition;
            }

            // 부드러워진 전체 볼륨 크기 * 부드러워진 각 채널의 비율
            _smoothFL = _smoothTotal * _distFL;
            _smoothFR = _smoothTotal * _distFR;
            _smoothFC = _smoothTotal * _distFC;
            _smoothBL = _smoothTotal * _distBL;
            _smoothBR = _smoothTotal * _distBR;
            _smoothSL = _smoothTotal * _distSL;
            _smoothSR = _smoothTotal * _distSR;
            _smoothLFE = _smoothTotal * _distLFE;

            bool isVisible = IsLabelVisible(_currentLabel);
            var activeColor = GetColorForLabel(_currentLabel);

            if (isVisible)
            {
                if (AppSettings.IsAdminMode)
                {
                    SetVisibilityIfChanged(AILabelBorder, ref _cachedAILabelBorderVisibility, Visibility.Visible);
                    
                    if (_cachedAILabelText != _currentLabel)
                    {
                        _cachedAILabelText = _currentLabel;
                        AILabelText.Text = _currentLabel;
                    }

                    SolidColorBrush activeBrush = GetBrushForLabel(_currentLabel);
                    if (_cachedAILabelForeground != activeBrush)
                    {
                        _cachedAILabelForeground = activeBrush;
                        AILabelText.Foreground = activeBrush;
                    }
                }
                else
                {
                    SetVisibilityIfChanged(AILabelBorder, ref _cachedAILabelBorderVisibility, Visibility.Collapsed);
                }
                SetVisibilityIfChanged(UnifiedWave, ref _cachedUnifiedWaveVisibility, Visibility.Visible);
            }
            else
            {
                SetVisibilityIfChanged(AILabelBorder, ref _cachedAILabelBorderVisibility, Visibility.Collapsed);
                SetVisibilityIfChanged(UnifiedWave, ref _cachedUnifiedWaveVisibility, Visibility.Collapsed);
            }

            Visibility targetModeUIStackVisibility = (DateTime.Now < _modeUIVisibleUntil && !_isEditMode) ? Visibility.Visible : Visibility.Collapsed;
            SetVisibilityIfChanged(ModeUIStack, ref _cachedModeUIStackVisibility, targetModeUIStackVisibility);

            if (AppSettings.IsAdminMode)
            {
                SetVisibilityIfChanged(StatusBorder, ref _cachedStatusBorderVisibility, Visibility.Collapsed);
                SetVisibilityIfChanged(FpsBorder, ref _cachedFpsBorderVisibility, Visibility.Visible);
            }
            else
            {
                SetVisibilityIfChanged(StatusBorder, ref _cachedStatusBorderVisibility, Visibility.Collapsed);
                SetVisibilityIfChanged(FpsBorder, ref _cachedFpsBorderVisibility, Visibility.Collapsed);
            }

            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (w == 0 || h == 0) return;

            // VisualizerContext 설정 (Intensity 100%가 화면 중앙 최대 한계선에 도달하도록 매핑)
            double maxBaseDepthRender = Math.Min(w, h) / 2.0 - 10;
            if (maxBaseDepthRender < 10) maxBaseDepthRender = 10;
            
            bool useOpacity = AppSettings.IntensityAsOpacity;
            double currentIntensity = useOpacity ? (AppSettings.OpacityFixedSize / 2.0) : AppSettings.WaveIntensity;
            double baseDepth = maxBaseDepthRender * (Math.Max(0.0, currentIntensity) / 100.0);
            
            if (AppSettings.SoundMode == 0)
            {
                // 0:상단중앙, 1:우상단, 2:우측중앙, 3:우하단, 4:하단중앙, 5:좌하단, 6:좌측중앙, 7:좌상단
                _channelDepths[0] = 0;
                _channelDepths[1] = 0;
                _channelDepths[2] = Math.Min(baseDepth, baseDepth * (useOpacity ? 1.0 : _smoothFR));
                _channelDepths[3] = 0;
                _channelDepths[4] = 0;
                _channelDepths[5] = 0;
                _channelDepths[6] = Math.Min(baseDepth, baseDepth * (useOpacity ? 1.0 : _smoothFL));
                _channelDepths[7] = 0;
            }
            else
            {
                double phantomBC = (_smoothBL + _smoothBR) / 2.0;
                double phantomDistBC = (_distBL + _distBR) / 2.0;
                
                _channelDepths[0] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distFC * 4.0) : _smoothFC));
                _channelDepths[1] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distFR * 4.0) : _smoothFR));
                _channelDepths[2] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distSR * 4.0) : _smoothSR));
                _channelDepths[3] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distBR * 4.0) : _smoothBR));
                _channelDepths[4] = Math.Min(baseDepth, baseDepth * (useOpacity ? (phantomDistBC * 4.0) : phantomBC));
                _channelDepths[5] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distBL * 4.0) : _smoothBL));
                _channelDepths[6] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distSL * 4.0) : _smoothSL));
                _channelDepths[7] = Math.Min(baseDepth, baseDepth * (useOpacity ? (_distFL * 4.0) : _smoothFL));
            }

            _channelDists[0] = _distFC;
            _channelDists[1] = _distFR;
            _channelDists[2] = _distSR;
            _channelDists[3] = _distBR;
            _channelDists[4] = _distLFE;
            _channelDists[5] = _distBL;
            _channelDists[6] = _distSL;
            _channelDists[7] = _distFL;

            _renderContext.Width = w;
            _renderContext.Height = h;
            _renderContext.BaseDepth = baseDepth;
            _renderContext.ChannelDepths = _channelDepths;
            _renderContext.ChannelDists = _channelDists;
            _renderContext.TotalVolume = (float)totalTarget;
            _renderContext.AnimationTime = _animationTime;

            // 선택된 렌더러(Wave 또는 Pad)에게 그리기 위임
            int modeIndex = (AppSettings.VisualMode >= 0 && AppSettings.VisualMode < _visualizers.Length) ? AppSettings.VisualMode : 0;
            var currentVisualizer = _visualizers[modeIndex];

            UnifiedWave.Data = currentVisualizer.GenerateGeometry(_renderContext);
            
            if (_cachedActiveColor != activeColor || _cachedVisualModeForBrush != modeIndex || UnifiedWave.Fill == null)
            {
                _cachedActiveColor = activeColor;
                _cachedVisualModeForBrush = modeIndex;
                
                if (modeIndex == 3)
                {
                    UnifiedWave.Fill = Brushes.Transparent;
                    UnifiedWave.Stroke = currentVisualizer.GetFillBrush(activeColor);
                    UnifiedWave.StrokeThickness = 4.0;
                }
                else
                {
                    UnifiedWave.Fill = currentVisualizer.GetFillBrush(activeColor);
                    UnifiedWave.Stroke = null;
                    UnifiedWave.StrokeThickness = 0;
                }
            }
            
            if (AppSettings.IsGlowMode && AppSettings.GlowIntensity > 0)
            {
                if (UnifiedWave.Effect != WaveGlowEffect)
                {
                    UnifiedWave.Effect = WaveGlowEffect;
                }
                WaveGlowEffect.Color = activeColor;
                WaveGlowEffect.Opacity = Math.Min(1.0, AppSettings.GlowIntensity / 100.0 * 1.6);
                WaveGlowEffect.BlurRadius = Math.Max(1.0, AppSettings.GlowIntensity * 0.5);
            }
            else
            {
                if (UnifiedWave.Effect != null)
                {
                    UnifiedWave.Effect = null;
                }
            }
            
            if (AppSettings.IntensityAsOpacity)
            {
                double maxOpacity = Math.Max(0.0, AppSettings.OpacityFixedMaxOpacity) / 100.0;
                double volumeFactor = Math.Max(0.0, Math.Min(1.0, _smoothTotal / 2.5));
                UnifiedWave.Opacity = maxOpacity * volumeFactor;
            }
            else
            {
                double baseOpacity = Math.Max(0.0, AppSettings.VisualOpacity) / 100.0;
                UnifiedWave.Opacity = baseOpacity;
            }



            _targetFL *= 0.87f;
            _targetFR *= 0.87f;
            _targetFC *= 0.87f;
            _targetBL *= 0.87f;
            _targetBR *= 0.87f;
            _targetSL *= 0.87f;
            _targetSR *= 0.87f;
            _targetLFE *= 0.87f;

            if (_isEditMode)
            {
                UpdateGuidelinePositions();
            }
        }

        private void HandleAudioDataAsync(object? sender, AudioDataAvailableEventArgs e)
        {
            if (_audioRouter == null || _vectorCalc == null || _soundAI == null) return;

            byte[] rawData = e.Buffer;
            int bytesRecorded = e.BytesRecorded;
            int channels = e.Channels;

            // 1. 오디오 라우팅, 실시간 활성 채널 파악, 멀티채널 볼륨 연산을 캡처 스레드에서 직접 동기 실행 (극히 가벼움)
            _audioRouter.OnDataReceived(this, rawData);
            
            long currentTick = Environment.TickCount64;
            if (currentTick - _lastChannelCountTick >= 500)
            {
                _lastChannelCountTick = currentTick;
                _lastSourceChannels = CountActiveChannels(rawData, channels);
            }
            
            var (fl, fr, fc, bl, br, sl, sr, lfe) = _vectorCalc.CalculateVolumes(rawData, bytesRecorded, channels);
            
            // 사용자가 2채널 확장(Upmix) 모드를 켰을 경우, 
            // 좌/우에서 들리는 소리(FL, FR)를 전체 화면으로 강제 복사하여 사방에서 파도가 치도록 만듭니다.
            if (AppSettings.SoundMode == 0)
            {
                sl = fl; bl = fl;
                sr = fr; br = fr;
            }

            // 렌더 스레드가 참조할 볼륨 변수를 즉시 갱신 (지연 시간 0)
            _targetFL = fl; _targetFR = fr; _targetFC = fc;
            _targetBL = bl; _targetBR = br;
            _targetSL = sl; _targetSR = sr; _targetLFE = lfe;

            // YAMNet 실시간 링 버퍼에 데이터를 끊김 없이 모노 다운믹스하여 적재 (데이터의 연속성 및 정확도 보존)
            int sampleRate = _captureEngine?.CaptureFormat?.SampleRate ?? SoundClassifier.DefaultCaptureSampleRate;
            _soundAI.IngestAudio(rawData, bytesRecorded, channels, sampleRate);

            // 2. AI 추론 스로틀링 (250ms 간격으로 백그라운드 스레드풀에서 무거운 ONNX 추론 실행)
            long nowTicks = Environment.TickCount64;
            long elapsedMs = nowTicks - _lastPredictTimeTicks;

            if (elapsedMs >= AI_PREDICT_INTERVAL_MS && !_isPredicting)
            {
                lock (_aiLock)
                {
                    if (!_isPredicting)
                    {
                        _isPredicting = true;
                        _lastPredictTimeTicks = nowTicks;

                        Task.Run(() =>
                        {
                            try
                            {
                                // 링 버퍼에 안전하게 축적된 데이터에서 추론 진행
                                string prediction = _soundAI.PredictSoundType(sampleRate);

                                // 이전 값과 다를 때만 UI 스레드에 디스패칭하여 가비지 및 컨텍스트 스위칭 최소화
                                if (_currentLabel != prediction)
                                {
                                    // UI 프레임 렌더링에 부정적인 영향을 미치지 않도록 Background 우선순위로 라벨 업데이트
                                    Dispatcher.InvokeAsync(() =>
                                    {
                                        _currentLabel = prediction;
                                    }, System.Windows.Threading.DispatcherPriority.Background);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[YAMNet Background Error] {ex.Message}");
                            }
                            finally
                            {
                                _isPredicting = false;
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// float32 멀티채널 버퍼에서 실제로 신호가 있는 채널 수를 반환합니다.
        /// WASAPI는 소스가 2ch여도 장치 포맷(8ch)으로 패딩하므로,
        /// 무음(절댓값 합산이 임계값 미만)인 채널은 제외합니다.
        /// </summary>
        private int CountActiveChannels(byte[] buffer, int totalChannels)
        {
            const float threshold = 1e-6f;
            const int bytesPerSample = 4; // float32
            int frames = buffer.Length / (totalChannels * bytesPerSample);
            if (frames == 0) return totalChannels;

            // 채널별 RMS 계산 (전체 프레임의 일부만 샘플링)
            int step = Math.Max(1, frames / 200);
            
            double[] rms = _rmsBuffer;
            if (totalChannels > rms.Length)
            {
                rms = new double[totalChannels];
            }
            else
            {
                Array.Clear(rms, 0, totalChannels);
            }

            ReadOnlySpan<float> samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, buffer.Length));
            int count = 0;
            for (int i = 0; i < frames; i += step)
            {
                int baseOffset = i * totalChannels;
                for (int ch = 0; ch < totalChannels; ch++)
                {
                    float s = samples[baseOffset + ch];
                    rms[ch] += s * s;
                }
                count++;
            }

            int active = 0;
            for (int ch = 0; ch < totalChannels; ch++)
            {
                if (Math.Sqrt(rms[ch] / count) > threshold)
                    active++;
            }
            return active > 0 ? active : totalChannels;
        }



        private void SetVisibilityIfChanged(UIElement element, ref Visibility cachedValue, Visibility newValue)
        {
            if (cachedValue != newValue)
            {
                cachedValue = newValue;
                element.Visibility = newValue;
            }
        }

        private void UpdateCachedBrushesIfNeeded()
        {
            string dangerHex = AppSettings.ColorDanger;
            string speechHex = AppSettings.ColorSpeech;
            string ambientHex = AppSettings.ColorAmbient;

            if (_dangerBrush == null || _cachedColorDangerHex != dangerHex)
            {
                _cachedColorDangerHex = dangerHex;
                _dangerColor = ParseColor(dangerHex);
                var brush = new SolidColorBrush(_dangerColor);
                brush.Freeze();
                _dangerBrush = brush;
            }
            if (_speechBrush == null || _cachedColorSpeechHex != speechHex)
            {
                _cachedColorSpeechHex = speechHex;
                _speechColor = ParseColor(speechHex);
                var brush = new SolidColorBrush(_speechColor);
                brush.Freeze();
                _speechBrush = brush;
            }
            if (_ambientBrush == null || _cachedColorAmbientHex != ambientHex)
            {
                _cachedColorAmbientHex = ambientHex;
                _ambientColor = ParseColor(ambientHex);
                var brush = new SolidColorBrush(_ambientColor);
                brush.Freeze();
                _ambientBrush = brush;
            }
        }

        private SolidColorBrush GetBrushForLabel(string label)
        {
            UpdateCachedBrushesIfNeeded();
            if (label.Contains("danger")) return _dangerBrush!;
            if (label.Contains("speech")) return _speechBrush!;
            return _ambientBrush!;
        }

        private Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.White;
            }
        }

        private Color GetColorForLabel(string label)
        {
            UpdateCachedBrushesIfNeeded();
            if (label.Contains("danger")) return _dangerColor;
            if (label.Contains("speech")) return _speechColor;
            return _ambientColor;
        }

        private bool IsLabelVisible(string label)
        {
            if (label.Contains("danger")) return AppSettings.ShowDanger;
            if (label.Contains("speech")) return AppSettings.ShowSpeech;
            return AppSettings.ShowAmbient;
        }

        // ==========================================
        // 윈도우 클릭 관통 및 전체화면 최상단(Overlay) 강제 방어 구현 
        // ==========================================
        private System.Windows.Threading.DispatcherTimer? _topmostTimer;

        private void ApplyClickThroughMagic()
        {
            this.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                // WS_EX_TOPMOST (0x0008) 추가하여 시스템 레벨 최상단 지정
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);

                // 전체화면 게임(Exclusive Fullscreen 등)이 강제로 Z-Order를 점유할 때를 대비하여
                // 주기적으로 오버레이 창을 최상단(HWND_TOPMOST)으로 다시 끌어올립니다.
                _topmostTimer = new System.Windows.Threading.DispatcherTimer();
                _topmostTimer.Interval = TimeSpan.FromSeconds(2);
                _topmostTimer.Tick += (ts, te) =>
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                };
                _topmostTimer.Start();
            };
        }

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_TOPMOST = 0x00000008;

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        private bool IsHotkeyPressed(System.Collections.Generic.List<int> keys)
        {
            if (keys == null || keys.Count == 0) return false;
            foreach (int key in keys)
            {
                if ((GetAsyncKeyState(key) & 0x8000) == 0) return false;
            }
            return true;
        }

        private string GetKeysName(System.Collections.Generic.List<int> codes)
        {
            if (codes == null || codes.Count == 0) return "없음";
            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
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

        // ==========================================
        // 실시간 오버레이 편집 모드 (F4) 핵심 비하인드 로직
        // ==========================================
        private void ToggleEditMode(bool enable)
        {
            _isEditMode = enable;
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (_isEditMode)
            {
                // WS_EX_TRANSPARENT를 제거하여 오버레이가 마우스 입력을 받게 함
                extendedStyle &= ~WS_EX_TRANSPARENT;
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);

                EditOverlayGrid.Visibility = Visibility.Visible;

                LoadSettingsToEditPanel();
                UpdateGuidelinePositions();

                // 오버레이 창 활성화 및 마우스 포커싱
                this.Activate();
                this.Focus();
            }
            else
            {
                // WS_EX_TRANSPARENT를 복구하여 오버레이를 마우스 관통 상태로 돌려놓음
                extendedStyle |= WS_EX_TRANSPARENT;
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle);

                EditOverlayGrid.Visibility = Visibility.Collapsed;

                _isDraggingCircleRadius = false;
                _isDraggingCircleIntensity = false;

                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
            }
        }

        private bool _isUpdatingEditPanelSliders = true;

        private void LoadSettingsToEditPanel()
        {
            _isUpdatingEditPanelSliders = true;

            if (CmbLanguage != null)
            {
                // AppSettings.Language stores native names ("日本語", "中文", etc.)
                // but CmbLanguage items have English Tags ("Japanese", "Chinese", etc.)
                string langToMatch = AppSettings.Language switch
                {
                    "日本語" => "Japanese",
                    "中文" => "Chinese",
                    "Español" => "Spanish",
                    "Français" => "French",
                    "Deutsch" => "German",
                    "Русский" => "Russian",
                    _ => AppSettings.Language // "KOR" and "English" match directly
                };
                foreach (System.Windows.Controls.ComboBoxItem item in CmbLanguage.Items)
                {
                    if (item.Tag?.ToString() == langToMatch)
                    {
                        CmbLanguage.SelectedItem = item;
                        break;
                    }
                }
            }

            // 콤보박스 선택 상태 동기화
            if (CmbEditPanelVisualMode != null)
                CmbEditPanelVisualMode.SelectedIndex = AppSettings.VisualMode;
            if (CmbEditPanelSoundMode != null)
                CmbEditPanelSoundMode.SelectedIndex = AppSettings.SoundMode;

            EditPanelSpeedSlider.Value = AppSettings.WavePositionSpeed;
            EditPanelSpeedValueText.Text = $"{AppSettings.WavePositionSpeed:F0}";

            EditPanelSensitivitySlider.Value = AppSettings.WaveSensitivity * 4.0;
            EditPanelSensitivityValueText.Text = $"{AppSettings.WaveSensitivity * 4.0:F0}";

            EditPanelOpacitySlider.Value = 100.0 - AppSettings.VisualOpacity;
            EditPanelOpacityValueText.Text = $"{100.0 - AppSettings.VisualOpacity:F0}%";

            if (EditPanelIntensitySlider != null)
            {
                EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
                if (EditPanelIntensityAsOpacityCheckBox != null)
                {
                    EditPanelIntensityAsOpacityCheckBox.IsChecked = AppSettings.IntensityAsOpacity;
                    bool isOpacity = AppSettings.IntensityAsOpacity;

                    if (EditPanelIntensityPanel != null)
                    {
                        EditPanelIntensityPanel.IsEnabled = !isOpacity;
                        EditPanelIntensityPanel.Opacity = !isOpacity ? 1.0 : 0.4;
                    }
                    
                    if (EditPanelOpacityPanel != null)
                    {
                        EditPanelOpacityPanel.IsEnabled = !isOpacity;
                        EditPanelOpacityPanel.Opacity = !isOpacity ? 1.0 : 0.4;
                    }

                    if (EditPanelOpacityFixedSizePanel != null)
                    {
                        EditPanelOpacityFixedSizePanel.IsEnabled = isOpacity;
                        EditPanelOpacityFixedSizePanel.Opacity = isOpacity ? 1.0 : 0.4;
                        
                        if (EditPanelOpacityFixedSizeLabel != null)
                        {
                            EditPanelOpacityFixedSizeLabel.Text = _opacityFixedSizeLabels[AppSettings.VisualMode];
                        }
                        
                        EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
                        EditPanelOpacityFixedSizeValueText.Text = $"{AppSettings.OpacityFixedSize:F0}%";
                    }

                    if (EditPanelOpacityFixedMaxOpacityPanel != null)
                    {
                        EditPanelOpacityFixedMaxOpacityPanel.IsEnabled = isOpacity;
                        EditPanelOpacityFixedMaxOpacityPanel.Opacity = isOpacity ? 1.0 : 0.4;
                        
                        EditPanelOpacityFixedMaxOpacitySlider.Value = 100.0 - AppSettings.OpacityFixedMaxOpacity;
                        EditPanelOpacityFixedMaxOpacityValueText.Text = $"{100.0 - AppSettings.OpacityFixedMaxOpacity:F0}%";
                    }
                }
            }

            EditPanelGlowCheckBox.IsChecked = AppSettings.IsGlowMode;
            EditPanelGlowIntensityPanel.Visibility = AppSettings.IsGlowMode ? Visibility.Visible : Visibility.Collapsed;

            EditPanelGlowSlider.Value = AppSettings.GlowIntensity;
            EditPanelGlowValueText.Text = $"{AppSettings.GlowIntensity:F0}%";

            // 소리 분류 표시 설정 바인딩
            if (EditPanelShowAmbientCheckBox != null)
                EditPanelShowAmbientCheckBox.IsChecked = AppSettings.ShowAmbient;
            if (EditPanelShowSpeechCheckBox != null)
                EditPanelShowSpeechCheckBox.IsChecked = AppSettings.ShowSpeech;
            if (EditPanelShowDangerCheckBox != null)
                EditPanelShowDangerCheckBox.IsChecked = AppSettings.ShowDanger;

            // 색상 버튼 배경색 설정
            if (EditPanelAmbientColorBtn != null)
                EditPanelAmbientColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppSettings.ColorAmbient));
            if (EditPanelSpeechColorBtn != null)
                EditPanelSpeechColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppSettings.ColorSpeech));
            if (EditPanelDangerColorBtn != null)
                EditPanelDangerColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppSettings.ColorDanger));

            // 단축키 설정 바인딩
            
            if (BtnEditPanelVisualHotkey != null)
                BtnEditPanelVisualHotkey.Content = GetKeysName(AppSettings.VisualModeKeyBind);
            if (BtnEditPanelSoundModeHotkey != null)
                BtnEditPanelSoundModeHotkey.Content = GetKeysName(AppSettings.StereoUpmixKeyBind);
            if (BtnEditPanelEditHotkey != null)
                BtnEditPanelEditHotkey.Content = GetKeysName(AppSettings.EditModeKeyBind);
            
            if (EditPanelHotkeyText != null)
                EditPanelHotkeyText.Text = GetKeysName(AppSettings.EditModeKeyBind);

            // 고급 설정 바인딩
            if (EditPanelTargetFpsSlider != null)
            {
                EditPanelTargetFpsSlider.Maximum = AppSettings.GetMonitorRefreshRate();
                EditPanelTargetFpsSlider.Value = AppSettings.TargetFps;
                EditPanelTargetFpsValueText.Text = $"{AppSettings.TargetFps:F0} FPS";
            }

            if (EditPanelAdminCheckBox != null)
                EditPanelAdminCheckBox.IsChecked = AppSettings.IsAdminMode;

            _isUpdatingEditPanelSliders = false;
        }

        private void CmbEditPanelVisualMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.VisualMode = CmbEditPanelVisualMode.SelectedIndex;
            AppSettings.Save();

            if (EditPanelOpacityFixedSizeLabel != null)
            {
                EditPanelOpacityFixedSizeLabel.Text = _opacityFixedSizeLabels[AppSettings.VisualMode];
            }

            // 모드가 변경되면 그에 맞추어 UI 정보와 가이드라인 형태도 리셋
            UpdateGuidelinePositions();

            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void CmbEditPanelSoundMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.SoundMode = CmbEditPanelSoundMode.SelectedIndex;
            AppSettings.Save();

            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingEditPanelSliders) return;

            if (sender == EditPanelSpeedSlider)
            {
                AppSettings.WavePositionSpeed = EditPanelSpeedSlider.Value;
                EditPanelSpeedValueText.Text = $"{EditPanelSpeedSlider.Value:F0}";
            }
            else if (sender == EditPanelSensitivitySlider)
            {
                AppSettings.WaveSensitivity = EditPanelSensitivitySlider.Value / 4.0;
                EditPanelSensitivityValueText.Text = $"{EditPanelSensitivitySlider.Value:F0}";
                OnSettingsChangedFromHotkey?.Invoke();
            }
            else if (sender == EditPanelOpacitySlider)
            {
                AppSettings.VisualOpacity = 100.0 - EditPanelOpacitySlider.Value;
                EditPanelOpacityValueText.Text = $"{EditPanelOpacitySlider.Value:F0}%";
                UnifiedWave.Opacity = Math.Max(0.0, AppSettings.VisualOpacity) / 100.0;
            }
            else if (sender == EditPanelIntensitySlider)
            {
                AppSettings.WaveIntensity = EditPanelIntensitySlider.Value;
                EditPanelIntensityValueText.Text = $"{EditPanelIntensitySlider.Value:F0}%";
                UpdateGuidelinePositions();
            }
            else if (sender == EditPanelOpacityFixedSizeSlider)
            {
                AppSettings.OpacityFixedSize = EditPanelOpacityFixedSizeSlider.Value;
                EditPanelOpacityFixedSizeValueText.Text = $"{EditPanelOpacityFixedSizeSlider.Value:F0}%";
                UpdateGuidelinePositions();
            }
            else if (sender == EditPanelOpacityFixedMaxOpacitySlider)
            {
                AppSettings.OpacityFixedMaxOpacity = 100.0 - EditPanelOpacityFixedMaxOpacitySlider.Value;
                EditPanelOpacityFixedMaxOpacityValueText.Text = $"{EditPanelOpacityFixedMaxOpacitySlider.Value:F0}%";
            }
            else if (sender == EditPanelGlowSlider)
            {
                AppSettings.GlowIntensity = EditPanelGlowSlider.Value;
                EditPanelGlowValueText.Text = $"{EditPanelGlowSlider.Value:F0}%";
            }
            else if (sender == EditPanelTargetFpsSlider)
            {
                AppSettings.TargetFps = EditPanelTargetFpsSlider.Value;
                EditPanelTargetFpsValueText.Text = $"{EditPanelTargetFpsSlider.Value:F0} FPS";
            }

            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelIntensityAsOpacity_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.IntensityAsOpacity = EditPanelIntensityAsOpacityCheckBox.IsChecked ?? false;
            bool isOpacity = AppSettings.IntensityAsOpacity;

            if (EditPanelIntensityPanel != null)
            {
                EditPanelIntensityPanel.IsEnabled = !isOpacity;
                EditPanelIntensityPanel.Opacity = !isOpacity ? 1.0 : 0.4;
            }

            if (EditPanelOpacityPanel != null)
            {
                EditPanelOpacityPanel.IsEnabled = !isOpacity;
                EditPanelOpacityPanel.Opacity = !isOpacity ? 1.0 : 0.4;
            }

            if (EditPanelOpacityFixedSizePanel != null)
            {
                EditPanelOpacityFixedSizePanel.IsEnabled = isOpacity;
                EditPanelOpacityFixedSizePanel.Opacity = isOpacity ? 1.0 : 0.4;
            }

            if (EditPanelOpacityFixedMaxOpacityPanel != null)
            {
                EditPanelOpacityFixedMaxOpacityPanel.IsEnabled = isOpacity;
                EditPanelOpacityFixedMaxOpacityPanel.Opacity = isOpacity ? 1.0 : 0.4;
            }

            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelGlow_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.IsGlowMode = EditPanelGlowCheckBox.IsChecked == true;
            EditPanelGlowIntensityPanel.Visibility = AppSettings.IsGlowMode ? Visibility.Visible : Visibility.Collapsed;

            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelShowAmbient_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.ShowAmbient = EditPanelShowAmbientCheckBox.IsChecked ?? true;
            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelShowSpeech_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.ShowSpeech = EditPanelShowSpeechCheckBox.IsChecked ?? true;
            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelShowDanger_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.ShowDanger = EditPanelShowDangerCheckBox.IsChecked ?? true;
            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelColorBtn_Click(object sender, RoutedEventArgs e)
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

                    AppSettings.Save();

                    // 버튼 배경색 즉시 변경
                    System.Windows.Media.Color newMediaColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                    btn.Background = new SolidColorBrush(newMediaColor);

                    // 런처 등 외부 프로그램에 즉각 실시간 동조 반영
                    OnSettingsChangedFromHotkey?.Invoke();
                }
            }
        }

        private void BtnCloseOverlay_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private string? _bindingTarget = null;
        private System.Collections.Generic.HashSet<int> _currentlyHeldKeys = new System.Collections.Generic.HashSet<int>();
        private System.Collections.Generic.HashSet<int> _maxKeysInCurrentBinding = new System.Collections.Generic.HashSet<int>();

        private void BtnEditPanelHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender == BtnEditPanelVisualHotkey) StartBinding("Visual");
            else if (sender == BtnEditPanelSoundModeHotkey) StartBinding("Sound");
            else if (sender == BtnEditPanelEditHotkey) StartBinding("Edit");
        }

        private void StartBinding(string target)
        {
            _bindingTarget = target;
            _currentlyHeldKeys.Clear();
            _maxKeysInCurrentBinding.Clear();
            string msg = AppSettings.Language == "KOR" ? "키 누르기.. (ESC 취소)" : "Press key.. (ESC cancel)";
            if (target == "Visual") BtnEditPanelVisualHotkey.Content = msg;
            else if (target == "Sound") BtnEditPanelSoundModeHotkey.Content = msg;
            else if (target == "Edit") BtnEditPanelEditHotkey.Content = msg;
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
                    LoadSettingsToEditPanel();
                    e.Handled = true;
                    return;
                }

                int vKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key);
                _currentlyHeldKeys.Add(vKey);
                _maxKeysInCurrentBinding.Add(vKey);
                
                string currentStr = GetKeysName(new System.Collections.Generic.List<int>(_maxKeysInCurrentBinding));
                if (_bindingTarget == "Visual") BtnEditPanelVisualHotkey.Content = currentStr;
                else if (_bindingTarget == "Sound") BtnEditPanelSoundModeHotkey.Content = currentStr;
                else if (_bindingTarget == "Edit") BtnEditPanelEditHotkey.Content = currentStr;

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

                    if (_bindingTarget == "Edit" && EditPanelHotkeyText != null) EditPanelHotkeyText.Text = GetKeysName(keysToSave);

                    AppSettings.Save();
                    OnSettingsChangedFromHotkey?.Invoke();

                    _bindingTarget = null;
                    _currentlyHeldKeys.Clear();
                    _maxKeysInCurrentBinding.Clear();
                    LoadSettingsToEditPanel();
                }
                e.Handled = true;
            }
        }

        private void EditPanelAdmin_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.IsAdminMode = EditPanelAdminCheckBox.IsChecked ?? false;
            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void UpdateGuidelinePositions()
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (w == 0 || h == 0) return;

            int visualMode = AppSettings.VisualMode;

            if (visualMode == 0 || visualMode == 1 || visualMode == 3)
            {
                RectGuideline.Visibility = Visibility.Visible;
                if (RectGuidelineLabelBox != null) RectGuidelineLabelBox.Visibility = Visibility.Visible;
                CircleGuideline.Visibility = Visibility.Collapsed;

                double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
                if (maxBaseDepth < 10) maxBaseDepth = 10;
                
                double currentIntensity = AppSettings.IntensityAsOpacity ? (AppSettings.OpacityFixedSize / 2.0) : AppSettings.WaveIntensity;
                double baseDepth = maxBaseDepth * (currentIntensity / 100.0);
                double displayBaseDepth = Math.Min(baseDepth, maxBaseDepth);

                double rectLeft = displayBaseDepth;
                double rectTop = displayBaseDepth;
                double rectWidth = w - 2 * displayBaseDepth;
                double rectHeight = h - 2 * displayBaseDepth;

                if (rectWidth < 10) rectWidth = 10;
                if (rectHeight < 10) rectHeight = 10;

                Canvas.SetLeft(RectGuideline, rectLeft);
                Canvas.SetTop(RectGuideline, rectTop);
                RectGuideline.Width = rectWidth;
                RectGuideline.Height = rectHeight;

                string modeName = _visualModeNames[visualMode];
                double displayIntensity = AppSettings.IntensityAsOpacity ? AppSettings.OpacityFixedSize : AppSettings.WaveIntensity;
                if (RectModeLabel != null) RectModeLabel.Text = $"{modeName} {_rectModeLabelPrefix}";
                if (RectSizeLabel != null) RectSizeLabel.Text = $"{modeName} {_rectSizeLabelPrefix}{displayIntensity:F0}%";
            }
            else if (visualMode == 2)
            {
                RectGuideline.Visibility = Visibility.Collapsed;
                if (RectGuidelineLabelBox != null) RectGuidelineLabelBox.Visibility = Visibility.Collapsed;
                CircleGuideline.Visibility = Visibility.Visible;

                double cx = w / 2.0;
                double cy = h / 2.0;

                double radiusRatio = 0.05 + (AppSettings.CircleRadius - 10.0) / 90.0 * 0.35;
                double baseRadius = Math.Min(w, h) * radiusRatio;

                double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
                if (maxBaseDepth < 10) maxBaseDepth = 10;
                double currentIntensity = AppSettings.IntensityAsOpacity ? (AppSettings.OpacityFixedSize / 2.0) : AppSettings.WaveIntensity;
                double baseDepth = maxBaseDepth * (currentIntensity / 100.0);
                double maxRadius = baseRadius + baseDepth * 0.35;

                Canvas.SetLeft(CircleGuideline, 0);
                Canvas.SetTop(CircleGuideline, 0);
                CircleGuideline.Width = w;
                CircleGuideline.Height = h;

                CircleBaseShape.Width = baseRadius * 2;
                CircleBaseShape.Height = baseRadius * 2;
                Canvas.SetLeft(CircleBaseShape, cx - baseRadius);
                Canvas.SetTop(CircleBaseShape, cy - baseRadius);

                CircleMaxShape.Width = maxRadius * 2;
                CircleMaxShape.Height = maxRadius * 2;
                Canvas.SetLeft(CircleMaxShape, cx - maxRadius);
                Canvas.SetTop(CircleMaxShape, cy - maxRadius);

                double displayIntensity = AppSettings.IntensityAsOpacity ? AppSettings.OpacityFixedSize : AppSettings.WaveIntensity;
                CircleRadiusLabel.Text = $"{_circleRadiusLabelPrefix}{AppSettings.CircleRadius:F0}";
                CircleIntensityLabel.Text = $"{_circleIntensityLabelPrefix}{displayIntensity:F0}%";
            }
        }

        private void ResizeHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (w == 0 || h == 0) return;

            Point mousePos = System.Windows.Input.Mouse.GetPosition(GuidelineCanvas);
            System.Windows.Controls.Primitives.Thumb? thumb = sender as System.Windows.Controls.Primitives.Thumb;
            if (thumb == null) return;

            double baseDepth = 0;
            if (thumb.Name == "ResizeHandle_NW")
                baseDepth = Math.Min(mousePos.X, mousePos.Y);
            else if (thumb.Name == "ResizeHandle_NE")
                baseDepth = Math.Min(w - mousePos.X, mousePos.Y);
            else if (thumb.Name == "ResizeHandle_SW")
                baseDepth = Math.Min(mousePos.X, h - mousePos.Y);
            else if (thumb.Name == "ResizeHandle_SE")
                baseDepth = Math.Min(w - mousePos.X, h - mousePos.Y);

            if (baseDepth < 0) baseDepth = 0;

            double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
            if (maxBaseDepth < 10) maxBaseDepth = 10;
            double intensity = (baseDepth / maxBaseDepth) * 100.0;

            if (AppSettings.IntensityAsOpacity)
            {
                double opSize = intensity * 2.0;
                if (opSize < 10.0) opSize = 10.0;
                if (opSize > 100.0) opSize = 100.0;
                AppSettings.OpacityFixedSize = opSize;
                if (EditPanelOpacityFixedSizeSlider != null) EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
                if (EditPanelOpacityFixedSizeValueText != null) EditPanelOpacityFixedSizeValueText.Text = $"{AppSettings.OpacityFixedSize:F0}%";
            }
            else
            {
                if (intensity < 10.0) intensity = 10.0;
                if (intensity > 100.0) intensity = 100.0;
                AppSettings.WaveIntensity = intensity;
                if (EditPanelIntensitySlider != null) EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                if (EditPanelIntensityValueText != null) EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
            }

            UpdateGuidelinePositions();
            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void GuidelineCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            if (AppSettings.VisualMode == 2)
            {
                double w = this.ActualWidth;
                double h = this.ActualHeight;
                double cx = w / 2.0;
                double cy = h / 2.0;

                Point clickPos = e.GetPosition(GuidelineCanvas);

                double dist = Math.Sqrt(Math.Pow(clickPos.X - cx, 2) + Math.Pow(clickPos.Y - cy, 2));

                double radiusRatio = 0.05 + (AppSettings.CircleRadius - 10.0) / 90.0 * 0.35;
                double baseRadius = Math.Min(w, h) * radiusRatio;

                double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
                if (maxBaseDepth < 10) maxBaseDepth = 10;
                double currentIntensity = AppSettings.IntensityAsOpacity ? (AppSettings.OpacityFixedSize / 2.0) : AppSettings.WaveIntensity;
                double baseDepth = maxBaseDepth * (currentIntensity / 100.0);
                double maxRadius = baseRadius + baseDepth * 0.35;

                double distToBase = Math.Abs(dist - baseRadius);
                double distToMax = Math.Abs(dist - maxRadius);

                if (distToBase < distToMax)
                {
                    _isDraggingCircleRadius = true;
                    _isDraggingCircleIntensity = false;
                    GuidelineCanvas.CaptureMouse();
                }
                else
                {
                    _isDraggingCircleIntensity = true;
                    _isDraggingCircleRadius = false;
                    GuidelineCanvas.CaptureMouse();
                }
            }
            else
            {
                _isDraggingRectIntensity = true;
                _rectDragStartPos = e.GetPosition(GuidelineCanvas);
                _rectDragStartIntensity = AppSettings.IntensityAsOpacity ? AppSettings.OpacityFixedSize : AppSettings.WaveIntensity;
                GuidelineCanvas.CaptureMouse();
            }
        }

        private void GuidelineCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isEditMode) return;

            if (_isDraggingCircleRadius || _isDraggingCircleIntensity)
            {
                double w = this.ActualWidth;
                double h = this.ActualHeight;
                double cx = w / 2.0;
                double cy = h / 2.0;

                Point mousePos = e.GetPosition(GuidelineCanvas);
                double dist = Math.Sqrt(Math.Pow(mousePos.X - cx, 2) + Math.Pow(mousePos.Y - cy, 2));

                if (_isDraggingCircleRadius)
                {
                    double maxMinWh = Math.Min(w, h);
                    if (maxMinWh > 0)
                    {
                        double radiusRatio = dist / maxMinWh;
                        double circleRadius = ((radiusRatio - 0.05) / 0.35) * 90.0 + 10.0;

                        if (circleRadius < 10.0) circleRadius = 10.0;
                        if (circleRadius > 100.0) circleRadius = 100.0;

                        AppSettings.CircleRadius = circleRadius;
                    }
                }
                else if (_isDraggingCircleIntensity)
                {
                    double radiusRatio = 0.05 + (AppSettings.CircleRadius - 10.0) / 90.0 * 0.35;
                    double baseRadius = Math.Min(w, h) * radiusRatio;

                    double baseDepth = (dist - baseRadius) / 0.35;
                    if (baseDepth < 0) baseDepth = 0;

                    double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
                    if (maxBaseDepth < 10) maxBaseDepth = 10;
                    double intensity = (baseDepth / maxBaseDepth) * 100.0;

                    if (AppSettings.IntensityAsOpacity)
                    {
                        double opSize = intensity * 2.0;
                        if (opSize < 0.0) opSize = 0.0;
                        if (opSize > 100.0) opSize = 100.0;
                        AppSettings.OpacityFixedSize = opSize;
                        if (EditPanelOpacityFixedSizeSlider != null) EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
                        if (EditPanelOpacityFixedSizeValueText != null) EditPanelOpacityFixedSizeValueText.Text = $"{AppSettings.OpacityFixedSize:F0}%";
                    }
                    else
                    {
                        if (intensity < 0.0) intensity = 0.0;
                        if (intensity > 100.0) intensity = 100.0;
                        AppSettings.WaveIntensity = intensity;
                        if (EditPanelIntensitySlider != null) EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                        if (EditPanelIntensityValueText != null) EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
                    }
                }

                UpdateGuidelinePositions();
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
            }
            else if (_isDraggingRectIntensity)
            {
                double w = this.ActualWidth;
                double h = this.ActualHeight;
                if (w == 0 || h == 0) return;

                Point mousePos = e.GetPosition(GuidelineCanvas);

                double distLeft = _rectDragStartPos.X;
                double distRight = w - _rectDragStartPos.X;
                double distTop = _rectDragStartPos.Y;
                double distBottom = h - _rectDragStartPos.Y;

                double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

                double baseDepth = 0;
                if (minDist == distLeft) baseDepth = mousePos.X;
                else if (minDist == distRight) baseDepth = w - mousePos.X;
                else if (minDist == distTop) baseDepth = mousePos.Y;
                else baseDepth = h - mousePos.Y;

                if (baseDepth < 0) baseDepth = 0;

                double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
                if (maxBaseDepth < 10) maxBaseDepth = 10;
                double intensity = (baseDepth / maxBaseDepth) * 100.0;

                if (AppSettings.IntensityAsOpacity)
                {
                    double opSize = intensity * 2.0;
                    if (opSize < 10.0) opSize = 10.0;
                    if (opSize > 100.0) opSize = 100.0;
                    AppSettings.OpacityFixedSize = opSize;
                    if (EditPanelOpacityFixedSizeSlider != null) EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
                    if (EditPanelOpacityFixedSizeValueText != null) EditPanelOpacityFixedSizeValueText.Text = $"{AppSettings.OpacityFixedSize:F0}%";
                }
                else
                {
                    if (intensity < 10.0) intensity = 10.0;
                    if (intensity > 100.0) intensity = 100.0;
                    AppSettings.WaveIntensity = intensity;
                    if (EditPanelIntensitySlider != null) EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                    if (EditPanelIntensityValueText != null) EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
                }

                UpdateGuidelinePositions();
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
            }
        }

        private void GuidelineCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDraggingCircleRadius || _isDraggingCircleIntensity || _isDraggingRectIntensity)
            {
                _isDraggingCircleRadius = false;
                _isDraggingCircleIntensity = false;
                _isDraggingRectIntensity = false;
                GuidelineCanvas.ReleaseMouseCapture();
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
            }
        }
        public void ApplyLanguage(string lang)
        {
            switch (lang)
            {
                case "KOR":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "오버레이 설정";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "표현 모드";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "사운드 모드";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "최대 프레임";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "크기 고정";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "세기에 비례하는 투명도를 활용하여 고정된 크기의 그래픽을 보여줍니다.";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "최대 투명도";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "크기";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "투명도";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "속도";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "민감도";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "광원 효과";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "소리 분류 표시";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "색상";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "색상";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "색상";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "단축키 설정";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "표현 모드 전환";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "사운드 모드 전환";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "오버레이 설정";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "고급 설정";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "개발자 모드";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "디버그 정보 및 오디오 상태 표시";
                    if (DragTipText != null) DragTipText.Text = "화면에 표시된 가이드 라인을 마우스 드래그하여 오버레이 파도의 높이 한계선 및 중앙 크기를 실시간 조절할 수 있습니다!";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "광원 세기";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "환경음 표시";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "말소리 표시";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "강조음 표시";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " 키를 누르면 설정 저장 후 닫힘";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "오버레이 실행 종료";
                    _rectModeLabelPrefix = "한계선"; _rectSizeLabelPrefix = "크기: "; _circleRadiusLabelPrefix = "기본 반경: "; _circleIntensityLabelPrefix = "원형 크기: "; _visualModeUIPrefix = "시각화 모드: "; _stereoModeUIPrefix = "채널 모드: "; _editModeUIText = "오버레이 설정: ";
                    _visualModeNames = new[] { "파도", "패드", "원형", "외곽선" }; _soundModeNames = new[] { "2 채널", "5.1 채널", "7.1 채널" };
                    _opacityFixedSizeLabels = new[] { "파도 크기", "패드 크기", "원형 크기", "외곽선 두께" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "English":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "Overlay Settings";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "Visual Mode";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "Sound Mode";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "Target FPS";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "Fixed Size";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "Displays a fixed-size graphic utilizing opacity proportional to intensity.";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "Max Opacity";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "Size";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "Opacity";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "Speed";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "Sensitivity";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "Glow Effect";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "Sound Classification";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "Color";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "Color";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "Color";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "Hotkeys";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "Toggle Visual Mode";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "Toggle Sound Mode";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "Overlay Settings";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "Advanced Settings";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "Developer Mode";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "Show debug info & audio status";
                    if (DragTipText != null) DragTipText.Text = "Drag the guidelines displayed on the screen with your mouse to adjust the height limit and center size of the overlay wave in real time!";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "Glow Intensity";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "Show Ambient";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "Show Speech";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "Show Danger";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " key to save & close";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "Exit Overlay";
                    _rectModeLabelPrefix = "Limit"; _rectSizeLabelPrefix = "Size: "; _circleRadiusLabelPrefix = "Base Radius: "; _circleIntensityLabelPrefix = "Circle Size: "; _visualModeUIPrefix = "Visual Mode: "; _stereoModeUIPrefix = "Channel Mode: "; _editModeUIText = "Overlay Settings: ";
                    _visualModeNames = new[] { "Wave", "Pad", "Circle", "Outline" }; _soundModeNames = new[] { "2 Channel", "5.1 Channel", "7.1 Channel" };
                    _opacityFixedSizeLabels = new[] { "Wave Size", "Pad Size", "Circle Size", "Outline Thickness" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "Japanese":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "オーバーレイ設定";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "ビジュアルモード";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "サウンドモード";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "最大FPS";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "固定サイズ";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "強度に比例する透明度を利用して固定サイズのグラフィックを表示します。";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "最大透明度";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "サイズ";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "透明度";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "速度";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "感度";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "グロー効果";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "音声分類表示";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "色";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "色";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "色";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "ホットキー設定";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "ビジュアルモード切替";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "サウンドモード切替";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "オーバーレイ設定";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "詳細設定";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "開発者モード";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "デバッグ情報とオーディオステータスを表示";
                    if (DragTipText != null) DragTipText.Text = "画面に表示されたガイドラインをマウスでドラッグし、オーバーレイ波の高さ制限と中央サイズをリアルタイムで調整できます！";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "グロー強度";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "環境音を表示";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "音声を表示";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "警告音を表示";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " キーで保存して閉じる";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "オーバーレイ終了";
                    _rectModeLabelPrefix = "限界線"; _rectSizeLabelPrefix = "サイズ: "; _circleRadiusLabelPrefix = "基本半径: "; _circleIntensityLabelPrefix = "円のサイズ: "; _visualModeUIPrefix = "視覚化モード: "; _stereoModeUIPrefix = "チャンネルモード: "; _editModeUIText = "オーバーレイ設定: ";
                    _visualModeNames = new[] { "波", "パッド", "円形", "アウトライン" }; _soundModeNames = new[] { "2 チャンネル", "5.1 チャンネル", "7.1 チャンネル" };
                    _opacityFixedSizeLabels = new[] { "波のサイズ", "パッドのサイズ", "円のサイズ", "アウトラインの太さ" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "Chinese":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "悬浮窗设置";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "表现模式";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "声音模式";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "目标 FPS";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "固定尺寸";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "使用与强度成比例的不透明度显示固定尺寸的图形。";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "最大透明度";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "尺寸";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "透明度";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "速度";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "灵敏度";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "发光效果";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "声音分类显示";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "颜色";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "颜色";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "颜色";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "快捷键设置";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "切换表现模式";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "切换声音模式";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "悬浮窗设置";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "高级设置";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "开发者模式";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "显示调试信息和音频状态";
                    if (DragTipText != null) DragTipText.Text = "用鼠标拖动屏幕上显示的辅助线，实时调整悬浮波的高度限制和中心尺寸！";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "发光强度";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "显示环境音";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "显示说话声";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "显示强调音";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " 键保存并关闭";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "退出悬浮窗";
                    _rectModeLabelPrefix = "限制线"; _rectSizeLabelPrefix = "大小: "; _circleRadiusLabelPrefix = "基础半径: "; _circleIntensityLabelPrefix = "圆形大小: "; _visualModeUIPrefix = "可视化模式: "; _stereoModeUIPrefix = "声道模式: "; _editModeUIText = "覆盖设置: ";
                    _visualModeNames = new[] { "波浪", "垫子", "圆形", "轮廓" }; _soundModeNames = new[] { "2 声道", "5.1 声道", "7.1 声道" };
                    _opacityFixedSizeLabels = new[] { "波浪大小", "背景音面板大小", "圆形大小", "轮廓厚度" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "Spanish":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "Configuración de superposición";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "Modo visual";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "Modo de sonido";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "FPS máximo";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "Tamaño fijo";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "Muestra un gráfico de tamaño fijo utilizando una opacidad proporcional a la intensidad.";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "Opacidad máx.";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "Tamaño";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "Opacidad";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "Velocidad";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "Sensibilidad";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "Efecto de brillo";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "Clasificación de sonido";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "Color";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "Color";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "Color";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "Atajos";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "Alternar modo visual";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "Alternar modo de sonido";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "Configuración de superposición";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "Configuración avanzada";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "Modo de desarrollador";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "Mostrar información de depuración";
                    if (DragTipText != null) DragTipText.Text = "¡Arrastre las pautas en la pantalla para ajustar el límite de altura y el tamaño central de la onda en tiempo real!";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "Intensidad de brillo";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "Mostrar ambiente";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "Mostrar voz";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "Mostrar peligro";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " para guardar y cerrar";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "Salir de superposición";
                    _rectModeLabelPrefix = "Límite"; _rectSizeLabelPrefix = "Tamaño: "; _circleRadiusLabelPrefix = "Radio Base: "; _circleIntensityLabelPrefix = "Tamaño de Círculo: "; _visualModeUIPrefix = "Modo Visual: "; _stereoModeUIPrefix = "Modo de Canal: "; _editModeUIText = "Ajustes de Capa: ";
                    _visualModeNames = new[] { "Onda", "Pad", "Círculo", "Contorno" }; _soundModeNames = new[] { "2 Canales", "5.1 Canales", "7.1 Canales" };
                    _opacityFixedSizeLabels = new[] { "Tamaño de Ola", "Tamaño de Pad", "Tamaño de Círculo", "Grosor de Contorno" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "French":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "Paramètres de superposition";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "Mode visuel";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "Mode sonore";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "FPS max";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "Taille fixe";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "Affiche un graphique de taille fixe utilisant une opacité proportionnelle à l'intensité.";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "Opacité max.";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "Taille";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "Opacité";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "Vitesse";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "Sensibilité";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "Effet lumineux";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "Classification sonore";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "Couleur";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "Couleur";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "Couleur";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "Raccourcis";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "Basculer mode visuel";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "Basculer mode sonore";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "Paramètres de superposition";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "Paramètres avancés";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "Mode développeur";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "Afficher les infos de débogage";
                    if (DragTipText != null) DragTipText.Text = "Faites glisser les lignes directrices à l'écran pour ajuster en temps réel la hauteur limite et la taille centrale de l'onde !";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "Intensité lumineuse";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "Afficher ambiant";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "Afficher la voix";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "Afficher danger";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " pour sauver et fermer";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "Quitter la superposition";
                    _rectModeLabelPrefix = "Limite"; _rectSizeLabelPrefix = "Taille: "; _circleRadiusLabelPrefix = "Rayon de Base: "; _circleIntensityLabelPrefix = "Taille de Cercle: "; _visualModeUIPrefix = "Mode Visuel: "; _stereoModeUIPrefix = "Mode de Canal: "; _editModeUIText = "Paramètres: ";
                    _visualModeNames = new[] { "Vague", "Pad", "Cercle", "Contour" }; _soundModeNames = new[] { "2 Canaux", "5.1 Canaux", "7.1 Canaux" };
                    _opacityFixedSizeLabels = new[] { "Taille de Vague", "Taille de Pad", "Taille de Cercle", "Épaisseur de Contour" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "German":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "Overlay-Einstellungen";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "Visueller Modus";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "Sound-Modus";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "Max. FPS";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "Feste Größe";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "Zeigt eine Grafik mit fester Größe unter Verwendung einer intensitätsproportionalen Deckkraft an.";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "Max. Deckkraft";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "Größe";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "Deckkraft";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "Geschwindigkeit";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "Empfindlichkeit";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "Leuchteffekt";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "Tonklassifizierung";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "Farbe";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "Farbe";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "Farbe";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "Tastenkürzel";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "Visuellen Modus umschalten";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "Sound-Modus umschalten";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "Overlay-Einstellungen";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "Erweiterte Einstellungen";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "Entwicklermodus";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "Debuginfos anzeigen";
                    if (DragTipText != null) DragTipText.Text = "Ziehen Sie die Hilfslinien auf dem Bildschirm mit der Maus, um die Höhenbegrenzung und die Mittelgröße der Welle in Echtzeit anzupassen!";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "Leuchtintensität";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "Umgebungston";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "Sprache";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "Gefahr";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " drücken zum Speichern";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "Overlay beenden";
                    _rectModeLabelPrefix = "Grenze"; _rectSizeLabelPrefix = "Größe: "; _circleRadiusLabelPrefix = "Grundradius: "; _circleIntensityLabelPrefix = "Kreisgröße: "; _visualModeUIPrefix = "Visueller Modus: "; _stereoModeUIPrefix = "Kanal-Modus: "; _editModeUIText = "Overlay-Einst: ";
                    _visualModeNames = new[] { "Welle", "Pad", "Kreis", "Umriss" }; _soundModeNames = new[] { "2 Kanäle", "5.1 Kanäle", "7.1 Kanäle" };
                    _opacityFixedSizeLabels = new[] { "Wellengröße", "Pad-Größe", "Kreisgröße", "Umrissdicke" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
                case "Russian":
                    if (EditPanelTitle != null) EditPanelTitle.Text = "Настройки оверлея";
                    if (EditPanelVisualModeLabel != null) EditPanelVisualModeLabel.Text = "Визуальный режим";
                    if (EditPanelSoundModeLabel != null) EditPanelSoundModeLabel.Text = "Звуковой режим";
                    if (EditPanelMaxFpsLabel != null) EditPanelMaxFpsLabel.Text = "Максимальный FPS";
                    if (EditPanelIntensityAsOpacityLabel != null) EditPanelIntensityAsOpacityLabel.Text = "Фиксированный размер";
                    if (EditPanelIntensityAsOpacityDesc != null) EditPanelIntensityAsOpacityDesc.Text = "Отображает графику фиксированного размера, используя непрозрачность, пропорциональную интенсивности.";
                    if (EditPanelMaxOpacityLabel != null) EditPanelMaxOpacityLabel.Text = "Макс. непрозрачность";
                    if (EditPanelIntensityLabel != null) EditPanelIntensityLabel.Text = "Размер";
                    if (EditPanelOpacityLabel != null) EditPanelOpacityLabel.Text = "Непрозрачность";
                    if (EditPanelSpeedLabel != null) EditPanelSpeedLabel.Text = "Скорость";
                    if (EditPanelSensitivityLabel != null) EditPanelSensitivityLabel.Text = "Чувствительность";
                    if (EditPanelGlowModeLabel != null) EditPanelGlowModeLabel.Text = "Эффект свечения";
                    if (EditPanelAIDisplayLabel != null) EditPanelAIDisplayLabel.Text = "Классификация звука";
                    if (EditPanelAmbientColorLabel != null) EditPanelAmbientColorLabel.Text = "Цвет";
                    if (EditPanelSpeechColorLabel != null) EditPanelSpeechColorLabel.Text = "Цвет";
                    if (EditPanelDangerColorLabel != null) EditPanelDangerColorLabel.Text = "Цвет";
                    if (EditPanelHotkeySettingsLabel != null) EditPanelHotkeySettingsLabel.Text = "Горячие клавиши";
                    if (EditPanelVisualHotkeyLabel != null) EditPanelVisualHotkeyLabel.Text = "Переключить визуальный режим";
                    if (EditPanelSoundModeHotkeyLabel != null) EditPanelSoundModeHotkeyLabel.Text = "Переключить звуковой режим";
                    if (EditPanelEditHotkeyLabel != null) EditPanelEditHotkeyLabel.Text = "Настройки оверлея";
                    if (EditPanelAdminSettingsLabel != null) EditPanelAdminSettingsLabel.Text = "Дополнительные настройки";
                    if (EditPanelAdminModeLabel != null) EditPanelAdminModeLabel.Text = "Режим разработчика";
                    if (EditPanelAdminModeDesc != null) EditPanelAdminModeDesc.Text = "Показывать отладочную информацию";
                    if (DragTipText != null) DragTipText.Text = "Перетаскивайте направляющие на экране мышью, чтобы регулировать ограничение высоты и центральный размер волны в реальном времени!";
                    if (EditPanelGlowIntensityLabel != null) EditPanelGlowIntensityLabel.Text = "Интенсивность свечения";
                    if (EditPanelShowAmbientCheckBox != null) EditPanelShowAmbientCheckBox.Content = "Показать окружение";
                    if (EditPanelShowSpeechCheckBox != null) EditPanelShowSpeechCheckBox.Content = "Показать речь";
                    if (EditPanelShowDangerCheckBox != null) EditPanelShowDangerCheckBox.Content = "Показать опасность";
                    if (EditPanelSaveAndCloseDesc != null) EditPanelSaveAndCloseDesc.Text = " для сохранения и закрытия";
                    if (BtnCloseOverlay != null) BtnCloseOverlay.Content = "Выход из оверлея";
                    _rectModeLabelPrefix = "Предел"; _rectSizeLabelPrefix = "Размер: "; _circleRadiusLabelPrefix = "Радиус: "; _circleIntensityLabelPrefix = "Размер Круга: "; _visualModeUIPrefix = "Визуальный: "; _stereoModeUIPrefix = "Режим канала: "; _editModeUIText = "Настройки: ";
                    _visualModeNames = new[] { "Волна", "Пэд", "Круг", "Контур" }; _soundModeNames = new[] { "2 Канала", "5.1 Каналов", "7.1 Каналов" };
                    _opacityFixedSizeLabels = new[] { "Размер Волны", "Размер Панели", "Размер Круга", "Толщина Контура" };
                    SetComboItems(_visualModeNames, _soundModeNames);
                    _forceUpdateHUDTexts = true; UpdateGuidelinePositions();
                    break;
            }
        }

        private void SetComboItems(string[] visualModes, string[] soundModes)
        {
            if (CmbEditPanelVisualMode != null)
            {
                for (int i = 0; i < visualModes.Length && i < CmbEditPanelVisualMode.Items.Count; i++)
                    ((System.Windows.Controls.ComboBoxItem)CmbEditPanelVisualMode.Items[i]).Content = visualModes[i];
            }
            if (CmbEditPanelSoundMode != null)
            {
                for (int i = 0; i < soundModes.Length && i < CmbEditPanelSoundMode.Items.Count; i++)
                    ((System.Windows.Controls.ComboBoxItem)CmbEditPanelSoundMode.Items[i]).Content = soundModes[i];
            }
            if (EditPanelOpacityFixedSizeLabel != null)
            {
                EditPanelOpacityFixedSizeLabel.Text = _opacityFixedSizeLabels[AppSettings.VisualMode];
            }
        }
        private void ComboBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && !comboBox.IsDropDownOpen)
            {
                e.Handled = true;
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders || CmbLanguage == null || CmbLanguage.SelectedItem == null) return;

            if (CmbLanguage.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag != null)
            {
                string newLang = item.Tag.ToString() ?? "KOR";
                
                // MainWindow uses English keys (e.g. "Japanese"), but LauncherWindow.SetLanguage
                // and AppSettings.Language expect native names (e.g. "日本語"). Convert.
                string nativeLangKey = newLang switch
                {
                    "KOR" => "KOR",
                    "English" => "English",
                    "Japanese" => "日本語",
                    "Chinese" => "中文",
                    "Spanish" => "Español",
                    "French" => "Français",
                    "German" => "Deutsch",
                    "Russian" => "Русский",
                    _ => newLang
                };

                if (AppSettings.Language != nativeLangKey)
                {
                    AppSettings.Language = nativeLangKey;
                    AppSettings.Save();
                    
                    ApplyLanguage(newLang);
                    
                    var lw = Application.Current.Windows.OfType<LauncherWindow>().FirstOrDefault();
                    if (lw != null)
                    {
                        lw.SetLanguage(nativeLangKey);
                    }
                }
            }
        }
    }
}
