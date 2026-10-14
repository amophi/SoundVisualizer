// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows.Controls;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// 런처 제목줄과 오버레이 편집 패널의 언어 콤보. "시스템 기본값" 다음에 18개 언어를 그 언어로 쓴 이름으로 둔다.
    /// 항목의 Tag 가 저장할 설정값(<see cref="UiLanguages.SystemCode"/> 또는 언어 코드)이고, 고르는 것도 Tag 로 한다.
    /// </summary>
    internal static class LanguagePicker
    {
        public static void Fill(ComboBox combo)
        {
            combo.Items.Clear();

            var system = new ComboBoxItem { Tag = UiLanguages.SystemCode };
            system.SetBinding(ContentControl.ContentProperty, Loc.BindingFor("language.system"));
            combo.Items.Add(system);

            foreach (var language in UiLanguages.All)
            {
                // 이름은 그 언어의 글꼴로 보인다(일본어 한자는 일본 글꼴, 번체는 번체 글꼴). 화면 언어가 무엇이든 목록이 제 모양이다.
                combo.Items.Add(new ComboBoxItem
                {
                    Content = language.NativeName,
                    Tag = language.Code,
                    FontFamily = AppFonts.FamilyFor(language.Code),
                });
            }
        }

        /// <summary>설정값에 맞는 항목을 고른다. 고르는 쪽이 SelectionChanged 를 막고 부른다.</summary>
        public static void Select(ComboBox combo, string setting)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag as string == setting)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        public static string? SelectedSetting(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string;
    }
}
