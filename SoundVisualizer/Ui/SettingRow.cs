// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 설정 한 줄(스위치 줄, 슬라이더 줄)에 붙이는 글자. Themes/Theme.xaml 의 SwitchRow·SliderRow 서식이 이 값을 읽어
    /// 이름·설명·값을 그린다. 그래서 XAML 에는 슬라이더나 체크박스 하나만 두고, 코드는 지금처럼 그 컨트롤만 다룬다.
    /// <code>&lt;Slider Style="{StaticResource SliderRow}" ui:SettingRow.Label="{l:Loc settings.size.title}" ui:SettingRow.Description="{l:Loc settings.size.desc.wave}"/&gt;</code>
    /// </summary>
    public static class SettingRow
    {
        /// <summary>슬라이더 줄의 이름(위 줄 왼쪽, 16 굵게). 화면 읽어주기도 이 이름으로 슬라이더를 읽는다. 스위치 줄의 이름은 Content 다.</summary>
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.RegisterAttached("Label", typeof(string), typeof(SettingRow), new FrameworkPropertyMetadata(null));

        /// <summary>이름 아래의 설명(13, 보조 글자색). 비어 있으면 자리를 차지하지 않는다.</summary>
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.RegisterAttached("Description", typeof(string), typeof(SettingRow), new FrameworkPropertyMetadata(null));

        /// <summary>슬라이더 값의 꼴(string.Format, InvariantCulture). 기본은 정수 "{0:F0}" 이고 "{0:F0}%", "{0:F0} FPS" 처럼 쓴다.</summary>
        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.RegisterAttached("ValueFormat", typeof(string), typeof(SettingRow), new FrameworkPropertyMetadata(ValueFormatConverter.DefaultFormat));

        public static string? GetLabel(DependencyObject element) => (string?)element.GetValue(LabelProperty);

        public static void SetLabel(DependencyObject element, string? value) => element.SetValue(LabelProperty, value);

        public static string? GetDescription(DependencyObject element) => (string?)element.GetValue(DescriptionProperty);

        public static void SetDescription(DependencyObject element, string? value) => element.SetValue(DescriptionProperty, value);

        public static string GetValueFormat(DependencyObject element) => (string)element.GetValue(ValueFormatProperty);

        public static void SetValueFormat(DependencyObject element, string value) => element.SetValue(ValueFormatProperty, value);
    }
}
