namespace SoundVisualizer
{
    public static class AppSettings
    {
        // 1. 파도의 크기 (최대 진폭 스케일 0~100)
        public static double WaveIntensity { get; set; } = 33.3;
        // 2. 파도 위치 변화 속도 (화면상 파도의 위치 변화 속도)
        public static double WavePositionSpeed { get; set; } = 10.0;
        // 3. 파도의 민감성/떨림 (소리 크기가 변할 때 얼마나 즉각적으로 출렁이는지)
        public static double WaveSensitivity { get; set; } = 10.0;
        // 4. 파도/그래픽 투명도 (0 ~ 100)
        public static double VisualOpacity { get; set; } = 60.0;
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
    }
}
