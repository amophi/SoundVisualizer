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
    /// 색 선택 창의 채도·밝기 사각형과 색상 막대. 키보드 초점을 받아 화살표로 값을 바꾸는데, 그냥 Border 는 화면 읽어주기에 보이지 않아
    /// 초점이 와도 아무것도 읽히지 않았다. 슬라이더로 알리고 이름(AutomationProperties.Name)과 지금 값(<see cref="ValueText"/>,
    /// 예: "채도 50%, 밝기 80%")을 읽힌다. 값이 바뀌면 다시 읽도록 알린다. 값은 창이 넣는다(ColorPickerWindow).
    /// 안의 손잡이와 그라디언트는 그림이라 따로 읽히지 않는다.
    /// </summary>
    public sealed class ColorArea : Border
    {
        public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
            nameof(ValueText), typeof(string), typeof(ColorArea), new PropertyMetadata("", OnValueTextChanged));

        /// <summary>화면 읽어주기가 읽는 지금 값.</summary>
        public string ValueText
        {
            get => (string)GetValue(ValueTextProperty);
            set => SetValue(ValueTextProperty, value);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        private static void OnValueTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (UIElementAutomationPeer.FromElement((ColorArea)d) is Peer peer)
            {
                peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, e.OldValue as string ?? "", e.NewValue as string ?? "");
            }
        }

        private sealed class Peer : FrameworkElementAutomationPeer, IValueProvider
        {
            public Peer(ColorArea owner) : base(owner)
            {
            }

            private ColorArea Area => (ColorArea)Owner;

            protected override string GetClassNameCore() => nameof(ColorArea);

            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

            protected override List<AutomationPeer>? GetChildrenCore() => null;

            public override object GetPattern(PatternInterface patternInterface) =>
                patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);

            // 읽기 전용으로 알리면 화면 읽어주기가 "읽기 전용"이라 읽어 바꿀 수 없는 것처럼 들린다. 값은 화살표 키로 바꾸고,
            // 글로 값을 넣는 일(SetValue)은 받지 않는다. 색을 글로 넣으려면 옆의 HEX 코드 칸을 쓴다.
            public bool IsReadOnly => false;

            public string Value => Area.ValueText;

            public void SetValue(string value)
            {
            }
        }
    }
}
