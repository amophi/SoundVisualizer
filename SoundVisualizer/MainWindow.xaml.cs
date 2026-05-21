using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        private AudioCaptureEngine? _captureEngine;
        private AudioRouter? _audioRouter;
        private VectorCalculator? _vectorCalc;
        private SoundClassifier? _soundAI;

        // 전체 볼륨 및 위치 분포 관련 변수
        private double _smoothTotal;
        private double _distFL = 0.125, _distFR = 0.125, _distFC = 0.125, _distBL = 0.125, _distBR = 0.125, _distSL = 0.125, _distSR = 0.125, _distLFE = 0.125;
        private double _smoothFL, _smoothFR, _smoothFC, _smoothBL, _smoothBR, _smoothSL, _smoothSR, _smoothLFE;
        private float _targetFL, _targetFR, _targetFC, _targetBL, _targetBR, _targetSL, _targetSR, _targetLFE;
        private string _currentLabel = "SoundVisualizer 대기 중...";
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
            // F3 키: 시각화 모드(Wave/Pad) 실시간 전환
            bool isVisualHotkeyPressed = (GetAsyncKeyState(AppSettings.VisualModeHotkey) & 0x8000) != 0;
            if (isVisualHotkeyPressed && !_wasVisualHotkeyPressed)
            {
                AppSettings.VisualMode = (AppSettings.VisualMode + 1) % _visualizers.Length;
                AppSettings.Save();
                OnSettingsChangedFromHotkey?.Invoke();
                _modeUIVisibleUntil = DateTime.Now.AddSeconds(5);
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

            Visibility targetModeUIStackVisibility = DateTime.Now < _modeUIVisibleUntil ? Visibility.Visible : Visibility.Collapsed;
            SetVisibilityIfChanged(ModeUIStack, ref _cachedModeUIStackVisibility, targetModeUIStackVisibility);

            if (AppSettings.IsAdminMode)
            {
                SetVisibilityIfChanged(StatusBorder, ref _cachedStatusBorderVisibility, Visibility.Visible);
                SetVisibilityIfChanged(FpsBorder, ref _cachedFpsBorderVisibility, Visibility.Visible);

                // PC 오디오 설정 채널 수
                int pcChannels = _captureEngine?.CaptureFormat?.Channels ?? 0;
                string pcLabel = pcChannels switch
                {
                    8 => "7.1",
                    6 => "5.1",
                    2 => "스테레오",
                    > 0 => $"{pcChannels}ch",
                    _ => "확인 중..."
                };
                string pcLine = pcChannels > 0
                    ? $"PC 오디오 설정: {pcLabel} ({pcChannels}ch)"
                    : "PC 오디오 설정: 확인 중...";

                // 현재 소스 채널 수
                int srcCh = _lastSourceChannels;
                string srcLine = srcCh == 0 ? "현재 캡처 중인 오디오 채널 정보: 대기 중..." : $"현재 캡처 중인 오디오 채널 정보: {srcCh}ch";

                string targetStatusText = $"{pcLine}\n{srcLine}";
                Brush targetStatusForeground = (pcChannels == 8)
                    ? (srcCh == 8 ? Brushes.LimeGreen : srcCh >= 6 ? Brushes.Yellow : Brushes.Orange)
                    : Brushes.Orange;

                if (_cachedStatusText != targetStatusText)
                {
                    _cachedStatusText = targetStatusText;
                    StatusText.Text = targetStatusText;
                }
                if (_cachedStatusForeground != targetStatusForeground)
                {
                    _cachedStatusForeground = targetStatusForeground;
                    StatusText.Foreground = targetStatusForeground;
                }
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
    }
}
