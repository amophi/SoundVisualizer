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

namespace SoundVisualizer
{
    public partial class MainWindow : Window
    {
        // 1. 파도의 크기 (최대 진폭 스케일)
        public double WaveIntensity = 100.0;
        // 2. 파도 위치 변화 속도 (화면상 파도의 위치 변화 속도)
        public double WavePositionSpeed = 10.0;
        // 3. 파도의 민감성/떨림 (소리 크기가 변할 때 얼마나 즉각적으로 출렁이는지)
        public double WaveSensitivity = 10.0;

        // 4. 시각화 모드 (0 = Wave 모드, 1 = Window 모드)
        public int VisualMode = 0;

        private AudioCaptureEngine? _captureEngine;
        private AudioRouter? _audioRouter;
        private VectorCalculator? _vectorCalc;
        private SoundClassifier? _soundAI;

        // 전체 볼륨 및 위치 분포 관련 변수
        private double _smoothTotal;
        private double _distFL = 0.125, _distFR = 0.125, _distFC = 0.125, _distBL = 0.125, _distBR = 0.125, _distSL = 0.125, _distSR = 0.125, _distLFE = 0.125;
        
        private double _lastWindowTheta = 0;
        private double _windowBarThickness = 0;

        private double _smoothFL, _smoothFR, _smoothFC, _smoothBL, _smoothBR, _smoothSL, _smoothSR, _smoothLFE;
        private float _targetFL, _targetFR, _targetFC, _targetBL, _targetBR, _targetSL, _targetSR, _targetLFE;
        private string _currentLabel = "WaveSight 7.1 대기 중...";
        private double _animationTime = 0;
        
        // 색상
        private readonly Color COLOR_CLASS1 = Colors.White;
        private readonly Color COLOR_CLASS2 = Colors.Yellow;
        private readonly Color COLOR_CLASS3 = Colors.Red;

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
            float sfTremor = (float)(Math.Max(0.1, WaveSensitivity) / 100.0);
            if (sfTremor > 1.0f) sfTremor = 1.0f;
            _smoothTotal += (totalTarget - _smoothTotal) * sfTremor;

            // 위치 변화 속도: 소리가 어디서 나는지 분포 채널 비율을 쫓아가는 팩터 (좌우 이동)
            float sfPosition = (float)(Math.Max(0.1, WavePositionSpeed) / 100.0);
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

            var activeColor = GetColorForLabel(_currentLabel);
            AILabelText.Text = _currentLabel;
            AILabelText.Foreground = new SolidColorBrush(activeColor);

            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (w == 0 || h == 0) return;

            // 파도 모드용 깊이 스케일 (윈도우 모드에서는 무시됨)
            double baseDepth = 450.0 * (Math.Max(0.0, WaveIntensity) / 100.0);
            double[] channelDepths = new double[]
            {
                baseDepth * _smoothFC,         // 상단 중앙
                baseDepth * _smoothFR,         // 우상단
                baseDepth * _smoothSR,         // 우측 중앙
                baseDepth * _smoothBR,         // 우하단
                baseDepth * _smoothLFE * 1.3,  // 하단 중앙
                baseDepth * _smoothBL,         // 좌하단
                baseDepth * _smoothSL,         // 좌측 중앙
                baseDepth * _smoothFL,         // 좌상단
            };

