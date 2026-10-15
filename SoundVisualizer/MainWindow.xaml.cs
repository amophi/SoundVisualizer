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
using SoundVisualizer.Rendering;
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

        // 캡처 스레드는 _peaks 에 넣고, 렌더 루프가 프레임마다 가져가 엔진에 넘긴다.
        // 엔진(전체 크기·방향 분포·깊이의 스무딩)은 렌더 스레드만 쓴다.
        private readonly PeakAccumulator _peaks = new();
        private readonly float[] _peakFrame = new float[PeakAccumulator.ChannelCount];
        private readonly VisualizerEngine _engine = new();
        private SoundType _currentType = SoundType.Ambient;
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

        // 쉬는 동안(엔진이 IsIdle) 프레임마다 그리지 않고 이 타이머로 단축키·HUD·소리를 확인한다.
        // 조용히 쉬는 동안에는 캡처 스레드가 큰 소리가 든 버퍼에서 확인을 바로 앞당긴다(_wake).
        private readonly System.Windows.Threading.DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromMilliseconds(VisualizerEngine.IdlePollMs) };
        private readonly SoundWakeSignal _wake = new(VisualizerEngine.WakeThreshold);
        private readonly Action _pollIdleNow;

        private const int WAVE_SAMPLE_COUNT = 400;

        public MainWindow()
        {
            _pollIdleNow = PollIdle;
            _idleTimer.Tick += (s, e) => PollIdle();
            InitializeComponent();
            LanguagePicker.Fill(CmbLanguage);
            ColorButtonNames.Apply(EditPanelAmbientColorBtn, EditPanelSpeechColorBtn, EditPanelDangerColorBtn);
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            LoadSettingsToEditPanel(); // UI 초기화 직후 설정값을 미리 반영하여, 지연된 레이아웃 패스에서 기본값(50)이 이벤트를 발생시켜도 올바른 값이 유지되도록 함
            _isUpdatingEditPanelSliders = false;
            ApplyClickThroughMagic();
            BootSequence();
            StartHighFpsRenderLoop();
            InitDebugHud(); // 개발자 모드 HUD 와 결과 기록(MainWindow.DebugHud.cs)

            this.Closed += (s, e) =>
            {
                Loc.Instance.LanguageChanged -= OnLanguageChanged;

                if (_topmostTimer != null)
                {
                    _topmostTimer.Stop();
                    _topmostTimer = null;
                }

                CompositionTarget.Rendering -= OnCompositionTargetRendering;
                _idleTimer.Stop();
                _wake.Disarm();

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
            WatchCaptureErrors();
            _captureEngine.StartCapture();
            CheckCaptureStarted(); // 시작하지 못했으면 런처가 이 창을 띄우지 않고 이유를 알린다(MainWindow.Status.cs)

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
            _frameCount++;
            UpdateHotkeysAndHud(nowTicks);

            // AI 결과는 프레임마다 한 번만 읽고, 이 프레임의 판단은 모두 그 한 결과로 한다.
            var aiResult = Volatile.Read(ref _aiPipeline)?.LastClassification();
            _currentType = aiResult?.Coarse ?? SoundType.Ambient;

            bool isVisible = IsTypeVisible(_currentType);
            var activeColor = GetColorForType(_currentType);

            // 지난 프레임 뒤로 들어온 버퍼들의 채널별 최고값. 버퍼가 없었으면 엔진이 이전 값을 흐른 시간만큼 줄인다.
            // 스무딩·깊이·패드와 원형의 움직임은 모두 60fps 기준으로 시간 정규화되어 최대 프레임과 상관없이 같은 빠르기다.
            // 숨긴 종류의 소리는 엔진이 약 1초 보관했다가, 위협음 판정이 늦게 오면 표시 중이었을 크기로 그린다.
            bool tookPeaks = _peaks.TryTake(_peakFrame);
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            _engine.SetSurfaceSize(w, h);
            _engine.Tick(_renderStopwatch.Elapsed.TotalMilliseconds, tookPeaks ? _peakFrame : ReadOnlySpan<float>.Empty,
                new OverlayInput(AppSettings.CurrentModeSettings, AppSettings.VisualMode, AppSettings.SoundMode, _currentType, isVisible),
                keepAwake: _isEditMode);

            // 보이는 것(표시를 켠 종류, 문턱을 넘는 진하기, 보이는 도형)이 있을 때만 도형을 만든다.
            // 크기 고정 모드에서 소리가 잦아들어 진하기가 0 에 다가가면 보이지 않는 도형을 화면 전체 크기로 그리지 않고 접는다.
            if (_engine.Visible && w > 0 && h > 0)
            {
                DrawWave(w, h, activeColor);
            }
            else
            {
                SetVisibilityIfChanged(UnifiedWave, ref _cachedUnifiedWaveVisibility, Visibility.Collapsed);
            }

            if (_isEditMode)
            {
                UpdateGuidelinePositions();
            }

            // 아무것도 보이지 않은 지 1초가 지나 엔진이 쉬기로 했으면, 프레임마다 그리지 않고 33ms 확인으로 내려간다.
            if (_engine.IsIdle) PauseRendering();
        }

        /// <summary>
        /// 그리기 말고 프레임마다 하던 일: 단축키, 모드 HUD, 개발자 모드의 FPS 표시와 AI HUD 켜고 끄기. 쉬는 동안에도 <see cref="PollIdle"/> 이 부른다.
        /// </summary>
        /// <param name="pollHotkeysNow">33ms 를 재지 않고 단축키를 지금 확인한다. 쉬는 동안의 확인은 타이머가 이미 그 간격이다
        /// (TickCount 는 15.6ms 단위라, 재면 31ms 만에 온 확인을 건너뛴다).</param>
        private void UpdateHotkeysAndHud(long nowTicks, bool pollHotkeysNow = false)
        {
            // 1. 핫키 체크 스로틀링 (약 30Hz - CPU 사용량 감소 및 P/Invoke 부하 최소화)
            if (pollHotkeysNow || nowTicks - _lastHotkeyCheckTick >= 33)
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

            // 개발자 모드·결과 기록 설정을 따른다. 런처나 편집 패널에서 바꾸면 다음 프레임(쉬는 동안에는 다음 확인)에 켜고 끈다.
            SyncDebugHud();
        }

        private void DrawWave(double w, double h, Color activeColor)
        {
            // 깊이(Intensity 100%가 화면 중앙 최대 한계선에 도달하도록 매핑)는 엔진이 정했다.
            // 0:상단중앙, 1:우상단, 2:우측중앙, 3:우하단, 4:하단중앙, 5:좌하단, 6:좌측중앙, 7:좌상단
            _engine.Depths.CopyTo(_channelDepths);

            // 분포를 둘레 자리 순서로 옮긴다. 2.0 은 이미 그 순서이고, 5.1·7.1 은 캡처 순서(FL, FR, FC, BL, BR, SL, SR, LFE)라
            // 옮겨 놓는다. 하단 가운데 자리에는 LFE 를 둔다.
            var dist = _engine.Distribution;
            if (AppSettings.SoundMode == 0)
            {
                dist.CopyTo(_channelDists);
            }
            else
            {
                _channelDists[0] = dist[2];
                _channelDists[1] = dist[1];
                _channelDists[2] = dist[6];
                _channelDists[3] = dist[4];
                _channelDists[4] = dist[7];
                _channelDists[5] = dist[3];
                _channelDists[6] = dist[5];
                _channelDists[7] = dist[0];
            }

            _renderContext.Width = w;
            _renderContext.Height = h;
            _renderContext.BaseDepth = _engine.BaseDepth;
            _renderContext.ChannelDepths = _channelDepths;
            _renderContext.ChannelDists = _channelDists;
            _engine.PadThickness.CopyTo(_renderContext.PadThickness);
            _engine.CircleDepths.CopyTo(_renderContext.CircleDepths);
            _renderContext.TotalVolume = _engine.TotalTarget;
            _renderContext.AnimationTime = _engine.AnimationTime;

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
            
            UnifiedWave.Opacity = _engine.Alpha;
            SetVisibilityIfChanged(UnifiedWave, ref _cachedUnifiedWaveVisibility, Visibility.Visible);
        }

        // ==========================================
        // 쉬기: 아무것도 보이지 않으면 프레임마다 그리지 않는다
        // ==========================================

        /// <summary>
        /// 엔진이 쉬기로 했다. CompositionTarget.Rendering 을 떼어 WPF 가 최대 프레임으로 렌더 틱을 돌리지 않게 하고,
        /// 33ms 타이머로 단축키·HUD·소리만 확인한다.
        /// </summary>
        private void PauseRendering()
        {
            CompositionTarget.Rendering -= OnCompositionTargetRendering;
            _idleTimer.Start();
        }

        /// <summary>그릴 것이 생겼다. 다음 화면 틱부터 다시 프레임마다 그린다.</summary>
        private void ResumeRendering()
        {
            _wake.Disarm();
            _idleTimer.Stop();
            if (_isClosed) return;
            CompositionTarget.Rendering -= OnCompositionTargetRendering; // 두 번 붙지 않게
            CompositionTarget.Rendering += OnCompositionTargetRendering;
        }

        /// <summary>
        /// 쉬는 동안 33ms 마다, 또는 캡처 스레드가 큰 소리로 앞당겨 부른다. 단축키·모드 HUD·개발자 표시는 깨어 있을 때처럼 하고,
        /// 그릴 수 있는 소리가 나거나 편집을 시작하면 다시 프레임마다 그린다.
        /// </summary>
        private void PollIdle()
        {
            if (_isClosed || !_engine.IsIdle) return;
            UpdateHotkeysAndHud(Environment.TickCount64, pollHotkeysNow: true);

            // 편집 중에는 쉬지 않는다. 가이드라인을 끌 때 그래픽도 바로 다시 그린다.
            if (_isEditMode)
            {
                _engine.Resume();
                ResumeRendering();
                return;
            }

            var aiResult = Volatile.Read(ref _aiPipeline)?.LastClassification();
            _currentType = aiResult?.Coarse ?? SoundType.Ambient;
            bool isVisible = IsTypeVisible(_currentType);

            // 확인하기 직전부터 들어오는 큰 소리만 다음 확인을 앞당긴다. 확인과 다음 확인 사이에 온 소리도 놓치지 않는다.
            _wake.Arm();
            bool tookPeaks = _peaks.TryTake(_peakFrame);
            if (_engine.PollWake(_renderStopwatch.Elapsed.TotalMilliseconds, tookPeaks ? _peakFrame : ReadOnlySpan<float>.Empty,
                new OverlayInput(AppSettings.CurrentModeSettings, AppSettings.VisualMode, AppSettings.SoundMode, _currentType, isVisible)))
            {
                ResumeRendering();
                return;
            }

            // 표시를 꺼 둔 소리가 나는 중이거나 늦게 올 위협음 판정을 위해 소리를 보관하는 중이면 33ms 확인만 한다.
            // 그때도 큰 소리로 앞당기면 버퍼마다(약 10ms) 확인하게 된다.
            if (!_engine.CanSleepUntilLoud) _wake.Disarm();
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

            // 2.0 모드에서 앞 좌우를 화면 둘레로 펼치는 것(Mid/Side)은 렌더 쪽 엔진이 한다(StereoUpmix).
            // 렌더 루프가 다음 프레임에 가져간다. 그사이 버퍼가 여럿이면 채널마다 가장 큰 값이 남는다.
            _peaks.Offer(fl, fr, fc, bl, br, sl, sr, lfe);

            // 이 버퍼의 채널별 최고값. 스택에 두어 버퍼마다 할당하지 않는다.
            Span<float> frame = stackalloc float[] { fl, fr, fc, bl, br, sl, sr, lfe };

            // 개발자 모드 HUD 의 소리 크기(모바일 AudioEngine.currentLevel). 읽어도 비우지 않는 가장 최근 버퍼의 값이다.
            _level.Offer(frame, Environment.TickCount64);

            // 조용히 쉬는 오버레이를 소리가 난 이 버퍼에서 바로 깨운다(33ms 확인을 기다리지 않는다).
            // 오버레이가 그리는 동안에는 원자 변수 읽기 한 번으로 끝난다.
            if (_wake.IsArmed)
            {
                if (_wake.OnBuffer(VisualizerEngine.Loudness(frame, AppSettings.SoundMode)))
                {
                    Dispatcher.InvokeAsync(_pollIdleNow);
                }
            }

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
                    NotifyAiAvailability(true);
                }
                catch (Exception ex)
                {
                    created?.Dispose();
                    AppLog.Warn("ai", $"AI 를 시작하지 못해 그래픽만 표시합니다({rate}Hz, {channels}ch).", ex);
                    NotifyAiAvailability(false); // 홈이 "모든 소리가 환경음 색" 이라고 알린다
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
            ShowCircleRadius();
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
            EditPanelRecordCheckBox.IsChecked = AppSettings.DeveloperRecord;
            EditPanelRecordCheckBox.Visibility = AppSettings.IsAdminMode ? Visibility.Visible : Visibility.Collapsed;

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

        /// <summary>
        /// 원형 모드면 반지름 슬라이더를 보이고 지금 값을 넣는다. 화면의 점선 고리를 마우스로 끄는 것과 같은 값을 키보드로도 바꾸게 한다.
        /// 값을 넣는 동안은 슬라이더 처리기가 설정에 다시 쓰지 않는다.
        /// </summary>
        private void ShowCircleRadius()
        {
            bool circle = AppSettings.VisualMode == 2;
            EditPanelCircleRadiusSlider.Visibility = circle ? Visibility.Visible : Visibility.Collapsed;
            if (!circle) return;
            bool wasUpdating = _isUpdatingEditPanelSliders;
            _isUpdatingEditPanelSliders = true;
            EditPanelCircleRadiusSlider.Value = AppSettings.CircleRadius;
            _isUpdatingEditPanelSliders = wasUpdating;
        }

        private void EditPanelVisualMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.VisualMode = EditPanelVisualModeChips.SelectedIndex;
            AppSettings.Save();

            // 크기·진하기·속도 같은 값은 모드마다 따로 저장된다. 새 모드의 값으로 패널을 다시 채운다(F3 단축키로 바꿀 때와 같다).
            // 고정 크기 이름과 원형 모드의 반지름 줄도 여기서 함께 바뀐다.
            LoadSettingsToEditPanel();

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
            else if (sender == EditPanelCircleRadiusSlider)
            {
                AppSettings.CircleRadius = EditPanelCircleRadiusSlider.Value;
                UpdateGuidelinePositions();
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

        /// <summary>
        /// 편집 패널에서 Esc 는 단축키(기본 F4)와 같이 저장하고 닫는다. 화면 전체를 덮는 패널이라 키보드로 쓰는 사람에게 닫는 키가 하나 더
        /// 있어야 한다(단축키는 바꿀 수 있어 무엇인지 기억해야 한다). 언어 목록이 열려 있거나 단축키를 받는 중이면 그쪽이 Esc 를 먼저 가져간다.
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || !_isEditMode || e.Key != System.Windows.Input.Key.Escape) return;
            ToggleEditMode(false);
            e.Handled = true;
        }

        private void EditPanelAdmin_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            AppSettings.IsAdminMode = EditPanelAdminCheckBox.IsChecked ?? false;
            // 결과 기록은 개발자 모드에서만 쓰므로 그때만 보인다(런처와 같다). 값은 건드리지 않는다.
            EditPanelRecordCheckBox.Visibility = AppSettings.IsAdminMode ? Visibility.Visible : Visibility.Collapsed;
            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
        }

        private void EditPanelRecord_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;
            // 기록기는 다음 프레임에 SyncDebugHud 가 켜고 끈다.
            AppSettings.DeveloperRecord = EditPanelRecordCheckBox.IsChecked ?? false;
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
                        EditPanelCircleRadiusSlider.Value = AppSettings.CircleRadius;
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
            ColorButtonNames.Apply(EditPanelAmbientColorBtn, EditPanelSpeechColorBtn, EditPanelDangerColorBtn);

            _forceUpdateHUDTexts = true; // HUD 는 다음 프레임에 새 언어로 그린다
            UpdateGuidelinePositions();
        }

        // 화면에 보이는 값(FPS, 가이드라인 라벨)은 Windows 지역 설정과 상관없이 같은 꼴로 적는다.
        // 편집 패널 슬라이더의 값은 서식(SliderRow)이 같은 꼴로 적는다(ValueFormatConverter).
        private static string Number(double value) => value.ToString("F0", CultureInfo.InvariantCulture);
    }
}

