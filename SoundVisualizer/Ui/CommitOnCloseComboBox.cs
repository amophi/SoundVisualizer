// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows.Controls;
using System.Windows.Input;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 고르는 즉시 앱 전체가 바뀌는 콤보(런처 제목줄과 오버레이 편집 패널의 언어). 닫혀 있을 때는 화살표·Home/End·Enter·스페이스·글자 키가
    /// 값을 바꾸지 않고 목록을 연다(규칙은 Core 의 <see cref="ComboListKeys"/>). 열린 목록에서는 WPF 가 화살표로 항목을 옮겨 보이기만 하고,
    /// Enter·Alt+↑·F4·마우스 클릭으로 고를 때 SelectionChanged 가 온다. Esc 와 Tab, 목록 밖 클릭은 고르지 않고 닫는다.
    /// 그래서 키보드로 언어 이름을 둘러봐도 고르기 전에는 앱이 바뀌지 않는다. 마우스는 예전처럼 한 번 눌러 고른다.
    /// </summary>
    public sealed class CommitOnCloseComboBox : ComboBox
    {
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled) return;
            bool alt = e.Key == Key.System || (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            // Ctrl+Tab 은 런처가 탭을 옮기는 데 쓴다. 콤보의 Tab 으로 치지 않는다.
            bool ctrlTab = key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            switch (ComboListKeys.OnKeyDown(ctrlTab ? ComboKey.Other : Map(key), alt, IsDropDownOpen))
            {
                case ComboKeyAction.OpenList:
                    SetCurrentValue(IsDropDownOpenProperty, true);
                    e.Handled = true;
                    break;
                case ComboKeyAction.CloseAndMoveFocus:
                    // 닫으면 콤보가 초점을 제게로 되돌린다. 그 자리에서 다음(Shift+Tab 이면 앞) 칸으로 간다.
                    SetCurrentValue(IsDropDownOpenProperty, false);
                    bool back = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                    MoveFocus(new TraversalRequest(back ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);
            if (e.Handled || !ComboListKeys.TextOpensList(e.Text, IsDropDownOpen)) return;
            // 친 글자는 목록을 여는 데 쓴다. 이름 찾기는 열린 목록에서 다음 글자부터 한다(열린 목록에서는 찾아도 고르지 않는다).
            SetCurrentValue(IsDropDownOpenProperty, true);
            e.Handled = true;
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);
            // 닫힌 콤보 위에서 굴린 휠도 값을 바꾸지 않는다. 화면을 굴리다 콤보를 지나칠 때 언어가 바뀌지 않게 한다.
            if (!IsDropDownOpen) e.Handled = true;
        }

        private static ComboKey Map(Key key) => key switch
        {
            Key.Up => ComboKey.Up,
            Key.Down => ComboKey.Down,
            Key.Left => ComboKey.Left,
            Key.Right => ComboKey.Right,
            Key.Home => ComboKey.Home,
            Key.End => ComboKey.End,
            Key.PageUp => ComboKey.PageUp,
            Key.PageDown => ComboKey.PageDown,
            Key.Enter => ComboKey.Enter,
            Key.Space => ComboKey.Space,
            Key.Tab => ComboKey.Tab,
            _ => ComboKey.Other,
        };
    }
}
