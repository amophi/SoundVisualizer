using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using SoundVisualizer.CoreAudio; 
using SoundVisualizer.DSP;       
using SoundVisualizer.AIModel;   

namespace SoundVisualizer
{
    public partial class MainWindow : Window
    {
        private AudioCaptureEngine? _captureEngine;
        private AudioRouter? _audioRouter;
        private VectorCalculator? _vectorCalc;
        private SoundClassifier? _soundAI;

        // 시각화 스무딩을 위한 변수
        private double _smoothFL, _smoothFR, _smoothFC, _smoothBL, _smoothBR, _smoothSL, _smoothSR, _smoothLFE;
        private float _targetFL, _targetFR, _targetFC, _targetBL, _targetBR, _targetSL, _targetSR, _targetLFE;
        private string _currentLabel = "WaveSight 7.1 대기 중...";
        private double _animationTime = 0;
        
        // 클래스별 색상 정의 (1: White, 2: Yellow, 3: Red)
        private readonly System.Windows.Media.Color COLOR_CLASS1 = System.Windows.Media.Colors.White;
        private readonly System.Windows.Media.Color COLOR_CLASS2 = System.Windows.Media.Colors.Yellow;
        private readonly System.Windows.Media.Color COLOR_CLASS3 = System.Windows.Media.Colors.Red;

        public MainWindow()
        {
            InitializeComponent();
            ApplyClickThroughMagic(); // 윈도우 클릭 관통 설정
            BootSequence();           // 엔진 초기화 및 가동
            
            // 고프레임 애니메이션을 위한 렌더링 루프 연결 (60FPS+)
            System.Windows.Media.CompositionTarget.Rendering += OnRendering;
        }

        private void BootSequence()
        {
            _vectorCalc = new VectorCalculator();
            _soundAI = new SoundClassifier();
            _captureEngine = new AudioCaptureEngine();
            _audioRouter = new AudioRouter();

            // 오디오 데이터 이벤트 연결
            _captureEngine.OnAudioDataAvailable += HandleAudioDataAsync;

            // 캡처 시작
            _captureEngine.StartCapture();

            // 채널 수 확인 및 경고 표시 (7.1채널 필수)
            if (_captureEngine.CaptureFormat != null)
            {
                int channels = _captureEngine.CaptureFormat.Channels;
                if (channels != 8)
                {
                    StatusText.Text = $"⚠ 경고: 현재 {channels}채널(스테레오) 모드입니다. 레이더 기능을 위해 7.1채널 설정이 필요합니다.";
                    StatusText.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    StatusText.Text = "✅ 8채널(7.1) 사운드 엔진 정상 가동 중";
                    StatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                }

                _audioRouter.StartRouting(_captureEngine.CaptureFormat);
            }
        }

