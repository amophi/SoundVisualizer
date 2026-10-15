// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer
{
    /// <summary>콤보에 누른 키. WPF 의 Key 를 Core 에서 쓰려고 필요한 것만 옮겼다.</summary>
    public enum ComboKey
    {
        Other,
        Up,
        Down,
        Left,
        Right,
        Home,
        End,
        PageUp,
        PageDown,
        Enter,
        Space,
        Tab,
    }

    /// <summary>키를 콤보에 넘기기 전에 할 일.</summary>
    public enum ComboKeyAction
    {
        /// <summary>콤보가 늘 하던 대로 한다.</summary>
        Default,

        /// <summary>값을 바꾸지 않고 목록을 연다(키는 콤보에 넘기지 않는다).</summary>
        OpenList,

        /// <summary>고르지 않고 목록을 닫은 뒤 초점을 다음(Shift 면 앞) 칸으로 옮긴다.</summary>
        CloseAndMoveFocus,
    }

    /// <summary>
    /// 고르는 즉시 앱 전체가 바뀌는 콤보(언어)의 키 규칙. WPF 콤보는 닫혀 있을 때 화살표·Home/End·글자 키로 바로 다음 항목을 골라서,
    /// 키를 누를 때마다 언어(글꼴, 쓰는 방향, 모든 글자)가 바뀌었다. 그래서 닫혀 있을 때는 이 키들로 목록을 열기만 하고,
    /// 열린 목록에서 둘러본 뒤 Enter(또는 Alt+↑, F4, 마우스 클릭)로 고를 때만 바꾼다. Esc 는 고르지 않고 닫는다.
    /// WAI-ARIA 의 "select-only combobox" 와 같은 동작이다. WPF 의 ui:CommitOnCloseComboBox 가 쓴다.
    /// </summary>
    public static class ComboListKeys
    {
        /// <summary>
        /// 키를 콤보에 넘기기 전에 할 일. 닫힌 콤보의 이동 키와 Enter·스페이스는 목록을 연다. 열린 목록의 Tab 은 고르지 않고 닫은 뒤
        /// 다음 칸으로 간다(WPF 는 목록 안의 다음 항목으로만 돌아 목록을 빠져나가지 못한다). Alt 와 함께 누른 키(Alt+↓ 는 WPF 가 이미
        /// 연다)와 그 밖의 키(F4, Esc)는 콤보가 하던 대로 둔다. 열린 목록의 이동 키는 WPF 가 항목을 옮겨 보이기만 하므로 손대지 않는다.
        /// </summary>
        public static ComboKeyAction OnKeyDown(ComboKey key, bool alt, bool listOpen)
        {
            if (alt || key == ComboKey.Other) return ComboKeyAction.Default;
            if (listOpen) return key == ComboKey.Tab ? ComboKeyAction.CloseAndMoveFocus : ComboKeyAction.Default;
            return key == ComboKey.Tab ? ComboKeyAction.Default : ComboKeyAction.OpenList;
        }

        /// <summary>닫힌 콤보에 친 글자도 목록부터 연다. 닫힌 채로 글자를 찾으면 찾은 항목이 바로 골라지기 때문이다.</summary>
        public static bool TextOpensList(string? text, bool listOpen) => !listOpen && !string.IsNullOrEmpty(text);
    }
}
