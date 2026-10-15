// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer
{
    /// <summary>
    /// 화면 색(0xRRGGBB, 불투명). 앞의 여덟 색은 안드로이드 판 UiColors.kt 와 같은 값이다.
    /// WPF 는 SoundVisualizer/Themes/Palette.xaml 에 같은 값을 붓(이름 + "Brush")으로 두고, 둘이 같은지는 테스트가 XAML 을 읽어 본다.
    ///
    /// 대비 기준(WCAG): 글자는 그 바탕 위에서 4.5:1, 스위치·슬라이더 막대와 초점 테두리 같은 그림 요소는 3:1 이상이다.
    /// 파랑을 둘로 나눈 까닭(#312): 한 색으로는 흰 글자를 얹는 바탕과 어두운 바탕 위의 글자 모두에서 4.5:1 을 넘길 수 없다.
    /// </summary>
    public static class UiColors
    {
        /// <summary>창 바탕.</summary>
        public const uint Background = 0x2A2C31;

        /// <summary>카드 바탕.</summary>
        public const uint Card = 0x1E2024;

        /// <summary>본 글자. 카드 위 14.8:1, 바탕 위 12.7:1.</summary>
        public const uint PrimaryText = 0xF2F4F6;

        /// <summary>설명·보조 글자. 카드 위 5.4:1, 바탕 위 4.6:1. 이 대비 때문에 설명을 13px 보다 작게 하지 않는다.</summary>
        public const uint SecondaryText = 0x8B95A1;

        /// <summary>어두운 바탕 위의 강조 글자·아이콘(슬라이더 값, 상태 점, 키보드 초점 테두리). 카드 위 5.9:1, 바탕 위 5.0:1.</summary>
        public const uint Accent = 0x5B9BFF;

        /// <summary>흰 글자를 얹는 강조 바탕(실행 버튼, 고른 칸)과 켠 스위치·슬라이더 막대. 흰 글자 5.2:1, 카드 위 3.2:1.</summary>
        public const uint AccentFill = 0x2563EB;

        /// <summary>실행 종료처럼 멈추거나 지우는 버튼의 바탕. 흰 글자 5.0:1.</summary>
        public const uint Stop = 0xD32F2F;

        /// <summary>안내·주의 글자. Stop 은 어두운 바탕에서 작은 글자로 읽기 어려워 밝은 주황을 쓴다. 카드 위 9.4:1.</summary>
        public const uint Warning = 0xFFB74D;

        /// <summary>강조 바탕(AccentFill, Stop) 위의 글자.</summary>
        public const uint OnFill = 0xFFFFFF;

        /// <summary>
        /// 조작 요소의 바탕: 고르지 않은 칸, 보조 버튼, 꺼진 스위치와 슬라이더의 빈 막대, 언어 콤보, 창 테두리.
        /// 본 글자 10.4:1. 보조 글자(3.8:1)와 강조 글자(4.1:1)는 이 위에 두지 않는다.
        /// </summary>
        public const uint Control = 0x333A44;

        /// <summary>마우스를 올린 조작 요소 바탕. 본 글자 8.6:1.</summary>
        public const uint ControlHover = 0x3D4654;

        /// <summary>누르고 있는 조작 요소 바탕. 본 글자 11.9:1.</summary>
        public const uint ControlPressed = 0x2B313A;

        /// <summary>마우스를 올린 강조 바탕. 흰 글자 6.7:1.</summary>
        public const uint AccentFillHover = 0x1D4ED8;

        /// <summary>누르고 있는 강조 바탕. 흰 글자 8.7:1.</summary>
        public const uint AccentFillPressed = 0x1E40AF;

        /// <summary>마우스를 올린 멈춤 버튼 바탕. 흰 글자 6.6:1.</summary>
        public const uint StopHover = 0xB71C1C;

        /// <summary>누르고 있는 멈춤 버튼 바탕. 흰 글자 8.3:1.</summary>
        public const uint StopPressed = 0x9A1A1A;

        /// <summary>쓸 수 없는 슬라이더 손잡이와 쓸 수 없는 꺼진 스위치 손잡이. 아래 셋과 함께 안드로이드 판의 비활성 색과 같다.</summary>
        public const uint DisabledThumb = 0x6B7684;

        /// <summary>쓸 수 없는 켠 스위치 손잡이.</summary>
        public const uint DisabledCheckedThumb = 0xB0B8C1;

        /// <summary>쓸 수 없는 슬라이더의 채운 막대와 켠 스위치 막대.</summary>
        public const uint DisabledActiveTrack = 0x3A4351;

        /// <summary>쓸 수 없는 슬라이더의 빈 막대와 꺼진 스위치 막대.</summary>
        public const uint DisabledTrack = 0x2A3038;
    }
}