            if (VisualMode == 1)
            {
                UnifiedWave.Data = GenerateWindowModeGeometry((float)totalTarget, w, h);
            }
            else
            {
                UnifiedWave.Data = GenerateUnifiedPerimeterWave(channelDepths, _animationTime, w, h);
            }
            UpdateUnifiedWaveColor(activeColor, w, h);

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
                _targetFL = fl; _targetFR = fr; _targetFC = fc;
                _targetBL = bl; _targetBR = br;
                _targetSL = sl; _targetSR = sr; _targetLFE = lfe;
                _currentLabel = _soundAI.PredictSoundType(rawData, bytesRecorded, e.Channels);
            });
        }

        // ==========================================
        // 직각 프레임(Picture Frame) 파동 생성
        // 
        // 테두리에 딱 붙는 '직선' 형태를 원하시는 요청을 반영.
        // 각 변의 파도는 완벽하게 직선 축을 따라 일어납니다.
        // 구석 모양이 이상해지는 것을 방지하기 위해 구석 지점을 수학적으로 Clamp(제한)하여
        // 완벽하게 딱 맞아떨어지는 90도 안쪽 조인트(액자 프레임 형태)를 생성합니다.
        // ==========================================
        private Geometry GenerateUnifiedPerimeterWave(double[] channelDepths, double time, double w, double h)
        {
            bool anyActive = false;
            for (int i = 0; i < channelDepths.Length; i++)
            {
                if (channelDepths[i] > 3) { anyActive = true; break; }
            }
            if (!anyActive) return Geometry.Empty;

            double P = 2 * (w + h);

            // 각 채널 위치와 거리를 모니터 테두리 둘레(dist)로 계산
            double dist_tc = 0;
            double dist_tr = w / 2.0;
            double dist_rc = w / 2.0 + h / 2.0;
            double dist_br = w / 2.0 + h;
            double dist_bc = w / 2.0 + h + w / 2.0;
            double dist_bl = w / 2.0 + h + w;
            double dist_lc = w / 2.0 + h + w + h / 2.0;
            double dist_tl = w / 2.0 + h + w + h;

            double[] channelPos = new double[]
            {
                dist_tc / P,
                dist_tr / P,
                dist_rc / P,
                dist_br / P,
                dist_bc / P,
                dist_bl / P,
                dist_lc / P,
                dist_tl / P
            };

            // 4개 코너 지점의 정확한 파도 깊이(Offset) 프리컴파일
            double d_tr = GetWaveDepth(dist_tr / P, time, channelDepths, channelPos);
            double d_br = GetWaveDepth(dist_br / P, time, channelDepths, channelPos);
            double d_bl = GetWaveDepth(dist_bl / P, time, channelDepths, channelPos);
            double d_tl = GetWaveDepth(dist_tl / P, time, channelDepths, channelPos);

            int N = WAVE_SAMPLE_COUNT;
            var inner = new Point[N];

            for (int i = 0; i < N; i++)
            {
                double dist = (P * i) / N; 
                double t = dist / P;
                
                // 테두리 위의 포인트와 파도 깊이 추출
                Point edgePos = GetEdgePosition(dist, w, h, P);
                double d = GetWaveDepth(t, time, channelDepths, channelPos);
                
                // 해당 점이 위치하는 변(Top, Right, Bottom, Left)을 판별 후,
                // 구석에서 서로 침범하지 않도록(Overlap 방지) 좌우상하를 완벽한 90도 직각으로 제한(Clamp)합니다.
                if (dist <= dist_tr || dist > dist_tl) // 상단 변 (상단 테두리에 매달린 파도)
                {
                    double x = Math.Max(d_tl, Math.Min(w - d_tr, edgePos.X));
                    inner[i] = new Point(x, d);
                }
                else if (dist <= dist_br) // 우측 변 (우측 테두리에 매달린 파도)
                {
                    double y = Math.Max(d_tr, Math.Min(h - d_br, edgePos.Y));
                    inner[i] = new Point(w - d, y);
                }
                else if (dist <= dist_bl) // 하단 변 (하단 테두리에 매달린 파도)
                {
                    double x = Math.Max(d_bl, Math.Min(w - d_br, edgePos.X));
                    inner[i] = new Point(x, h - d);
                }
                else // 좌측 변 (좌측 테두리에 매달린 파도)
                {
                    double y = Math.Max(d_tl, Math.Min(h - d_bl, edgePos.Y));
                    inner[i] = new Point(d, y);
                }
            }

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            using (var ctx = geometry.Open())
            {
                // ===== 서브도형 1: 바깥쪽 모니터 직각 테두리 =====
                ctx.BeginFigure(new Point(0, 0), true, true);
                ctx.LineTo(new Point(w, 0), false, false);
                ctx.LineTo(new Point(w, h), false, false);
                ctx.LineTo(new Point(0, h), false, false);

                // ===== 서브도형 2: 안쪽 액자 프레임 형태의 연결 파도 =====
                ctx.BeginFigure(inner[0], true, true);

                var bezierPts = new List<Point>(N * 3);
                for (int i = 0; i < N; i++)
                {
                    Point p0 = inner[(i - 1 + N) % N];
                    Point p1 = inner[i];
                    Point p2 = inner[(i + 1) % N];
                    Point p3 = inner[(i + 2) % N];

                    Point cp1 = new Point(
                        p1.X + (p2.X - p0.X) / 6.0,
                        p1.Y + (p2.Y - p0.Y) / 6.0);
                    Point cp2 = new Point(
                        p2.X - (p3.X - p1.X) / 6.0,
                        p2.Y - (p3.Y - p1.Y) / 6.0);

                    bezierPts.Add(cp1);
                    bezierPts.Add(cp2);
                    bezierPts.Add(p2);
                }

                ctx.PolyBezierTo(bezierPts, true, true);
            }

            geometry.Freeze();
            return geometry;
        }

        // ==========================================
        // Window 모드: 정확한 소리 방향에 디스플레이 세로 길이 1/3 크기의 바 한 개만 표시
        // WaveIntensity, WaveSensitivity 무시, WavePositionSpeed 로만 이동
        // ==========================================
        private Geometry GenerateWindowModeGeometry(float totalVolume, double w, double h)
        {
            // 소리가 날 때만 바 렌더링. 고정된 페이드 아웃(0.2)
            // 아름다운 빔 형태를 위해 최대 두께를 25.0까지 주고 부드럽게 깎습니다.
            double targetThickness = (totalVolume > 0.05f) ? 25.0 : 0.0;
            _windowBarThickness += (targetThickness - _windowBarThickness) * 0.2; 
            if (_windowBarThickness < 0.5) return Geometry.Empty;

            // 채널 분포(_dist)를 통해 중심점 방향(Vector) 추적
            // 정확히 화면 구석(Corner)을 찌르기 위해 모니터 가로(w)/세로(h) 스케일을 각각의 축에 곱해줍니다.
            double dx = 0, dy = 0;
            dx +=  0 * _distFC; dy += -h * _distFC; // Top
            dx +=  w * _distFR; dy += -h * _distFR; // TopRight (정확히 우상단 모서리)
            dx +=  w * _distSR; dy +=  0 * _distSR; // Right
            dx +=  w * _distBR; dy +=  h * _distBR; // BottomRight (정확히 우하단 모서리)
            dx +=  0 * _distLFE; dy+=  h * _distLFE; // Bottom
            dx += -w * _distBL; dy +=  h * _distBL; // BottomLeft (정확히 좌하단 모서리)
            dx += -w * _distSL; dy +=  0 * _distSL; // Left
            dx += -w * _distFL; dy += -h * _distFL; // TopLeft (정확히 좌상단 모서리)

            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.01)
            {
                // 소리 방향 갱신
                _lastWindowTheta = Math.Atan2(dy, dx);
            }

            double theta = _lastWindowTheta;
            double P = 2 * (w + h);
            double centerDist = 0;

            double trAngle = Math.Atan2(-h / 2.0, w / 2.0);
            double brAngle = Math.Atan2(h / 2.0, w / 2.0);
            double blAngle = Math.Atan2(h / 2.0, -w / 2.0);
            double tlAngle = Math.Atan2(-h / 2.0, -w / 2.0);

            // 각도(Theta)에 따라 모니터 테두리 상의 중심(Dist) 구하기
            if (theta >= trAngle && theta < brAngle) // Right
            {
                double y = (w / 2.0) * Math.Tan(theta);
                centerDist = w / 2.0 + (y + h / 2.0);
            }
            else if (theta >= brAngle && theta <= blAngle) // Bottom
            {
                double x = (Math.Abs(theta - Math.PI / 2.0) < 0.001) ? 0 : (h / 2.0) / Math.Tan(theta);
                centerDist = w / 2.0 + h + (w / 2.0 - x);
            }
            else if (theta > tlAngle && theta < trAngle) // Top
            {
                double x = (Math.Abs(theta + Math.PI / 2.0) < 0.001) ? 0 : (-h / 2.0) / Math.Tan(theta);
                if (x >= 0) centerDist = x;
                else centerDist = P + x; // 0 미만은 P방향(Top Left측)으로 연장
            }
            else // Left
            {
                double y = (-w / 2.0) * Math.Tan(theta);
                centerDist = w / 2.0 + h + w + (h / 2.0 - y);
            }

            double barLen = h / 3.0; // 디스플레이 세로 길이 1/3 (크기 고정)
            double maxThickness = _windowBarThickness;

            double startDist = centerDist - barLen / 2.0;
            
            int N = 50; 
            var outerPts = new Point[N + 1];
            var innerPts = new Point[N + 1];

            for (int i = 0; i <= N; i++)
            {
                double dist = startDist + (barLen * i) / N;
                Point edgePoint = GetEdgePosition(dist, w, h, P);
                outerPts[i] = edgePoint;

                // 양 끝단으로 갈수록 두께가 슬림해지는(Tapered) 곡선 디자인 (물방울/유선형)
                double t = (double)i / N;
                double ease = Math.Sin(t * Math.PI); 
                ease = Math.Pow(ease, 0.6); // 끝부분을 더 부드럽고 날렵하게 깎습니다.
                double currentThickness = maxThickness * ease;

                double dMod = ((dist % P) + P) % P;
                double ix = edgePoint.X, iy = edgePoint.Y;

                // 테두리 방향에 따라 내측(Inward) 좌표 밀어넣고, 코너에서 안전하게 Clamp
                if (dMod <= w / 2.0 || dMod > w / 2.0 + h + w + h) 
                { iy += currentThickness; ix = Math.Max(currentThickness, Math.Min(w - currentThickness, ix)); }
                else if (dMod <= w / 2.0 + h) 
                { ix -= currentThickness; iy = Math.Max(currentThickness, Math.Min(h - currentThickness, iy)); }
                else if (dMod <= w / 2.0 + h + w) 
                { iy -= currentThickness; ix = Math.Max(currentThickness, Math.Min(w - currentThickness, ix)); }
                else 
                { ix += currentThickness; iy = Math.Max(currentThickness, Math.Min(h - currentThickness, iy)); }

                innerPts[i] = new Point(ix, iy);
            }

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(outerPts[0], true, true);
                for (int i = 1; i <= N; i++) ctx.LineTo(outerPts[i], false, false);
                for (int i = N; i >= 0; i--) ctx.LineTo(innerPts[i], false, false);
            }
            geometry.Freeze();
            return geometry;
        }

        // ==========================================
        // 테두리 둘레(dist)에 따른 정확한 모니터 모서리 좌표 반환
        // ==========================================
        private Point GetEdgePosition(double dist, double w, double h, double P)
        {
            dist = ((dist % P) + P) % P;

            // 상단 우측 절반 (Top Center -> Top Right)
            if (dist <= w / 2) return new Point(w / 2 + dist, 0);
            
            // 우측 변 전체 (Top Right -> Bottom Right)
            if (dist <= w / 2 + h) return new Point(w, dist - w / 2);

            // 하단 변 전체 (Bottom Right -> Bottom Left)
            if (dist <= w / 2 + h + w) return new Point(w - (dist - (w / 2 + h)), h);

            // 좌측 변 전체 (Bottom Left -> Top Left)
            if (dist <= w / 2 + h + w + h) return new Point(0, h - (dist - (w / 2 + h + w)));

            // 상단 좌측 절반 (Top Left -> Top Center)
            return new Point(dist - (w / 2 + h + w + h), 0);
        }

        // ==========================================
        // 보간 및 파동 수학 유틸리티
        // ==========================================

        private double GetWaveDepth(double t, double time, double[] depths, double[] positions)
        {
            double depth = InterpolateDepthCatmullRom(t, depths, positions);
            return Math.Max(0, depth); // 인위적인 출렁임 제거, 실제 볼륨만 반영
        }

        private double InterpolateDepthCatmullRom(double t, double[] depths, double[] positions)
        {
            t = ((t % 1.0) + 1.0) % 1.0;
            int n = positions.Length;

            int idx1 = 0;
            for (int i = 0; i < n; i++)
            {
                if (positions[i] <= t) idx1 = i;
            }
            int idx0 = (idx1 - 1 + n) % n;
            int idx2 = (idx1 + 1) % n;
            int idx3 = (idx1 + 2) % n;

            double p1 = positions[idx1];
            double p2 = positions[idx2];
            if (p2 <= p1) p2 += 1.0;

            double at = t;
            if (at < p1) at += 1.0;

            double segLen = p2 - p1;
            double lt = (segLen > 0) ? (at - p1) / segLen : 0;
            lt = Math.Max(0, Math.Min(1, lt));

            double d0 = depths[idx0], d1 = depths[idx1];
            double d2 = depths[idx2], d3 = depths[idx3];

            double a = -0.5 * d0 + 1.5 * d1 - 1.5 * d2 + 0.5 * d3;
            double b = d0 - 2.5 * d1 + 2.0 * d2 - 0.5 * d3;
            double c = -0.5 * d0 + 0.5 * d2;
            double dv = d1;

            double result = a * lt * lt * lt + b * lt * lt + c * lt + dv;
            return Math.Max(0, result);
        }


        // ==========================================
        // 통합 웨이브 색상 업데이트
        // ==========================================
        private void UpdateUnifiedWaveColor(Color color, double w, double h)
        {
            if (VisualMode == 1)
            {
                // Window 모드: 투명도/그라데이션을 사용하지 않고 100% 단색(Solid) 적용
                UnifiedWave.Fill = new SolidColorBrush(color);
                return;
            }

            var transparent = color;
            transparent.A = 0;
            var semiTransparent = color;
            semiTransparent.A = 160;

            var brush = new RadialGradientBrush();
            brush.GradientOrigin = new Point(0.5, 0.5);
            brush.Center = new Point(0.5, 0.5);
            brush.RadiusX = 0.5;
            brush.RadiusY = 0.5;
            brush.GradientStops.Add(new GradientStop(transparent, 0.0));
            brush.GradientStops.Add(new GradientStop(semiTransparent, 0.7));
            brush.GradientStops.Add(new GradientStop(color, 1.0));

            UnifiedWave.Fill = brush;
        }

        private Color GetColorForLabel(string label)
        {
            if (label.Contains("danger")) return COLOR_CLASS3;
            if (label.Contains("speech")) return COLOR_CLASS2;
            return COLOR_CLASS1;
        }

        // ==========================================
        // 윈도우 클릭 관통 구현 
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