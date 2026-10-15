// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// 한 언어의 화면 문자열 표. 만든 뒤에는 바뀌지 않으므로 여러 스레드가 잠금 없이 읽는다.
    ///
    /// 파일은 평평한 JSON 객체 하나다(<c>{ "launcher.tab.home": "홈" }</c>). 번역 도구가 그대로 열 수 있게 중첩과 주석은 쓰지 않고,
    /// 키는 화면별로 점을 찍어 묶는다. 값이 문자열이 아니거나 같은 키가 두 번 나오면 읽지 않는다(뒤엣것이 조용히 이기면 번역이 사라진다).
    /// </summary>
    public sealed class StringTable
    {
        private readonly FrozenDictionary<string, string> _entries;

        private StringTable(string code, FrozenDictionary<string, string> entries)
        {
            Code = code;
            _entries = entries;
        }

        /// <summary>표의 언어 코드(<see cref="UiLanguage.Code"/>).</summary>
        public string Code { get; }

        /// <summary>키와 문자열. 읽기 전용이다.</summary>
        public IReadOnlyDictionary<string, string> Entries => _entries;

        public bool TryGet(string key, [MaybeNullWhen(false)] out string value) => _entries.TryGetValue(key, out value);

        /// <summary>아직 번역 파일이 없는 언어의 표. 모든 키가 영어로 넘어간다.</summary>
        public static StringTable Empty(string code) => new(code, FrozenDictionary<string, string>.Empty);

        /// <summary>JSON 을 읽는다. 형식이 맞지 않으면 <see cref="FormatException"/> 을 던진다.</summary>
        public static StringTable Parse(string code, string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"{code}: 문자열 표가 올바른 JSON 이 아닙니다({ex.Message}).", ex);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new FormatException($"{code}: 문자열 표는 JSON 객체 하나여야 합니다.");

                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                        throw new FormatException($"{code}: '{property.Name}' 의 값이 문자열이 아닙니다.");
                    if (!entries.TryAdd(property.Name, property.Value.GetString()!))
                        throw new FormatException($"{code}: '{property.Name}' 키가 두 번 나옵니다.");
                }
                return new StringTable(code, entries.ToFrozenDictionary(StringComparer.Ordinal));
            }
        }
    }
}
