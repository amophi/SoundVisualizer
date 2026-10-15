// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Windows.Data;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 단축키 버튼의 화면 읽어주기 이름(Themes/Theme.xaml 의 Button.Hotkey). 버튼 글자는 지금 키("F3")뿐이라, 무엇의 단축키인지
    /// (ui:SettingRow.Label)를 앞에 붙인다: "표현 모드 전환, 단축키: F3". 키를 받는 동안에는 키 자리에 안내 글("키를 누르세요")이 온다.
    /// 값: [무엇의 단축키인지, 버튼 글자]. 이름표는 {l:Loc} 바인딩이라 언어가 바뀌면 다시 불려 새 언어의 꼴로 만든다.
    /// </summary>
    public sealed class HotkeyNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            string label = values.Length > 0 && values[0] is string given ? given : "";
            string key = values.Length > 1 && values[1] is string text ? text : "";
            return label.Length == 0 ? key : Loc.Format("hotkey.button_name", label, key);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
