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

            // 개발용: 환경 변수 SOUNDVISUALIZER_TRACE 에 파일 경로를 주면 AppLog 기록을 그 파일에도 남긴다.
            var tracePath = Environment.GetEnvironmentVariable("SOUNDVISUALIZER_TRACE");
            if (!string.IsNullOrWhiteSpace(tracePath))
            {
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(tracePath));
                System.Diagnostics.Trace.AutoFlush = true;
                AppLog.Info("app", $"시작합니다({DateTime.Now:yyyy-MM-dd HH:mm:ss}).");
            }

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

