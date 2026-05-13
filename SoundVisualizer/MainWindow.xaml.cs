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
        
        // 시각화 모듈 (Strategy Pattern)
        private IVisualizerMode[] _visualizers = new IVisualizerMode[3];
        
        public Action? OnSettingsChangedFromHotkey;

        private Thread? _renderThread;
        private volatile bool _renderRunning;
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
                _renderRunning = false;
                _captureEngine?.StopCapture();
                _soundAI?.Dispose();
            };
        }

        private void BootSequence()
        {
            _visualizers[0] = new WaveVisualizer();
            _visualizers[1] = new PadVisualizer();
            _visualizers[2] = new CircleRippleVisualizer();

            _vectorCalc = new VectorCalculator();
            _soundAI = new SoundClassifier();
            _captureEngine = new AudioCaptureEngine();
            _audioRouter = new AudioRouter();

            _captureEngine.OnAudioDataAvailable += HandleAudioDataAsync;
            _captureEngine.StartCapture();

            if (_captureEngine.CaptureFormat != null)
            {
                int channels = _captureEngine.CaptureFormat.Channels;
                if (channels != 8)
                {
                    StatusText.Text = $"⚠ 경고: 현재 {channels}채널(스테레오) 모드입니다. 레이더 기능을 위해 7.1채널 설정이 필요합니다.";
                    StatusText.Foreground = Brushes.Orange;
                }
                else
                {
                    StatusText.Text = "✅ 8채널(7.1) 사운드 엔진 정상 가동 중";
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
            _renderRunning = true;
            _fpsStopwatch.Start();

            _renderThread = new Thread(() =>
            {
                var sw = new Stopwatch();
                sw.Start();
                double lastFrameTime = 0;

                while (_renderRunning)
                {
                    double now = sw.Elapsed.TotalMilliseconds;
                    double elapsed = now - lastFrameTime;

                    if (elapsed >= FRAME_INTERVAL_MS)
                    {
                        lastFrameTime = now - (elapsed % FRAME_INTERVAL_MS);
                        try
                        {
                            Dispatcher.Invoke(RenderFrame, System.Windows.Threading.DispatcherPriority.Send);
                        }
                        catch { break; }
                    }
                    else
                    {
                        double remaining = FRAME_INTERVAL_MS - elapsed;
                        if (remaining > 2) Thread.Sleep(1);
                        else Thread.SpinWait(100);
                    }
                }
            });
            _renderThread.IsBackground = true;
            _renderThread.Priority = ThreadPriority.AboveNormal;
            _renderThread.Start();
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

            VisualModeText.Text = AppSettings.VisualMode == 0 
                ? "🎨 시각화 모드: [F3] 파도 모드 (Wave)" 
                : AppSettings.VisualMode == 1
                    ? "🎨 시각화 모드: [F3] 패드 모드 (Pad)"
                    : "🎨 시각화 모드: [F3] 원형 모드 (Circle)";

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

            StereoModeText.Text = AppSettings.SoundMode == 0 
                ? "🎧 사운드 모드: [F2] 2 채널" 
                : (AppSettings.SoundMode == 1 ? "🔊 사운드 모드: [F2] 5.1 채널" : "🔊 사운드 모드: [F2] 7.1 채널");
            StereoModeText.Foreground = AppSettings.SoundMode == 0 ? Brushes.Cyan : (AppSettings.SoundMode == 1 ? Brushes.Gold : Brushes.White);

            _frameCount++;
            double fpsElapsed = _fpsStopwatch.Elapsed.TotalSeconds;
            if (fpsElapsed >= 0.5)
            {
                _currentFps = _frameCount / fpsElapsed;
                _frameCount = 0;
                _fpsStopwatch.Restart();
                FpsText.Text = $"FPS: {_currentFps:F0}";
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
                    AILabelBorder.Visibility = Visibility.Visible;
                    AILabelText.Text = _currentLabel;
                    AILabelText.Foreground = new SolidColorBrush(activeColor);
                }
                else
                {
                    AILabelBorder.Visibility = Visibility.Collapsed;
                }
                UnifiedWave.Visibility = Visibility.Visible;
            }
            else
            {
                AILabelBorder.Visibility = Visibility.Collapsed;
                UnifiedWave.Visibility = Visibility.Collapsed;
            }

            if (DateTime.Now < _modeUIVisibleUntil)
                ModeUIStack.Visibility = Visibility.Visible;
            else
                ModeUIStack.Visibility = Visibility.Collapsed;

            if (AppSettings.IsAdminMode)
            {
                StatusBorder.Visibility = Visibility.Visible;
                FpsBorder.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                FpsBorder.Visibility = Visibility.Collapsed;
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
            var fillBrush = currentVisualizer.GetFillBrush(activeColor);
            UnifiedWave.Fill = fillBrush;
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

        private async void HandleAudioDataAsync(object? sender, AudioDataAvailableEventArgs e)
        {
            if (_audioRouter == null || _vectorCalc == null || _soundAI == null) return;

            byte[] rawData = e.Buffer;
            int bytesRecorded = rawData.Length;

            await Task.Run(() =>
            {
                _audioRouter.OnDataReceived(this, rawData);
                var (fl, fr, fc, bl, br, sl, sr, lfe) = _vectorCalc.CalculateVolumes(rawData, bytesRecorded, e.Channels);
                
                // 사용자가 2채널 확장(Upmix) 모드를 켰을 경우, 
                // 좌/우에서 들리는 소리(FL, FR)를 전체 화면으로 강제 복사하여 사방에서 파도가 치도록 만듭니다.
                if (AppSettings.SoundMode == 0)
                {
                    sl = fl; bl = fl;
                    sr = fr; br = fr;
                }

                _targetFL = fl; _targetFR = fr; _targetFC = fc;
                _targetBL = bl; _targetBR = br;
                _targetSL = sl; _targetSR = sr; _targetLFE = lfe;
                _currentLabel = _soundAI.PredictSoundType(rawData, bytesRecorded, e.Channels);
            });
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
