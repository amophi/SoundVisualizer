// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 소리 탭의 카드와 소리 줄. 어디를 눌러도(Enter·스페이스도) 종류 목록이 열린다(모바일 dropdownRow). 목록은 소리 탭이 연다.
    ///
    /// 화면 읽어주기에는 줄 하나를 이름(AutomationProperties.Name, 모바일 cd_classify_*) 한 덩어리로 읽히고 안의 글자는 따로 읽히지
    /// 않는다. 글자마다 멈추면 고급 모드의 521줄을 지나기가 너무 길다. 목록을 여는 단추라 펼침·접힘도 알린다(모바일 Role.DropdownList).
    /// </summary>
    public sealed class TypeMenuButton : Button
    {
        public static readonly DependencyProperty IsMenuOpenProperty = DependencyProperty.Register(
            nameof(IsMenuOpen), typeof(bool), typeof(TypeMenuButton), new PropertyMetadata(false, OnIsMenuOpenChanged));

        /// <summary>이 줄의 종류 목록이 열려 있는지. 서식이 종류 단추를 눌린 모양으로 두고, 화면 읽어주기에 펼침으로 알린다.</summary>
        public bool IsMenuOpen
        {
            get => (bool)GetValue(IsMenuOpenProperty);
            set => SetValue(IsMenuOpenProperty, value);
        }

        /// <summary>열려 있는 목록. 화면 읽어주기가 접으라고 하면 닫는다.</summary>
        internal ContextMenu? OpenMenu { get; set; }

        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        private static void OnIsMenuOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (UIElementAutomationPeer.FromElement((TypeMenuButton)d) is Peer peer)
            {
                peer.RaisePropertyChangedEvent(
                    ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    (bool)e.OldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                    (bool)e.NewValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
            }
        }

        private sealed class Peer : ButtonAutomationPeer, IExpandCollapseProvider
        {
            public Peer(TypeMenuButton owner) : base(owner)
            {
            }

            private TypeMenuButton Button => (TypeMenuButton)Owner;

            public ExpandCollapseState ExpandCollapseState => Button.IsMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

            public override object GetPattern(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

            // 안의 글자는 이름에 이미 들어 있다(모바일 clearAndSetSemantics).
            protected override List<AutomationPeer>? GetChildrenCore() => null;

            public void Expand()
            {
                if (!Button.IsMenuOpen) ((IInvokeProvider)this).Invoke();
            }

            public void Collapse()
            {
                if (Button.OpenMenu is { } menu) menu.IsOpen = false;
            }
        }
    }
}