        private async void HandleAudioDataAsync(object? sender, AudioDataAvailableEventArgs e)
        {
            if (_audioRouter == null || _vectorCalc == null || _soundAI == null) return;

            byte[] rawData = e.Buffer;
            int bytesRecorded = rawData.Length;

            await Task.Run(() =>
            {
                // 오디오 데이터 라우팅
                _audioRouter.OnDataReceived(this, rawData);

                // DSP: 각 채널별 볼륨 추출
                var (fl, fr, fc, bl, br, sl, sr, lfe) = _vectorCalc.CalculateVolumes(rawData, bytesRecorded, e.Channels);
                
                // 타겟 볼륨 업데이트 (렌더링 루프에서 사용)
                _targetFL = fl;
                _targetFR = fr;
                _targetFC = fc;
                _targetBL = bl;
                _targetBR = br;
                _targetSL = sl;
                _targetSR = sr;
                _targetLFE = lfe;

                // AI 가공 데이터 추출
                _currentLabel = _soundAI.PredictSoundType(rawData, bytesRecorded, e.Channels);
            });
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            _animationTime += 0.05;

            // 부드러운 애니메이션을 위한 보간 (Interpolation)
            float smoothFactor = 0.15f; 
            _smoothFL += (_targetFL - _smoothFL) * smoothFactor;
            _smoothFR += (_targetFR - _smoothFR) * smoothFactor;
            _smoothFC += (_targetFC - _smoothFC) * smoothFactor;
            _smoothBL += (_targetBL - _smoothBL) * smoothFactor;
            _smoothBR += (_targetBR - _smoothBR) * smoothFactor;
            _smoothSL += (_targetSL - _smoothSL) * smoothFactor;
            _smoothSR += (_targetSR - _smoothSR) * smoothFactor;
            _smoothLFE += (_targetLFE - _smoothLFE) * smoothFactor;

            // 사운드 클래스 기반 색상 업데이트
            var activeColor = GetColorForLabel(_currentLabel);
            UpdateWaveColors(activeColor);

            // AI 라벨 업데이트
            AILabelText.Text = _currentLabel;
            AILabelText.Foreground = new System.Windows.Media.SolidColorBrush(activeColor);

            // 화면 가장자리에서 피어오르는 유기적인 파동 지오메트리 생성
            double baseDepth = 450.0;
            double w = this.ActualWidth;
            double h = this.ActualHeight;

            if (w == 0 || h == 0) return; // 아직 렌더링 준비 안됨

            // 상단
            WaveFC.Data = GenerateEdgeWave(baseDepth * _smoothFC, _animationTime, "Top", w / 2, 0);
            WaveFL.Data = GenerateCornerWave(baseDepth * _smoothFL, _animationTime, "TopLeft", 0, 0);
            WaveFR.Data = GenerateCornerWave(baseDepth * _smoothFR, _animationTime, "TopRight", w, 0);
            
            // 하단
            WaveLFE.Data = GenerateEdgeWave(baseDepth * _smoothLFE * 1.3, _animationTime, "Bottom", w / 2, h);
            WaveBL.Data = GenerateCornerWave(baseDepth * _smoothBL, _animationTime, "BottomLeft", 0, h);
            WaveBR.Data = GenerateCornerWave(baseDepth * _smoothBR, _animationTime, "BottomRight", w, h);
            
            // 측면
            WaveSL.Data = GenerateEdgeWave(baseDepth * _smoothSL, _animationTime, "Left", 0, h / 2);
            WaveSR.Data = GenerateEdgeWave(baseDepth * _smoothSR, _animationTime, "Right", w, h / 2);

            // 데이터 감쇠
            _targetFL *= 0.85f;
            _targetFR *= 0.85f;
            _targetFC *= 0.85f;
            _targetBL *= 0.85f;
            _targetBR *= 0.85f;
            _targetSL *= 0.85f;
            _targetSR *= 0.85f;
            _targetLFE *= 0.85f;
        }

