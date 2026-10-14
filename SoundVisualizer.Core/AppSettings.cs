// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using SoundVisualizer.Localization;

namespace SoundVisualizer
{
    public class VisualModeSettings
    {
        public double Intensity { get; set; } = 50.0;
        public double PositionSpeed { get; set; } = 20.0;
        public double Sensitivity { get; set; } = 3.75;
        public double VisualOpacity { get; set; } = 50.0;
        public bool IsGlowMode { get; set; } = false;
        public double GlowIntensity { get; set; } = 0.0;
        public double CircleRadius { get; set; } = 40.0;
        public bool IntensityAsOpacity { get; set; } = false;
        public double OpacityFixedSize { get; set; } = 30.0;
        public double OpacityFixedMaxOpacity { get; set; } = 100.0;
    }

    public static class AppSettings
    {
        // 모드별 개별 설정 인스턴스. 기본값은 VisualModeSettings 의 초기값 한 곳에만 둔다.
        public static VisualModeSettings WaveMode { get; set; } = new VisualModeSettings();
        public static VisualModeSettings PadMode { get; set; } = new VisualModeSettings();
        public static VisualModeSettings CircleMode { get; set; } = new VisualModeSettings();
        public static VisualModeSettings OutlineMode { get; set; } = new VisualModeSettings();

        // 단축키 기본값(가상 키 코드). F2 사운드 모드, F3 표현 모드, F4 오버레이 설정.
        public const int DefaultSoundModeKey = 0x71;
        public const int DefaultVisualModeKey = 0x72;
        public const int DefaultEditModeKey = 0x73;
        public const double DefaultTargetFps = 144.0;

        // 1. 파도의 크기 (현재 모드값 반환/설정)
        public static double WaveIntensity
        {
            get => GetCurrentModeSettings().Intensity;
            set => GetCurrentModeSettings().Intensity = value;
        }

        // 2. 파도 위치 변화 속도
        public static double WavePositionSpeed
        {
            get => GetCurrentModeSettings().PositionSpeed;
            set => GetCurrentModeSettings().PositionSpeed = value;
        }

        // 3. 파도의 민감성/떨림
        public static double WaveSensitivity
        {
            get => GetCurrentModeSettings().Sensitivity;
            set => GetCurrentModeSettings().Sensitivity = value;
        }

        // 4. 파도/그래픽 투명도 (0 ~ 100)
        public static double VisualOpacity
        {
            get => GetCurrentModeSettings().VisualOpacity;
            set => GetCurrentModeSettings().VisualOpacity = value;
        }

        // 5. 시각화 모드 (0 = Wave 모드, 1 = Pad 모드, 2 = Circle 모드)
        public static int VisualMode { get; set; } = 0;

        // 5.5. 원형 모드 반지름 (10 ~ 100)
        public static double CircleRadius
        {
            get => GetCurrentModeSettings().CircleRadius;
            set => GetCurrentModeSettings().CircleRadius = value;
        }

        // 6. 투명도로 세기 표현 (크기 고정)
        public static bool IntensityAsOpacity
        {
            get => GetCurrentModeSettings().IntensityAsOpacity;
            set => GetCurrentModeSettings().IntensityAsOpacity = value;
        }

        public static double OpacityFixedSize
        {
            get => GetCurrentModeSettings().OpacityFixedSize;
            set => GetCurrentModeSettings().OpacityFixedSize = value;
        }

        public static double OpacityFixedMaxOpacity
        {
            get => GetCurrentModeSettings().OpacityFixedMaxOpacity;
            set => GetCurrentModeSettings().OpacityFixedMaxOpacity = value;
        }

        // 6. 스테레오 확장 모드
        public static int SoundMode { get; set; } = 2;

        // 6.5. 광원(Glow) 효과 모드
        public static bool IsGlowMode
        {
            get => GetCurrentModeSettings().IsGlowMode;
            set => GetCurrentModeSettings().IsGlowMode = value;
        }

        public static double GlowIntensity
        {
            get => GetCurrentModeSettings().GlowIntensity;
            set => GetCurrentModeSettings().GlowIntensity = value;
        }

        // 7. 단축키 설정
        public static List<int> StereoUpmixKeyBind { get; set; } = new List<int> { DefaultSoundModeKey }; // F2
        public static List<int> VisualModeKeyBind { get; set; } = new List<int> { DefaultVisualModeKey };  // F3
        public static List<int> EditModeKeyBind { get; set; } = new List<int> { DefaultEditModeKey };    // F4

