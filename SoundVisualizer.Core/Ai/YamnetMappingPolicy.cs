// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 사용자가 분류 탭에서 바꾼 소리 종류의 불변 스냅샷. 추론 한 번의 모든 단계(투표, 가려진 위협음, 안전 단서, 치명적 단서)가
    /// 같은 스냅샷을 본다. 모바일 YamnetMappingPolicy.kt 와 같다.
    ///
    /// 위협음에서 뺀 라벨은 투표뿐 아니라 키워드로 다시 들어오는 길(가려진 위협음, 안전 단서, 치명적 단서와 그에 따른 기준 완화)에서도
    /// 빠진다. 새로 위협음으로 지정한 라벨은 투표에만 들어간다.
    /// </summary>
    public sealed class YamnetMappingPolicy
    {
        /// <summary>사용자 지정이 없는 기본 매핑.</summary>
        public static YamnetMappingPolicy Default { get; } = new(FrozenDictionary<string, SoundType>.Empty);

        private readonly FrozenDictionary<string, SoundType> _overrides;

        private YamnetMappingPolicy(FrozenDictionary<string, SoundType> overrides)
        {
            _overrides = overrides;
            // 해시가 아니라 내용 그대로다. 평가 CSV 에 남긴 값만으로 그때의 매핑을 되살릴 수 있다.
            // 순서는 모바일의 toSortedMap 과 같은 서수(UTF-16) 순이다. 문화권 정렬을 쓰면 대소문자가 섞인 이름에서 어긋난다.
            Signature = overrides.Count == 0
                ? "default"
                : string.Join(";", overrides
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => entry.Key + "=" + entry.Value.ToLabel()));
        }

        /// <summary>기본 종류와 다른, 유효한 사용자 지정 수.</summary>
        public int OverrideCount => _overrides.Count;

        /// <summary>"default", 또는 영어 이름 순으로 "이름=종류;이름=종류". 개발자 모드 기록과 매핑이 바뀌었는지 판단하는 데 쓴다.</summary>
        public string Signature { get; }

        /// <summary>이 매핑에서 <paramref name="name"/> 의 종류. 사용자 지정이 없으면 기본 종류다.</summary>
        public SoundType Coarse(string name) =>
            _overrides.TryGetValue(name, out var coarse) ? coarse : YamnetThreeClassMapper.MapDisplayNameToCoarse(name);

        /// <summary>추론 경로용. 기본 종류를 이름에서 다시 구하지 않고 미리 계산한 값을 쓴다.</summary>
        internal SoundType CoarseAt(YamnetClassMap classes, int index) =>
            _overrides.TryGetValue(classes[index], out var coarse) ? coarse : classes.DefaultCoarse(index);

        /// <summary>
        /// 이 라벨을 키워드 기반 안전 단서로 써도 되는지. 위협음에서 뺀 라벨은 키워드로 다시 들어오지 못한다.
        /// 사용자 지정이 위협음인 라벨은 true 지만, 그런 라벨이 단서 키워드에 걸리지 않으면 결국 투표에만 쓰인다.
        /// </summary>
        public bool AllowsSafetyCue(string name) =>
            !_overrides.TryGetValue(name, out var coarse) || coarse == SoundType.Danger;

        /// <summary>표시 이름이나 상위 5개 중 하나가, 위협음에서 빼지 않은 치명적 단서(총기, 폭발, 폭죽)인지.</summary>
        public bool HasCriticalDangerCue(string display, IReadOnlyList<YamnetCoarseClassifier.TopClassHit> top5) =>
            HasCriticalDangerCue(null, -1, display, top5);

        /// <summary>추론 경로용. 같은 판정을 미리 계산한 값으로 한다(<paramref name="classes"/> 가 null 이면 이름으로 계산한다).</summary>
        internal bool HasCriticalDangerCue(
            YamnetClassMap? classes,
            int displayIndex,
            string display,
            IReadOnlyList<YamnetCoarseClassifier.TopClassHit> top5)
        {
            if (AllowsSafetyCue(display) &&
                (YamnetClassMap.TraitsOf(classes, displayIndex, display) & YamnetLabelTraits.CriticalDanger) != 0)
            {
                return true;
            }
            for (int i = 0; i < top5.Count; i++)
            {
                var hit = top5[i];
                if (AllowsSafetyCue(hit.Name) &&
                    (YamnetClassMap.TraitsOf(classes, hit.Index, hit.Name) & YamnetLabelTraits.CriticalDanger) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 저장된 사용자 지정(영어 클래스 이름 → "ambient"/"speech"/"danger")에서 스냅샷을 만든다. 입력은 복사하므로 나중에 바뀌어도 상관없다.
        /// 목록에 정확히(대소문자 포함) 있는 이름, 세 문자열 중 하나인 값, 기본 종류와 다른 값만 남긴다. 남는 것이 없으면 <see cref="Default"/> 다.
        /// </summary>
        public static YamnetMappingPolicy From(IReadOnlyDictionary<string, string> overrides, IReadOnlyList<string> classNames)
        {
            ArgumentNullException.ThrowIfNull(overrides);
            ArgumentNullException.ThrowIfNull(classNames);
            var names = new HashSet<string>(classNames, StringComparer.Ordinal);
            var valid = new Dictionary<string, SoundType>(StringComparer.Ordinal);
            foreach (var (name, label) in overrides)
            {
                if (name is null || !names.Contains(name)) continue;
                if (!SoundTypeLabels.TryParse(label, out var coarse)) continue;
                if (coarse == YamnetThreeClassMapper.MapDisplayNameToCoarse(name)) continue;
                valid[name] = coarse;
            }
            return valid.Count == 0 ? Default : new YamnetMappingPolicy(valid.ToFrozenDictionary(StringComparer.Ordinal));
        }
    }
}
