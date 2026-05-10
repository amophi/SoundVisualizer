using System;
using System.IO;
using System.Text.Json;

namespace SoundVisualizer
{
    public static class AppSettings
    {
        public const double WaveIntensityMax = 50.0;
        // 1. 파도의 크기 (최대 진폭 스케일 0~50)
        public static double WaveIntensity { get; set; } = 50.0;
        // 2. 파도 위치 변화 속도 (화면상 파도의 위치 변화 속도)
        public static double WavePositionSpeed { get; set; } = 20.0;
        // 3. 파도의 민감성/떨림 (소리 크기가 변할 때 얼마나 즉각적으로 출렁이는지)
        public static double WaveSensitivity { get; set; } = 3.75; // UI에서 15로 보이도록 설정 (15 / 4)
        // 4. 파도/그래픽 투명도 (0 ~ 100)
        public static double VisualOpacity { get; set; } = 50.0;
        // 5. 시각화 모드 (0 = Wave 모드, 1 = Pad 모드)
        public static int VisualMode { get; set; } = 0;
        // 6. 스테레오 확장 모드 (2채널 소스를 좌/우 전용으로 표시할지 여부)
        public static bool IsStereoUpmixMode { get; set; } = false;
        // 7. 단축키 설정
        public static int StereoUpmixHotkey { get; set; } = 0x71; // F2
        public static int VisualModeHotkey { get; set; } = 0x72;  // F3
        // 7. 현재 언어 설정
        public static string Language { get; set; } = "KOR";
        // 8. 설정 모드 (일반/고급)
        public static bool IsAdvancedSensitivity { get; set; } = false;
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
                        WaveIntensity = ClampWaveIntensity(data.WaveIntensity);
                        WavePositionSpeed = data.WavePositionSpeed;
                        WaveSensitivity = data.WaveSensitivity;
                        VisualOpacity = data.VisualOpacity;
                        VisualMode = data.VisualMode;
                        IsStereoUpmixMode = data.IsStereoUpmixMode;
                        StereoUpmixHotkey = data.StereoUpmixHotkey;
                        VisualModeHotkey = data.VisualModeHotkey;
                        Language = data.Language ?? "KOR";
                        IsAdvancedSensitivity = data.IsAdvancedSensitivity;
                        IsAdminMode = data.IsAdminMode;
                        ShowAmbient = data.ShowAmbient;
                        ShowSpeech = data.ShowSpeech;
                        ShowDanger = data.ShowDanger;
                        ColorAmbient = data.ColorAmbient ?? "#FFFFFFFF";
                        ColorSpeech = data.ColorSpeech ?? "#FFFFFF00";
                        ColorDanger = data.ColorDanger ?? "#FFFF0000";
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
                    WaveIntensity = ClampWaveIntensity(WaveIntensity),
                    WavePositionSpeed = WavePositionSpeed,
                    WaveSensitivity = WaveSensitivity,
                    VisualOpacity = VisualOpacity,
                    VisualMode = VisualMode,
                    IsStereoUpmixMode = IsStereoUpmixMode,
                    StereoUpmixHotkey = StereoUpmixHotkey,
                    VisualModeHotkey = VisualModeHotkey,
                    Language = Language,
                    IsAdvancedSensitivity = IsAdvancedSensitivity,
                    IsAdminMode = IsAdminMode,
                    ShowAmbient = ShowAmbient,
                    ShowSpeech = ShowSpeech,
                    ShowDanger = ShowDanger,
                    ColorAmbient = ColorAmbient,
                    ColorSpeech = ColorSpeech,
                    ColorDanger = ColorDanger
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
            public bool IsStereoUpmixMode { get; set; } = false;
            public int StereoUpmixHotkey { get; set; } = 0x71; 
            public int VisualModeHotkey { get; set; } = 0x72;  
            public string Language { get; set; } = "KOR";
            public bool IsAdvancedSensitivity { get; set; } = false;
            public bool IsAdminMode { get; set; } = false;
            public bool ShowAmbient { get; set; } = true;
            public bool ShowSpeech { get; set; } = true;
            public bool ShowDanger { get; set; } = true;
            public string ColorAmbient { get; set; } = "#FFFFFFFF";
            public string ColorSpeech { get; set; } = "#FFFFFF00";
            public string ColorDanger { get; set; } = "#FFFF0000";
        }

        private static double ClampWaveIntensity(double value)
            => Math.Max(0.0, Math.Min(WaveIntensityMax, value));
    }
}
