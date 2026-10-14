// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SoundVisualizer.Audio;
using SoundVisualizer.CoreAudio;
using SoundVisualizer.DSP;
using SoundVisualizer.Ai;
using SoundVisualizer.Localization;
using SoundVisualizer.Ui;
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
        // AI 는 캡처 형식(샘플레이트·채널 수)이 정해진 뒤 백그라운드에서 만든다. 만드는 동안과 만들지 못했을 때는 null 이고,
        // 그동안 그래픽은 환경음 색으로 그린다. 캡처 스레드가 넣고(Ingest) 렌더 루프가 읽는다(LastClassification).
        private RealtimeAiPipeline? _aiPipeline;
        private readonly object _aiGate = new();
        private int _aiRequestedRate, _aiRequestedChannels; // 만들기를 요청한 형식. 같은 형식을 거듭 만들지 않게 한다.
        private volatile bool _isClosed;
        private static YamnetClassMap? _classMap;
        private static readonly string ModelDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "AIModel");

        // 전체 볼륨 및 위치 분포 관련 변수
        private double _smoothTotal;
        private double _distFL = 0.125, _distFR = 0.125, _distFC = 0.125, _distBL = 0.125, _distBR = 0.125, _distSL = 0.125, _distSR = 0.125, _distLFE = 0.125;
        private double _smoothFL, _smoothFR, _smoothFC, _smoothBL, _smoothBR, _smoothSL, _smoothSR, _smoothLFE;
        // 렌더 스레드만 쓴다. 캡처 스레드는 _peaks 에 넣고, 렌더 루프가 프레임마다 가져와 여기에 옮긴다.
        private float _targetFL, _targetFR, _targetFC, _targetBL, _targetBR, _targetSL, _targetSR, _targetLFE;
        private readonly PeakAccumulator _peaks = new();
        private readonly float[] _peakFrame = new float[PeakAccumulator.ChannelCount];
        private SoundType _currentType = SoundType.Ambient;
        private AiClassificationResult? _labelTextSource; // 개발자 모드 라벨을 마지막으로 만든 결과(같으면 문자열을 다시 만들지 않는다)
        private double _animationTime = 0;
        private DateTime _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);

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
        
        // 표현 모드(AppSettings.VisualMode)·사운드 모드(AppSettings.SoundMode) 순서대로 놓은 문자열 키.
        // 글자는 쓸 때마다 Loc 에서 꺼내므로 언어를 바꾸면 다음 갱신부터 새 언어로 나온다.
        private static readonly string[] VisualModeKeys = { "settings.mode.wave", "settings.mode.pad", "settings.mode.circle", "settings.mode.outline" };
        private static readonly string[] SoundModeKeys = { "settings.channels.2", "settings.channels.5_1", "settings.channels.7_1" };
        private static readonly string[] FixedSizeKeys = { "settings.fixed_size.wave", "settings.fixed_size.pad", "settings.fixed_size.circle", "settings.fixed_size.outline" };

        // 시각화 모듈 (Strategy Pattern)
        private IVisualizerMode[] _visualizers = new IVisualizerMode[4];
        
        // 렌더링 최적화를 위한 재사용 객체
        private VisualizerContext _renderContext = new VisualizerContext();
        private double[] _channelDepths = new double[8];
        private double[] _channelDists = new double[8];
        
        public Action? OnSettingsChangedFromHotkey;

        private readonly Stopwatch _renderStopwatch = new();
        private double _lastRenderTimeMs = 0;
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
            LanguagePicker.Fill(CmbLanguage);
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            LoadSettingsToEditPanel(); // UI 초기화 직후 설정값을 미리 반영하여, 지연된 레이아웃 패스에서 기본값(50)이 이벤트를 발생시켜도 올바른 값이 유지되도록 함
            _isUpdatingEditPanelSliders = false;
            ApplyClickThroughMagic();
            BootSequence();
            StartHighFpsRenderLoop();

            this.Closed += (s, e) =>
            {
                Loc.Instance.LanguageChanged -= OnLanguageChanged;

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

                // 캡처를 먼저 멈춘 뒤 AI 를 닫는다. 도는 추론이 있으면 끝날 때까지 최대 1초 기다린다.
                lock (_aiGate) _isClosed = true;
                Interlocked.Exchange(ref _aiPipeline, null)?.Dispose();
            };
        }

        private void BootSequence()
        {
            _visualizers[0] = new WaveVisualizer();
            _visualizers[1] = new PadVisualizer();
            _visualizers[2] = new CircleRippleVisualizer();
            _visualizers[3] = new OutlineVisualizer();

            _vectorCalc = new VectorCalculator();
            _captureEngine = new AudioCaptureEngine();
            _audioRouter = new AudioRouter();

            _captureEngine.OnChannelsChanged += (s, currentChannels) =>
            {
                // 장치를 다시 여는 스레드에서 불린다. 기다리지 않고 넘긴다. 기다리면 그사이 UI 스레드가 캡처를 멈추려 할 때
                // 서로 상대를 기다리며 멈출 수 있다.
                Dispatcher.InvokeAsync(() =>
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

            var captureFormat = _captureEngine.CaptureFormat;
            AppLog.Info("capture", captureFormat == null
                ? "캡처 장치를 열지 못해 AI 를 시작하지 않습니다."
                : $"캡처 형식: {captureFormat.SampleRate}Hz, {captureFormat.Channels}ch, {captureFormat.BitsPerSample}bit {captureFormat.Encoding}.");
            if (_captureEngine.CaptureFormat != null)
            {
                int channels = _captureEngine.CaptureFormat.Channels;
                if (channels != 8)
                {
                    StatusText.Text = Loc.Format("hud.status.stereo", channels);
                    StatusText.Foreground = Brushes.Orange;
                }
                else
                {
                    StatusText.Text = Loc.Get("hud.status.surround");
                    StatusText.Foreground = Brushes.LimeGreen;
                }
                _audioRouter.StartRouting(_captureEngine.CaptureFormat);
                RequestAiPipeline(_captureEngine.CaptureFormat.SampleRate, _captureEngine.CaptureFormat.Channels);
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
                string targetVisualModeText = Loc.Format("hud.visual_mode", visualKeyName, Loc.Get(VisualModeKeys[AppSettings.VisualMode]));
                if (_cachedVisualModeText != targetVisualModeText)
                {
                    _cachedVisualModeText = targetVisualModeText;
                    VisualModeText.Text = targetVisualModeText;
                }

                string stereoKeyName = GetKeysName(AppSettings.StereoUpmixKeyBind);
                string targetStereoModeText = Loc.Format("hud.sound_mode", stereoKeyName, Loc.Get(SoundModeKeys[AppSettings.SoundMode]));
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
                string targetEditModeText = Loc.Format("hud.overlay_settings", editKeyName);
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
                
                string targetFpsText = "FPS: " + Number(_currentFps);
                if (_cachedFpsText != targetFpsText)
                {
                    _cachedFpsText = targetFpsText;
                    FpsText.Text = targetFpsText;
                }
            }

            _animationTime += 0.035;

            // 지난 프레임 뒤로 들어온 버퍼들의 채널별 최고값. 버퍼가 없었으면 이전 값을 그대로 두고 아래에서 줄인다.
            if (_peaks.TryTake(_peakFrame))
            {
                _targetFL = _peakFrame[0]; _targetFR = _peakFrame[1]; _targetFC = _peakFrame[2]; _targetBL = _peakFrame[3];
                _targetBR = _peakFrame[4]; _targetSL = _peakFrame[5]; _targetSR = _peakFrame[6]; _targetLFE = _peakFrame[7];
            }

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

            // AI 결과는 프레임마다 한 번만 읽고, 이 프레임의 판단은 모두 그 한 결과로 한다.
            var aiResult = Volatile.Read(ref _aiPipeline)?.LastClassification();
            _currentType = aiResult?.Coarse ?? SoundType.Ambient;

            bool isVisible = IsTypeVisible(_currentType);
            var activeColor = GetColorForType(_currentType);

            if (isVisible)
            {
                if (AppSettings.IsAdminMode)
                {
                    SetVisibilityIfChanged(AILabelBorder, ref _cachedAILabelBorderVisibility, Visibility.Visible);
                    
                    if (_labelTextSource != aiResult || _cachedAILabelText.Length == 0)
                    {
                        _labelTextSource = aiResult;
                        string labelText = FormatAiLabel(aiResult);
                        if (_cachedAILabelText != labelText)
                        {
                            _cachedAILabelText = labelText;
                            AILabelText.Text = labelText;
                        }
                    }

                    SolidColorBrush activeBrush = GetBrushForType(_currentType);
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
            if (_audioRouter == null || _vectorCalc == null) return;

            byte[] rawData = e.Buffer;
            int bytesRecorded = e.BytesRecorded;
            int channels = e.Channels;

            // 1. 오디오 라우팅과 멀티채널 볼륨 연산을 캡처 스레드에서 직접 동기 실행 (극히 가벼움)
            _audioRouter.OnDataReceived(rawData, bytesRecorded);

            var (fl, fr, fc, bl, br, sl, sr, lfe) = _vectorCalc.CalculateVolumes(rawData, bytesRecorded, channels);
            
            // 사용자가 2채널 확장(Upmix) 모드를 켰을 경우, 
            // 좌/우에서 들리는 소리(FL, FR)를 전체 화면으로 강제 복사하여 사방에서 파도가 치도록 만듭니다.
            if (AppSettings.SoundMode == 0)
            {
                sl = fl; bl = fl;
                sr = fr; br = fr;
            }

            // 렌더 루프가 다음 프레임에 가져간다. 그사이 버퍼가 여럿이면 채널마다 가장 큰 값이 남는다.
            _peaks.Offer(fl, fr, fc, bl, br, sl, sr, lfe);

            // AI 는 이 캡처 형식에 맞춰 만든 파이프라인에만 넣는다. 장치가 바뀌어 형식이 달라졌으면 새로 만들기를 요청한다.
            // float32 가 아닌 형식은 AI 가 읽지 못하므로 넣지 않는다(WASAPI 공유 모드의 믹스 형식은 보통 float32 다).
            var format = _captureEngine?.CaptureFormat;
            if (format != null && format.BitsPerSample == 32)
            {
                var ai = Volatile.Read(ref _aiPipeline);
                if (ai != null && ai.SampleRate == format.SampleRate && ai.ChannelCount == channels)
                {
                    ai.Ingest(MemoryMarshal.Cast<byte, float>(rawData.AsSpan(0, bytesRecorded)));
                }
                else
                {
                    RequestAiPipeline(format.SampleRate, channels);
                }
            }
        }

        /// <summary>
        /// 캡처 형식에 맞는 AI 파이프라인을 백그라운드에서 만든다. 모델을 읽는 데 0.1초 남짓 걸려 UI·캡처 스레드에서 하지 않는다.
        /// 형식이 바뀌면(장치 변경) 옛 파이프라인을 버리고 새로 만든다. 지원하지 않는 샘플레이트이거나 모델 파일이 맞지 않으면
        /// AI 없이 그래픽만 돈다(이유는 AppLog 에 남는다). 같은 형식은 다시 요청해도 한 번만 만든다.
        /// </summary>
        private void RequestAiPipeline(int rate, int channels)
        {
            lock (_aiGate)
            {
                if (_isClosed) return;
                if (_aiRequestedRate == rate && _aiRequestedChannels == channels) return;
                _aiRequestedRate = rate;
                _aiRequestedChannels = channels;
            }

            var previous = Interlocked.Exchange(ref _aiPipeline, null);
            Task.Run(() =>
            {
                previous?.Dispose();
                RealtimeAiPipeline? created = null;
                try
                {
                    var classes = _classMap ??= YamnetClassMap.Load(System.IO.Path.Combine(ModelDirectory, "yamnet_class_map.csv"));
                    created = RealtimeAiPipeline.Create(rate, channels, ModelDirectory, classes,
                        mappingOverrides: () => AppSettings.SoundTypes,
                        tickFailed: ex => AppLog.Warn("ai", "AI 추론 한 번이 실패했습니다.", ex));
                    lock (_aiGate)
                    {
                        // 만드는 사이 형식이 또 바뀌었거나 창이 닫혔으면 버린다.
                        if (_isClosed || _aiRequestedRate != rate || _aiRequestedChannels != channels)
                        {
                            created.Dispose();
                            return;
                        }
                        created.Start();
                        Volatile.Write(ref _aiPipeline, created);
                    }
                    AppLog.Info("ai", $"AI 를 시작했습니다({rate}Hz, {channels}ch).");
                }
                catch (Exception ex)
                {
                    created?.Dispose();
                    AppLog.Warn("ai", $"AI 를 시작하지 못해 그래픽만 표시합니다({rate}Hz, {channels}ch).", ex);
                }
            });
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

        private SolidColorBrush GetBrushForType(SoundType type)
        {
            UpdateCachedBrushesIfNeeded();
            return type switch
            {
                SoundType.Danger => _dangerBrush!,
                SoundType.Speech => _speechBrush!,
                _ => _ambientBrush!,
            };
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

        private Color GetColorForType(SoundType type)
        {
            UpdateCachedBrushesIfNeeded();
            return type switch
            {
                SoundType.Danger => _dangerColor,
                SoundType.Speech => _speechColor,
                _ => _ambientColor,
            };
        }

        private static bool IsTypeVisible(SoundType type) => type switch
        {
            SoundType.Danger => AppSettings.ShowDanger,
            SoundType.Speech => AppSettings.ShowSpeech,
            _ => AppSettings.ShowAmbient,
        };

        /// <summary>개발자 모드 라벨. AI 가 아직 결과를 내지 않았거나 동작하지 않으면 그렇다고 적는다.</summary>
        private static string FormatAiLabel(AiClassificationResult? result) => result == null
            ? "AI: -"
            : string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{result.Display} | {result.Coarse.ToLabel()} | {result.Confidence * 100:0.0}%");

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
            if (codes == null || codes.Count == 0) return Loc.Get("hotkey.none");
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

            // 콤보 항목의 Tag 가 설정값(system 또는 언어 코드)이다.
            LanguagePicker.Select(CmbLanguage, AppSettings.Language);

            // 모드 칸 선택 상태 동기화
            EditPanelVisualModeChips.SelectedIndex = AppSettings.VisualMode;
            EditPanelSoundModeChips.SelectedIndex = AppSettings.SoundMode;

            // 슬라이더 값 글자는 서식(SliderRow)이 값에서 바로 만든다.
            EditPanelSpeedSlider.Value = AppSettings.WavePositionSpeed;
            EditPanelSensitivitySlider.Value = AppSettings.WaveSensitivity * 4.0;
            // 진하기·최대 진하기는 저장값(불투명도 %)을 그대로 보인다. 예전에는 100 에서 뺀 투명도로 보였다.
            EditPanelOpacitySlider.Value = AppSettings.VisualOpacity;
            EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;

            EditPanelIntensityAsOpacityCheckBox.IsChecked = AppSettings.IntensityAsOpacity;
            ShowSizeRows(AppSettings.IntensityAsOpacity);
            SettingRow.SetLabel(EditPanelOpacityFixedSizeSlider, Loc.Get(FixedSizeKeys[AppSettings.VisualMode]));
            EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
            EditPanelOpacityFixedMaxOpacitySlider.Value = AppSettings.OpacityFixedMaxOpacity;

            EditPanelGlowCheckBox.IsChecked = AppSettings.IsGlowMode;
            EditPanelGlowSlider.Visibility = AppSettings.IsGlowMode ? Visibility.Visible : Visibility.Collapsed;
            EditPanelGlowSlider.Value = AppSettings.GlowIntensity;

            // 소리 분류 표시 설정 바인딩
            EditPanelShowAmbientCheckBox.IsChecked = AppSettings.ShowAmbient;
            EditPanelShowSpeechCheckBox.IsChecked = AppSettings.ShowSpeech;
            EditPanelShowDangerCheckBox.IsChecked = AppSettings.ShowDanger;

            // 색상 버튼 배경색 설정
            EditPanelAmbientColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppSettings.ColorAmbient));
            EditPanelSpeechColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppSettings.ColorSpeech));
            EditPanelDangerColorBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AppSettings.ColorDanger));

            // 단축키 설정 바인딩
            BtnEditPanelVisualHotkey.Content = GetKeysName(AppSettings.VisualModeKeyBind);
            BtnEditPanelSoundModeHotkey.Content = GetKeysName(AppSettings.StereoUpmixKeyBind);
            BtnEditPanelEditHotkey.Content = GetKeysName(AppSettings.EditModeKeyBind);
            EditPanelHotkeyText.Text = GetKeysName(AppSettings.EditModeKeyBind);

            // 고급 설정 바인딩
            EditPanelTargetFpsSlider.Maximum = AppSettings.GetMonitorRefreshRate();
            EditPanelTargetFpsSlider.Value = AppSettings.TargetFps;
            EditPanelAdminCheckBox.IsChecked = AppSettings.IsAdminMode;

            _isUpdatingEditPanelSliders = false;
        }

        /// <summary>
        /// 크기 고정을 켜면 고정 크기·최대 진하기를, 끄면 크기·진하기를 보인다. 지금 먹지 않는 쪽은 흐리게 남기지 않고 접는다
        /// (런처의 모드 카드, 안드로이드 설정과 같다).
        /// </summary>
        private void ShowSizeRows(bool sizeLocked)
        {
            EditPanelSizeLockedRows.Visibility = sizeLocked ? Visibility.Visible : Visibility.Collapsed;
            EditPanelSizeFreeRows.Visibility = sizeLocked ? Visibility.Collapsed : Visibility.Visible;
        }

        private void EditPanelVisualMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.VisualMode = EditPanelVisualModeChips.SelectedIndex;
            AppSettings.Save();

            SettingRow.SetLabel(EditPanelOpacityFixedSizeSlider, Loc.Get(FixedSizeKeys[AppSettings.VisualMode]));

            // 모드가 변경되면 그에 맞추어 UI 정보와 가이드라인 형태도 리셋
            UpdateGuidelinePositions();

            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelSoundMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.SoundMode = EditPanelSoundModeChips.SelectedIndex;
            AppSettings.Save();

            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingEditPanelSliders) return;

            if (sender == EditPanelSpeedSlider)
            {
                AppSettings.WavePositionSpeed = EditPanelSpeedSlider.Value;
            }
            else if (sender == EditPanelSensitivitySlider)
            {
                AppSettings.WaveSensitivity = EditPanelSensitivitySlider.Value / 4.0;
                OnSettingsChangedFromHotkey?.Invoke();
            }
            else if (sender == EditPanelOpacitySlider)
            {
                AppSettings.VisualOpacity = EditPanelOpacitySlider.Value;
                UnifiedWave.Opacity = Math.Max(0.0, AppSettings.VisualOpacity) / 100.0;
            }
            else if (sender == EditPanelIntensitySlider)
            {
                AppSettings.WaveIntensity = EditPanelIntensitySlider.Value;
                UpdateGuidelinePositions();
            }
            else if (sender == EditPanelOpacityFixedSizeSlider)
            {
                AppSettings.OpacityFixedSize = EditPanelOpacityFixedSizeSlider.Value;
                UpdateGuidelinePositions();
            }
            else if (sender == EditPanelOpacityFixedMaxOpacitySlider)
            {
                AppSettings.OpacityFixedMaxOpacity = EditPanelOpacityFixedMaxOpacitySlider.Value;
            }
            else if (sender == EditPanelGlowSlider)
            {
                AppSettings.GlowIntensity = EditPanelGlowSlider.Value;
            }
            else if (sender == EditPanelTargetFpsSlider)
            {
                AppSettings.TargetFps = EditPanelTargetFpsSlider.Value;
            }

            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelIntensityAsOpacity_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.IntensityAsOpacity = EditPanelIntensityAsOpacityCheckBox.IsChecked ?? false;
            ShowSizeRows(AppSettings.IntensityAsOpacity);

            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelGlow_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.IsGlowMode = EditPanelGlowCheckBox.IsChecked == true;
            EditPanelGlowSlider.Visibility = AppSettings.IsGlowMode ? Visibility.Visible : Visibility.Collapsed;

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
            string msg = Loc.Get("hotkey.prompt");
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

                string modeName = Loc.Get(VisualModeKeys[visualMode]);
                double displayIntensity = AppSettings.IntensityAsOpacity ? AppSettings.OpacityFixedSize : AppSettings.WaveIntensity;
                if (RectModeLabel != null) RectModeLabel.Text = Loc.Format("editor.guide.limit", modeName);
                if (RectSizeLabel != null) RectSizeLabel.Text = Loc.Format("editor.guide.size", modeName, Number(displayIntensity));
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
                CircleRadiusLabel.Text = Loc.Format("editor.guide.base_radius", Number(AppSettings.CircleRadius));
                CircleIntensityLabel.Text = Loc.Format("editor.guide.circle_size", Number(displayIntensity));
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
                EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
            }
            else
            {
                if (intensity < 10.0) intensity = 10.0;
                if (intensity > 100.0) intensity = 100.0;
                AppSettings.WaveIntensity = intensity;
                EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
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
                        EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
                    }
                    else
                    {
                        if (intensity < 0.0) intensity = 0.0;
                        if (intensity > 100.0) intensity = 100.0;
                        AppSettings.WaveIntensity = intensity;
                        EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
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
                    EditPanelOpacityFixedSizeSlider.Value = AppSettings.OpacityFixedSize;
                }
                else
                {
                    if (intensity < 10.0) intensity = 10.0;
                    if (intensity > 100.0) intensity = 100.0;
                    AppSettings.WaveIntensity = intensity;
                    EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
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
        private void ComboBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && !comboBox.IsDropDownOpen)
            {
                e.Handled = true;
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            if (LanguagePicker.SelectedSetting(CmbLanguage) is { } setting && setting != AppSettings.Language)
            {
                // 저장하고 런처까지 열린 창 모두에 적용한다. 이 창의 코드가 넣은 글자는 OnLanguageChanged 가 바꾼다.
                Loc.Choose(setting);
            }
        }

        /// <summary>언어가 바뀌었다(런처나 이 패널에서). 언어 콤보를 맞추고, 바인딩이 아니라 코드가 넣은 글자를 다시 넣는다.</summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            bool wasUpdating = _isUpdatingEditPanelSliders;
            _isUpdatingEditPanelSliders = true; // 콤보를 맞추는 동안 다시 언어를 바꾸지 않게
            LanguagePicker.Select(CmbLanguage, AppSettings.Language);
            _isUpdatingEditPanelSliders = wasUpdating;

            SettingRow.SetLabel(EditPanelOpacityFixedSizeSlider, Loc.Get(FixedSizeKeys[AppSettings.VisualMode]));
            if (_bindingTarget != "Visual") BtnEditPanelVisualHotkey.Content = GetKeysName(AppSettings.VisualModeKeyBind);
            if (_bindingTarget != "Sound") BtnEditPanelSoundModeHotkey.Content = GetKeysName(AppSettings.StereoUpmixKeyBind);
            if (_bindingTarget != "Edit") BtnEditPanelEditHotkey.Content = GetKeysName(AppSettings.EditModeKeyBind);
            EditPanelHotkeyText.Text = GetKeysName(AppSettings.EditModeKeyBind);

            _forceUpdateHUDTexts = true; // HUD 는 다음 프레임에 새 언어로 그린다
            UpdateGuidelinePositions();
        }

        // 화면에 보이는 값(FPS, 가이드라인 라벨)은 Windows 지역 설정과 상관없이 같은 꼴로 적는다.
        // 편집 패널 슬라이더의 값은 서식(SliderRow)이 같은 꼴로 적는다(ValueFormatConverter).
        private static string Number(double value) => value.ToString("F0", CultureInfo.InvariantCulture);
    }
}

