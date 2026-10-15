// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 마우스로만 끄는 손잡이(오버레이 편집 화면의 가이드라인 모서리). Thumb 은 키보드로 움직일 수 없어, 화면 읽어주기에 보이면 이름도 없고
    /// 쓸 수도 없는 요소가 된다. 같은 값을 편집 패널의 슬라이더(크기, 크기 고정이면 고정 크기)가 바꾸므로 손잡이는 화면 읽어주기에서 숨기고,
    /// 키보드와 화면 읽어주기로는 그 슬라이더를 쓴다.
    /// </summary>
    public sealed class MouseOnlyThumb : Thumb
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new HiddenPeer(this);

        private sealed class HiddenPeer : FrameworkElementAutomationPeer
        {
            public HiddenPeer(FrameworkElement owner) : base(owner)
            {
            }

            protected override List<AutomationPeer>? GetChildrenCore() => null;

            protected override bool IsControlElementCore() => false;

            protected override bool IsContentElementCore() => false;
        }
    }
}
