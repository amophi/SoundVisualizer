// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Diagnostics
{
    /// <summary>
    /// 개발자 모드 HUD 에 뿌릴 글자들(모바일 AiDebugLines). 화면 배치는 오버레이가 한다. 모바일 HUD 의 첫 줄은
    /// <c>Coarse ● Label Confidence</c> 이고(점은 <see cref="ColorType"/> 의 색), 그 아래로 Detail, Verdict, Timing, top-5 가 한 줄씩이다.
    /// </summary>
    /// <param name="ColorType">점을 칠할 색의 소리 종류(사용자가 그 종류에 고른 색). 결과가 없으면 null.</param>
    /// <param name="Coarse">소리 종류를 대문자로. 결과가 없으면 왜 없는지를 대신 넣는다.</param>
    /// <param name="Label">모델이 말한 이름(번역하지 않은 YAMNet 클래스 이름).</param>
    /// <param name="Confidence">확신도 두 자리.</param>
    /// <param name="Detail">임계값·나이.</param>
    /// <param name="Verdict">YAMNet 위험 단서로 올렸는지.</param>
    /// <param name="Timing">소리 크기·표시 여부·단계별 소요시간.</param>
    /// <param name="Top5">모델이 가장 높게 본 이름들. 한 줄에 하나씩 순위·이름·확률.</param>
    public sealed record AiDebugLines(
        SoundType? ColorType,
        string Coarse,
        string Label,
        string Confidence,
        string Detail,
        string Verdict,
        string Timing,
        IReadOnlyList<string> Top5)
    {
        // 모바일 data class 처럼 top-5 도 내용으로 비교한다. 오버레이는 지난번과 같으면 글자를 다시 넣지 않는다.
        public bool Equals(AiDebugLines? other) =>
            other is not null &&
            ColorType == other.ColorType &&
            Coarse == other.Coarse &&
            Label == other.Label &&
            Confidence == other.Confidence &&
            Detail == other.Detail &&
            Verdict == other.Verdict &&
            Timing == other.Timing &&
            Top5.SequenceEqual(other.Top5);

        public override int GetHashCode() =>
            HashCode.Combine(ColorType, Coarse, Label, Confidence, Detail, Verdict, Timing, Top5.Count);
    }

    /// <summary>
    /// AI 분류 결과를 개발자 모드 HUD 의 글자로 바꾼다(모바일 AiDebugText.kt).
    ///
    /// 이 도구의 목적은 분류가 맞는지 사람이 눈으로 채점하는 것이라, 모델이 실제로 말한 것을 손대지 않고 보여준다.
    /// 라벨을 번역하지 않는 이유도 같다. 확신도가 낮을 때도 숨기지 않는다. 무엇을 왜 보여주는지는 모바일 docs/ARCHITECTURE.md 4장에 있다.
    /// 화면과 상관없는 순수 함수라 어느 스레드에서 불러도 된다. 부를 때마다 문자열을 여럿 만들므로 렌더 프레임마다가 아니라 HUD 를 고칠 때
    /// (모바일은 100ms 마다. 추론 주기 250ms 보다 촘촘해야 짧은 위협음 판정 하나를 건너뛰지 않는다) 부른다.
    /// </summary>
    public static class AiDebugText
    {
        /// <summary>값이 없거나 뜻이 없을 때. 숫자 자리에 NaN 이 그대로 뜨는 것을 막는다.</summary>
        public const string None = "-";

        /// <summary>모델을 못 불러와 분류가 아예 돌지 않는 상태.</summary>
        public const string CoarseAiOff = "AI OFF";

        /// <summary>분류는 돌지만 첫 결과가 아직 없는 상태. 캡처 직후와 장치를 바꿔 AI 를 다시 만든 직후가 여기다.</summary>
        public const string CoarseWaiting = "WAIT";

        /// <summary>
        /// top-5 이름 칸의 폭(글자 수). 고정폭 글꼴에서 순위·확률까지 한 줄이 폰 폭 안에 들어간다.
        /// 더 긴 이름("Vehicle horn, car horn, honking" 등)은 줄여서 끝에 <see cref="Ellipsis"/> 를 붙인다.
        /// </summary>
        public const int Top5NameWidth = 26;

        public const string Ellipsis = "…";

        /// <summary>
        /// HUD 한 줄에 들어가는 글자 수. 이보다 길면 폰에서 줄이 두 줄로 꺾인다(#165). 폭 411dp 폰에서 고정폭 11sp 글자(6.6dp)가
        /// 383dp 에 58자다. PC 화면은 훨씬 넓지만 두 판이 같은 줄을 만들도록 같은 한도를 지킨다(나이를 짧게 쓰는 규칙이 여기서 나온다).
        /// </summary>
        public const int HudMaxColumns = 58;

        /// <param name="result">마지막 결과. 아직 없거나 AI 가 돌지 않으면 null.</param>
        /// <param name="nowMs">지금 벽시계 시각(유닉스 밀리초, <see cref="AiClassificationResult.TimestampMs"/> 와 같은 시계).</param>
        /// <param name="level">지금 소리 크기(0..1). 모바일은 가장 최근 버퍼의 좌우 중 큰 피크를 쓴다.</param>
        /// <param name="shown">그 종류의 표시가 켜져 있는지. 결과가 없으면 false.</param>
        /// <param name="aiAvailable">AI 를 만들지 못해 분류가 아예 돌지 않으면 false. 만드는 중에는 true 다.</param>
        public static AiDebugLines Format(AiClassificationResult? result, long nowMs, float level, bool shown, bool aiAvailable)
        {
            string levelText = "lvl " + TwoDecimals(level);

            if (result is null)
            {
                // 분류기가 없거나 첫 추론이 끝나지 않았다. 낡은 줄을 남겨 두면 그것을 지금 결과로 착각하므로,
                // 값이 없다는 것을 그대로 보여준다.
                return new AiDebugLines(
                    ColorType: null,
                    Coarse: aiAvailable ? CoarseWaiting : CoarseAiOff,
                    Label: aiAvailable ? "no result yet" : "model load failed",
                    Confidence: None,
                    Detail: None,
                    Verdict: None,
                    Timing: levelText,
                    Top5: []);
            }

            var top5 = new string[Math.Min(result.Top5.Count, 5)];
            for (int i = 0; i < top5.Length; i++)
            {
                var hit = result.Top5[i];
                top5[i] = string.Create(CultureInfo.InvariantCulture, $"{i + 1} {FitName(hit.Name)} {TwoDecimals(hit.Probability)}");
            }

            return new AiDebugLines(
                ColorType: result.Coarse,
                Coarse: result.Coarse.ToLabel().ToUpperInvariant(),
                // 확정된 이름이 아직 없으면 빈 문자열이 온다. 임계값을 한 번도 넘지 못한 상태다.
                Label: result.Display.Length == 0 ? "(none)" : result.Display,
                Confidence: TwoDecimals(result.Confidence),
                Detail: $"thr {YesNo(result.MeetsThreshold)}   age {AgeText(result.TimestampMs, nowMs)}",
                // 투표와 따로, YAMNet 의 강한 위험 단서로 위협음을 지켰는지.
                Verdict: $"cue {YesNo(result.DangerCuePromoted)}",
                Timing: $"{levelText}   shown {YesNo(shown)}   {TimingOf(result)}",
                Top5: top5);
        }

        /// <summary>고정폭 칸에 맞춘 이름. 짧으면 공백으로 채워 확률이 세로 한 열에 선다.</summary>
        private static string FitName(string name) =>
            name.Length > Top5NameWidth
                ? string.Concat(name.AsSpan(0, Top5NameWidth - Ellipsis.Length), Ellipsis)
                : name.PadRight(Top5NameWidth);

        private static string TimingOf(AiClassificationResult result) =>
            $"{WholeMillis(result.TotalMs)}ms ({WholeMillis(result.PreprocessMs)}/{WholeMillis(result.YamnetMs)})";

        /// <summary>
        /// 결과가 나온 뒤 흐른 시간. 무음이면 추론을 건너뛰고 마지막 결과를 그대로 들고 있으므로, 이 값이 계속 자라는 것이
        /// "지금 추론이 돌지 않는다" 는 신호다. 벽시계라 시계가 뒤로 조정되면 음수가 될 수 있다. 그때는 0 으로 본다.
        ///
        /// 오래될수록 짧게 쓴다. 100초가 넘으면 소수점은 뜻이 없고, 그대로 두면 <c>123.4s</c> 처럼 길어져 detail 줄이
        /// <see cref="HudMaxColumns"/> 를 넘는다(#165). 값 자리는 늘 5글자 이하다.
        /// </summary>
        private static string AgeText(long timestampMs, long nowMs)
        {
            long elapsed = Math.Max(nowMs - timestampMs, 0L);
            if (elapsed < 99_950L) return JavaNumberFormat.Fixed(elapsed / 1000.0, 1) + "s";   // 0.0s ~ 99.9s
            if (elapsed < 999_500L) return string.Create(CultureInfo.InvariantCulture, $"{(elapsed + 500L) / 1000L}s");   // 100s ~ 999s
            return string.Create(CultureInfo.InvariantCulture, $"{(elapsed + 30_000L) / 60_000L}m");   // 17m ~
        }

        private static string TwoDecimals(float value) =>
            float.IsNaN(value) ? None : JavaNumberFormat.Fixed(value, 2);

        private static string WholeMillis(double value) =>
            double.IsNaN(value) ? None : JavaNumberFormat.Fixed(value, 0);

        private static string YesNo(bool value) => value ? "Y" : "N";
    }
}
