// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Configuration;
using System.Data;
using System.Windows;

namespace SoundVisualizer
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppSettings.Load();
            AppFonts.Apply(AppSettings.Language);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 슬라이더를 움직인 직후에 닫아도 마지막 값이 남게, 미뤄 둔 저장을 지금 쓴다.
            AppSettings.Flush();
            base.OnExit(e);
        }
    }
}

