// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows.Automation;
using System.Windows.Controls;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 소리 종류 색 버튼(런처 설정 탭, 오버레이 편집 패널)의 화면 읽어주기 이름: "색상 설정: 환경음". 버튼 셋의 글자가 같거나("색상 설정")
    /// 없어서(편집 패널은 동그라미만 있다) 이름에 어느 종류의 색인지 넣어야 셋을 가려 들을 수 있다. 버튼의 Tag 가 종류다.
    /// 코드가 넣는 이름이라 언어가 바뀌면 창이 다시 부른다.
    /// </summary>
    internal static class ColorButtonNames
    {
        public static void Apply(params Button[] buttons)
        {
            foreach (var button in buttons) AutomationProperties.SetName(button, Loc.Format("settings.set_color_for", TypeName(button.Tag as string)));
        }

        private static string TypeName(string? tag) => tag switch
        {
            "Speech" => Loc.Get("sounds.type.speech"),
            "Danger" => Loc.Get("sounds.type.danger"),
            _ => Loc.Get("sounds.type.ambient"),
        };
    }
}
