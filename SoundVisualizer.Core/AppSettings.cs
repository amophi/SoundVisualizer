// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Frozen;
using System.Collections.Generic;
using SoundVisualizer.Localization;
using SoundVisualizer.Sounds;

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

        // 8.5.1. 개발자 모드의 결과 기록(모바일 developer_record, #193). 개발자 모드를 꺼도 값은 남고, 오버레이가 떠 있고 개발자 모드가
        // 켜져 있을 때만 기록한다(Diagnostics.AiDebugHud). 켜 두고 잊으면 디스크를 먹으므로 기본은 꺼짐이다.
        public static bool DeveloperRecord { get; set; } = false;

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

        // 11. 소리 탭에서 바꾼 소리 종류(모바일 sound_types). 키는 모델의 영어 소리 이름, 값은 SoundTypeLabels 의 문자열이고
        // 기본 종류와 같은 값은 두지 않는다(SoundTypeOverrides). 오버레이의 AI 가 틱마다 다른 스레드에서 읽으므로 고치지 않는 맵을
        // 통째로 바꿔 끼운다. 넣을 때 얼려 두어, 넘긴 쪽이 나중에 맵을 고쳐도 AI 가 반쯤 바뀐 맵을 읽는 일이 없다.
        private static readonly IReadOnlyDictionary<string, string> NoSoundTypes = FrozenDictionary<string, string>.Empty;
        private static IReadOnlyDictionary<string, string> _soundTypes = NoSoundTypes;

        public static IReadOnlyDictionary<string, string> SoundTypes
        {
            get => Volatile.Read(ref _soundTypes);
            set => Volatile.Write(ref _soundTypes, Freeze(value ?? throw new ArgumentNullException(nameof(value))));
        }

        // 12. 소리 탭을 고급 모드(소리를 하나씩 바꾸는 화면)로 볼지(모바일 classify_advanced). 바꾼 종류와 따로 두는 화면 설정이다.
        public static bool ClassifyAdvanced { get; set; } = false;

        // 13. 튜토리얼을 본 적이 있는지(모바일 tutorial_seen). 아직이면 런처를 열 때 튜토리얼부터 띄운다. 튜토리얼을 닫으면(끝까지 넘기거나
        // 건너뛰면) true 가 되고, 그 뒤로는 홈의 "튜토리얼 보기"로만 연다. 저장한 적이 없으면 읽을 때 정한다(Load).
        // 보는 사람에 대한 값이라 전체 초기화(ResetToDefaults)가 건드리지 않는다. 모양 설정을 되돌렸다고 튜토리얼이 다시 뜨면 안 된다.
        public static bool TutorialSeen { get; set; } = false;

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

        /// <summary>지금 표현 모드(<see cref="VisualMode"/>)의 설정. 오버레이 엔진이 프레임마다 읽는다.</summary>
        public static VisualModeSettings CurrentModeSettings => GetCurrentModeSettings();

        private static VisualModeSettings GetCurrentModeSettings()
        {
            if (VisualMode == 0) return WaveMode;
            if (VisualMode == 1) return PadMode;
            if (VisualMode == 2) return CircleMode;
            return OutlineMode;
        }

        public static void Load()
        {
            // 튜토리얼(모바일 loadTutorialSeen): 설정 파일이 없으면 새로 설치한 것이라 아직 보지 않았다. 파일이 있으면 이미 앱을 써 본
            // 사람이라, 아래에서 저장한 값을 읽지 못하면(이 기능 전의 파일, 깨진 파일) 본 것으로 둔다.
            // 모바일은 정한 값을 곧바로 저장한다(기기 표시가 먼저 저장되면 새 설치를 쓰던 사람으로 잘못 친다). PC 는 저장할 때마다 모든
            // 항목을 쓰므로, 무엇을 먼저 저장하든 정한 값이 함께 남는다. 파일이 없는 동안은 늘 새 설치로 읽힌다.
            TutorialSeen = File.Exists(SettingsFilePath);
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
                        DeveloperRecord = data.DeveloperRecord;
                        AutoMinimizeOnLaunch = data.AutoMinimizeOnLaunch;
                        if (data.TargetFps >= 30.0) TargetFps = data.TargetFps;
                        ShowAmbient = data.ShowAmbient;
                        ShowSpeech = data.ShowSpeech;
                        ShowDanger = data.ShowDanger;
                        ColorAmbient = data.ColorAmbient ?? DefaultColorAmbient;
                        ColorSpeech = data.ColorSpeech ?? DefaultColorSpeech;
                        ColorDanger = data.ColorDanger ?? DefaultColorDanger;

                        // 소리 탭이 생기기 전의 설정 파일에는 둘 다 없다. 그때는 바꾼 소리가 없고 기본 모드로 연다.
                        SoundTypes = ReadSoundTypes(data.SoundTypes);
                        ClassifyAdvanced = data.ClassifyAdvanced ?? DefaultClassifyAdvanced(SoundTypes);

                        // 튜토리얼 항목이 없는 파일은 이 기능 전부터 쓰던 사람의 것이다. 이미 앱을 알므로 튜토리얼을 띄우지 않는다.
                        TutorialSeen = data.TutorialSeen ?? true;

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
        ///
        /// 소리 탭에서 바꾼 소리 종류와 고급 모드도 건드리지 않는다(모바일과 같다). 모바일에서 바꾼 종류를 지우는 것은 소리 탭의
        /// 모두 되돌리기뿐이고(바꾼 수를 보이고 한 번 더 묻는다), 고급 모드는 어느 되돌리기도 건드리지 않는 화면 설정이다.
        /// 소리를 하나씩 골라 둔 것을 모양 설정을 되돌리다 함께 잃지 않게 한다.
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
            DeveloperRecord = false;
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
                    DeveloperRecord = DeveloperRecord,
                    AutoMinimizeOnLaunch = AutoMinimizeOnLaunch,
                    TargetFps = TargetFps,
                    ShowAmbient = ShowAmbient,
                    ShowSpeech = ShowSpeech,
                    ShowDanger = ShowDanger,
                    ColorAmbient = ColorAmbient,
                    ColorSpeech = ColorSpeech,
                    ColorDanger = ColorDanger,
                    SoundTypes = WriteSoundTypes(SoundTypes),
                    ClassifyAdvanced = ClassifyAdvanced,
                    TutorialSeen = TutorialSeen,

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

        /// <summary>바꿔 끼울 맵. 이미 얼린 맵(글자 그대로 비교)이면 그대로 쓰고, 아니면 얼린 복사본을 만든다.</summary>
        private static IReadOnlyDictionary<string, string> Freeze(IReadOnlyDictionary<string, string> types)
        {
            if (types.Count == 0) return NoSoundTypes;
            if (types is FrozenDictionary<string, string> frozen && ReferenceEquals(frozen.Comparer, StringComparer.Ordinal)) return frozen;
            return types.ToFrozenDictionary(StringComparer.Ordinal);
        }

        /// <summary>
        /// 저장된 소리 종류(이름 → 라벨 JSON 객체). 문자열이 아닌 값은 버리고, 남은 것은 <see cref="SoundTypeOverrides.Sanitize"/> 로
        /// 거른다(세 라벨이 아닌 값과 지금 기본 종류와 같은 값을 버린다). 객체가 아니면 바꾼 것이 없는 것으로 본다(모바일 loadSoundTypes).
        /// 항목을 JSON 그대로 받는 것은 이 값 하나가 깨졌다고 다른 설정까지 기본값으로 시작하지 않게 하려는 것이다.
        /// </summary>
        private static IReadOnlyDictionary<string, string> ReadSoundTypes(JsonElement? stored)
        {
            if (stored is not { ValueKind: JsonValueKind.Object } json) return NoSoundTypes;
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var property in json.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String) pairs.Add(new(property.Name, property.Value.GetString()!));
            }
            return SoundTypeOverrides.Sanitize(pairs);
        }

        /// <summary>이름 순서로 적는다. 얼린 맵은 순서가 정해져 있지 않아, 그대로 적으면 같은 값도 저장할 때마다 줄이 뒤섞인다.</summary>
        private static JsonElement WriteSoundTypes(IReadOnlyDictionary<string, string> types)
        {
            var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, label) in types) sorted[name] = label;
            return JsonSerializer.SerializeToElement(sorted);
        }

        /// <summary>
        /// 고급 모드를 저장한 적이 없을 때의 값(모바일 loadClassifyAdvanced). 이미 소리를 하나씩 바꿔 둔 사람이면 고급 모드로 연다.
        /// 기본 모드에서 묶음을 고르면 하나씩 바꾼 것이 먼저 묻지 않고 덮어써질 수 있어서다. 기본 모드로 돌아갈 때는 탭이 먼저 알린다.
        /// 모바일은 정한 값을 곧바로 저장하지만 PC 는 저장할 때마다 모든 항목을 쓰므로, 다음 저장에서 함께 남는다.
        /// </summary>
        private static bool DefaultClassifyAdvanced(IReadOnlyDictionary<string, string> soundTypes) => soundTypes.Count > 0;

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
            // 결과 기록이 생기기 전의 설정 파일에는 없어 꺼진 채로 읽힌다.
            public bool DeveloperRecord { get; set; } = false;
            public bool AutoMinimizeOnLaunch { get; set; } = false;
            public double TargetFps { get; set; } = 144.0;
            public bool ShowAmbient { get; set; } = true;
            public bool ShowSpeech { get; set; } = true;
            public bool ShowDanger { get; set; } = true;
            public string ColorAmbient { get; set; } = "#FFFFFFFF";
            public string ColorSpeech { get; set; } = "#FFFFFF00";
            public string ColorDanger { get; set; } = "#FFFF0000";

            // 소리 탭. 예전 설정 파일에는 없어 null 로 읽힌다. 소리 종류는 JSON 그대로 받아 ReadSoundTypes 가 거른다.
            public JsonElement? SoundTypes { get; set; }
            public bool? ClassifyAdvanced { get; set; }

            // 튜토리얼을 본 적이 있는지. 예전 설정 파일에는 없어 null 로 읽힌다(그때는 본 것으로 친다).
            public bool? TutorialSeen { get; set; }

            public VisualModeSettings WaveMode { get; set; } = new VisualModeSettings();
            public VisualModeSettings PadMode { get; set; } = new VisualModeSettings();
            public VisualModeSettings CircleMode { get; set; } = new VisualModeSettings();
            public VisualModeSettings OutlineMode { get; set; } = new VisualModeSettings();
        }
    }
}


