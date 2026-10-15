// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 화면 읽어주기에 이름 있는 묶음으로 보이는 UniformGrid(색 선택 창의 "자주 쓰는 색"). 패널은 자동화 요소가 없어 안의 칸들이 창에
    /// 흩어져 읽히는데, 묶음으로 두면 칸에 들어설 때 무엇의 칸인지 함께 읽힌다. 이름은 AutomationProperties.LabeledBy 나 Name 으로 준다.
    /// </summary>
    public sealed class GroupUniformGrid : UniformGrid
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

        private sealed class GroupPeer : FrameworkElementAutomationPeer
        {
            public GroupPeer(FrameworkElement owner) : base(owner)
            {
            }

            protected override string GetClassNameCore() => nameof(GroupUniformGrid);

            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        }
    }
}
