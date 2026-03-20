using System;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SoundVisualizer.AIModel
{
    public class SoundClassifier : IDisposable
    {
        private InferenceSession _session;
        private string[] _classNames; // YAMNet의 521개 소리 이름 목록

        public SoundClassifier()
        {
            // 1. AI 뇌(ONNX 모델) 로드
            // (주의: yamnet.onnx 파일이 실행 파일과 같은 폴더에 있어야 합니다!)
            try
            {
                // csproj에서 AIModel 폴더 안의 yamnet.onnx가 그대로 출력 디렉터리의 AIModel 하위 폴더로 복사되도록 설정되었으므로 경로를 수정합니다.
                string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIModel", "yamnet.onnx");
                _session = new InferenceSession(modelPath);
                LoadClassNames(); // 실제로는 csv 파일에서 "총소리", "폭발음" 목록을 불러옴
                Console.WriteLine("🧠 AI 모델(YAMNet) 로딩 완료!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 AI 모델 로드 실패: {ex.Message}");
            }
        }

        // 🚀 가람 님이 분석할 때, 성진 님은 이 함수를 동시에 실행합니다.
        public string PredictSoundType(byte[] rawAudioData, int bytesRecorded)
        {
            if (_session == null) return "AI 꺼짐";

            // 2. 전처리 (Pre-processing): AI는 8채널을 못 먹습니다. 
            // 가장 대사와 효과음이 선명한 '센터 채널(Index 2)'만 쏙 빼서 1채널(Mono)로 만듭니다.
            float[] monoAudio = ExtractCenterChannel(rawAudioData, bytesRecorded);

            // 3. C# 배열을 AI가 이해할 수 있는 '텐서(Tensor)' 형태로 변환
            // YAMNet 입력 규격: [데이터 개수] 형태의 1차원 float 텐서
            var inputTensor = new DenseTensor<float>(monoAudio, new[] { monoAudio.Length });
            var inputs = new[] { NamedOnnxValue.CreateFromTensor("waveform", inputTensor) };

            // 4. 추론 (Inference) 시작! (0.1초 컷)
            using var results = _session.Run(inputs);

            // 5. 결과 분석: 521개의 소리 중 가장 확률이 높은(Max) 인덱스 찾기
            var output = results.First().AsEnumerable<float>().ToArray();
            int maxIndex = Array.IndexOf(output, output.Max());
            float probability = output[maxIndex] * 100;

            // 너무 확신이 없는(확률이 낮은) 소리는 무시
            if (probability < 30.0f) return "배경음";

            return $"{_classNames[maxIndex]} ({probability:F1}%)";
        }

        private float[] ExtractCenterChannel(byte[] rawAudioData, int bytesRecorded)
        {
            int floatCount = bytesRecorded / 4;
            // 8채널 중 센터(1가닥)만 뽑으니까 길이는 1/8
            float[] centerChannel = new float[floatCount / 8];

            int centerIndex = 0;
            for (int i = 0; i < floatCount - 7; i += 8)
            {
                // 인덱스 2번이 센터(Center) 스피커 채널입니다.
                // byte 배열을 float로 변환했다고 가정하고 값을 빼옵니다.
                centerChannel[centerIndex++] = BitConverter.ToSingle(rawAudioData, (i + 2) * 4);
            }
            return centerChannel;
        }

        private void LoadClassNames()
        {
            // 프로토타입용 가짜 데이터 (실제로는 YAMNet class map CSV를 읽어야 함)
            _classNames = new string[521];
            for (int i = 0; i < 521; i++) _classNames[i] = "알 수 없는 소리";
            _classNames[426] = "💥 폭발음";
            _classNames[427] = "🔫 총소리";
            _classNames[322] = "👣 발소리";
        }

        // 메모리 누수 방지용
        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}