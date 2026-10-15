// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 소리 탭 줄의 글 꼴(모바일 GroupCard·SoundTypeRow 의 description 과 accent 갈래). 문구는 앱의 문자열 표에 있고, 여기서는 어느
/// 꼴을 고르는지만 본다.
/// </summary>
public class SoundRowTextTests
{
    // 전기톱 묶음은 이름이 곧 묶음 이름인 소리 하나뿐이라 미리보기가 없다. 사이렌 묶음은 미리보기가 있다.
    private static readonly SoundEntry Chainsaw = new(0, "Chainsaw", SoundType.Danger, SoundGroup.Chainsaw, 0);
    private static readonly SoundEntry Siren = new(1, "Siren", SoundType.Danger, SoundGroup.Sirens, 1);
    private static readonly SoundEntry Ambulance = new(2, "Ambulance (siren)", SoundType.Danger, SoundGroup.Sirens, 2);
    private static readonly SoundEntry Police = new(3, "Police car (siren)", SoundType.Danger, SoundGroup.Sirens, 3);

    private static readonly SoundCard ChainsawCard = new(SoundGroup.Chainsaw, SoundType.Danger, new[] { Chainsaw }, Chainsaw);
    private static readonly SoundCard SirensCard = new(SoundGroup.Sirens, SoundType.Danger, new[] { Siren, Ambulance, Police }, Siren);

    private static SoundListItem.GroupCard Item(SoundCard card, params (string Name, SoundType Type)[] changed)
    {
        var overrides = changed.ToDictionary(value => value.Name, value => value.Type.ToLabel());
        return new SoundListItem.GroupCard(new ShownCard(card, card.Members), SoundGroups.State(card, overrides), SoundGroups.Preview(card, card.Members));
    }

    [Fact]
    public void 미리보기가_없는_카드는_소리_한_줄처럼_읽는다()
    {
        Assert.Empty(Item(ChainsawCard).Preview.Names);
        Assert.Equal(SoundRowSpeech.Sound, SoundRowText.SpeechOf(Item(ChainsawCard)));
        Assert.Equal(SoundRowSpeech.SoundChanged, SoundRowText.SpeechOf(Item(ChainsawCard, ("Chainsaw", SoundType.Ambient))));
    }

    [Fact]
    public void 묶음_카드는_소리_수와_미리보기를_함께_읽고_바꿨으면_기본_종류도_읽는다()
    {
        Assert.Equal(SoundRowSpeech.Group, SoundRowText.SpeechOf(Item(SirensCard)));
        Assert.Equal(SoundRowSpeech.GroupChanged, SoundRowText.SpeechOf(Item(SirensCard,
            ("Siren", SoundType.Speech), ("Ambulance (siren)", SoundType.Speech), ("Police car (siren)", SoundType.Speech))));
        // 일부만 바꾼 카드는 지금 종류가 "일부 다름" 이라 기본 종류를 함께 읽는다(모바일 current == null).
        Assert.Equal(SoundRowSpeech.GroupChanged, SoundRowText.SpeechOf(Item(SirensCard, ("Police car (siren)", SoundType.Ambient))));
    }

    [Fact]
    public void 이름_밑에는_묶음째_바꾼_것과_일부만_바꾼_것을_적는다()
    {
        Assert.Equal(SoundRowNote.None, SoundRowText.NoteOf(Item(SirensCard)));
        Assert.Equal(SoundRowNote.Mixed, SoundRowText.NoteOf(Item(SirensCard, ("Police car (siren)", SoundType.Ambient))));
        Assert.Equal(SoundRowNote.GroupChanged, SoundRowText.NoteOf(Item(SirensCard,
            ("Siren", SoundType.Ambient), ("Ambulance (siren)", SoundType.Ambient), ("Police car (siren)", SoundType.Ambient))));
        Assert.Equal(SoundRowNote.GroupChanged, SoundRowText.NoteOf(Item(ChainsawCard, ("Chainsaw", SoundType.Speech))));
    }

    [Fact]
    public void 소리_한_줄은_바꿨을_때만_기본_종류를_적고_읽는다()
    {
        var same = new SoundListItem.Sound(Police, SoundType.Danger);
        var changed = new SoundListItem.Sound(Police, SoundType.Ambient);

        Assert.Equal(SoundRowSpeech.Sound, SoundRowText.SpeechOf(same));
        Assert.Equal(SoundRowNote.None, SoundRowText.NoteOf(same));
        Assert.Equal(SoundRowSpeech.SoundChanged, SoundRowText.SpeechOf(changed));
        Assert.Equal(SoundRowNote.SoundChanged, SoundRowText.NoteOf(changed));
    }

    [Fact]
    public void 미리보기의_기호와_나머지_수는_번역하지_않는다()
    {
        Assert.Equal(" • ", SoundRowText.ShownSeparator);
        Assert.Equal(", ", SoundRowText.SpokenSeparator);
        Assert.Equal("+12", SoundRowText.More(12));
    }
}
