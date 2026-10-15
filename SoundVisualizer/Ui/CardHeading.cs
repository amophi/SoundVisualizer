// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Automation;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 펼치기 카드(Themes/Theme.xaml 의 ExpanderCard)의 머리가 화면 읽어주기에 몇 단계 제목인지. 카드에 적으면 서식이 머리 버튼에 넘긴다.
    /// 카드(Expander)에 AutomationProperties.HeadingLevel 을 바로 적으면 카드 묶음과 머리 버튼이 둘 다 제목이 되어, 제목으로 건너뛸 때
    /// 같은 제목을 두 번 지나게 된다. 설정 탭의 모드 카드는 구역 제목 밑이라 2단계(서식의 기본값), 도움말 탭의 카드는 탭의 맨 위라 1단계다.
    /// </summary>
    public static class CardHeading
    {
        public static readonly DependencyProperty LevelProperty = DependencyProperty.RegisterAttached(
            "Level", typeof(AutomationHeadingLevel), typeof(CardHeading), new FrameworkPropertyMetadata(AutomationHeadingLevel.None));

        public static AutomationHeadingLevel GetLevel(DependencyObject element) => (AutomationHeadingLevel)element.GetValue(LevelProperty);

        public static void SetLevel(DependencyObject element, AutomationHeadingLevel value) => element.SetValue(LevelProperty, value);
    }
}
