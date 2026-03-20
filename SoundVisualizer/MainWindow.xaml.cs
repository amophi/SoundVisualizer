using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using SoundVisualizer.CoreAudio; // 수현
using SoundVisualizer.DSP;       // 가람
using SoundVisualizer.AIModel;   // 성진

namespace SoundVisualizer
{
    public partial class MainWindow : Window
    {
        // 3명의 팀원이 만든 엔진 장착
        private AudioCaptureEngine _captureEngine;
        private AudioRouter _audioRouter;
        private VectorCalculator _vectorCalc;
        private SoundClassifier _soundAI;

        public MainWindow()
        {
            InitializeComponent();
            ApplyClickThroughMagic(); // 1. 마우스 클릭 관통 세팅
            BootSequence();           // 2. 통합 엔진 가동
        }

        private void BootSequence()
        {
            // 각 모듈 초기화
            _vectorCalc = new VectorCalculator();
            _soundAI = new SoundClassifier();
            _captureEngine = new AudioCaptureEngine();
            _audioRouter = new AudioRouter();

            // 🌟 [핵심] 수현이가 퍼온 오디오 데이터를 가람, 성진, 메인 UI로 분배!
            _captureEngine.OnAudioDataAvailable += HandleAudioDataAsync;

            // 엔진 스타트!
            _captureEngine.StartCapture();
            // _audioRouter.StartRouting(_captureEngine.WaveFormat); // 라우팅 엔진 동시 가동
        }

        // 0.01초 단위로 미친듯이 호출되는 통합 이벤트 핸들러
        private async void HandleAudioDataAsync(object sender, byte[] rawData)
        {
            // 🚨 UI 스레드가 멈추지 않도록 Task.Run으로 무거운 연산들을 백그라운드로 던져버립니다!
            await Task.Run(() =>
            {
                // [Path A] 라우팅 (수현): 훔친 소리 유저 이어폰으로 쏴주기
                _audioRouter.OnDataReceived(this, rawData);

                // [Path B] 수학 (가람): 각 방향 퍼센트 뽑기
                var (l, r, f, b, isActive) = _vectorCalc.CalculateDirection(rawData, rawData.Length);
                if (!isActive) return; // 노이즈면 여기서 컷! AI 돌릴 필요도 없음.

                // [Path C] AI 추론 (성진): 센터 채널만 뽑아서 라벨링
                string soundLabel = _soundAI.PredictSoundType(rawData, rawData.Length);

                // [Path D] UI 렌더링 (도환): 계산 다 끝났으니 화면에 그려라!
                // 백그라운드 스레드에서 화면을 건드리면 앱이 터지므로, Dispatcher를 써서 UI 스레드에 안전하게 명령을 하달함.
                Dispatcher.InvokeAsync(() => RenderRadarUI(l, r, f, b, soundLabel));
            });
        }

        private void RenderRadarUI(double l, double r, double f, double b, string label)
        {
            // 1. AI 텍스트 업데이트
            AILabelText.Text = label;

            // 2. 가람이가 뽑아준 퍼센트 수치를 기반으로 UI에 직접 숫자를 띄웁니다.
            FrontText.Text = $"↑ {f:F0}%";
            RearText.Text = $"↓ {b:F0}%";
            LeftText.Text = $"← L {l:F0}%";
            RightText.Text = $"R {r:F0}% →";
        }

        // ==========================================
        // 👻 [팀장의 흑마법] 클릭 관통 (Win32 API)
        // 화면을 덮고 있어도 넷플릭스 일시정지, 게임 총 쏘기 클릭이 다 통과됨!
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
        const int WS_EX_TRANSPARENT = 0x00000020; // 클릭 관통
        const int WS_EX_TOOLWINDOW = 0x00000080;  // Alt+Tab 목록에서 숨기기

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    }
}