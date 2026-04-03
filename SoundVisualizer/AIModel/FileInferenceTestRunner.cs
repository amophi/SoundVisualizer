using System;
using System.IO;
using System.Linq;

namespace SoundVisualizer.AIModel
{
    /// <summary>
    /// 사전 녹음 WAV로 추론 파이프라인 검증(콘솔 출력). UI/DSP와 무관.
    /// </summary>
    public static class FileInferenceTestRunner
    {
        /// <summary>상위 확률이 이 값 이상일 때만 "수용"으로 표시합니다.</summary>
        public const float ConfidenceThreshold = 0.6f;

        /// <summary>단일 .wav 파일 추론 결과를 출력합니다.</summary>
        public static void RunWavFile(SoundClassifier classifier, string wavPath)
        {
            if (classifier == null) throw new ArgumentNullException(nameof(classifier));
            if (string.IsNullOrWhiteSpace(wavPath))
                throw new ArgumentException("path required", nameof(wavPath));

            string full = Path.GetFullPath(wavPath);
            if (!File.Exists(full))
            {
                Console.WriteLine($"file: {wavPath}");
                Console.WriteLine("error: file not found");
                return;
            }

            float[] mono = WavAudioLoader.LoadMono16kHz(full);
            InferenceResult r = classifier.PredictFromMono16k(mono, ConfidenceThreshold);

            Console.WriteLine($"file: {Path.GetFileName(full)}");
            Console.WriteLine($"predicted_class: {r.CoarseClass}");
            Console.WriteLine($"confidence: {r.Confidence:F2}");
            Console.WriteLine($"inference_time_ms: {r.InferenceTimeMs:F1}");
        }

        /// <summary>폴더 내 .wav 파일을 순회합니다(비재귀).</summary>
        public static void RunWavFolder(SoundClassifier classifier, string folderPath)
        {
            if (classifier == null) throw new ArgumentNullException(nameof(classifier));
            string full = Path.GetFullPath(folderPath);
            if (!Directory.Exists(full))
            {
                Console.WriteLine($"error: folder not found: {folderPath}");
                return;
            }

            var files = Directory.GetFiles(full, "*.wav", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var f in files)
            {
                RunWavFile(classifier, f);
                Console.WriteLine();
            }
        }
    }
}
