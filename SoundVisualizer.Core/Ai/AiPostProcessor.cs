// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 확신도 기준과 히스테리시스로 화면에 보낼 라벨을 확정한다. 모바일 AiPostProcessor.kt 와 같다.
    ///
    /// 라벨이 추론(250ms)마다 흔들리지 않게 환경음·대화음은 기준을 넘는 프레임이 두 번 이어져야 바뀐다. 위협음은 놓치면 안 되므로
    /// 첫 프레임에 바로 확정하고, 위협음에서 벗어날 때도 확신도가 0.27 이상이면 한 프레임으로 충분하다.
    /// 기준을 못 넘은 프레임은 아무것도 바꾸지 않으므로 이전 라벨이 그대로 남는다.
    ///
    /// 상태를 가지므로 한 스레드(추론 스레드)에서만 부른다.
    /// </summary>
    public sealed class AiPostProcessor
    {
        public const float DefaultBaseThreshold = 0.25f;
        public const int CoarseHysteresisThreshold = 2;
        public const int DangerHysteresisThreshold = 1;
        public const float DangerImmediateSwitchConfidence = 0.28f;
        public const float DangerExitRelaxedConfidence = 0.27f;

        // 기준 완화. 안전 단서로 올린 위협음은 0.12, 상위 5개에 강한·치명적 단서가 있는 위협음은 0.20 까지 내린다.
        // 대화음은 기본 기준을 낮게 줘도 0.25 아래로 내리지 않는다.
        private const float PromotedCueThreshold = 0.12f;
        private const float DangerCueThreshold = 0.20f;
        private const float SpeechMinThreshold = 0.25f;

        /// <summary>한 프레임의 입력. 안전 단서 판단(<see cref="YamnetSafetyCueDecision"/>)을 거친 라벨이다.</summary>
        /// <param name="Coarse">이번 프레임의 종류.</param>
        /// <param name="Display">이번 프레임의 영어 클래스 이름. 확정되면 화면에 간다.</param>
        /// <param name="Confidence">이번 프레임의 확신도.</param>
        /// <param name="DangerCuePromoted">안전 단서로 위협음으로 올렸는지(기준을 0.12 까지 내린다).</param>
        /// <param name="HasStrongDangerCue">상위 5개에 강한 위협 단서가 있는지(기준 완화용).</param>
        /// <param name="HasCriticalDangerCue">표시 이름이나 상위 5개에 치명적 단서(총기, 폭발, 폭죽)가 있는지(기준 완화용).</param>
        /// <param name="TopKSummary">상위 후보 이름을 이은 문자열. <paramref name="CriticalDangerEvent"/> 가 없을 때만 치명적 사건 판단에 쓴다.</param>
        /// <param name="CriticalDangerEvent">
        /// 사용자 매핑을 반영해 미리 정한 치명적 사건 여부. 파이프라인은 늘 넣는다. 없으면(null) 이름 키워드로 판단하는데,
        /// 그러면 위협음에서 뺀 라벨도 다시 걸리므로 매핑을 아는 쪽은 꼭 넣는다.
        /// </param>
        public readonly record struct FrameInput(
            SoundType Coarse,
            string Display,
            float Confidence,
            bool DangerCuePromoted = false,
            bool HasStrongDangerCue = false,
            bool HasCriticalDangerCue = false,
            string TopKSummary = "",
            bool? CriticalDangerEvent = null);

        /// <summary>
        /// 처리 뒤 상태. <see cref="MeetsThreshold"/> 만 이번 프레임의 값이고, Confirmed 는 이전 프레임에서 확정한 값일 수 있다.
        /// <see cref="CandidateCoarse"/> 는 바뀌려고 기다리는 종류이고, 없으면 null 이다(모바일은 빈 문자열).
        /// </summary>
        public readonly record struct FrameResult(
            float EffectiveThreshold,
            bool MeetsThreshold,
            SoundType? CandidateCoarse,
            int CandidateStreak,
            SoundType ConfirmedCoarse,
            string ConfirmedDisplay,
            float ConfirmedConfidence)
        {
            // 모바일은 옛 Booster 미리보기 때문에 화면용 값을 따로 두었다. 미리보기를 없앤 지금은 늘 확정 값과 같다.
            public SoundType UiCoarse => ConfirmedCoarse;

            public string UiDisplay => ConfirmedDisplay;

            public float UiConfidence => ConfirmedConfidence;
        }

        private readonly float _baseConfidenceThreshold;
        private SoundType _confirmedCoarse = SoundType.Ambient;
        private string _confirmedDisplay = "";
        private float _confirmedConfidence;
        private SoundType? _candidateCoarse;
        private int _candidateStreak;

        public AiPostProcessor(float baseConfidenceThreshold = DefaultBaseThreshold)
        {
            _baseConfidenceThreshold = baseConfidenceThreshold;
        }

        /// <summary>
        /// 이번 프레임의 기준. 안전 단서로 올린 위협음 0.12, 강한·치명적 단서가 있는 위협음 0.20 까지 내리고, 대화음은 0.25 아래로 내리지 않는다.
        /// </summary>
        public static float ComputeEffectiveThreshold(
            SoundType coarse,
            float baseThreshold,
            bool hasStrongDangerCue,
            bool hasCriticalDangerCue,
            bool dangerCuePromoted = false)
        {
            float effective = baseThreshold;
            if (coarse == SoundType.Danger && dangerCuePromoted)
            {
                effective = Math.Min(effective, PromotedCueThreshold);
            }
            else if (coarse == SoundType.Danger && (hasStrongDangerCue || hasCriticalDangerCue))
            {
                effective = Math.Min(effective, DangerCueThreshold);
            }
            else if (coarse == SoundType.Speech)
            {
                effective = Math.Max(effective, SpeechMinThreshold);
            }
            return effective;
        }

        /// <summary>치명적 단서: 총기, 폭발, 폭죽. 확신도가 낮아도 위협음으로 바로 확정한다.</summary>
        public static bool IsCriticalDangerKeyword(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (YamnetSafetyCueDecision.IsGunshotKeyword(name)) return true;
            string s = name.ToLowerInvariant();
            return s.Contains("explosion") || s.Contains("fireworks") || s.Contains("firecracker");
        }

        public static bool IsCriticalDangerEvent(string? display, string? topKSummary) =>
            IsCriticalDangerKeyword(display) || IsCriticalDangerKeyword(topKSummary);

        /// <summary>처음 상태(환경음, 이름 없음)로 돌린다. 캡처를 다시 시작하거나 사용자 매핑이 바뀌면 부른다.</summary>
        public void Reset()
        {
            _confirmedCoarse = SoundType.Ambient;
            _confirmedDisplay = "";
            _confirmedConfidence = 0f;
            _candidateCoarse = null;
            _candidateStreak = 0;
        }

        public FrameResult Process(FrameInput frame)
        {
            float effective = ComputeEffectiveThreshold(
                frame.Coarse,
                _baseConfidenceThreshold,
                frame.HasStrongDangerCue,
                frame.HasCriticalDangerCue,
                frame.DangerCuePromoted);
            bool meets = frame.Confidence >= effective;
            bool critical = frame.CriticalDangerEvent ?? IsCriticalDangerEvent(frame.Display, frame.TopKSummary);

            // 매개변수 없는 생성자로 만든 입력은 이름이 null 이다. 확정 이름은 늘 문자열로 둔다.
            ApplyCoarseHysteresis(meets, frame.Coarse, frame.Display ?? "", frame.Confidence, critical);

            return new FrameResult(
                EffectiveThreshold: effective,
                MeetsThreshold: meets,
                CandidateCoarse: _candidateCoarse,
                CandidateStreak: _candidateStreak,
                ConfirmedCoarse: _confirmedCoarse,
                ConfirmedDisplay: _confirmedDisplay,
                ConfirmedConfidence: _confirmedConfidence);
        }

        private void ApplyCoarseHysteresis(bool meetsThreshold, SoundType newCoarse, string display, float confidence, bool criticalDangerEvent)
        {
            if (!meetsThreshold) return;

            if (newCoarse == _confirmedCoarse)
            {
                _candidateStreak = 0;
                _candidateCoarse = null;
                _confirmedDisplay = display;
                _confirmedConfidence = confidence;
                return;
            }

            if (newCoarse == SoundType.Danger &&
                (confidence >= DangerImmediateSwitchConfidence || criticalDangerEvent))
            {
                Confirm(newCoarse, display, confidence);
                return;
            }

            if (newCoarse == _candidateCoarse)
            {
                _candidateStreak++;
            }
            else
            {
                _candidateCoarse = newCoarse;
                _candidateStreak = 1;
            }

            int required = newCoarse == SoundType.Danger ? DangerHysteresisThreshold : CoarseHysteresisThreshold;
            // 위협음이 끝났다는 확신이 충분하면 한 프레임 만에 놓아 준다. 위협음 색이 소리보다 오래 남지 않게 한다.
            if (_confirmedCoarse == SoundType.Danger &&
                newCoarse != SoundType.Danger &&
                confidence >= DangerExitRelaxedConfidence)
            {
                required = 1;
            }

            if (_candidateStreak >= required)
            {
                Confirm(newCoarse, display, confidence);
            }
        }

        private void Confirm(SoundType coarse, string display, float confidence)
        {
            _confirmedCoarse = coarse;
            _confirmedDisplay = display;
            _confirmedConfidence = confidence;
            _candidateStreak = 0;
            _candidateCoarse = null;
        }
    }
}
