// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 튜토리얼의 그림 틀. 그림은 옆의 글이 말하는 것을 보여 주기만 하므로 화면 읽어주기에서는 안쪽까지 통째로 건너뛴다
    /// (안드로이드 clearAndSetSemantics). 홈 그림의 "실행" 글자를 진짜 버튼으로 읽으면 누르려 들게 된다.
    /// 자동화 요소를 만들지 않는 것만으로는 안쪽 글자가 그대로 읽히므로, 자식이 없는 요소로 알린다.
    /// </summary>
    public sealed class TutorialDecoration : Border
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
