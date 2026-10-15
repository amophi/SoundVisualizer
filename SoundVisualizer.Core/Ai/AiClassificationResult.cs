// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 추론 한 번의 결과. 오버레이·진동·개발자 모드가 읽는 불변 스냅샷이다(모바일 AiClassificationResult.kt).
    ///
    /// <see cref="Coarse"/>·<see cref="Display"/>·<see cref="Confidence"/> 는 히스테리시스를 거친 확정 값이라 이번 프레임보다
    /// 오래된 것일 수 있다. <see cref="MeetsThreshold"/> 만 이번 프레임의 값이다. 필드끼리는 한 결과 안에서만 짝이 맞으므로
    /// 여러 필드를 쓰는 쪽은 결과를 한 번만 읽어 쓴다.
    /// </summary>
    public sealed class AiClassificationResult
    {
        /// <summary>확정한 소리 종류. 화면 색·표시 여부·진동은 이것만 보면 된다.</summary>
        public required SoundType Coarse { get; init; }

        /// <summary>확정할 때의 영어 클래스 이름(가려진 위협음이나 안전 단서로 바뀐 이름일 수 있다). 아직 확정한 것이 없으면 빈 문자열.</summary>
        public required string Display { get; init; }

        /// <summary>확정할 때의 확신도.</summary>
        public required float Confidence { get; init; }

        /// <summary>이번 프레임의 모델 원출력 상위 5개. 사용자 지정으로 거르지 않는다.</summary>
        public IReadOnlyList<YamnetCoarseClassifier.TopClassHit> Top5 { get; init; } = [];

        /// <summary>이번 프레임에서 안전 단서로 위협음으로 올렸는지.</summary>
        public bool DangerCuePromoted { get; init; }

        /// <summary>이번 프레임이 확신도 기준을 넘었는지. 못 넘었으면 확정 값은 이전 그대로다.</summary>
        public required bool MeetsThreshold { get; init; }

        /// <summary>결과를 만든 벽시계 시각(유닉스 밀리초).</summary>
        public required long TimestampMs { get; init; }

        public double PreprocessMs { get; init; }

        public double YamnetMs { get; init; }

        /// <summary>전처리 시작부터 후처리 끝까지.</summary>
        public double TotalMs { get; init; }

        /// <summary>이 결과에 쓴 유효한 사용자 지정 수.</summary>
        public int MappingOverrideCount { get; init; }

        /// <summary>이 결과에 쓴 매핑(<see cref="YamnetMappingPolicy.Signature"/>).</summary>
        public string MappingSignature { get; init; } = "default";

        /// <summary>
        /// 이 결과를 만들 때 받은 사용자 지정 원본(걸러내기 전). 설정이 바뀌면 다음 추론을 기다리지 않고 옛 결과를 바로 숨기는 데 쓴다
        /// (<see cref="AiClassificationStage.LastClassification"/>).
        /// </summary>
        internal FrozenDictionary<string, string> MappingInputs { get; init; } = FrozenDictionary<string, string>.Empty;
    }
}