        private System.Windows.Media.Geometry GenerateEdgeWave(double depth, double time, string edge, double centerX, double centerY)
        {
            if (depth < 5) return System.Windows.Media.Geometry.Empty;

            var geometry = new System.Windows.Media.StreamGeometry();
            using (var context = geometry.Open())
            {
                int points = 40; 
                double size = 800;
                
                double startX, startY;
                if (edge == "Top" || edge == "Bottom") { startX = centerX - size / 2; startY = centerY; }
                else { startX = centerX; startY = centerY - size / 2; }

                context.BeginFigure(new System.Windows.Point(startX, startY), true, true);

                for (int i = 1; i <= points; i++)
                {
                    double t = (double)i / points;
                    double bell = 0.5 - 0.5 * Math.Cos(t * 2 * Math.PI);
                    double wave = Math.Sin(t * 12 + time * 3) * 8 + Math.Sin(t * 24 - time * 5) * 4;
                    double d = (depth * bell) + (wave * (bell + 0.1));

                    double px, py;
                    if (edge == "Top") { px = centerX - size / 2 + size * t; py = centerY + d; }
                    else if (edge == "Bottom") { px = centerX - size / 2 + size * t; py = centerY - d; }
                    else if (edge == "Left") { py = centerY - size / 2 + size * t; px = centerX + d; }
                    else { py = centerY - size / 2 + size * t; px = centerX - d; }

                    context.LineTo(new System.Windows.Point(px, py), true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private System.Windows.Media.Geometry GenerateCornerWave(double depth, double time, string corner, double cornerX, double cornerY)
        {
            if (depth < 5) return System.Windows.Media.Geometry.Empty;

            var geometry = new System.Windows.Media.StreamGeometry();
            using (var context = geometry.Open())
            {
                int points = 20;
                context.BeginFigure(new System.Windows.Point(cornerX, cornerY), true, true);
                
                for (int i = 0; i <= points; i++)
                {
                    double angle = (double)i / points * (Math.PI / 2);
                    double bell = Math.Sin(angle * 2); 
                    double wave = Math.Sin(angle * 10 + time * 3.5) * 6;
                    double r = depth * (0.8 + 0.2 * bell) + wave;

                    double dx = Math.Cos(angle) * r;
                    double dy = Math.Sin(angle) * r;

                    double px = cornerX, py = cornerY;
                    if (corner == "TopLeft") { px += dx; py += dy; }
                    else if (corner == "TopRight") { px -= dx; py += dy; }
                    else if (corner == "BottomRight") { px -= dx; py -= dy; }
                    else if (corner == "BottomLeft") { px += dx; py -= dy; }

                    context.LineTo(new System.Windows.Point(px, py), true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        // ==========================================
        // 사운드 라벨에 따른 클래스 색상 결정
        // ==========================================
        private System.Windows.Media.Color GetColorForLabel(string label)
        {
            if (label.Contains("총소리") || label.Contains("폭발음") || label.Contains("기관총"))
                return COLOR_CLASS3; // 위험 (Red)
            
            if (label.Contains("발소리") || label.Contains("사이렌") || label.Contains("경적") || 
                label.Contains("자동차") || label.Contains("헬리콥터") || label.Contains("엔진소리"))
                return COLOR_CLASS2; // 주의 (Yellow)

            return COLOR_CLASS1; // 일반 (White)
        }

        private void UpdateWaveColors(System.Windows.Media.Color color)
        {
            var transparent = color;
            transparent.A = 0;
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (w == 0 || h == 0) return;

            // 절대 좌표 모드로 그라데이션 브러시 생성
            System.Windows.Media.RadialGradientBrush CreateBrush(double ax, double ay)
            {
                var brush = new System.Windows.Media.RadialGradientBrush();
                brush.MappingMode = System.Windows.Media.BrushMappingMode.Absolute;
                brush.GradientOrigin = new System.Windows.Point(ax, ay);
                brush.Center = new System.Windows.Point(ax, ay);
                brush.RadiusX = 400; // 파동 너비에 맞춘 절대 반경
                brush.RadiusY = 400; 
                
                brush.GradientStops.Add(new System.Windows.Media.GradientStop(color, 0));
                brush.GradientStops.Add(new System.Windows.Media.GradientStop(transparent, 1));
                return brush;
            }

            // 각 요소에 대해 화면 절대 좌표로 색상 중심점 고정
            WaveFC.Fill = CreateBrush(w / 2, 0); 
            WaveLFE.Fill = CreateBrush(w / 2, h); 
            WaveSL.Fill = CreateBrush(0, h / 2); 
            WaveSR.Fill = CreateBrush(w, h / 2); 
            
            WaveFL.Fill = CreateBrush(0, 0); 
            WaveFR.Fill = CreateBrush(w, 0); 
            WaveBL.Fill = CreateBrush(0, h); 
            WaveBR.Fill = CreateBrush(w, h); 
        }

        // ==========================================
        // 윈도우 클릭 관통 구현 (Win32 API 연동)
        // ==========================================
        private void ApplyClickThroughMagic()
        {
            this.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
            };
        }

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    }
}