// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;

namespace SoundVisualizer.Diagnostics
{
    /// <summary>
    /// 소수를 모바일 판(Kotlin <c>String.format(Locale.US, "%.Nf", value)</c>)과 같은 글자로 적는다.
    ///
    /// .NET 의 "F" 서식은 이진값 그대로를 반올림하고 딱 절반이면 짝수 쪽으로 보낸다(0.15 → "0.1", 0.25 → "0.2", 12.5 → "12").
    /// Java 는 값을 가장 짧은 십진수(Double.toString 의 자릿수)로 적은 뒤 5 에서 올린다(0.15 → "0.2", 0.25 → "0.3", 12.5 → "13").
    /// HUD 의 나이는 밀리초를 1000 으로 나눈 값이라 이 경계에 자주 걸린다. 그대로 두면 같은 결과를 두 판이 다르게 적어,
    /// 두 기기의 기록을 나란히 놓고 볼 때 헷갈린다. 로캘은 늘 고정이다(아랍어 로캘에서는 숫자가 아랍 숫자로 나간다).
    /// </summary>
    internal static class JavaNumberFormat
    {
        /// <summary>
        /// 소수점 아래 <paramref name="decimals"/> 자리로 적는다. float 는 double 로 넓혀서 넘긴다. Java 의 Formatter 도
        /// float 를 double 로 바꾼 뒤 적으므로, 0.145f 는 0.14499999582767487 로 보고 "0.14" 가 된다.
        /// </summary>
        public static string Fixed(double value, int decimals)
        {
            // NaN·Infinity·-Infinity 는 Java 와 글자가 같다. 값이 없다는 표시("-" 등)는 부르는 쪽이 정한다.
            if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);

            string format = "F" + decimals.ToString(CultureInfo.InvariantCulture);
            // Java 는 크기만 적고 부호를 앞에 붙인다. 그래서 -0.0 과 -0.001 이 "-0.00" 이 된다.
            string sign = double.IsNegative(value) ? "-" : "";
            double magnitude = Math.Abs(value);
            // 가장 짧은 왕복 표현은 17자리 이하라 decimal 에 그대로 담긴다. decimal 범위(약 7.9e28)를 넘는 값만
            // .NET 서식으로 적는다. 여기서 적는 확률·소리 크기·밀리초는 그 근처에도 가지 않는다.
            string shortest = magnitude.ToString("R", CultureInfo.InvariantCulture);
            string text = decimal.TryParse(shortest, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal exact)
                ? Math.Round(exact, decimals, MidpointRounding.AwayFromZero).ToString(format, CultureInfo.InvariantCulture)
                : magnitude.ToString(format, CultureInfo.InvariantCulture);
            return sign + text;
        }
    }
}
