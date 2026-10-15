// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using static SoundVisualizer.UiColors;

namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 글자와 바탕의 대비가 WCAG 기준(글자 4.5:1, 그림 요소 3:1)을 넘는지. 안드로이드 판 ColorContrastTest(#312)를 옮기고,
/// PC 에만 있는 마우스 올림·누름 바탕과 키보드 초점 테두리를 더했다. 색을 바꾸다 기준 아래로 내려가면 여기서 막힌다.
/// </summary>
public class UiColorsContrastTests
{
    [Fact]
    public void 대비_계산은_WCAG_의_양_끝_값과_같다()
    {
        Assert.Equal(21.0, Contrast(0x000000, 0xFFFFFF), 6);
        Assert.Equal(1.0, Contrast(0x777777, 0x777777), 6);
        Assert.Equal(Contrast(AccentFill, OnFill), Contrast(OnFill, AccentFill), 9);
    }

    [Fact]
    public void 팔레트의_여덟_색은_안드로이드_판과_같다()
    {
        // 안드로이드 UiColors.kt 의 값. 두 판이 같은 앱으로 보이도록 바꿀 때는 함께 바꾼다.
        Assert.Equal(0x2A2C31u, Background);
        Assert.Equal(0x1E2024u, Card);
        Assert.Equal(0xF2F4F6u, PrimaryText);
        Assert.Equal(0x8B95A1u, SecondaryText);
        Assert.Equal(0x5B9BFFu, Accent);
        Assert.Equal(0x2563EBu, AccentFill);
        Assert.Equal(0xD32F2Fu, Stop);
        Assert.Equal(0xFFB74Du, Warning);
    }

    [Fact]
    public void 흰_글자를_얹는_바탕은_4_5대1_을_넘는다()
    {
        AssertReadable("실행 버튼·고른 칸", OnFill, AccentFill);
        AssertReadable("실행 버튼에 마우스를 올림", OnFill, AccentFillHover);
        AssertReadable("실행 버튼을 누름", OnFill, AccentFillPressed);
        AssertReadable("실행 종료 버튼", OnFill, Stop);
        AssertReadable("실행 종료 버튼에 마우스를 올림", OnFill, StopHover);
        AssertReadable("실행 종료 버튼을 누름", OnFill, StopPressed);
    }

    [Fact]
    public void 어두운_바탕_위의_글자는_4_5대1_을_넘는다()
    {
        AssertReadable("카드 위 본 글자", PrimaryText, Card);
        AssertReadable("바탕 위 본 글자", PrimaryText, Background);
        AssertReadable("카드 위 파란 글자", Accent, Card);
        AssertReadable("바탕 위 파란 글자", Accent, Background);
        AssertReadable("카드 위 회색 글자", SecondaryText, Card);
        // 탭 이름, 언어 콤보 이름, 홈 설명은 카드가 아니라 바탕 위에 있다. 이 대비(4.6:1) 때문에 더 작게 하지 않는다.
        AssertReadable("바탕 위 회색 글자", SecondaryText, Background);
        AssertReadable("카드 위 안내 글자", Warning, Card);
        AssertReadable("바탕 위 안내 글자", Warning, Background);
    }

    [Fact]
    public void 조작_요소_바탕_위의_본_글자는_4_5대1_을_넘는다()
    {
        AssertReadable("고르지 않은 칸·보조 버튼·언어 콤보", PrimaryText, Control);
        AssertReadable("마우스를 올린 칸·버튼", PrimaryText, ControlHover);
        AssertReadable("누르고 있는 칸·버튼", PrimaryText, ControlPressed);
    }

    [Fact]
    public void 스위치와_슬라이더_막대는_카드_위에서_그림_요소_기준_3대1_을_넘는다()
    {
        // 슬라이더와 스위치는 카드 안에만 둔다. 바탕(#2A2C31) 위에서는 2.7:1 로 모자란다.
        AssertVisible("켠 스위치·채운 슬라이더 막대", AccentFill, Card);
        // 꺼진 스위치는 막대(#333A44)가 카드와 거의 같아, 상태는 손잡이 색과 자리로 읽힌다.
        AssertVisible("꺼진 스위치 손잡이와 카드", SecondaryText, Card);
        AssertVisible("꺼진 스위치 손잡이와 막대", SecondaryText, Control);
        AssertVisible("켠 스위치 손잡이와 막대", OnFill, AccentFill);
    }

    [Fact]
    public void 키보드_초점_테두리는_바탕과_카드_위에서_3대1_을_넘는다()
    {
        AssertVisible("카드 위 초점 테두리", Accent, Card);
        AssertVisible("바탕 위 초점 테두리", Accent, Background);
    }

    private static void AssertReadable(string what, uint text, uint background)
    {
        double ratio = Contrast(text, background);
        Assert.True(ratio >= 4.5, $"{what}: {ratio:F2}:1 < 4.5:1");
    }

    private static void AssertVisible(string what, uint graphic, uint background)
    {
        double ratio = Contrast(graphic, background);
        Assert.True(ratio >= 3.0, $"{what}: {ratio:F2}:1 < 3:1");
    }

    /// <summary>WCAG 2 의 대비. 상대 휘도는 sRGB 를 선형으로 풀어 구한다(안드로이드 Color.luminance() 와 같은 식).</summary>
    private static double Contrast(uint a, uint b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(uint rgb)
    {
        static double Linear(uint channel)
        {
            double c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear((rgb >> 16) & 0xFF) + 0.7152 * Linear((rgb >> 8) & 0xFF) + 0.0722 * Linear(rgb & 0xFF);
    }
}
