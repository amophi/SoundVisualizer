// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// WPF 화면이 문자열을 얻는 곳. XAML 은 <see cref="LocExtension"/>(<c>{l:Loc 키}</c>)으로 이 객체의 인덱서에 바인딩하고,
    /// 코드는 <see cref="Get"/>·<see cref="Format"/> 을 부른다. 언어를 바꾸면 인덱서 전체(<c>Item[]</c>)가 바뀌었다고 알려
    /// 열린 창의 글자가 한꺼번에 바뀐다. 코드에서 직접 넣은 글자(상태 문구, 단축키 이름 등)는 각 창이
    /// <see cref="LanguageChanged"/> 를 받아 다시 넣는다.
    /// </summary>
    public sealed class Loc : INotifyPropertyChanged
    {
        public static Loc Instance { get; } = new();

        private readonly Localizer _localizer = new();

        private Loc()
        {
        }

        public string this[string key] => _localizer.Get(key);

        /// <summary>지금 화면 언어의 코드("system" 을 푼 결과).</summary>
        public string Code => _localizer.Code;

        public bool IsRightToLeft => _localizer.IsRightToLeft;

        /// <summary>아랍어처럼 오른쪽에서 왼쪽으로 쓰는 언어면 RightToLeft. 런처·색 선택 창·오버레이 편집 패널이 바인딩한다.</summary>
        public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>언어를 바꾼 뒤(바인딩이 새 글자를 받은 뒤) 알린다.</summary>
        public event EventHandler? LanguageChanged;

        public static string Get(string key) => Instance._localizer.Get(key);

        /// <summary>문자열의 {0}, {1} 자리를 채운다. 숫자는 InvariantCulture 로 적힌다.</summary>
        public static string Format(string key, params object?[] args) => Instance._localizer.Format(key, args);

        /// <summary>코드에서 만드는 요소에 붙일 바인딩. XAML 의 <c>{l:Loc 키}</c> 와 같다.</summary>
        public static Binding BindingFor(string key) => new($"[{key}]") { Source = Instance, Mode = BindingMode.OneWay };

        /// <summary>설정값("system" 또는 언어 코드)을 화면 언어로 풀어 적용한다. 앱을 시작할 때 부른다.</summary>
        public static void Apply(string? setting) => Instance.SetLanguage(UiLanguages.Resolve(setting, CultureInfo.CurrentUICulture));

        /// <summary>사용자가 언어 콤보에서 고른 값을 저장하고 열린 창 모두에 바로 적용한다.</summary>
        public static void Choose(string setting)
        {
            AppSettings.Language = UiLanguages.NormalizeSetting(setting);
            AppSettings.Save();
            Apply(AppSettings.Language);
        }

        private void SetLanguage(string code)
        {
            _localizer.SetLanguage(code);
            AppFonts.Apply(_localizer.Code);

            // 같은 언어로 풀려도(시스템 기본값 ↔ 그 언어) 알린다. 두 창의 언어 콤보가 고른 값을 서로 맞춰야 한다.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
