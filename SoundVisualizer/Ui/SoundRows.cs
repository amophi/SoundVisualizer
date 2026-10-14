// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Ui
{
    // 소리 탭 목록의 줄. Core 의 SoundListItem 하나에 하나이고, 키가 같으면 같은 줄을 고쳐 쓴다(KeyedList.Sync).
    // 줄의 모양은 SoundsTab.xaml 의 DataTemplate 이 형식마다 정한다. 글은 소리 탭이 문자열 표에서 만들어 넣고,
    // 바뀐 값만 알린다. 그래서 종류를 하나 고르면 그 카드와 바꾼 수 줄만 다시 그려진다.

    /// <summary>목록의 줄. <see cref="Key"/> 는 <see cref="SoundListItem.Key"/> 와 같다.</summary>
    public abstract class SoundRow : INotifyPropertyChanged
    {
        protected SoundRow(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>값이 같아도 알린다. 바인딩이 화면의 값을 저장값으로 되돌려 놓게 한다.</summary>
        protected void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>제목·설명·고급 모드 스위치. 글은 XAML 이 문자열 표에 바인딩한다.</summary>
    public sealed class IntroRow : SoundRow
    {
        private bool _isAdvanced;

        public IntroRow(string key) : base(key)
        {
        }

        public bool IsAdvanced
        {
            get => _isAdvanced;
            set => Set(ref _isAdvanced, value);
        }

        /// <summary>묻고 나서 그대로 두기로 한 스위치를 저장값으로 되돌린다.</summary>
        public void RestoreSwitch() => Notify(nameof(IsAdvanced));
    }

    /// <summary>"바꾼 소리: N" 과 모두 되돌리기.</summary>
    public sealed class ChangedRow : SoundRow
    {
        private string _countText = "";
        private bool _canReset;

        public ChangedRow(string key) : base(key)
        {
        }

        public string CountText
        {
            get => _countText;
            set => Set(ref _countText, value);
        }

        /// <summary>바꾼 소리가 없으면 모두 되돌리기를 누를 수 없다.</summary>
        public bool CanReset
        {
            get => _canReset;
            set => Set(ref _canReset, value);
        }
    }

    /// <summary>맞는 소리가 없다. 글은 XAML 이 문자열 표에 바인딩한다.</summary>
    public sealed class EmptyRow : SoundRow
    {
        public EmptyRow(string key) : base(key)
        {
        }
    }

    /// <summary>목록 끝 여백.</summary>
    public sealed class EndRow : SoundRow
    {
        public EndRow(string key) : base(key)
        {
        }
    }

    /// <summary>구역 머리(그 아래 카드들의 기본 종류).</summary>
    public sealed class SectionRow : SoundRow
    {
        private string _title = "";

        public SectionRow(string key, SoundType type) : base(key)
        {
            Type = type;
        }

        /// <summary>점의 색을 정한다. 키에 들어 있어 바뀌지 않는다.</summary>
        public SoundType Type { get; }

        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }
    }

    /// <summary>고급 모드의 묶음 머리: 이름과 소리 수.</summary>
    public sealed class GroupRow : SoundRow
    {
        private string _title = "";
        private string _countText = "";
        private string _automationName = "";

        public GroupRow(string key) : base(key)
        {
        }

        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        public string CountText
        {
            get => _countText;
            set => Set(ref _countText, value);
        }

        /// <summary>화면 읽어주기가 읽는 글("사이렌 묶음, 소리 5개").</summary>
        public string AutomationName
        {
            get => _automationName;
            set => Set(ref _automationName, value);
        }
    }

    /// <summary>
    /// 누르면 종류 목록이 열리는 줄: 기본 모드의 카드(<see cref="SoundListItem.GroupCard"/>)와 고급 모드의 소리 줄
    /// (<see cref="SoundListItem.Sound"/>). 둘은 미리보기 줄이 있는지만 다르다(모바일 GroupCard·SoundTypeRow). 한 모양으로 두어
    /// 목록이 줄을 다시 쓸 때 서식을 바꿔 끼우지 않는다.
    /// </summary>
    public sealed class TypeRow : SoundRow
    {
        private SoundListItem _item;
        private string _title = "";
        private string? _note;
        private string? _preview;
        private SoundType? _dotType;
        private string _typeLabel = "";
        private string _automationName = "";

        public TypeRow(string key, SoundListItem item) : base(key)
        {
            _item = item;
        }

        /// <summary>지금 그리는 항목. 종류를 고를 때 카드의 소리와 거르기에 맞은 소리를 여기서 읽는다.</summary>
        public SoundListItem Item
        {
            get => _item;
            set => _item = value;
        }

        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        /// <summary>이름 밑의 강조색 글씨(바꾼 것). 없으면 null.</summary>
        public string? Note
        {
            get => _note;
            set
            {
                bool had = HasNote;
                Set(ref _note, value);
                if (had != HasNote) Notify(nameof(HasNote));
            }
        }

        public bool HasNote => _note is not null;

        /// <summary>카드 이름 밑의 소리들("A • B • C +5"). 소리 줄과 미리보기가 없는 카드는 null.</summary>
        public string? Preview
        {
            get => _preview;
            set
            {
                bool had = HasPreview;
                Set(ref _preview, value);
                if (had != HasPreview) Notify(nameof(HasPreview));
            }
        }

        public bool HasPreview => _preview is not null;

        /// <summary>종류 단추의 점. 소리마다 종류가 다른 카드는 null 이라 점을 그리지 않는다.</summary>
        public SoundType? DotType
        {
            get => _dotType;
            set => Set(ref _dotType, value);
        }

        /// <summary>종류 단추의 글자(지금 종류, 또는 "일부 다름").</summary>
        public string TypeLabel
        {
            get => _typeLabel;
            set => Set(ref _typeLabel, value);
        }

        /// <summary>화면 읽어주기가 읽는 글(모바일 cd_classify_*).</summary>
        public string AutomationName
        {
            get => _automationName;
            set => Set(ref _automationName, value);
        }
    }
}
