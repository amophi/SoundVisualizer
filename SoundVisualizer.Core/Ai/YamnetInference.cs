// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// YAMNet ONNX 실행(Android 의 YamnetInference.kt). 로그멜 [1, 1, 96, 64] → 로짓 521개 → softmax 확률.
    ///
    /// 가중치는 yamnet.data 에 따로 있고 ONNX Runtime 은 그 파일을 yamnet.onnx 가 있는 폴더에서 찾는다. 그래서 두 파일을 한 폴더에 두고
    /// 경로로 세션을 만든다. 만들기 전에 두 파일의 크기와 SHA-256 을 Android 와 같은 고정값으로 확인한다(<see cref="YamnetModelFiles"/>).
    /// 세션 설정도 Android 와 같다. 연산 스레드를 하나로 묶어 캡처·화면과 CPU 를 다투지 않게 한다.
    ///
    /// PC 에서 바꾼 것: 입력·출력 버퍼를 한 번만 잡아 IoBinding 으로 묶어 두고, 로짓과 확률은 부르는 쪽 칸에 쓴다. 그래서 추론마다
    /// 관리 힙에 할당하지 않는다(Android 는 추론마다 텐서와 결과 배열을 새로 만든다). 대신 스레드 안전하지 않으므로 한 번에 한 스레드만
    /// 부른다. <see cref="RealtimeAiPipeline"/> 이 추론 잠금으로 줄 세운다.
    /// </summary>
    public sealed class YamnetInference : IYamnetModel
    {
        public const string InputName = "audio";
        public const string OutputName = "class_scores";
        public const int NumClasses = 521;
        public const int TimeFrames = 96;
        public const int MelBins = 64;
        public const int LogMelSize = TimeFrames * MelBins;

        private static readonly long[] InputShape = { 1, 1, TimeFrames, MelBins };
        private static readonly long[] OutputShape = { 1, NumClasses };

        private readonly InferenceSession _session;
        private readonly float[] _input;
        private readonly float[] _output;
        private readonly OrtValue _inputValue;
        private readonly OrtValue _outputValue;
        private readonly RunOptions _runOptions;
        private readonly OrtIoBinding _binding;
        private bool _disposed;

        private YamnetInference(InferenceSession session)
        {
            ValidateContract(session);
            _session = session;
            // 고정 힙에 둔다. ONNX Runtime 이 두 배열의 주소를 들고 있는 동안 GC 가 옮기거나 힙을 조각내지 않게 한다.
            _input = GC.AllocateArray<float>(LogMelSize, pinned: true);
            _output = GC.AllocateArray<float>(NumClasses, pinned: true);
            try
            {
                _inputValue = OrtValue.CreateTensorValueFromMemory(_input, InputShape);
                _outputValue = OrtValue.CreateTensorValueFromMemory(_output, OutputShape);
                _runOptions = new RunOptions();
                // 묶어 둔 입력은 복사하지 않고 주소로 읽으므로, _input 을 고쳐 쓰면 다음 실행이 새 값을 본다.
                _binding = session.CreateIoBinding();
                _binding.BindInput(InputName, _inputValue);
                _binding.BindOutput(OutputName, _outputValue);
            }
            catch
            {
                ReleaseBuffers();
                throw;
            }
        }

        /// <summary>
        /// <paramref name="modelDirectory"/> 의 yamnet.onnx·yamnet.data 로 세션을 만든다. 실패하면 앱은 AI 없이 화면만 돈다.
        /// </summary>
        /// <exception cref="FileNotFoundException">모델 파일이 없다.</exception>
        /// <exception cref="InvalidDataException">모델 파일이 배포한 것과 다르거나(크기·SHA-256), 입출력이 YAMNet 계약과 다르다.</exception>
        /// <exception cref="OnnxRuntimeException">ONNX Runtime 이 세션을 만들지 못했다.</exception>
        public static YamnetInference Create(string modelDirectory)
        {
            ArgumentException.ThrowIfNullOrEmpty(modelDirectory);
            string directory = Path.GetFullPath(modelDirectory);
            ModelFileIntegrity.Verify(directory, YamnetModelFiles.All);

            InferenceSession session;
            using (var options = new SessionOptions
            {
                IntraOpNumThreads = 1,
                InterOpNumThreads = 1,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            })
            {
                session = new InferenceSession(Path.Combine(directory, YamnetModelFiles.Onnx.Name), options);
            }

            try
            {
                return new YamnetInference(session);
            }
            catch
            {
                // 계약 확인이나 버퍼 준비에 실패했다. 세션을 닫되 닫다가 난 오류가 원래 예외를 덮지 않게 한다(Kotlin closeOnFailure).
                try
                {
                    session.Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    AppLog.Warn(nameof(YamnetInference), "Failed to release a rejected YAMNet session", cleanupFailure);
                }
                throw;
            }
        }

        /// <summary>
        /// 로그멜 6,144칸(시간 우선 time × 64 + mel, <see cref="QualcommSourceAudioPreprocessor"/> 의 출력)으로 추론해 로짓 521개를
        /// <paramref name="logits"/> 에, softmax 확률을 <paramref name="probabilities"/> 에 쓴다. 할당하지 않는다.
        /// </summary>
        /// <exception cref="ObjectDisposedException">닫은 뒤에 불렀다. 닫힌 네이티브 세션에 들어가기 전에 여기서 막는다.</exception>
        public void Infer(ReadOnlySpan<float> logMel, Span<float> logits, Span<float> probabilities)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (logMel.Length != LogMelSize)
                throw new ArgumentException($"Expected log-mel size {LogMelSize}, got {logMel.Length}", nameof(logMel));
            if (logits.Length != NumClasses)
                throw new ArgumentException($"Expected {NumClasses} logits, got {logits.Length}", nameof(logits));
            if (probabilities.Length != NumClasses)
                throw new ArgumentException($"Expected {NumClasses} probabilities, got {probabilities.Length}", nameof(probabilities));

            logMel.CopyTo(_input);
            _session.RunWithBinding(_runOptions, _binding);
            _output.AsSpan().CopyTo(logits);
            Softmax(logits, probabilities);
        }

        /// <summary>진단용 한 줄. 예: <c>in=audio[1,1,96,64] out=class_scores[1,521]</c></summary>
        public string InputOutputSummary()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return $"in={InputName}[{string.Join(",", _session.InputMetadata[InputName].Dimensions)}] " +
                $"out={OutputName}[{string.Join(",", _session.OutputMetadata[OutputName].Dimensions)}]";
        }

        /// <summary>
        /// 세션과 버퍼를 놓는다. 여러 번 불러도 된다. 다른 스레드의 <see cref="Infer"/> 와 겹쳐 부르면 안 된다
        /// (파이프라인은 추론 잠금 안에서만 부른다).
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseBuffers();
            _session.Dispose();
        }

        /// <summary>
        /// Kotlin 의 softmax 와 같은 순서로 계산한다. 최댓값을 뺀 차를 float 로 구한 뒤 double 로 넓혀 exp 하고, 합은 double 로 더하고,
        /// float 로 줄인 exp 값을 그 합으로 나눈다. 합이 0 이하이거나 유한하지 않으면(NaN·무한 로짓) 모두 1/n 이다.
        /// Python 기준 구현은 차를 double 로 구하므로 Python 과는 float 마지막 자리에서만 다를 수 있다.
        /// </summary>
        public static void Softmax(ReadOnlySpan<float> logits, Span<float> probabilities)
        {
            if (probabilities.Length < logits.Length)
                throw new ArgumentException($"probabilities must hold {logits.Length} values", nameof(probabilities));
            if (logits.IsEmpty) return;

            float max = logits[0];
            for (int i = 1; i < logits.Length; i++)
            {
                if (logits[i] > max) max = logits[i];
            }

            double sum = 0.0;
            for (int i = 0; i < logits.Length; i++)
            {
                // (float) 로 차를 float 에서 반올림한다. Kotlin 은 Float 끼리 뺀 뒤 toDouble() 한다.
                double e = Math.Exp((float)(logits[i] - max));
                probabilities[i] = (float)e;
                sum += e;
            }

            if (sum <= 0.0 || !double.IsFinite(sum))
            {
                probabilities[..logits.Length].Fill(1f / logits.Length);
                return;
            }
            for (int i = 0; i < logits.Length; i++)
            {
                probabilities[i] = (float)(probabilities[i] / sum);
            }
        }

        /// <summary>Android 처럼 첫 입력·출력의 이름과 입력 모양을 보고, PC 는 IoBinding 이 기대는 개수·자료형·출력 모양까지 본다.</summary>
        private static void ValidateContract(InferenceSession session)
        {
            if (session.InputNames.Count != 1 || session.InputNames[0] != InputName)
                throw new InvalidDataException($"Expected a single input '{InputName}', got [{string.Join(", ", session.InputNames)}]");
            if (session.OutputNames.Count != 1 || session.OutputNames[0] != OutputName)
                throw new InvalidDataException($"Expected a single output '{OutputName}', got [{string.Join(", ", session.OutputNames)}]");
            RequireFloatTensor(session.InputMetadata[InputName], InputName, InputShape);
            RequireFloatTensor(session.OutputMetadata[OutputName], OutputName, OutputShape);
        }

        private static void RequireFloatTensor(NodeMetadata metadata, string name, long[] shape)
        {
            bool matches = metadata.IsTensor &&
                metadata.ElementDataType == TensorElementType.Float &&
                metadata.Dimensions.Length == shape.Length &&
                metadata.Dimensions.Select(dimension => (long)dimension).SequenceEqual(shape);
            if (!matches)
            {
                throw new InvalidDataException(
                    $"Expected '{name}' as float[{string.Join(",", shape)}], got {metadata.ElementDataType}[{string.Join(",", metadata.Dimensions)}]");
            }
        }

        private void ReleaseBuffers()
        {
            // 생성자 도중에 실패하면 일부는 아직 없다.
            _binding?.Dispose();
            _runOptions?.Dispose();
            _outputValue?.Dispose();
            _inputValue?.Dispose();
        }
    }

    /// <summary>
    /// 파이프라인이 쓰는 모델 단계. 실제로는 <see cref="YamnetInference"/> 이고, 테스트는 오래 걸리거나 실패하는 모델로 바꿔 끼워
    /// 닫기 경합과 실패 경로를 순서대로 재현한다.
    /// </summary>
    internal interface IYamnetModel : IDisposable
    {
        /// <inheritdoc cref="YamnetInference.Infer"/>
        void Infer(ReadOnlySpan<float> logMel, Span<float> logits, Span<float> probabilities);
    }

    /// <summary>
    /// 배포하는 YAMNet 두 파일의 크기와 SHA-256. Android 의 YamnetModelFiles 와 글자까지 같아야 하고, 모델을 바꾸면 두 앱에서 함께 고친다.
    /// </summary>
    internal static class YamnetModelFiles
    {
        public static readonly ModelFileIntegrity.ExpectedFile Onnx = new(
            "yamnet.onnx",
            25_592L,
            "290369c40886a4ae948f77671fff901023eec81484ac393465dfb1b342b1ca85");

        public static readonly ModelFileIntegrity.ExpectedFile Data = new(
            "yamnet.data",
            14_915_108L,
            "aa05b5b196bdfd74fb59ae4cbba22578c5ab25b9e792e52867678844bcecc839");

        public static readonly IReadOnlyList<ModelFileIntegrity.ExpectedFile> All = [Onnx, Data];
    }
}