        // 구버전 호환용
        public static int StereoUpmixHotkey { get; set; } = DefaultSoundModeKey;
        public static int VisualModeHotkey { get; set; } = DefaultVisualModeKey;
        public static int EditModeHotkey { get; set; } = DefaultEditModeKey;

        // 7. 화면 언어 설정. "system"(Windows 화면 언어를 따름) 또는 언어 코드("ko", "zh-Hans" 등). 새로 설치하면 system 이다.
        public const string DefaultLanguage = UiLanguages.SystemCode;
        public static string Language { get; set; } = DefaultLanguage;

        // 8.5. 관리자 모드
        public static bool IsAdminMode { get; set; } = false;
        public static bool AutoMinimizeOnLaunch { get; set; } = false;

        // 8.6. 렌더링 최대 프레임
        public static double TargetFps { get; set; } = DefaultTargetFps;

        // 9. 화면 표시 여부 (환경음, 말소리, 강조음)
        public static bool ShowAmbient { get; set; } = true;
        public static bool ShowSpeech { get; set; } = true;
        public static bool ShowDanger { get; set; } = true;

        // 10. 화면 표시 색상 (환경음, 말소리, 강조음)
        public const string DefaultColorAmbient = "#FFFFFFFF";
        public const string DefaultColorSpeech = "#FFFFFF00";
        public const string DefaultColorDanger = "#FFFF0000";
        public static string ColorAmbient { get; set; } = DefaultColorAmbient;
        public static string ColorSpeech { get; set; } = DefaultColorSpeech;
        public static string ColorDanger { get; set; } = DefaultColorDanger;

        /// <summary>설정 파일 위치. 포터블 배포라 실행 파일 옆에 둔다. 테스트는 임시 파일로 바꾼다.</summary>
        public static string SettingsFilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        /// <summary>
        /// 저장을 이만큼 미뤄 모은다. 슬라이더를 끌면 값이 바뀔 때마다 저장을 부르는데, 그때마다 파일을 쓰면
        /// UI 스레드가 디스크를 기다린다. 마지막 변경 뒤 이만큼 조용하면 한 번 쓴다.
        /// </summary>
        public const int SaveDelayMs = 400;

        private static readonly object SaveGate = new();
        private static System.Threading.Timer? _saveTimer;
        private static bool _savePending;

        private static VisualModeSettings GetCurrentModeSettings()
        {
            if (VisualMode == 0) return WaveMode;
            if (VisualMode == 1) return PadMode;
            if (VisualMode == 2) return CircleMode;
            return OutlineMode;
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        VisualMode = data.VisualMode;
                        SoundMode = data.SoundMode;
                        if (data.StereoUpmixKeyBind != null && data.StereoUpmixKeyBind.Count > 0) StereoUpmixKeyBind = data.StereoUpmixKeyBind;
                        else StereoUpmixKeyBind = new List<int> { data.StereoUpmixHotkey != 0 ? data.StereoUpmixHotkey : DefaultSoundModeKey };

                        if (data.VisualModeKeyBind != null && data.VisualModeKeyBind.Count > 0) VisualModeKeyBind = data.VisualModeKeyBind;
                        else VisualModeKeyBind = new List<int> { data.VisualModeHotkey != 0 ? data.VisualModeHotkey : DefaultVisualModeKey };

                        if (data.EditModeKeyBind != null && data.EditModeKeyBind.Count > 0) EditModeKeyBind = data.EditModeKeyBind;
                        else EditModeKeyBind = new List<int> { data.EditModeHotkey != 0 ? data.EditModeHotkey : DefaultEditModeKey };

                        // 예전 판은 언어 이름("日本語")이나 약어("KOR")를 적었다. 읽을 때 코드로 옮기고, 다음 저장부터 코드로 쓴다.
                        Language = UiLanguages.NormalizeSetting(data.Language);

                        IsAdminMode = data.IsAdminMode;
                        AutoMinimizeOnLaunch = data.AutoMinimizeOnLaunch;
                        if (data.TargetFps >= 30.0) TargetFps = data.TargetFps;
                        ShowAmbient = data.ShowAmbient;
                        ShowSpeech = data.ShowSpeech;
                        ShowDanger = data.ShowDanger;
                        ColorAmbient = data.ColorAmbient ?? DefaultColorAmbient;
                        ColorSpeech = data.ColorSpeech ?? DefaultColorSpeech;
                        ColorDanger = data.ColorDanger ?? DefaultColorDanger;

                        // 하이브리드 모드 로드 (모드별 설정이 없는 구버전 호환)
                        if (data.WaveMode != null) WaveMode = data.WaveMode;
                        else WaveMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        if (data.PadMode != null) PadMode = data.PadMode;
                        else PadMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        if (data.CircleMode != null) CircleMode = data.CircleMode;
                        else CircleMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        if (data.OutlineMode != null) OutlineMode = data.OutlineMode;
                        else OutlineMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        int maxRate = GetMonitorRefreshRate();
                        if (TargetFps > maxRate) TargetFps = maxRate;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("settings", "설정 파일을 읽지 못해 기본값으로 시작합니다.", ex);
            }
        }

