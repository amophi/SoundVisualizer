// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Concurrent;
using System.Globalization;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// 화면 문자열을 지금 언어의 표에서 찾는다. 그 언어 표에 없는 키는 영어 표에서 찾고, 영어에도 없으면 키를 그대로 돌려준다
    /// (빈칸보다 키가 보여야 빠진 문자열을 바로 알아챈다).
    ///
    /// 언어를 바꿀 때는 (코드, 그 언어 표, 영어 표) 묶음을 새로 만들어 참조 하나로 바꿔 끼운다. 그래서 다른 스레드(캡처 스레드의
    /// 오류 문구 등)가 읽는 도중에 언어가 바뀌어도 두 언어가 섞인 결과를 보지 않는다.
    /// </summary>
    public sealed class Localizer
    {
        private sealed record Snapshot(string Code, StringTable Current, StringTable Fallback);

        private readonly Func<string, StringTable?> _tables;
        private readonly ConcurrentDictionary<string, byte> _reportedMissing = new(StringComparer.Ordinal);
        private Snapshot _snapshot;

        /// <summary>Core 에 넣어 둔 표를 쓴다. 영어로 시작한다.</summary>
        public Localizer() : this(StringTables.Get)
        {
        }

        /// <summary>표를 주는 함수를 바꿔 끼운다(테스트용).</summary>
        internal Localizer(Func<string, StringTable?> tables)
        {
            _tables = tables;
            _snapshot = Build(UiLanguages.DefaultCode);
        }

        /// <summary>지금 화면 언어의 코드. 지원하지 않는 코드를 받았으면 영어다.</summary>
        public string Code => Volatile.Read(ref _snapshot).Code;

        public bool IsRightToLeft => UiLanguages.IsRightToLeft(Code);

        /// <summary>화면 언어를 바꾼다. "system" 같은 설정값이 아니라 <see cref="UiLanguages.Resolve"/> 로 푼 코드를 받는다.</summary>
        public void SetLanguage(string code) => Volatile.Write(ref _snapshot, Build(code));

        public string Get(string key) => Lookup(Volatile.Read(ref _snapshot), key);

        /// <summary>
        /// 문자열의 {0}, {1} 자리에 값을 넣는다. 숫자는 InvariantCulture 로 적는다(FPS, % 같은 기술 표기가 Windows 지역 설정을 따라
        /// 바뀌지 않게). 번역문의 자리표시자가 깨졌으면 영어 문장으로 채운다. 테스트가 막는 경우지만 화면이 멈추지는 않게 한다.
        /// </summary>
        public string Format(string key, params object?[] args)
        {
            var snapshot = Volatile.Read(ref _snapshot);
            string template = Lookup(snapshot, key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException ex)
            {
                AppLog.Warn("l10n", $"{snapshot.Code} 문자열 '{key}' 의 자리표시자가 맞지 않아 영어로 보여 줍니다.", ex);
                if (snapshot.Fallback.TryGet(key, out var english))
                {
                    try
                    {
                        return string.Format(CultureInfo.InvariantCulture, english, args);
                    }
                    catch (FormatException)
                    {
                    }
                }
                return template;
            }
        }

        private string Lookup(Snapshot snapshot, string key)
        {
            if (snapshot.Current.TryGet(key, out var text)) return text;
            if (snapshot.Fallback.TryGet(key, out text)) return text;

            // 화면 갱신마다 부르는 곳이 있어, 같은 키는 한 번만 남긴다.
            if (_reportedMissing.TryAdd(key, 0)) AppLog.Warn("l10n", $"문자열 키 '{key}' 가 영어 표에도 없습니다.");
            return key;
        }

        private Snapshot Build(string code)
        {
            string resolved = UiLanguages.Find(code)?.Code ?? UiLanguages.DefaultCode;
            var fallback = Load(UiLanguages.DefaultCode) ?? StringTable.Empty(UiLanguages.DefaultCode);
            var current = resolved == UiLanguages.DefaultCode ? fallback : Load(resolved) ?? StringTable.Empty(resolved);
            return new Snapshot(resolved, current, fallback);
        }

        private StringTable? Load(string code)
        {
            try
            {
                return _tables(code);
            }
            catch (FormatException ex)
            {
                // 깨진 번역 파일 하나 때문에 앱이 뜨지 않으면 안 된다. 그 언어는 영어로 보인다.
                AppLog.Warn("l10n", $"{code} 문자열 표를 읽지 못해 영어로 보여 줍니다.", ex);
                return null;
            }
        }
    }
}
