// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;

namespace SoundVisualizer
{
    /// <summary>
    /// 확인·취소를 묻는 창(안드로이드 ConfirmDialog). 소리 탭의 모두 되돌리기, 묶음 전체 바꾸기, 기본 모드로 돌아가기가 함께 쓴다.
    /// 버튼 글자도 앱 언어로 받는다. 부르는 쪽 창의 가운데에 뜨고, Esc 와 취소는 취소, Enter 는 확인이다.
    /// </summary>
    public partial class ConfirmWindow : Window
    {
        private ConfirmWindow(string title, string message, string confirmLabel, string cancelLabel)
        {
            InitializeComponent();
            // 창 제목은 화면에 없지만 화면 읽어주기가 창을 열 때 읽는다.
            Title = title;
            TxtTitle.Text = title;
            TxtMessage.Text = message;
            BtnConfirm.Content = confirmLabel;
            BtnCancel.Content = cancelLabel;
            // 확인 버튼에서 시작해 Enter 가 어디로 가는지 초점 테두리로 보인다(메시지 상자의 기본 단추와 같다).
            Loaded += (_, _) => BtnConfirm.Focus();
        }

        /// <summary>
        /// 묻고 답을 기다린다. 확인을 눌렀을 때만 true 다.
        /// </summary>
        /// <param name="from">묻는 화면. 그 창을 주인으로 두어 가운데에 띄우고 주인 창을 막는다.</param>
        public static bool Ask(DependencyObject? from, string title, string message, string confirmLabel, string cancelLabel)
        {
            var dialog = new ConfirmWindow(title, message, confirmLabel, cancelLabel);
            Window? owner = from as Window ?? (from is null ? null : Window.GetWindow(from));
            if (owner is { IsLoaded: true }) dialog.Owner = owner;
            else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return dialog.ShowDialog() == true;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
