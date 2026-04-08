using System;
using System.Diagnostics;
using System.IO;
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
        // =========================================================
        // ✅ 테스트 모드 (WAV 기반 AI 단독 테스트)
        // - 실시간 엔진이 준비되기 전, 모델/전처리/추론 검증용
        // - Windows VM에서 pull 후 바로 실행 가능한 형태 목표
        // =========================================================
        private const bool EnableWavAiTestMode = true;

        // 3명의 팀원이 만든 엔진 장착
        private AudioCaptureEngine _captureEngine;
        private AudioRouter _audioRouter;
        private VectorCalculator _vectorCalc;
        private SoundClassifier _soundAI;

        public MainWindow()
        {
            InitializeComponent();
            ApplyClickThroughMagic(); // 1. 마우스 클릭 관통 세팅

            if (EnableWavAiTestMode)
            {
                _soundAI = new SoundClassifier();

                // UI가 뜬 다음에 실행 (생성자에서 바로 MessageBox/IO 하면 UX가 안 좋을 수 있음)
                Loaded += async (_, __) => await RunWavAiTestsAsync();
            }
            else
            {
                BootSequence(); // 2. 통합 엔진 가동
            }
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

        private async Task RunWavAiTestsAsync()
        {
            try
            {
                // 테스트 WAV는 실행 폴더 기준으로 찾습니다.
                // 권장 위치: (빌드 출력)\test1.wav, (빌드 출력)\test2.wav
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                string test1 = FindExistingPath(
                    Path.Combine(baseDir, "test1.wav"),
                    Path.Combine(baseDir, "AIModel", "test1.wav"));

                string test2 = FindExistingPath(
                    Path.Combine(baseDir, "test2.wav"),
                    Path.Combine(baseDir, "AIModel", "test2.wav"));

                if (test1 == null || test2 == null)
                {
                    string msg =
                        "WAV 테스트 파일을 찾지 못했습니다.\n\n" +
                        $"찾는 위치 예:\n- {Path.Combine(baseDir, "test1.wav")}\n- {Path.Combine(baseDir, "test2.wav")}\n\n" +
                        "해결:\n- 빌드 출력 폴더에 test1.wav/test2.wav를 복사해 주세요.";
                    MessageBox.Show(msg, "AI WAV Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // test1.wav / test2.wav 각각 테스트
                var r1 = await Task.Run(() => RunSingleTest(test1, threshold: 0.2f));
                var r2 = await Task.Run(() => RunSingleTest(test2, threshold: 0.2f));

                string summary =
                    $"file: {Path.GetFileName(test1)}\n" +
                    $"yamnet: {r1.YamnetDisplayName}\n" +
                    $"coarse: {r1.CoarseClass}\n" +
                    $"confidence: {r1.Confidence:F3}\n" +
                    $"time(ms): {r1.InferenceTimeMs:F1}\n\n" +
                    $"file: {Path.GetFileName(test2)}\n" +
                    $"yamnet: {r2.YamnetDisplayName}\n" +
                    $"coarse: {r2.CoarseClass}\n" +
                    $"confidence: {r2.Confidence:F3}\n" +
                    $"time(ms): {r2.InferenceTimeMs:F1}\n";

                Debug.WriteLine(summary);
                AILabelText.Text = $"WAV 테스트 완료: {r1.CoarseClass} / {r2.CoarseClass}";
                MessageBox.Show(summary, "AI WAV Test Results", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "AI WAV Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private InferenceResult RunSingleTest(string wavPath, float threshold)
        {
            float[] mono = WavAudioLoader.LoadMono16kHz(wavPath);
            float[] trimmed = TrimSilence(mono, thresholdAbs: 0.01f, minKeepSamples: 1600);
            float[] normalized = NormalizePeak(trimmed, targetPeak: 0.95f);

            // [C안] 오프셋 다중 추론: 시작점을 조금씩 이동해 3회 추론 후 최고 confidence 채택
            int[] offsets = new[] { 0, 800, 1600 }; // 16kHz 기준 약 0ms, 50ms, 100ms
            InferenceResult best = _soundAI.PredictFromMono16k(normalized, threshold);
            foreach (int offset in offsets)
            {
                float[] shifted = SliceFromOffset(normalized, offset);
                if (shifted.Length == 0) continue;

                InferenceResult candidate = _soundAI.PredictFromMono16k(shifted, threshold);
                if (candidate.Confidence > best.Confidence)
                    best = candidate;
            }

            return best;
        }

        private static float[] SliceFromOffset(float[] samples, int offset)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<float>();
            if (offset <= 0) return samples;
            if (offset >= samples.Length) return Array.Empty<float>();

            int len = samples.Length - offset;
            var sliced = new float[len];
            Array.Copy(samples, offset, sliced, 0, len);
            return sliced;
        }

        private static string FindExistingPath(params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                    return c;
            }
            return null;
        }

        private static float[] TrimSilence(float[] samples, float thresholdAbs, int minKeepSamples)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<float>();

            int start = 0;
            while (start < samples.Length && Math.Abs(samples[start]) < thresholdAbs)
                start++;

            int end = samples.Length - 1;
            while (end > start && Math.Abs(samples[end]) < thresholdAbs)
                end--;

            int len = end - start + 1;
            if (len <= 0) return Array.Empty<float>();

            // 너무 짧게 남으면 원본에서 앞부분을 최소 길이만큼 유지
            if (len < minKeepSamples)
            {
                start = Math.Max(0, Math.Min(start, samples.Length - minKeepSamples));
                len = Math.Min(minKeepSamples, samples.Length - start);
            }

            var trimmed = new float[len];
            Array.Copy(samples, start, trimmed, 0, len);
            return trimmed;
        }

        private static float[] NormalizePeak(float[] samples, float targetPeak)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<float>();

            float peak = 0f;
            foreach (var s in samples)
                peak = Math.Max(peak, Math.Abs(s));

            if (peak <= 1e-8f) return samples; // 거의 무음

            float gain = targetPeak / peak;
            var outSamples = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                outSamples[i] = samples[i] * gain;

            return outSamples;
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