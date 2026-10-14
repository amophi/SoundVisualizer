// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using SoundVisualizer.Diagnostics;

namespace SoundVisualizer.Tests.Diagnostics;

/// <summary>
/// HUD·CSV 의 소수가 모바일(Kotlin <c>String.format(Locale.US, "%.Nf")</c>)과 같은 글자인지. 기대값은 Java Formatter 의 규칙
/// (가장 짧은 십진 표현을 5 에서 올리고, 부호는 크기와 따로 붙인다)으로 정했다. .NET 의 "F" 서식과 갈리는 경계를 고정한다.
/// </summary>
public class JavaNumberFormatTests
{
    [Theory]
    [InlineData(0.15, 1, "0.2")]   // .NET "F1" 은 "0.1" (이진값이 0.1499…)
    [InlineData(0.25, 1, "0.3")]   // .NET 은 짝수 쪽으로 "0.2"
    [InlineData(0.35, 1, "0.4")]
    [InlineData(0.125, 2, "0.13")]
    [InlineData(1.005, 2, "1.01")]
    [InlineData(12.5, 0, "13")]
    [InlineData(2.5, 0, "3")]
    [InlineData(0.5, 0, "1")]
    [InlineData(0.4, 0, "0")]
    [InlineData(65.0, 0, "65")]
    [InlineData(41.0, 1, "41.0")]
    [InlineData(99.949, 1, "99.9")]
    [InlineData(1e-30, 2, "0.00")]
    public void 자바처럼_가장_짧은_십진_표현을_5에서_올린다(double value, int decimals, string expected)
    {
        Assert.Equal(expected, JavaNumberFormat.Fixed(value, decimals));
    }

    [Fact]
    public void float_는_자바처럼_double_로_넓힌_값을_적는다()
    {
        // Java Formatter 는 float 를 double 로 바꾼 뒤 적는다. 0.145f 는 0.14499999582767487 이라 "0.15" 가 아니다.
        Assert.Equal("0.14", JavaNumberFormat.Fixed(0.145f, 2));
        Assert.Equal("0.41", JavaNumberFormat.Fixed(0.41f, 2));
        Assert.Equal("0.54321", JavaNumberFormat.Fixed(0.54321f, 5));
        Assert.Equal("0.42000", JavaNumberFormat.Fixed(0.42f, 5));
        Assert.Equal("0.10000", JavaNumberFormat.Fixed(0.1f, 5));
    }

    [Fact]
    public void 부호는_크기를_적은_뒤에_붙인다()
    {
        double negativeZero = -0.0;
        Assert.True(double.IsNegative(negativeZero));
        Assert.Equal("-0.00", JavaNumberFormat.Fixed(negativeZero, 2));
        Assert.Equal("-0.00", JavaNumberFormat.Fixed(-0.001, 2));
        Assert.Equal("-1.3", JavaNumberFormat.Fixed(-1.25, 1));
    }

    [Fact]
    public void 숫자가_아닌_값은_자바와_같은_글자다()
    {
        Assert.Equal("NaN", JavaNumberFormat.Fixed(double.NaN, 2));
        Assert.Equal("Infinity", JavaNumberFormat.Fixed(double.PositiveInfinity, 2));
        Assert.Equal("-Infinity", JavaNumberFormat.Fixed(double.NegativeInfinity, 2));
    }

    [Fact]
    public void 로캘과_무관하다()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-EG");
            // 이 로캘의 기본 서식은 소수점이 다르다(0٫41). 고정하지 않았다면 여기서 드러난다.
            Assert.NotEqual("0.41", 0.41.ToString("F2", CultureInfo.CurrentCulture));
            Assert.Equal("0.41", JavaNumberFormat.Fixed(0.41, 2));
            Assert.Equal("-1.3", JavaNumberFormat.Fixed(-1.25, 1));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