        /// <summary>
        /// 저장을 예약한다. <see cref="SaveDelayMs"/> 안에 다시 부르면 마지막 것 한 번만 쓴다.
        /// 부른 스레드(보통 UI 스레드)에서 쓰므로 설정을 고치는 쪽과 엇갈리지 않는다. 앱을 닫을 때는 <see cref="Flush"/> 를 부른다.
        /// </summary>
        public static void Save()
        {
            lock (SaveGate)
            {
                _savePending = true;
                _saveContext = SynchronizationContext.Current;
                _saveTimer ??= new System.Threading.Timer(OnSaveTimer, null, Timeout.Infinite, Timeout.Infinite);
                _saveTimer.Change(SaveDelayMs, Timeout.Infinite);
            }
        }

        /// <summary>예약된 저장이 있으면 지금 쓴다. 앱을 닫을 때와 오버레이 편집을 마칠 때 부른다.</summary>
        public static void Flush()
        {
            lock (SaveGate)
            {
                if (!_savePending) return;
                _savePending = false;
                _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                WriteFile();
            }
        }

        /// <summary>미루지 않고 바로 쓴다. 예약된 저장도 이것으로 끝난다.</summary>
        public static void SaveNow()
        {
            lock (SaveGate)
            {
                _savePending = false;
                _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                WriteFile();
            }
        }

        /// <summary>예약된 저장이 남아 있는지. 테스트가 쓴다.</summary>
        public static bool HasPendingSave
        {
            get { lock (SaveGate) return _savePending; }
        }

        private static SynchronizationContext? _saveContext;

        private static void OnSaveTimer(object? state)
        {
            SynchronizationContext? context;
            lock (SaveGate) context = _saveContext;
            if (context != null) context.Post(_ => Flush(), null);
            else Flush();
        }

        /// <summary>
        /// 언어를 뺀 모든 설정을 기본값으로 되돌린다(전체 초기화 버튼). 단축키는 실제로 쓰는 키 목록까지 되돌린다.
        /// 저장은 부르는 쪽이 한다.
        /// </summary>
        public static void ResetToDefaults()
        {
            WaveMode = new VisualModeSettings();
            PadMode = new VisualModeSettings();
            CircleMode = new VisualModeSettings();
            OutlineMode = new VisualModeSettings();

            VisualMode = 0;
            SoundMode = 2; // 실행 중이면 장치의 채널 수에 맞춰 다시 정해진다

            StereoUpmixKeyBind = new List<int> { DefaultSoundModeKey };
            VisualModeKeyBind = new List<int> { DefaultVisualModeKey };
            EditModeKeyBind = new List<int> { DefaultEditModeKey };
            StereoUpmixHotkey = DefaultSoundModeKey;
            VisualModeHotkey = DefaultVisualModeKey;
            EditModeHotkey = DefaultEditModeKey;

            IsAdminMode = false;
            AutoMinimizeOnLaunch = false;
            TargetFps = Math.Min(DefaultTargetFps, GetMonitorRefreshRate());

            ShowAmbient = true;
            ShowSpeech = true;
            ShowDanger = true;
            ColorAmbient = DefaultColorAmbient;
            ColorSpeech = DefaultColorSpeech;
            ColorDanger = DefaultColorDanger;
        }

