// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections;
using System.Globalization;
using System.Text;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// yamnet_class_map.csv 의 클래스 이름 목록(모델 출력 번호 → 영어 display_name). 사용자 지정 종류도 이 이름을 키로 쓴다.
    ///
    /// 모바일 판은 이름 목록만 들고 추론 때마다 이름을 소문자로 바꿔 키워드를 찾는다(효과음에 가려진 위협음을 찾을 때는 521개 전부).
    /// 여기서는 만들 때 이름마다 기본 종류와 키워드 판정을 한 번 계산해 두고, 추론 중에는 번호로 꺼내 쓴다.
    /// 규칙은 문자열을 받는 함수(<see cref="YamnetThreeClassMapper"/> 등) 한 곳에만 있고 이 표는 그 결과를 담기만 하므로,
    /// 같은 이름이면 결과도 같다.
    /// </summary>
    public sealed class YamnetClassMap : IReadOnlyList<string>
    {
        private readonly string[] _names;
        private readonly SoundType[] _defaultCoarse;
        private readonly YamnetLabelTraits[] _traits;
        private readonly int[] _defaultDangerIndices;
        private readonly Dictionary<string, int> _indexByName;

        /// <summary>이름 목록을 복사해 판정을 미리 계산한다. 분류기는 521개만 받지만 목록 자체는 길이를 따지지 않는다.</summary>
        public YamnetClassMap(IReadOnlyList<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);
            int count = names.Count;
            _names = new string[count];
            _defaultCoarse = new SoundType[count];
            _traits = new YamnetLabelTraits[count];
            _indexByName = new Dictionary<string, int>(count, StringComparer.Ordinal);
            var dangers = new List<int>();
            for (int i = 0; i < count; i++)
            {
                string name = names[i] ?? throw new ArgumentException($"Class name at {i} is null", nameof(names));
                _names[i] = name;
                _defaultCoarse[i] = YamnetThreeClassMapper.MapDisplayNameToCoarse(name);
                _traits[i] = ComputeTraits(name);
                _indexByName.TryAdd(name, i); // 같은 이름이 또 있으면 앞 번호를 남긴다(List.indexOf 와 같다).
                if (_defaultCoarse[i] == SoundType.Danger) dangers.Add(i);
            }
            _defaultDangerIndices = dangers.ToArray();
        }

        public int Count => _names.Length;

        public string this[int index] => _names[index];

        /// <summary>이름과 정확히 같은(대소문자 포함) 클래스의 번호. 없으면 -1.</summary>
        public int IndexOf(string? displayName) =>
            displayName is not null && _indexByName.TryGetValue(displayName, out int index) ? index : -1;

        /// <summary>사용자 지정을 반영하지 않은 기본 종류. <see cref="YamnetThreeClassMapper.MapDisplayNameToCoarse"/> 와 같은 값이다.</summary>
        public SoundType DefaultCoarse(int index) => _defaultCoarse[index];

        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_names).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>파일에서 읽는다. 앱과 테스트는 Models/yamnet_class_map.csv 를 쓴다.</summary>
        public static YamnetClassMap Load(string csvPath)
        {
            using var stream = File.OpenRead(csvPath);
            return Load(stream);
        }

        public static YamnetClassMap Load(Stream csv) => new(LoadClassNames(csv));

        /// <summary>
        /// CSV(index, mid, display_name — 따옴표로 감싼 칸 가능)에서 521개 이름을 읽는다. 모바일 YamnetCoarseClassifier.loadClassNames 와 같다.
        /// 번호가 숫자가 아닌 줄(머리글)과 범위를 벗어난 번호는 건너뛰고, 빠진 번호는 "class_{번호}" 로 남긴다.
        /// 받은 스트림은 닫지 않는다.
        /// </summary>
        public static IReadOnlyList<string> LoadClassNames(Stream csv)
        {
            ArgumentNullException.ThrowIfNull(csv);
            var names = new string[YamnetCoarseClassifier.NumClasses];
            for (int i = 0; i < names.Length; i++) names[i] = "class_" + i.ToString(CultureInfo.InvariantCulture);

            using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var parts = ParseCsvLine(line);
                if (parts.Count == 0) continue;
                if (!int.TryParse(parts[0].Trim().Trim('"'), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index)) continue;
                if (index < 0 || index >= names.Length) continue;

                string name;
                if (parts.Count >= 3) name = parts[2].Trim();
                else if (parts.Count >= 2) name = parts[1].Trim();
                else continue;

                if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
                {
                    name = name[1..^1].Replace("\"\"", "\"");
                }
                if (name.Length > 0) names[index] = name;
            }
            return names;
        }

        /// <summary>yamnet_class_map.csv 에 맞춘 최소한의 RFC 4180 식 분리. 따옴표 안의 쉼표와 두 번 쓴 따옴표("")를 처리한다.</summary>
        internal static List<string> ParseCsvLine(string line)
        {
            var output = new List<string>(3);
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    output.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            output.Add(sb.ToString());
            return output;
        }

        /// <summary>기본 매핑이 위협음인 클래스 번호. 오름차순이라 확률이 같으면 앞 번호가 남는 것까지 모바일의 전체 순회와 같다.</summary>
        internal ReadOnlySpan<int> DefaultDangerIndices => _defaultDangerIndices;

        internal YamnetLabelTraits TraitsAt(int index) => _traits[index];

        /// <summary>
        /// <paramref name="name"/> 의 판정. 그 번호의 이름과 같으면 미리 계산한 값을 쓰고, 다르면(직접 만든 결과 등) 이름으로 다시 계산한다.
        /// 결과 안의 표시 이름은 늘 그 번호의 클래스 이름이라 추론 중에는 계산하는 일이 없다.
        /// </summary>
        internal YamnetLabelTraits TraitsOf(int index, string? name) =>
            (uint)index < (uint)_names.Length && string.Equals(_names[index], name, StringComparison.Ordinal)
                ? _traits[index]
                : ComputeTraits(name);

        /// <summary>목록이 없으면 이름으로 계산한다. 문자열만 받는 공개 함수와 번호를 아는 추론 경로가 같은 구현을 쓰게 한다.</summary>
        internal static YamnetLabelTraits TraitsOf(YamnetClassMap? classes, int index, string? name) =>
            classes is null ? ComputeTraits(name) : classes.TraitsOf(index, name);

        internal static YamnetLabelTraits ComputeTraits(string? name)
        {
            var traits = YamnetLabelTraits.None;
            if (YamnetCoarseClassifier.IsGameMixMaskDisplay(name)) traits |= YamnetLabelTraits.GameMixMask;
            if (YamnetSafetyCueDecision.IsSpeechLikeDisplay(name)) traits |= YamnetLabelTraits.SpeechLikeDisplay;
            if (YamnetSafetyCueDecision.IsSilenceLikeDisplay(name)) traits |= YamnetLabelTraits.SilenceLikeDisplay;
            if (YamnetSafetyCueDecision.IsGunshotKeyword(name)) traits |= YamnetLabelTraits.Gunshot;
            if (YamnetSafetyCueDecision.IsStrongDangerKeyword(name)) traits |= YamnetLabelTraits.StrongDangerCue;
            if (AiPostProcessor.IsCriticalDangerKeyword(name)) traits |= YamnetLabelTraits.CriticalDanger;
            return traits;
        }
    }

    /// <summary>클래스 이름에서 미리 계산해 두는 키워드 판정. 각 값은 같은 이름의 공개 문자열 함수 결과다.</summary>
    [Flags]
    internal enum YamnetLabelTraits : byte
    {
        None = 0,

        /// <summary><see cref="YamnetCoarseClassifier.IsGameMixMaskDisplay"/>: 음악·게임·효과음이라 위협음을 가릴 수 있는 1위.</summary>
        GameMixMask = 1 << 0,

        /// <summary>말소리 계열 표시 이름. 안전 단서 승격과 가려진 위협음 찾기를 막는다.</summary>
        SpeechLikeDisplay = 1 << 1,

        /// <summary>무음 계열 표시 이름. 안전 단서 승격과 가려진 위협음 찾기를 막는다.</summary>
        SilenceLikeDisplay = 1 << 2,

        /// <summary><see cref="YamnetSafetyCueDecision.IsGunshotKeyword"/></summary>
        Gunshot = 1 << 3,

        /// <summary><see cref="YamnetSafetyCueDecision.IsStrongDangerKeyword"/></summary>
        StrongDangerCue = 1 << 4,

        /// <summary><see cref="AiPostProcessor.IsCriticalDangerKeyword"/></summary>
        CriticalDanger = 1 << 5,
    }
}
