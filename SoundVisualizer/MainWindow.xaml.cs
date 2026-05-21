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

        private Color _cachedActiveColor = Colors.Transparent;
        private int _cachedVisualModeForBrush = -1;

        private string _cachedVisualModeText = "";
        private string _cachedStereoModeText = "";
        private Brush? _cachedStereoModeForeground = null;
        private string _cachedFpsText = "";
        private string _cachedStatusText = "";
        private Brush? _cachedStatusForeground = null;

        private Visibility _cachedAILabelBorderVisibility = Visibility.Collapsed;
        private string _cachedAILabelText = "";
        private Brush? _cachedAILabelForeground = null;
        private Visibility _cachedUnifiedWaveVisibility = Visibility.Collapsed;

        private Visibility _cachedStatusBorderVisibility = Visibility.Collapsed;
        private Visibility _cachedFpsBorderVisibility = Visibility.Collapsed;
        private Visibility _cachedModeUIStackVisibility = Visibility.Collapsed;
        
        // YAMNet 추론 스로틀링 제어 필드
        private readonly object _aiLock = new();
        private volatile bool _isPredicting = false;
        private long _lastPredictTimeTicks = 0;
        private const long AI_PREDICT_INTERVAL_MS = 250; // YAMNet 추론 주기 (250ms)

        // 시각화 모듈 (Strategy Pattern)
        private IVisualizerMode[] _visualizers = new IVisualizerMode[4];
        
        public Action? OnSettingsChangedFromHotkey;

        private readonly Stopwatch _renderStopwatch = new();
        private double _lastRenderTimeMs = 0;
        private long _lastChannelCountTick = 0;
        private const double TARGET_FPS = 144.0;
        private const double FRAME_INTERVAL_MS = 1000.0 / TARGET_FPS;
        
        private int _frameCount;
        private double _currentFps;
        private readonly Stopwatch _fpsStopwatch = new();

        private const int WAVE_SAMPLE_COUNT = 400;

        public MainWindow()
        {
            InitializeComponent();
            ApplyClickThroughMagic();
            BootSequence();
            StartHighFpsRenderLoop();

            this.Closed += (s, e) =>
            {
                CompositionTarget.Rendering -= OnCompositionTargetRendering;
                
                if (_captureEngine != null)
                {
                    _captureEngine.OnAudioDataAvailable -= HandleAudioDataAsync;
                    _captureEngine.StopCapture();
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
                    StatusText.Text = $"⚠ PC 오디오 설정: {channels}ch (스테레오)\n소스: 대기 중...";
                    StatusText.Foreground = Brushes.Orange;
                }
                else
                {
                    StatusText.Text = "✅ PC 오디오 설정: 8ch (7.1)\n소스: 대기 중...";
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
            double now = _renderStopwatch.Elapsed.TotalMilliseconds;
            double elapsed = now - _lastRenderTimeMs;

            if (elapsed >= FRAME_INTERVAL_MS)
            {
                _lastRenderTimeMs = now - (elapsed % FRAME_INTERVAL_MS);
                RenderFrame();
            }
        }

        private void RenderFrame()
        {
            // 실시간 오버레이 편집 모드 토글
            bool isEditHotkeyPressed = (GetAsyncKeyState(AppSettings.EditModeHotkey) & 0x8000) != 0;
            if (isEditHotkeyPressed && !_wasEditHotkeyPressed)
            {
                ToggleEditMode(!_isEditMode);
            }
            _wasEditHotkeyPressed = isEditHotkeyPressed;

            // F3 키: 시각화 모드(Wave/Pad) 실시간 전환
            bool isVisualHotkeyPressed = (GetAsyncKeyState(AppSettings.VisualModeHotkey) & 0x8000) != 0;
            if (isVisualHotkeyPressed && !_wasVisualHotkeyPressed)
            {
                AppSettings.VisualMode = (AppSettings.VisualMode + 1) % _visualizers.Length;
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
                _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);

                if (_isEditMode)
                {
                    LoadSettingsToEditPanel();
                    UpdateGuidelinePositions();
                }
            }
            _wasVisualHotkeyPressed = isVisualHotkeyPressed;

            string targetVisualModeText = AppSettings.VisualMode == 0 
                ? "🎨 시각화 모드: [F3] 파도 모드 (Wave)" 
                : AppSettings.VisualMode == 1
                    ? "🎨 시각화 모드: [F3] 패드 모드 (Pad)"
                    : AppSettings.VisualMode == 2
                        ? "🎨 시각화 모드: [F3] 원형 모드 (Circle)"
                        : "🎨 시각화 모드: [F3] 외각선 모드 (Outline)";
            if (_cachedVisualModeText != targetVisualModeText)
            {
                _cachedVisualModeText = targetVisualModeText;
                VisualModeText.Text = targetVisualModeText;
            }

            // F2 키: 스테레오 확장 모드 실시간 전환
            bool isStereoHotkeyPressed = (GetAsyncKeyState(AppSettings.StereoUpmixHotkey) & 0x8000) != 0;
            if (isStereoHotkeyPressed && !_wasStereoHotkeyPressed)
            {
                AppSettings.SoundMode = (AppSettings.SoundMode + 1) % 3;
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
                _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);
                
                if (_isEditMode)
                {
                    LoadSettingsToEditPanel();
                }
            }
            _wasStereoHotkeyPressed = isStereoHotkeyPressed;

            string targetStereoModeText = AppSettings.SoundMode == 0 
                ? "🎧 사운드 모드: [F2] 2 채널" 
                : (AppSettings.SoundMode == 1 ? "🔊 사운드 모드: [F2] 5.1 채널" : "🔊 사운드 모드: [F2] 7.1 채널");
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

            // VisualizerContext 설정 (Intensity 100일때 기존 대비 2배 효과를 주기 위해 * 6.0)
            double baseDepth = 450.0 * (Math.Max(0.0, AppSettings.WaveIntensity * 6.0) / 100.0);
            double[] channelDepths;
            
            if (AppSettings.SoundMode == 0)
            {
                // 0:상단중앙, 1:우상단, 2:우측중앙, 3:우하단, 4:하단중앙, 5:좌하단, 6:좌측중앙, 7:좌상단
                channelDepths = new double[]
                {
                    0, 0, baseDepth * _smoothFR, 0,
                    0, 0, baseDepth * _smoothFL, 0
                };
            }
            else
            {
                double phantomBC = (_smoothBL + _smoothBR) / 2.0;
                channelDepths = new double[]
                {
                    baseDepth * _smoothFC, baseDepth * _smoothFR, baseDepth * _smoothSR, baseDepth * _smoothBR,
                    baseDepth * phantomBC, baseDepth * _smoothBL, baseDepth * _smoothSL, baseDepth * _smoothFL
                };
            }

            var context = new VisualizerContext
            {
                Width = w,
                Height = h,
                ChannelDepths = channelDepths,
                ChannelDists = new double[] { _distFC, _distFR, _distSR, _distBR, _distLFE, _distBL, _distSL, _distFL },
                TotalVolume = (float)totalTarget,
                AnimationTime = _animationTime
            };

            // 선택된 렌더러(Wave 또는 Pad)에게 그리기 위임
            int modeIndex = (AppSettings.VisualMode >= 0 && AppSettings.VisualMode < _visualizers.Length) ? AppSettings.VisualMode : 0;
            var currentVisualizer = _visualizers[modeIndex];

            UnifiedWave.Data = currentVisualizer.GenerateGeometry(context);
            
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
            
            UnifiedWave.Opacity = AppSettings.VisualOpacity / 100.0;

            if (AppSettings.IsGlowMode)
            {
                WaveGlowEffect.Color = activeColor;
                WaveGlowEffect.Opacity = Math.Min(1.0, AppSettings.GlowIntensity / 100.0 * 1.6); 
                WaveGlowEffect.BlurRadius = Math.Max(1.0, AppSettings.GlowIntensity * 0.5); 
            }
            else
            {
                WaveGlowEffect.Opacity = 0;
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
            int bytesRecorded = rawData.Length;
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
        private static int CountActiveChannels(byte[] buffer, int totalChannels)
        {
            const float threshold = 1e-6f;
            const int bytesPerSample = 4; // float32
            int frames = buffer.Length / (totalChannels * bytesPerSample);
            if (frames == 0) return totalChannels;

            // 채널별 RMS 계산 (전체 프레임의 일부만 샘플링)
            int step = Math.Max(1, frames / 200);
            var rms = new double[totalChannels];
            int count = 0;
            for (int i = 0; i < frames; i += step)
            {
                int baseOffset = i * totalChannels * bytesPerSample;
                for (int ch = 0; ch < totalChannels; ch++)
                {
                    float s = BitConverter.ToSingle(buffer, baseOffset + ch * bytesPerSample);
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
                var brush = new SolidColorBrush(ParseColor(dangerHex));
                brush.Freeze();
                _dangerBrush = brush;
            }
            if (_speechBrush == null || _cachedColorSpeechHex != speechHex)
            {
                _cachedColorSpeechHex = speechHex;
                var brush = new SolidColorBrush(ParseColor(speechHex));
                brush.Freeze();
                _speechBrush = brush;
            }
            if (_ambientBrush == null || _cachedColorAmbientHex != ambientHex)
            {
                _cachedColorAmbientHex = ambientHex;
                var brush = new SolidColorBrush(ParseColor(ambientHex));
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
            if (label.Contains("danger")) return ParseColor(AppSettings.ColorDanger);
            if (label.Contains("speech")) return ParseColor(AppSettings.ColorSpeech);
            return ParseColor(AppSettings.ColorAmbient);
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

        private readonly Dictionary<string, int> _hotkeys = new Dictionary<string, int>
        {
            {"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73},
            {"F5", 0x74}, {"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77},
            {"F9", 0x78}, {"F10", 0x79}, {"F11", 0x7A}, {"F12", 0x7B}
        };

        private string GetKeyName(int code)
        {
            foreach (var pair in _hotkeys)
            {
                if (pair.Value == code) return pair.Key;
            }
            return null;
        }

        private void InitHotkeyComboBoxes()
        {
            if (CmbEditPanelVisualHotkey == null || CmbEditPanelVisualHotkey.Items.Count > 0) return;

            foreach (var key in _hotkeys.Keys)
            {
                CmbEditPanelVisualHotkey.Items.Add(key);
                CmbEditPanelSoundModeHotkey.Items.Add(key);
                CmbEditPanelEditHotkey.Items.Add(key);
            }
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

        private bool _isUpdatingEditPanelSliders = false;

        private void LoadSettingsToEditPanel()
        {
            _isUpdatingEditPanelSliders = true;

            // 콤보박스 선택 상태 동기화
            if (CmbEditPanelVisualMode != null)
                CmbEditPanelVisualMode.SelectedIndex = AppSettings.VisualMode;
            if (CmbEditPanelSoundMode != null)
                CmbEditPanelSoundMode.SelectedIndex = AppSettings.SoundMode;

            EditPanelSpeedSlider.Value = AppSettings.WavePositionSpeed;
            EditPanelSpeedValueText.Text = $"{AppSettings.WavePositionSpeed:F0}";

            EditPanelSensitivitySlider.Value = AppSettings.WaveSensitivity;
            EditPanelSensitivityValueText.Text = $"{AppSettings.WaveSensitivity:F2}";

            EditPanelOpacitySlider.Value = AppSettings.VisualOpacity;
            EditPanelOpacityValueText.Text = $"{AppSettings.VisualOpacity:F0}%";

            if (EditPanelIntensitySlider != null)
            {
                EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
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
            InitHotkeyComboBoxes();

            if (CmbEditPanelVisualHotkey != null)
                CmbEditPanelVisualHotkey.SelectedItem = GetKeyName(AppSettings.VisualModeHotkey) ?? "F3";
            if (CmbEditPanelSoundModeHotkey != null)
                CmbEditPanelSoundModeHotkey.SelectedItem = GetKeyName(AppSettings.StereoUpmixHotkey) ?? "F2";
            if (CmbEditPanelEditHotkey != null)
                CmbEditPanelEditHotkey.SelectedItem = GetKeyName(AppSettings.EditModeHotkey) ?? "F4";
            
            if (EditPanelHotkeyText != null)
                EditPanelHotkeyText.Text = GetKeyName(AppSettings.EditModeHotkey) ?? "F4";

            // 고급 설정 바인딩
            if (EditPanelAdminCheckBox != null)
                EditPanelAdminCheckBox.IsChecked = AppSettings.IsAdminMode;

            _isUpdatingEditPanelSliders = false;
        }

        private void CmbEditPanelVisualMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            AppSettings.VisualMode = CmbEditPanelVisualMode.SelectedIndex;
            AppSettings.Save();

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
                AppSettings.WaveSensitivity = EditPanelSensitivitySlider.Value;
                EditPanelSensitivityValueText.Text = $"{EditPanelSensitivitySlider.Value:F2}";
            }
            else if (sender == EditPanelOpacitySlider)
            {
                AppSettings.VisualOpacity = EditPanelOpacitySlider.Value;
                EditPanelOpacityValueText.Text = $"{EditPanelOpacitySlider.Value:F0}%";
                UnifiedWave.Opacity = AppSettings.VisualOpacity / 100.0;
            }
            else if (sender == EditPanelIntensitySlider)
            {
                AppSettings.WaveIntensity = EditPanelIntensitySlider.Value;
                EditPanelIntensityValueText.Text = $"{EditPanelIntensitySlider.Value:F0}%";
                UpdateGuidelinePositions();
            }
            else if (sender == EditPanelGlowSlider)
            {
                AppSettings.GlowIntensity = EditPanelGlowSlider.Value;
                EditPanelGlowValueText.Text = $"{EditPanelGlowSlider.Value:F0}%";
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

                        AppSettings.Save();

                        // 버튼 배경색 즉시 변경
                        btn.Background = new SolidColorBrush(newMediaColor);

                        // 런처 등 외부 프로그램에 즉각 실시간 동조 반영
                        OnSettingsChangedFromHotkey?.Invoke();
                    }
                }
            }
        }

        private void CmbEditPanelHotkey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingEditPanelSliders) return;

            if (sender == CmbEditPanelVisualHotkey && CmbEditPanelVisualHotkey.SelectedItem is string vKey && _hotkeys.TryGetValue(vKey, out int vCode))
            {
                AppSettings.VisualModeHotkey = vCode;
            }
            else if (sender == CmbEditPanelSoundModeHotkey && CmbEditPanelSoundModeHotkey.SelectedItem is string sKey && _hotkeys.TryGetValue(sKey, out int sCode))
            {
                AppSettings.StereoUpmixHotkey = sCode;
            }
            else if (sender == CmbEditPanelEditHotkey && CmbEditPanelEditHotkey.SelectedItem is string eKey && _hotkeys.TryGetValue(eKey, out int eCode))
            {
                AppSettings.EditModeHotkey = eCode;
                if (EditPanelHotkeyText != null) EditPanelHotkeyText.Text = eKey;
            }

            AppSettings.Save();
            OnSettingsChangedFromHotkey?.Invoke();
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
                CircleGuideline.Visibility = Visibility.Collapsed;

                double baseDepth = 450.0 * (AppSettings.WaveIntensity * 6.0) / 100.0;
                
                double maxBaseDepth = Math.Min(w, h) / 2.0 - 10;
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

                string modeName = visualMode == 1 ? "패드" : visualMode == 3 ? "외각선" : "파도";
                RectSizeLabel.Text = $"{modeName} 한계선 (크기: {AppSettings.WaveIntensity:F0}%)";
            }
            else if (visualMode == 2)
            {
                RectGuideline.Visibility = Visibility.Collapsed;
                CircleGuideline.Visibility = Visibility.Visible;

                double cx = w / 2.0;
                double cy = h / 2.0;

                double radiusRatio = 0.05 + (AppSettings.CircleRadius - 10.0) / 90.0 * 0.35;
                double baseRadius = Math.Min(w, h) * radiusRatio;

                double baseDepth = 450.0 * (AppSettings.WaveIntensity * 6.0) / 100.0;
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

                CircleRadiusLabel.Text = $"기본 반경: {AppSettings.CircleRadius:F0}";
                CircleIntensityLabel.Text = $"파도 크기: {AppSettings.WaveIntensity:F0}%";
            }
        }

        private void ResizeHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (w == 0 || h == 0) return;

            Point mousePos = System.Windows.Input.Mouse.GetPosition(GuidelineCanvas);

            double dx = Math.Abs(mousePos.X - w / 2.0);
            double dy = Math.Abs(mousePos.Y - h / 2.0);

            double depthX = w / 2.0 - dx;
            double depthY = h / 2.0 - dy;

            double baseDepth = Math.Min(depthX, depthY);
            if (baseDepth < 0) baseDepth = 0;

            double intensity = (baseDepth / 450.0) * 100.0 / 6.0;

            if (intensity < 0.0) intensity = 0.0;
            if (intensity > 100.0) intensity = 100.0;

            AppSettings.WaveIntensity = intensity;

            UpdateGuidelinePositions();
            if (EditPanelIntensitySlider != null) EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
            if (EditPanelIntensityValueText != null) EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
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

                double baseDepth = 450.0 * (AppSettings.WaveIntensity * 6.0) / 100.0;
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
                _rectDragStartIntensity = AppSettings.WaveIntensity;
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

                    double intensity = (baseDepth / 450.0) * 100.0 / 6.0;

                    if (intensity < 0.0) intensity = 0.0;
                    if (intensity > 100.0) intensity = 100.0;

                    AppSettings.WaveIntensity = intensity;
                }

                UpdateGuidelinePositions();
                if (EditPanelIntensitySlider != null) EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                if (EditPanelIntensityValueText != null) EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
            }
            else if (_isDraggingRectIntensity)
            {
                double w = this.ActualWidth;
                double h = this.ActualHeight;
                if (w == 0 || h == 0) return;

                Point mousePos = e.GetPosition(GuidelineCanvas);

                double dx = Math.Abs(mousePos.X - w / 2.0);
                double dy = Math.Abs(mousePos.Y - h / 2.0);

                double depthX = w / 2.0 - dx;
                double depthY = h / 2.0 - dy;

                double baseDepth = Math.Min(depthX, depthY);
                if (baseDepth < 0) baseDepth = 0;

                double intensity = (baseDepth / 450.0) * 100.0 / 6.0;

                if (intensity < 10.0) intensity = 10.0;
                if (intensity > 100.0) intensity = 100.0;

                AppSettings.WaveIntensity = intensity;

                UpdateGuidelinePositions();
                if (EditPanelIntensitySlider != null) EditPanelIntensitySlider.Value = AppSettings.WaveIntensity;
                if (EditPanelIntensityValueText != null) EditPanelIntensityValueText.Text = $"{AppSettings.WaveIntensity:F0}%";
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
    }
}
