using System;
using System.IO;
using System.Text.Json;

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
    }

    public static class AppSettings
    {
        // 모드별 개별 설정 인스턴스
        public static VisualModeSettings WaveMode { get; set; } = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
        public static VisualModeSettings PadMode { get; set; } = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
        public static VisualModeSettings CircleMode { get; set; } = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };
        public static VisualModeSettings OutlineMode { get; set; } = new VisualModeSettings { Intensity = 50.0, PositionSpeed = 20.0, Sensitivity = 3.75, VisualOpacity = 50.0, IsGlowMode = false, GlowIntensity = 0.0, CircleRadius = 40.0 };

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
        public static int StereoUpmixHotkey { get; set; } = 0x71; // F2
        public static int VisualModeHotkey { get; set; } = 0x72;  // F3

        // 7. 현재 언어 설정
        public static string Language { get; set; } = "KOR";

        // 8.5. 관리자 모드
        public static bool IsAdminMode { get; set; } = false;

        // 9. 화면 표시 여부 (환경음, 말소리, 강조음)
        public static bool ShowAmbient { get; set; } = true;
        public static bool ShowSpeech { get; set; } = true;
        public static bool ShowDanger { get; set; } = true;

        // 10. 화면 표시 색상 (환경음, 말소리, 강조음)
        public static string ColorAmbient { get; set; } = "#FFFFFFFF";
        public static string ColorSpeech { get; set; } = "#FFFFFF00";
        public static string ColorDanger { get; set; } = "#FFFF0000";

        private static string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

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
                        StereoUpmixHotkey = data.StereoUpmixHotkey;
                        VisualModeHotkey = data.VisualModeHotkey;
                        Language = data.Language ?? "KOR";
                        
                        IsAdminMode = data.IsAdminMode;
                        ShowAmbient = data.ShowAmbient;
                        ShowSpeech = data.ShowSpeech;
                        ShowDanger = data.ShowDanger;
                        ColorAmbient = data.ColorAmbient ?? "#FFFFFFFF";
                        ColorSpeech = data.ColorSpeech ?? "#FFFFFF00";
                        ColorDanger = data.ColorDanger ?? "#FFFF0000";

                        // 하이브리드 모드 로드 (모드별 설정이 없는 구버전 호환)
                        if (data.WaveMode != null) WaveMode = data.WaveMode;
                        else WaveMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        if (data.PadMode != null) PadMode = data.PadMode;
                        else PadMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        if (data.CircleMode != null) CircleMode = data.CircleMode;
                        else CircleMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };

                        if (data.OutlineMode != null) OutlineMode = data.OutlineMode;
                        else OutlineMode = new VisualModeSettings { Intensity = data.WaveIntensity, PositionSpeed = data.WavePositionSpeed, Sensitivity = data.WaveSensitivity, VisualOpacity = data.VisualOpacity, IsGlowMode = data.IsGlowMode, GlowIntensity = data.GlowIntensity, CircleRadius = data.CircleRadius };
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var data = new SettingsData
                {
                    VisualMode = VisualMode,
                    SoundMode = SoundMode,
                    StereoUpmixHotkey = StereoUpmixHotkey,
                    VisualModeHotkey = VisualModeHotkey,
                    Language = Language,
                    
                    IsAdminMode = IsAdminMode,
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
                    GlowIntensity = GlowIntensity
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
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
            public int StereoUpmixHotkey { get; set; } = 0x71; 
            public int VisualModeHotkey { get; set; } = 0x72;  
            public string Language { get; set; } = "KOR";
            
            public bool IsAdminMode { get; set; } = false;
            public bool ShowAmbient { get; set; } = true;
            public bool ShowSpeech { get; set; } = true;
            public bool ShowDanger { get; set; } = true;
            public string ColorAmbient { get; set; } = "#FFFFFFFF";
            public string ColorSpeech { get; set; } = "#FFFFFF00";
            public string ColorDanger { get; set; } = "#FFFF0000";

            public VisualModeSettings WaveMode { get; set; }
            public VisualModeSettings PadMode { get; set; }
            public VisualModeSettings CircleMode { get; set; }
            public VisualModeSettings OutlineMode { get; set; }
        }
    }
}

