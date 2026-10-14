// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Media;

namespace SoundVisualizer
{
    /// <summary>
    /// 앱 전체 글꼴(<c>AppFontFamily</c> 리소스)을 언어에 맞게 바꾼다. 창들은 이 리소스를 DynamicResource 로 쓰므로
    /// 언어를 바꾸면 열린 창의 글꼴도 바로 바뀐다. 글꼴 순서는 <see cref="UiFontChain"/> 이 정한다.
    /// </summary>
    internal static class AppFonts
    {
        public const string ResourceKey = "AppFontFamily";

        private static readonly Uri BaseUri = new("pack://application:,,,/");

        public static void Apply(string? languageKey)
        {
            if (Application.Current == null) return;
            Application.Current.Resources[ResourceKey] = new FontFamily(BaseUri, UiFontChain.FamilyListFor(languageKey));
        }
    }
}
