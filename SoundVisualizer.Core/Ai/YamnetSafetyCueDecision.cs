// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 상위 5개 안에 분명한 경보·사이렌·폭발 단서가 있으면 투표가 환경음이어도 위협음으로 올린다. 모바일 YamnetSafetyCueDecision.kt 와 같다.
    ///
    /// 옛 Gunshot Booster 경로는 보조 모델 점수 채택과 이 안전 정책을 함께 했다. 모델 점수 쪽은 없앴고 이 정책만 남긴다.
    /// 총기 라벨은 이 길로 올리지 않는다(총소리는 투표로만 위협음이 된다).
    /// </summary>
    public static class YamnetSafetyCueDecision
    {
        /// <summary>단서가 상위 5개 안에서 이 확률 이상이어야 올린다(#353, #358).</summary>
        public const float StrongDangerCueMinProbability = 0.05f;

        /// <summary>표시 이름의 확신도가 이보다 낮으면 올리지 않는다.</summary>
        public const float MinDisplayConfidence = 0.12f;

        /// <summary>
        /// 안전 단서 판단 결과. Pre 는 투표 결과, Post 는 승격을 반영한 결과다. 승격해도 <see cref="PostConfidence"/> 는 단서의 확률이
        /// 아니라 원래 표시 이름의 확신도다(모바일과 같다).
        /// </summary>
        public readonly record struct Result(
            SoundType PreCoarse,
            string PreDisplay,
            int PreClassIndex,
            float PreConfidence,
            SoundType PostCoarse,
            string PostDisplay,
            int PostClassIndex,
            float PostConfidence,
            bool HasGunshotCue,
            bool HasStrongDangerCue,
            bool DangerCuePromoted);

        /// <summary>
        /// <paramref name="pre"/> 를 만든 분류기와 같은 <paramref name="classNames"/>, 같은 <paramref name="mapping"/> 으로 부른다.
        /// </summary>
        public static Result Decide(YamnetClassMap classNames, YamnetCoarseClassifier.Result pre, YamnetMappingPolicy? mapping = null)
        {
            ArgumentNullException.ThrowIfNull(classNames);
            ArgumentNullException.ThrowIfNull(pre);
            mapping ??= YamnetMappingPolicy.Default;

            // 사용자가 위협음에서 뺀 라벨은 번호를 -1 로 지워 단서로 쓰지 않는다. 확률 자리는 남겨 순위는 그대로 둔다.
            Span<int> topIndices = stackalloc int[5];
            Span<float> topProbabilities = stackalloc float[5];
            topIndices.Fill(-1);
            topProbabilities.Fill(-1f);
            for (int i = 0; i < pre.Top5.Count && i < 5; i++)
            {
                var hit = pre.Top5[i];
                topIndices[i] = mapping.AllowsSafetyCue(hit.Name) ? hit.Index : -1;
                topProbabilities[i] = hit.Probability;
            }

            bool hasGunshotCue = HasGunshotCueInTop5(topIndices, classNames);
            bool hasStrongDangerCue = HasStrongDangerCueInTop5(topIndices, topProbabilities, classNames);
            var displayTraits = classNames.TraitsOf(pre.YamnetClassIndex, pre.DisplayName);
            bool blockPromotion =
                // 사용자가 대화음으로 지정한 라벨 때문에 다른 독립 단서까지 막지 않게, 기본 매핑의 투표로 본다(#283 B).
                // 단서에서 빼는 것은 위의 사용자 지정을 그대로 따른다.
                pre.DefaultCoarse == SoundType.Speech ||
                (displayTraits & (YamnetLabelTraits.SpeechLikeDisplay | YamnetLabelTraits.SilenceLikeDisplay)) != 0 ||
                pre.Confidence < MinDisplayConfidence;
            int cueIndex = TryPickBestStrongDangerDisplay(topIndices, topProbabilities, classNames);
            // 총기 후보가 함께 있어도 충분히 강한 다른 단서는 막지 않는다(#351). 총기 라벨 자체는 여기서 올리지 않는다.
            bool promote = !blockPromotion && hasStrongDangerCue && cueIndex >= 0;

            return new Result(
                PreCoarse: pre.Coarse,
                PreDisplay: pre.DisplayName,
                PreClassIndex: pre.YamnetClassIndex,
                PreConfidence: pre.Confidence,
                PostCoarse: promote ? SoundType.Danger : pre.Coarse,
                PostDisplay: promote ? classNames[cueIndex] : pre.DisplayName,
                PostClassIndex: promote ? cueIndex : pre.YamnetClassIndex,
                PostConfidence: pre.Confidence,
                HasGunshotCue: hasGunshotCue,
                HasStrongDangerCue: hasStrongDangerCue,
                DangerCuePromoted: promote);
        }

        public static bool IsGunshotKeyword(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string s = name.ToLowerInvariant();
            return s.Contains("gunshot") || s.Contains("gunfire") || s.Contains("machine gun") ||
                s.Contains("artillery") || s.Contains("fusillade") || s.Contains("cap gun");
        }

        /// <summary>안전 단서가 되는 이름. 총기, 폭발·폭죽, siren·alarm(alarm clock 제외), 승인한 경적·깨짐 라벨이다.</summary>
        public static bool IsStrongDangerKeyword(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (IsGunshotKeyword(name)) return true;
            string s = name.ToLowerInvariant();
            if (s.Contains("alarm clock")) return false;
            if (ApprovedAttentionCueLabels.Contains(s)) return true;
            return s.Contains("explosion") || s.Contains("fireworks") || s.Contains("firecracker") ||
                s.Contains("siren") || s.Contains("alarm");
        }

        // 모바일은 이 두 함수를 분류기(YamnetCoarseClassifier)에도 같은 내용으로 따로 둔다. 여기서는 한 벌만 두고 같이 쓴다.
        internal static bool IsSpeechLikeDisplay(string? display)
        {
            if (string.IsNullOrEmpty(display)) return false;
            string s = display.ToLowerInvariant();
            return s.Contains("speech") || s.Contains("conversation") || s.Contains("narration") ||
                s.Contains("speaking") || s.Contains("babbling") || s.Contains("whisper") ||
                s.Contains("singing") || s.Contains("choir") || s.Contains("laughter") ||
                s.Contains("crying") || s.Contains("sobbing") || s.Contains("shout");
        }

        internal static bool IsSilenceLikeDisplay(string? display)
        {
            if (string.IsNullOrEmpty(display)) return false;
            string s = display.ToLowerInvariant();
            return s.Contains("silence") || s.Contains("quiet") || s.Contains("background noise");
        }

        private static bool HasGunshotCueInTop5(ReadOnlySpan<int> topIndices, YamnetClassMap classNames)
        {
            foreach (int index in topIndices)
            {
                if (index >= 0 && (classNames.TraitsAt(index) & YamnetLabelTraits.Gunshot) != 0) return true;
            }
            return false;
        }

        private static bool HasStrongDangerCueInTop5(ReadOnlySpan<int> topIndices, ReadOnlySpan<float> topProbabilities, YamnetClassMap classNames)
        {
            for (int position = 0; position < topIndices.Length; position++)
            {
                int index = topIndices[position];
                if (index >= 0 && topProbabilities[position] >= StrongDangerCueMinProbability &&
                    (classNames.TraitsAt(index) & YamnetLabelTraits.StrongDangerCue) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>총기가 아닌 단서 중 확률이 가장 큰 것의 번호. 없으면 -1. 총기 후보의 확률을 다른 약한 단서에 빌려주지 않는다.</summary>
        private static int TryPickBestStrongDangerDisplay(ReadOnlySpan<int> topIndices, ReadOnlySpan<float> topProbabilities, YamnetClassMap classNames)
        {
            int bestIndex = -1;
            float bestProbability = 0f;
            for (int position = 0; position < topIndices.Length; position++)
            {
                int index = topIndices[position];
                if (index < 0) continue;
                var traits = classNames.TraitsAt(index);
                if ((traits & YamnetLabelTraits.Gunshot) != 0) continue;
                float probability = topProbabilities[position];
                if (probability >= StrongDangerCueMinProbability &&
                    (traits & YamnetLabelTraits.StrongDangerCue) != 0 &&
                    probability > bestProbability)
                {
                    bestIndex = index;
                    bestProbability = probability;
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// 3분류 매퍼가 이미 위협음으로 보는 정확한 라벨 중 안전 단서로도 승인한 것(소문자). 비슷한 이름이 따라오지 않게 정확히 같은 이름만 본다.
        /// </summary>
        internal static readonly FrozenSet<string> ApprovedAttentionCueLabels = new[]
        {
            "vehicle horn, car horn, honking",
            "air horn, truck horn",
            "burst, pop",
            "boom",
            "bang",
            "smash, crash",
            "breaking",
            "shatter",
        }.ToFrozenSet(StringComparer.Ordinal);
    }
}
