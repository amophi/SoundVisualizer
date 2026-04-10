using System;
using System.Collections.Generic;
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
        private static readonly string[] CoarseClasses = { "ambient", "speech", "danger" };

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

        /// <summary>
        /// 파일명 접두사로 기대 라벨을 읽어(ambient_/speech_/danger_) 성능을 정량 평가합니다.
        /// 예) speech_meeting_01.wav, danger_gunshot_02.wav, ambient_rain_01.wav
        /// </summary>
        public static void RunWavFolderBenchmark(
            SoundClassifier classifier,
            string folderPath,
            float confidenceThreshold = ConfidenceThreshold)
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

            if (files.Length == 0)
            {
                Console.WriteLine("error: no wav files found");
                return;
            }

            int total = 0;
            int labeled = 0;
            int correct = 0;
            int accepted = 0;
            int acceptedCorrect = 0;
            double confSum = 0;
            double inferMsSum = 0;

            var confusion = CreateConfusionMatrix();

            Console.WriteLine("=== YAMNet Benchmark (coarse classes) ===");
            Console.WriteLine($"folder: {full}");
            Console.WriteLine($"threshold: {confidenceThreshold:F2}");
            Console.WriteLine();

            foreach (var f in files)
            {
                total++;
                string fileName = Path.GetFileName(f);
                string expected = TryParseExpectedCoarseFromFileName(fileName);

                float[] mono = WavAudioLoader.LoadMono16kHz(f);
                InferenceResult r = classifier.PredictFromMono16k(mono, confidenceThreshold);

                bool isAccepted = r.Confidence >= confidenceThreshold;
                bool hasExpected = expected != null;
                bool isCorrect = hasExpected && string.Equals(r.CoarseClass, expected, StringComparison.Ordinal);

                confSum += r.Confidence;
                inferMsSum += r.InferenceTimeMs;

                if (isAccepted) accepted++;
                if (hasExpected)
                {
                    labeled++;
                    if (isCorrect) correct++;
                    if (isAccepted && isCorrect) acceptedCorrect++;
                    AddConfusion(confusion, expected, r.CoarseClass);
                }

                string expectedText = hasExpected ? expected : "n/a";
                string status = hasExpected ? (isCorrect ? "OK" : "MISS") : "SKIP";
                string acceptMark = isAccepted ? "accepted" : "rejected";
                Console.WriteLine(
                    $"{status,-5} [{acceptMark}] file={fileName} | expected={expectedText} | pred={r.CoarseClass} | top={r.YamnetDisplayName} | conf={r.Confidence:F3} | t={r.InferenceTimeMs:F1}ms");
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"total_files: {total}");
            Console.WriteLine($"labeled_files: {labeled}");
            Console.WriteLine($"accepted(>=th): {accepted} ({ToPercent(accepted, total):F1}%)");
            Console.WriteLine($"avg_confidence: {(total > 0 ? confSum / total : 0):F3}");
            Console.WriteLine($"avg_inference_ms: {(total > 0 ? inferMsSum / total : 0):F2}");
            Console.WriteLine($"coarse_accuracy(all_labeled): {ToPercent(correct, labeled):F1}% ({correct}/{labeled})");
            Console.WriteLine($"coarse_accuracy(accepted_only): {ToPercent(acceptedCorrect, accepted):F1}% ({acceptedCorrect}/{accepted})");
            Console.WriteLine();
            PrintConfusionMatrix(confusion);
        }

        private static string TryParseExpectedCoarseFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string lower = fileName.ToLowerInvariant();
            if (lower.StartsWith("ambient_")) return "ambient";
            if (lower.StartsWith("speech_")) return "speech";
            if (lower.StartsWith("danger_")) return "danger";
            return null;
        }

        private static Dictionary<string, Dictionary<string, int>> CreateConfusionMatrix()
        {
            var matrix = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (string expected in CoarseClasses)
            {
                var row = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string predicted in CoarseClasses)
                    row[predicted] = 0;
                matrix[expected] = row;
            }
            return matrix;
        }

        private static void AddConfusion(
            Dictionary<string, Dictionary<string, int>> matrix,
            string expected,
            string predicted)
        {
            if (!matrix.ContainsKey(expected))
                return;
            if (!matrix[expected].ContainsKey(predicted))
                return;
            matrix[expected][predicted]++;
        }

        private static void PrintConfusionMatrix(Dictionary<string, Dictionary<string, int>> matrix)
        {
            Console.WriteLine("confusion_matrix (rows=expected, cols=predicted)");
            Console.WriteLine("            ambient  speech  danger");
            foreach (string expected in CoarseClasses)
            {
                int a = matrix[expected]["ambient"];
                int s = matrix[expected]["speech"];
                int d = matrix[expected]["danger"];
                Console.WriteLine($"{expected,-10} {a,7} {s,7} {d,7}");
            }
        }

        private static double ToPercent(int numerator, int denominator)
        {
            if (denominator <= 0) return 0;
            return 100.0 * numerator / denominator;
        }
    }
}