        private static void WriteFile()
        {
            try
            {
                var data = new SettingsData
                {
                    VisualMode = VisualMode,
                    SoundMode = SoundMode,
                    StereoUpmixKeyBind = StereoUpmixKeyBind,
                    VisualModeKeyBind = VisualModeKeyBind,
                    EditModeKeyBind = EditModeKeyBind,
                    Language = Language,
                    
                    IsAdminMode = IsAdminMode,
                    AutoMinimizeOnLaunch = AutoMinimizeOnLaunch,
                    TargetFps = TargetFps,
                    ShowAmbient = ShowAmbient,
                    ShowSpeech = ShowSpeech,
                    ShowDanger = ShowDanger,
                    ColorAmbient = ColorAmbient,
                    ColorSpeech = ColorSpeech,
                    ColorDanger = ColorDanger,

                    WaveMode = WaveMode,
                    PadMode = PadMode,
                    CircleMode = CircleMode,
                    OutlineMode = OutlineMode,

                    // 구버전 호환성 저장을 위해 현재 모드의 최종 설정값도 루트 필드에 동시 저장
                    WaveIntensity = WaveIntensity,
                    WavePositionSpeed = WavePositionSpeed,
                    WaveSensitivity = WaveSensitivity,
                    VisualOpacity = VisualOpacity,
                    CircleRadius = CircleRadius,
                    IsGlowMode = IsGlowMode,
                    GlowIntensity = GlowIntensity,
                    OpacityFixedMaxOpacity = OpacityFixedMaxOpacity
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                // 쓰기 금지 폴더(Program Files 등)에 풀어 둔 경우 여기로 온다. 설정은 이번 실행 동안만 남는다.
                AppLog.Warn("settings", $"설정을 저장하지 못했습니다: {SettingsFilePath}", ex);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hDC, int nIndex);

        private const int VREFRESH = 116;

        public static int GetMonitorRefreshRate()
        {
            IntPtr hDC = GetDC(IntPtr.Zero);
            try
            {
                int rate = GetDeviceCaps(hDC, VREFRESH);
                return rate > 0 ? rate : 60; // 디바이스 주사율 질의 실패 시 안전하게 60Hz 리턴
            }
            catch
            {
                return 60;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hDC);
            }
        }

        private class SettingsData
        {
            public double WaveIntensity { get; set; } = 50.0;
            public double WavePositionSpeed { get; set; } = 20.0;
            public double WaveSensitivity { get; set; } = 3.75;
            public double VisualOpacity { get; set; } = 50.0;
            public int VisualMode { get; set; } = 0;
            public double CircleRadius { get; set; } = 40.0;
            public int SoundMode { get; set; } = 2;
            public bool IsGlowMode { get; set; } = false;
            public double GlowIntensity { get; set; } = 0.0;
            public double OpacityFixedMaxOpacity { get; set; } = 100.0;
            public List<int> StereoUpmixKeyBind { get; set; } = new List<int>();
            public List<int> VisualModeKeyBind { get; set; } = new List<int>();
            public List<int> EditModeKeyBind { get; set; } = new List<int>();
            
            // 구버전 호환
            public int StereoUpmixHotkey { get; set; } = 0x71; 
            public int VisualModeHotkey { get; set; } = 0x72;  
            public int EditModeHotkey { get; set; } = 0x73;
            // JSON 이름은 예전 그대로 Language 다. 언어 항목이 없던 아주 예전 설정 파일은 한국어로 읽던 그대로 둔다.
            public string? Language { get; set; } = "KOR";
            
            public bool IsAdminMode { get; set; } = false;
            public bool AutoMinimizeOnLaunch { get; set; } = false;
            public double TargetFps { get; set; } = 144.0;
            public bool ShowAmbient { get; set; } = true;
            public bool ShowSpeech { get; set; } = true;
            public bool ShowDanger { get; set; } = true;
            public string ColorAmbient { get; set; } = "#FFFFFFFF";
            public string ColorSpeech { get; set; } = "#FFFFFF00";
            public string ColorDanger { get; set; } = "#FFFF0000";

            public VisualModeSettings WaveMode { get; set; } = new VisualModeSettings();
            public VisualModeSettings PadMode { get; set; } = new VisualModeSettings();
            public VisualModeSettings CircleMode { get; set; } = new VisualModeSettings();
            public VisualModeSettings OutlineMode { get; set; } = new VisualModeSettings();
        }
    }
}


