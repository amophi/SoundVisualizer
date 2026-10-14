// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Diagnostics;

namespace SoundVisualizer
{
    /// <summary>
    /// 조용히 삼키던 실패를 남긴다. 사용자에게 보이지는 않고, DebugView 나 디버거 출력 창에서 볼 수 있다.
    /// 설정 파일, 캡처 장치, AI 모델처럼 실패해도 앱이 계속 돌아야 하는 곳에서 쓴다.
    /// </summary>
    public static class AppLog
    {
        /// <summary>정상 동작의 이정표(예: AI 를 어떤 형식으로 시작했는지). 문제를 되짚을 때 쓴다.</summary>
        public static void Info(string tag, string message) => Trace.WriteLine($"[SoundVisualizer:{tag}] {message}");

        public static void Warn(string tag, string message, Exception? error = null)
        {
            Trace.WriteLine(error == null
                ? $"[SoundVisualizer:{tag}] {message}"
                : $"[SoundVisualizer:{tag}] {message} ({error.GetType().Name}: {error.Message})");
        }
    }
}
