// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// 메시지 상자. 아랍어 화면이면 Windows 에 오른쪽에서 왼쪽으로 읽고 오른쪽에 붙이라고 알린다.
    /// 예/아니요 같은 버튼 글자는 Windows 가 자기 언어로 그린다.
    /// </summary>
    internal static class Dialogs
    {
        public static MessageBoxResult Show(string text, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var options = Loc.Instance.IsRightToLeft ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None;
            return MessageBox.Show(text, caption, button, icon, MessageBoxResult.None, options);
        }

        /// <summary>예/아니요로 묻는다. 예를 눌렀을 때만 true.</summary>
        public static bool Confirm(string text, string caption) =>
            Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
