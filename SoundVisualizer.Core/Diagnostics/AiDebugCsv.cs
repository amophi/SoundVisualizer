// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Text;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Diagnostics
{
    /// <summary>
    /// AI 판정 하나를 CSV 한 줄로 바꾼다(모바일 AiDebugCsv.kt, #193). 파일로 쓰는 일은 <see cref="AiDebugRecorder"/> 가 한다.
    ///
    /// 머리글, 칸 순서, 따옴표, 숫자 서식이 모바일과 글자까지 같다. 두 판의 기록을 같은 스크립트로 읽고 나란히 비교한다.
    /// HUD 는 지금 순간만 보여주므로, 총소리 한 발처럼 짧은 판정은 이 기록으로 되짚는다.
    /// </summary>
    public static class AiDebugCsv
    {
        /// <summary>top-5 를 몇 칸까지 적는지. 모델이 덜 내놓으면 남은 칸은 비워 둔다.</summary>
        public const int TopSlots = 5;

        /// <summary>값이 없을 때 넣는 것.</summary>
        public const string None = "";

        /// <summary>
        /// capture_source 칸. PC 는 시스템 소리(WASAPI 루프백)만 받으므로 늘 이 값이다. 모바일은 실행마다 고른
        /// <c>InternalPlayback</c>·<c>Microphone</c> 을 적는다.
        /// </summary>
        public const string CaptureSource = "Loopback";

        public static IReadOnlyList<string> Columns { get; } = BuildColumns();

        /// <summary>머리글 한 줄. 파일을 새로 열 때 한 번만 쓴다. 칸 이름에는 쉼표가 없어 따옴표로 감싸지 않는다.</summary>
        public static string Header { get; } = string.Join(",", Columns);

        /// <summary>결과 하나를 한 줄로. 줄 끝 문자는 붙이지 않는다.</summary>
        /// <param name="result">적을 결과.</param>
        /// <param name="nowMs">
        /// 읽은 시각(유닉스 밀리초). <see cref="AiClassificationResult.TimestampMs"/> 와의 차이가 age 다. 무음이면 추론을 건너뛰고
        /// 마지막 결과를 들고 있으므로, 나이가 자라는 것이 보여야 "추론이 멈췄다" 를 알 수 있다.
        /// </param>
        /// <param name="level">그때의 소리 크기. "왜 아무것도 안 그려졌나" 를 조용해서인지 AI 탓인지 가른다.</param>
        /// <param name="shown">
        /// 그 종류 표시가 켜져 있었는지. 꺼 두면 오버레이가 아무것도 그리지 않으므로, 이것 없이는 설정 상태를 "AI 가 못 잡았다" 로 오독한다.
        /// </param>
        public static string Row(AiClassificationResult result, long nowMs, float level, bool shown)
        {
            ArgumentNullException.ThrowIfNull(result);
            var row = new StringBuilder(384);
            Cell(row, Integer(nowMs));
            Cell(row, Integer(result.TimestampMs));
            // 벽시계는 뒤로 튈 수 있다. 음수 나이는 뜻이 없으니 0 으로 붙인다.
            Cell(row, Integer(Math.Max(nowMs - result.TimestampMs, 0L)));
            Cell(row, CaptureSource);
            Cell(row, result.Coarse.ToLabel());
            Cell(row, result.Display);
            Cell(row, Prob(result.Confidence));
            Cell(row, Bool(result.MeetsThreshold));
            Cell(row, Bool(result.DangerCuePromoted));
            for (int slot = 0; slot < TopSlots; slot++)
            {
                bool has = slot < result.Top5.Count;
                Cell(row, has ? result.Top5[slot].Name : None);
                Cell(row, has ? Prob(result.Top5[slot].Probability) : None);
            }
            Cell(row, Prob(level));
            Cell(row, Bool(shown));
            Cell(row, Millis(result.PreprocessMs));
            Cell(row, Millis(result.YamnetMs));
            Cell(row, Millis(result.TotalMs));
            Cell(row, result.MappingOverrideCount.ToString(CultureInfo.InvariantCulture));
            Cell(row, result.MappingSignature);
            return row.ToString();
        }

        private static IReadOnlyList<string> BuildColumns()
        {
            var columns = new List<string>
            {
                "time_ms",
                "result_ms",
                "age_ms",
                "capture_source",
                "coarse",
                "display",
                "confidence",
                // 임계값 자체는 결과에 실리지 않는다(후처리 안에만 있다). 넘었는지만 적는다.
                "meets_threshold",
                "danger_cue_promoted",
            };
            for (int slot = 1; slot <= TopSlots; slot++)
            {
                columns.Add(string.Create(CultureInfo.InvariantCulture, $"top{slot}_name"));
                columns.Add(string.Create(CultureInfo.InvariantCulture, $"top{slot}_prob"));
            }
            columns.AddRange(["level", "shown", "preprocess_ms", "yamnet_ms", "total_ms", "mapping_override_count", "mapping_signature"]);
            return columns.AsReadOnly();
        }

        /// <summary>
        /// 칸 하나를 따옴표로 감싸 붙인다. YAMNet 이름에는 쉼표가 들어간다("Gunshot, gunfire"). 감싸지 않으면 그 줄만 칸이 하나 밀려
        /// 표 전체가 어긋난다. 이름에 든 따옴표는 두 번 적는다. 모바일처럼 숫자 칸도 모두 감싼다.
        /// </summary>
        private static void Cell(StringBuilder row, string value)
        {
            if (row.Length > 0) row.Append(',');
            row.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        }

        private static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Kotlin Boolean.toString 과 같은 소문자. .NET 의 bool.ToString 은 "True" 다.</summary>
        private static string Bool(bool value) => value ? "true" : "false";

        /// <summary>확률·소리 크기는 다섯 자리로. 로캘을 고정한다(아랍어 로캘에서 소수점이 아랍 숫자로 나가면 표가 읽히지 않는다).</summary>
        private static string Prob(float value) => float.IsNaN(value) ? None : JavaNumberFormat.Fixed(value, 5);

        /// <summary>소요 시간은 한 자리면 된다.</summary>
        private static string Millis(double value) => double.IsNaN(value) ? None : JavaNumberFormat.Fixed(value, 1);
    }
}
