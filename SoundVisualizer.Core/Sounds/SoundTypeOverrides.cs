// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭에서 사용자가 바꾼 소리 종류(설정의 sound_types)를 고치는 계산. 모바일 SettingsManager 의 setSoundTypes·loadSoundTypes 와 같다.
    ///
    /// 맵의 키는 모델의 영어 이름, 값은 <see cref="SoundTypeLabels"/> 의 문자열이다. 기본 종류와 같은 값은 두지 않으므로, 맵에 있으면
    /// 바꾼 소리다. AI 는 같은 맵으로 <see cref="YamnetMappingPolicy.From"/> 을 만든다. 모두 되돌리기는 빈 맵을 저장하는 것이다.
    /// </summary>
    public static class SoundTypeOverrides
    {
        /// <summary>
        /// <paramref name="sounds"/> 를 모두 <paramref name="type"/> 으로 본 새 맵. 소리마다 기본 종류와 같으면 바꾼 것을 지우고, 다르면
        /// 그 라벨을 적는다. 다른 소리의 값은 그대로 둔다. 그래서 기본 종류와 같은 값이 새로 들어가는 일은 없다.
        ///
        /// 기본 모드에서 카드를 고르면 찾는 말·거르기와 상관없이 카드의 소리(<see cref="SoundCard.Members"/>)를 모두 한 번에 넘긴다(#349).
        /// AI 는 맵이 바뀌면 판정을 새로 시작하므로, 소리마다 따로 바꾸면 묶음(음악은 140개)을 바꾸는 동안 반쯤 바뀐 맵을 읽어 거듭 다시
        /// 시작하고 저장도 소리 수만큼 한다. 고급 모드는 소리 하나를 넘긴다.
        ///
        /// 바뀌는 것이 없으면 <paramref name="current"/> 를 그대로(같은 인스턴스) 돌려준다. 그때는 저장하지 않아도 된다.
        /// </summary>
        public static IReadOnlyDictionary<string, string> WithType(
            IReadOnlyDictionary<string, string> current,
            IEnumerable<SoundEntry> sounds,
            SoundType type)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(sounds);
            string label = type.ToLabel();
            Dictionary<string, string>? updated = null;
            foreach (SoundEntry entry in sounds)
            {
                IReadOnlyDictionary<string, string> now = updated ?? current;
                if (type == entry.DefaultType)
                {
                    if (!now.ContainsKey(entry.Name)) continue;
                    updated ??= Copy(current);
                    updated.Remove(entry.Name);
                }
                else
                {
                    if (now.TryGetValue(entry.Name, out string? old) && old == label) continue;
                    updated ??= Copy(current);
                    updated[entry.Name] = label;
                }
            }
            return updated is null ? current : updated.AsReadOnly();
        }

        /// <summary>
        /// 저장값에서 남길 것. 세 라벨이 아닌 값과 지금 기본 종류(AI 의 매핑)와 같은 값은 버린다.
        /// 기본 종류는 AI 의 매핑이 바뀌면 달라질 수 있는데, 그때 사용자가 고른 것과 같아진 소리는 "바꾼 소리" 로 남아 있을 까닭이 없다.
        /// 모르는 이름은 모바일처럼 남긴다(모델의 이름이 바뀐 뒤 등). 세는 쪽(<see cref="SoundCatalog.CountChanged"/>)은 목록에서 세고,
        /// AI 는 <see cref="YamnetMappingPolicy.From"/> 에서 따로 거른다.
        /// </summary>
        public static IReadOnlyDictionary<string, string> Sanitize(IEnumerable<KeyValuePair<string, string>> stored)
        {
            ArgumentNullException.ThrowIfNull(stored);
            var kept = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, label) in stored)
            {
                if (name is null || !SoundTypeLabels.TryParse(label, out SoundType type)) continue;
                if (type == YamnetThreeClassMapper.MapDisplayNameToCoarse(name)) continue;
                kept[name] = label;
            }
            return kept.AsReadOnly();
        }

        private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(source.Count + 8, StringComparer.Ordinal);
            foreach (var (name, label) in source) copy[name] = label;
            return copy;
        }
    }
}
