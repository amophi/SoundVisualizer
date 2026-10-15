// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Windows;
using System.Windows.Threading;
using SoundVisualizer.Tutorial;

namespace SoundVisualizer
{
    // 튜토리얼(안드로이드 MainActivity·HomeTab): 처음 여는 사람에게 한 번 저절로 띄우고, 홈의 "튜토리얼 보기"로 다시 연다.
    public partial class LauncherWindow
    {
        private bool _tutorialOpen;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            // 본 적이 없으면 런처가 다 그려진 뒤에 띄운다. 튜토리얼은 런처 가운데에 떠서 뒤에 런처가 보인다.
            if (!AppSettings.TutorialSeen) Dispatcher.InvokeAsync(ShowTutorial, DispatcherPriority.ApplicationIdle);
        }

        private void BtnTutorial_Click(object sender, RoutedEventArgs e) => ShowTutorial();

        /// <summary>
        /// 튜토리얼을 런처 위에 띄우고 닫힐 때까지 기다린다(그동안 런처는 누를 수 없다). 사용자가 닫았으면(끝까지 넘기거나 건너뛰면)
        /// 본 것으로 저장해 다음부터는 저절로 뜨지 않는다. 닫지 않은 채 앱을 끝내면(트레이의 종료) 다음에 다시 띄운다.
        /// </summary>
        private void ShowTutorial()
        {
            if (_tutorialOpen || !IsLoaded) return;
            _tutorialOpen = true;
            try
            {
                var tutorial = new TutorialWindow { Owner = this };
                tutorial.ShowDialog();
                if (tutorial.Finished && !AppSettings.TutorialSeen)
                {
                    AppSettings.TutorialSeen = true;
                    AppSettings.Save();
                }
            }
            finally
            {
                _tutorialOpen = false;
            }
        }
    }
}
