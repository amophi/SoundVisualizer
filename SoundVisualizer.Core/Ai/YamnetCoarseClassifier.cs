// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// YAMNet 확률 521개 → 상위 5개 → (효과음·음악에 가려진 위협음이면 표시 이름을 그쪽으로) → 상위 3개 확률 투표.
    /// 모바일 YamnetCoarseClassifier.kt 와 같다. 안전 단서는 <see cref="YamnetSafetyCueDecision"/>, 확신도 기준과
    /// 히스테리시스는 <see cref="AiPostProcessor"/> 가 맡는다. 옛 PC 판의 Gunshot Booster 모델은 모바일처럼 없앴다.
    /// </summary>
    public sealed class YamnetCoarseClassifier
    {
        public const int NumClasses = 521;
        public const int TopK = 5;
        public const int VoteK = 3;

        // 가려진 위협음을 표시 이름으로 고르는 문턱: max(0.06, 1위 확률 × 0.22).
        private const float GameMixMinDangerProbability = 0.06f;
        private const float GameMixRelativeToTop = 0.22f;

        private readonly YamnetClassMap _classes;

        public YamnetCoarseClassifier(YamnetClassMap classes)
        {
            ArgumentNullException.ThrowIfNull(classes);
            if (classes.Count != NumClasses)
                throw new ArgumentException($"Expected {NumClasses} class names, got {classes.Count}", nameof(classes));
            _classes = classes;
        }

        /// <summary>이름 목록에서 바로 만든다. 판정을 다시 계산하므로 같은 목록이면 <see cref="YamnetClassMap"/> 하나를 나눠 쓴다.</summary>
        public YamnetCoarseClassifier(IReadOnlyList<string> classNames)
            : this(classNames as YamnetClassMap ?? new YamnetClassMap(classNames))
        {
        }

        public YamnetClassMap Classes => _classes;

        /// <summary>상위 후보 하나. <see cref="Name"/> 은 영어 클래스 이름이다.</summary>
        public readonly record struct TopClassHit(int Index, string Name, float Probability);

        /// <summary>상위 3개를 종류별로 더한 확률.</summary>
        public readonly record struct VoteScores(float Ambient, float Speech, float Danger);

        /// <summary>
        /// 한 프레임의 투표 결과.
        /// </summary>
        /// <param name="Coarse">사용자 지정까지 반영한 상위 3개 투표.</param>
        /// <param name="DefaultCoarse">
        /// 같은 상위 3개를 기본 매핑으로만 투표한 결과. 사용자가 어떤 소리를 대화음으로 바꿔도 다른 독립 경보까지
        /// 막지 않도록, 안전 단서의 "대화음이면 올리지 않음" 은 이 값으로 판단한다(#283 B).
        /// </param>
        /// <param name="DisplayName">표시 이름. 1위이거나, 가려진 위협음을 고른 경우 그 위협음 이름이다.</param>
        /// <param name="Confidence"><paramref name="DisplayName"/> 의 확률.</param>
        /// <param name="YamnetClassIndex"><paramref name="DisplayName"/> 의 클래스 번호. 후보가 없으면 -1.</param>
        /// <param name="Top5">걸러내지 않은 모델 원출력 상위 5개. 사용자 지정과 상관없다.</param>
        /// <param name="AmbientScore">사용자 지정을 반영한 투표 점수.</param>
        /// <param name="SpeechScore">사용자 지정을 반영한 투표 점수.</param>
        /// <param name="DangerScore">사용자 지정을 반영한 투표 점수.</param>
        /// <param name="GameMixPreferenceApplied">가려진 위협음을 표시 이름으로 골랐는지. 투표는 그대로 원래 상위 3개다.</param>
        public sealed record Result(
            SoundType Coarse,
            SoundType DefaultCoarse,
            string DisplayName,
            float Confidence,
            int YamnetClassIndex,
            IReadOnlyList<TopClassHit> Top5,
            float AmbientScore,
            float SpeechScore,
            float DangerScore,
            bool GameMixPreferenceApplied)
        {
            // 모바일 data class 처럼 상위 5개도 내용으로 비교한다. record 기본 비교는 목록을 참조로만 본다.
            public bool Equals(Result? other) =>
                other is not null &&
                Coarse == other.Coarse &&
                DefaultCoarse == other.DefaultCoarse &&
                DisplayName == other.DisplayName &&
                Confidence.Equals(other.Confidence) &&
                YamnetClassIndex == other.YamnetClassIndex &&
                Top5.SequenceEqual(other.Top5) &&
                AmbientScore.Equals(other.AmbientScore) &&
                SpeechScore.Equals(other.SpeechScore) &&
                DangerScore.Equals(other.DangerScore) &&
                GameMixPreferenceApplied == other.GameMixPreferenceApplied;

            public override int GetHashCode() =>
                HashCode.Combine(Coarse, DefaultCoarse, DisplayName, Confidence, YamnetClassIndex, Top5.Count, GameMixPreferenceApplied);
        }

        /// <summary>
        /// 확률 521개를 투표한다. <paramref name="mapping"/> 은 추론 한 번 동안 바뀌지 않는 사용자 지정 스냅샷이고, 없으면 기본 매핑이다.
        /// 상위 5개 배열은 스택에 두고(모바일은 추론마다 새로 만든다) 힙에는 결과와 상위 5개 목록만 남긴다.
        /// </summary>
        public Result Classify(ReadOnlySpan<float> probabilities, YamnetMappingPolicy? mapping = null)
        {
            mapping ??= YamnetMappingPolicy.Default;
            if (probabilities.Length != NumClasses)
                throw new ArgumentException($"Expected {NumClasses} probabilities, got {probabilities.Length}", nameof(probabilities));

            Span<int> topIdx = stackalloc int[TopK];
            Span<float> topProbs = stackalloc float[TopK];
            ComputeTop5(probabilities, topIdx, topProbs);

            int maxIndex = topIdx[0];
            float conf = maxIndex >= 0 ? topProbs[0] : 0f;
            string display = maxIndex >= 0 ? _classes[maxIndex] : "";

            var (shownIndex, shownConf, shownDisplay) =
                PreferDangerWhenTopIsMaskedByGameMix(probabilities, maxIndex, conf, display, mapping);
            bool gameMixApplied = shownIndex != maxIndex || shownConf != conf || shownDisplay != display;

            var scores = CoarseVoteScores(topIdx, topProbs, VoteK, mapping);
            var coarse = VoteCoarseFromScores(scores);

            int count = 0;
            for (int i = 0; i < TopK; i++)
            {
                if (topIdx[i] >= 0) count++;
            }
            var top5 = new TopClassHit[count];
            for (int i = 0, j = 0; i < TopK; i++)
            {
                int idx = topIdx[i];
                if (idx < 0) continue;
                top5[j++] = new TopClassHit(idx, _classes[idx], topProbs[i]);
            }

            return new Result(
                Coarse: coarse,
                DefaultCoarse: mapping.OverrideCount == 0 ? coarse : VoteCoarseFromTop5(topIdx, topProbs, VoteK),
                DisplayName: shownDisplay,
                Confidence: shownConf,
                YamnetClassIndex: shownIndex,
                Top5: top5,
                AmbientScore: scores.Ambient,
                SpeechScore: scores.Speech,
                DangerScore: scores.Danger,
                GameMixPreferenceApplied: gameMixApplied);
        }

        /// <summary>
        /// 확률이 큰 5개. 앞선 값보다 "커야" 자리를 차지하므로 같은 확률이면 앞 번호가 남는다. 남는 자리는 번호 -1, 확률 -1 이다.
        /// </summary>
        public void ComputeTop5(ReadOnlySpan<float> probs, Span<int> outIndices, Span<float> outProbs)
        {
            if (outIndices.Length < TopK || outProbs.Length < TopK)
                throw new ArgumentException($"Output spans must hold {TopK} entries");
            for (int i = 0; i < TopK; i++)
            {
                outIndices[i] = -1;
                outProbs[i] = -1f;
            }
            int n = Math.Min(probs.Length, _classes.Count);
            for (int i = 0; i < n; i++)
            {
                float p = probs[i];
                for (int j = 0; j < TopK; j++)
                {
                    if (p > outProbs[j])
                    {
                        for (int k = TopK - 1; k > j; k--)
                        {
                            outProbs[k] = outProbs[k - 1];
                            outIndices[k] = outIndices[k - 1];
                        }
                        outProbs[j] = p;
                        outIndices[j] = i;
                        break;
                    }
                }
            }
        }

        /// <summary>앞의 <paramref name="k"/>개(최대 5개) 후보 확률을 <paramref name="mapping"/> 의 종류별로 더한다.</summary>
        public VoteScores CoarseVoteScores(ReadOnlySpan<int> topIndices, ReadOnlySpan<float> topProbs, int k, YamnetMappingPolicy? mapping = null)
        {
            mapping ??= YamnetMappingPolicy.Default;
            float danger = 0f;
            float speech = 0f;
            float ambient = 0f;
            int limit = Math.Min(TopK, k);
            for (int idx = 0; idx < limit; idx++)
            {
                int i = topIndices[idx];
                if (i < 0) continue;
                float p = topProbs[idx];
                switch (mapping.CoarseAt(_classes, i))
                {
                    case SoundType.Danger:
                        danger += p;
                        break;
                    case SoundType.Speech:
                        speech += p;
                        break;
                    default:
                        ambient += p;
                        break;
                }
            }
            return new VoteScores(Ambient: ambient, Speech: speech, Danger: danger);
        }

        /// <summary>기본 매핑으로 투표한다(사용자 지정 무시).</summary>
        public SoundType VoteCoarseFromTop5(ReadOnlySpan<int> topIndices, ReadOnlySpan<float> topProbs, int k) =>
            VoteCoarseFromScores(CoarseVoteScores(topIndices, topProbs, k));

        /// <summary>점수가 가장 큰 종류. 같으면 위협음 → 대화음 → 환경음 순으로 고른다. 놓치면 더 위험한 쪽이 이긴다.</summary>
        public static SoundType VoteCoarseFromScores(VoteScores scores)
        {
            if (scores.Danger >= scores.Speech && scores.Danger >= scores.Ambient) return SoundType.Danger;
            if (scores.Speech >= scores.Ambient) return SoundType.Speech;
            return SoundType.Ambient;
        }

        /// <summary>
        /// 1위가 위협음을 가릴 수 있는 소리(효과음, 음악, 비디오 게임)인지. 게임·영상 속 총성은 효과음이나 음악이 1위를 차지하기 쉽다.
        /// 말소리·무음 계열 이름은 제외한다.
        /// </summary>
        public static bool IsGameMixMaskDisplay(string? display)
        {
            if (string.IsNullOrEmpty(display)) return false;
            // 모바일은 이 두 판정을 분류기와 안전 단서에 같은 내용으로 따로 두는데, 여기서는 한 벌을 같이 쓴다.
            if (YamnetSafetyCueDecision.IsSpeechLikeDisplay(display) || YamnetSafetyCueDecision.IsSilenceLikeDisplay(display)) return false;
            if (YamnetThreeClassMapper.IsGenericSoundEffectLabel(display)) return true;
            string s = display.ToLowerInvariant();
            return s.Contains("music") || s.Contains("video game");
        }

        /// <summary>
        /// 1위가 효과음·음악이면, 기본 매핑이 위협음인 클래스 중 확률이 가장 큰 것을 찾아 max(0.06, 1위 × 0.22) 이상이면
        /// 표시 이름과 확신도를 그쪽으로 바꾼다. 투표에는 영향이 없다.
        /// 사용자가 위협음에서 뺀 라벨은 고르지 않고, 새로 위협음으로 지정한 라벨은 투표에만 들어가므로 여기서 찾지 않는다.
        /// </summary>
        public (int Index, float Confidence, string Display) PreferDangerWhenTopIsMaskedByGameMix(
            ReadOnlySpan<float> probs,
            int maxIndex,
            float conf,
            string display,
            YamnetMappingPolicy? mapping = null)
        {
            mapping ??= YamnetMappingPolicy.Default;
            if (maxIndex < 0 || maxIndex >= _classes.Count || probs.Length != _classes.Count)
            {
                return (maxIndex, conf, display);
            }
            if ((_classes.TraitsOf(maxIndex, display) & YamnetLabelTraits.GameMixMask) == 0)
            {
                return (maxIndex, conf, display);
            }

            // 모바일은 521개 이름을 매번 소문자로 바꿔 기본 종류를 다시 구한다. 미리 모아 둔 위협음 번호만 돈다.
            int bestDangerIdx = -1;
            float bestDangerProb = 0f;
            foreach (int i in _classes.DefaultDangerIndices)
            {
                if (!mapping.AllowsSafetyCue(_classes[i])) continue;
                if (probs[i] > bestDangerProb)
                {
                    bestDangerProb = probs[i];
                    bestDangerIdx = i;
                }
            }
            if (bestDangerIdx < 0) return (maxIndex, conf, display);

            float topProb = probs[maxIndex];
            float bar = Math.Max(GameMixMinDangerProbability, topProb * GameMixRelativeToTop);
            if (bestDangerProb < bar) return (maxIndex, conf, display);

            return (bestDangerIdx, bestDangerProb, _classes[bestDangerIdx]);
        }
    }
}
