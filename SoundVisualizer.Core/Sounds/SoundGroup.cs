// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.Frozen;

namespace SoundVisualizer.Sounds
{
    /// <summary>
    /// 분류 탭의 소리 묶음(#349). 항목 순서가 화면에 보이는 순서다. 모바일 SoundGroup(SoundGroups.kt)과 같다.
    ///
    /// 어느 소리가 어느 묶음인지는 여기가 아니라 묶음 표(Data/sound_groups.tsv)에 둔다. 그 표는 AudioSet 온톨로지(CC BY-SA 4.0)의
    /// 갈래를 읽고 만든 것이라 같은 라이선스로 따로 둔다(Data/NOTICE.txt). 표의 group_id 는 <see cref="SoundGroupInfo.Id"/> 다.
    ///
    /// 묶음이 어느 구역에 보이는지는 여기서 정하지 않는다. 소리마다의 기본 종류(AI 의 매핑)로 그때그때 나눈다
    /// (<see cref="SoundGroups.Sections"/>). 아래 주석의 구역은 지금 매핑에서 그렇다는 뜻이다.
    /// </summary>
    public enum SoundGroup
    {
        // 위협음
        Alarms,
        Sirens,
        Horns,
        VehicleWarnings,
        Breaking,
        Slam,
        Gunshots,
        Explosions,
        Fireworks,
        Thunder,
        Chainsaw,

        // 대화음. 사람의 발성(웃음·고함·울음·환호)이 대화음이 된 뒤(#369) 아기 울음은 외침·비명·울음 바로 뒤에 둔다(#370).
        // 말소리 묶음은 새로 지은 이름을 쓴다(#374). 소리 이름 'Speech' 는 여러 언어에서 구역 머리(대화음)와 같은 낱말이다.
        Talking,
        Distress,
        Baby,
        Crowd,
        Laughter,
        Singing,

        // 환경음. 주의를 끄는 소리(초인종·전화·알람·불·개)부터 둔다.
        Doorbell,
        Phone,
        Signals,
        Fire,
        Dog,
        Voices,
        Footsteps,
        Door,
        Water,
        Kitchen,
        Appliances,
        Tools,
        Vehicle,
        Bell,
        Animal,
        Nature,
        Body,
        Objects,
        Music,
        Background,
    }

    /// <summary>
    /// 묶음마다 정해 둔 것. 모바일 enum 이 들고 있던 id 와 이름(titleRes·titleSound)이다.
    ///
    /// 묶음 이름은 둘 중 하나다. 22개는 이름이 곧 묶음 이름인 소리(사이렌, 초인종 등)의 소리 이름 번역을 그대로 써서 따로 번역하지
    /// 않는다(<see cref="TitleSound"/>). 15개는 새로 지은 이름이고, 언어마다 Data/GroupTitles 에 있다(<see cref="HasOwnTitle"/>).
    /// </summary>
    public static class SoundGroupInfo
    {
        private readonly record struct Info(SoundGroup Group, string Id, string? TitleSound);

        // id 는 모바일 enum 이름의 소문자(VEHICLE_WARNINGS → vehicle_warnings)라 표의 group_id 와 같다.
        private static readonly Info[] Infos =
        {
            new(SoundGroup.Alarms, "alarms", "Alarm"),
            new(SoundGroup.Sirens, "sirens", "Siren"),
            new(SoundGroup.Horns, "horns", null),
            new(SoundGroup.VehicleWarnings, "vehicle_warnings", null),
            new(SoundGroup.Breaking, "breaking", null),
            new(SoundGroup.Slam, "slam", "Slam"),
            new(SoundGroup.Gunshots, "gunshots", "Gunshot, gunfire"),
            new(SoundGroup.Explosions, "explosions", "Explosion"),
            new(SoundGroup.Fireworks, "fireworks", "Fireworks"),
            new(SoundGroup.Thunder, "thunder", "Thunder"),
            new(SoundGroup.Chainsaw, "chainsaw", "Chainsaw"),
            new(SoundGroup.Talking, "talking", null),
            new(SoundGroup.Distress, "distress", null),
            new(SoundGroup.Baby, "baby", "Baby cry, infant cry"),
            new(SoundGroup.Crowd, "crowd", "Crowd"),
            new(SoundGroup.Laughter, "laughter", "Laughter"),
            new(SoundGroup.Singing, "singing", "Singing"),
            new(SoundGroup.Doorbell, "doorbell", "Doorbell"),
            new(SoundGroup.Phone, "phone", "Telephone"),
            new(SoundGroup.Signals, "signals", null),
            new(SoundGroup.Fire, "fire", "Fire"),
            new(SoundGroup.Dog, "dog", "Dog"),
            new(SoundGroup.Voices, "voices", null),
            new(SoundGroup.Footsteps, "footsteps", "Walk, footsteps"),
            new(SoundGroup.Door, "door", "Door"),
            new(SoundGroup.Water, "water", null),
            new(SoundGroup.Kitchen, "kitchen", null),
            new(SoundGroup.Appliances, "appliances", null),
            new(SoundGroup.Tools, "tools", null),
            new(SoundGroup.Vehicle, "vehicle", "Vehicle"),
            new(SoundGroup.Bell, "bell", "Bell"),
            new(SoundGroup.Animal, "animal", "Animal"),
            new(SoundGroup.Nature, "nature", null),
            new(SoundGroup.Body, "body", null),
            new(SoundGroup.Objects, "objects", null),
            new(SoundGroup.Music, "music", "Music"),
            new(SoundGroup.Background, "background", null),
        };

        private static readonly FrozenDictionary<SoundGroup, Info> ByGroup = Infos.ToFrozenDictionary(info => info.Group);

        private static readonly FrozenDictionary<string, SoundGroup> ById =
            Infos.ToFrozenDictionary(info => info.Id, info => info.Group, StringComparer.Ordinal);

        /// <summary>모든 묶음. 화면 순서(항목 순서)다.</summary>
        public static IReadOnlyList<SoundGroup> All { get; } = Array.AsReadOnly(Enum.GetValues<SoundGroup>());

        /// <summary>표(Data/sound_groups.tsv)의 group_id.</summary>
        public static string Id(this SoundGroup group) => ByGroup[group].Id;

        /// <summary>이름(소리 이름)이 그대로 묶음 이름인 소리의 모델 이름. 새로 지은 이름을 쓰는 묶음이면 null.</summary>
        public static string? TitleSound(this SoundGroup group) => ByGroup[group].TitleSound;

        /// <summary>새로 지은 이름(Data/GroupTitles)을 쓰는지. 모바일의 titleRes != 0 이다.</summary>
        public static bool HasOwnTitle(this SoundGroup group) => ByGroup[group].TitleSound is null;

        /// <summary>표의 group_id 로 찾는다. 대소문자까지 같아야 하고, 모르는 이름이면 null.</summary>
        public static SoundGroup? FromId(string? id) =>
            id is not null && ById.TryGetValue(id, out SoundGroup group) ? group : null;
    }
}
