// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;
using System.Diagnostics;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// YAMNet 확률 521개 → 화면이 쓸 확정 결과. 모바일 RealtimeAiPipeline 에서 모델 뒤쪽(사용자 매핑 갱신, 투표, 안전 단서,
    /// 후처리, 결과 게시)만 옮겼다. 오디오 버퍼·전처리·모델 실행·250ms 주기는 갖고 있지 않다.
    ///
    /// 스레드: <see cref="UpdateMapping"/>·Process·<see cref="Reset"/> 은 추론 스레드에서만(또는 서로 겹치지 않게) 부른다.
    /// <see cref="LastClassification"/> 은 렌더 스레드 등 어디서 불러도 된다. 결과는 불변이고 참조 하나로 게시하므로 읽는 쪽에 락이 필요 없다.
    /// </summary>
    public sealed class AiClassificationStage
    {
        private readonly YamnetClassMap _classes;
        private readonly YamnetCoarseClassifier _classifier;
        private readonly AiPostProcessor _postProcessor = new();
        private FrozenDictionary<string, string> _previousOverrides = FrozenDictionary<string, string>.Empty;
        private YamnetMappingPolicy _mapping = YamnetMappingPolicy.Default;
        private AiClassificationResult? _lastResult;

        public AiClassificationStage(YamnetClassMap classes)
        {
            _classifier = new YamnetCoarseClassifier(classes);
            _classes = classes;
        }

        public YamnetClassMap Classes => _classes;

        /// <summary>지금 쓰는 매핑 스냅샷.</summary>
        public YamnetMappingPolicy Mapping => _mapping;

        /// <summary>
        /// 설정의 사용자 지정(영어 클래스 이름 → "ambient"/"speech"/"danger")을 반영한다. 추론 틱마다 먼저 부르고, 무음이라 추론을
        /// 건너뛰는 틱에도 부른다. 그래야 이전 매핑에서 확정한 위협음이 소리가 다시 날 때 새 매핑으로 넘어오지 않는다.
        ///
        /// 원본이 이전과 같으면 아무것도 하지 않는다. 다르면 새 스냅샷을 만들고, 실제 매핑(서명)까지 달라졌으면 히스테리시스를
        /// 처음부터 다시 시작하고 마지막 결과를 지운다. 그랬으면 true 를 돌려준다.
        /// </summary>
        public bool UpdateMapping(IReadOnlyDictionary<string, string> overrides)
        {
            ArgumentNullException.ThrowIfNull(overrides);
            if (SameEntries(_previousOverrides, overrides)) return false;

            var next = YamnetMappingPolicy.From(overrides, _classes);
            bool restarted = next.Signature != _mapping.Signature;
            if (restarted)
            {
                _postProcessor.Reset();
                Volatile.Write(ref _lastResult, null);
            }
            // 입력을 복사해 둔다. 설정 쪽 맵이 나중에 바뀌어도 이 결과가 어떤 입력으로 만들어졌는지는 그대로 남는다.
            _previousOverrides = overrides.ToFrozenDictionary(StringComparer.Ordinal);
            _mapping = next;
            return restarted;
        }

        /// <summary>소요시간을 따로 재지 않을 때. 시간 필드는 이 호출 안에서 걸린 시간만 담는다.</summary>
        public AiClassificationResult Process(ReadOnlySpan<float> probabilities) =>
            Process(probabilities, Stopwatch.GetTimestamp(), preprocessMs: 0, yamnetMs: 0);

        /// <summary>모델 출력으로 결과를 만들고 게시한다.</summary>
        /// <param name="probabilities">YAMNet softmax 확률 521개. 길이가 다르면 <see cref="ArgumentException"/>.</param>
        /// <param name="startedTimestamp">전처리를 시작할 때 <see cref="Stopwatch.GetTimestamp"/> 로 잰 값. 전체 소요시간을 여기서부터 잰다.</param>
        /// <param name="preprocessMs">리샘플·log-mel 에 걸린 시간. 결과에 그대로 담는다.</param>
        /// <param name="yamnetMs">모델 실행에 걸린 시간. 결과에 그대로 담는다.</param>
        public AiClassificationResult Process(ReadOnlySpan<float> probabilities, long startedTimestamp, double preprocessMs, double yamnetMs)
        {
            var mapping = _mapping;
            var pre = _classifier.Classify(probabilities, mapping);
            var decision = YamnetSafetyCueDecision.Decide(_classes, pre, mapping);
            // 사용자 매핑으로 거른 치명적 단서. 기준 완화와 즉시 확정에 같은 값을 넣어, 후처리가 위협음에서 뺀 라벨을 이름 키워드로
            // 다시 잡지 않게 한다. 그래서 모바일이 만들던 상위 후보 요약 문자열은 쓰이지 않으므로 만들지 않는다.
            bool hasCritical = mapping.HasCriticalDangerCue(_classes, decision.PostClassIndex, decision.PostDisplay, pre.Top5);
            var post = _postProcessor.Process(new AiPostProcessor.FrameInput(
                Coarse: decision.PostCoarse,
                Display: decision.PostDisplay,
                Confidence: decision.PostConfidence,
                DangerCuePromoted: decision.DangerCuePromoted,
                HasStrongDangerCue: decision.HasStrongDangerCue,
                HasCriticalDangerCue: hasCritical,
                CriticalDangerEvent: hasCritical));

            var result = new AiClassificationResult
            {
                Coarse = post.UiCoarse,
                Display = post.UiDisplay,
                Confidence = post.UiConfidence,
                Top5 = pre.Top5,
                DangerCuePromoted = decision.DangerCuePromoted,
                MeetsThreshold = post.MeetsThreshold,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PreprocessMs = preprocessMs,
                YamnetMs = yamnetMs,
                TotalMs = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds,
                MappingOverrideCount = mapping.OverrideCount,
                MappingSignature = mapping.Signature,
                MappingInputs = _previousOverrides,
            };
            Volatile.Write(ref _lastResult, result);
            return result;
        }

        /// <summary>
        /// 마지막 결과. 아직 없거나, 결과를 만든 뒤 설정의 사용자 지정이 바뀌었으면(다음 추론 전이라도) null 이다.
        /// 위협음에서 뺀 소리가 다음 추론까지 위협음 색으로 남지 않게 한다. 화면은 null 이면 환경음으로 그린다.
        /// </summary>
        public AiClassificationResult? LastClassification(IReadOnlyDictionary<string, string> currentOverrides)
        {
            ArgumentNullException.ThrowIfNull(currentOverrides);
            var last = Volatile.Read(ref _lastResult);
            return last is not null && SameEntries(last.MappingInputs, currentOverrides) ? last : null;
        }

        /// <summary>캡처를 다시 시작할 때 부른다. 확정 상태와 마지막 결과를 지운다. 매핑 스냅샷은 그대로 둔다.</summary>
        public void Reset()
        {
            _postProcessor.Reset();
            Volatile.Write(ref _lastResult, null);
        }

        // 모바일의 맵 비교(overrides != previousOverrides)와 같다. 스냅샷 쪽을 돌아서 틱마다 할당하지 않는다.
        private static bool SameEntries(FrozenDictionary<string, string> snapshot, IReadOnlyDictionary<string, string> current)
        {
            if (ReferenceEquals(snapshot, current)) return true;
            if (snapshot.Count != current.Count) return false;
            foreach (var (name, label) in snapshot)
            {
                if (!current.TryGetValue(name, out var other) || !string.Equals(label, other, StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
