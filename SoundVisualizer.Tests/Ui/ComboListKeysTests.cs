// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 언어 콤보의 키 규칙. 닫혀 있을 때 화살표나 글자 키를 누르면 언어가 바로 바뀌던 결함(키마다 앱 전체의 글꼴·방향·글자가 바뀜)이
/// 다시 생기지 않게, 닫힌 콤보에서 값을 바꾸던 키가 모두 목록 열기로 바뀌는지 지킨다.
/// </summary>
public class ComboListKeysTests
{
    public static TheoryData<ComboKey> MovingKeys() => new()
    {
        ComboKey.Up, ComboKey.Down, ComboKey.Left, ComboKey.Right, ComboKey.Home, ComboKey.End, ComboKey.PageUp, ComboKey.PageDown,
    };

    [Theory]
    [MemberData(nameof(MovingKeys))]
    public void 닫힌_콤보의_이동_키는_값을_바꾸지_않고_목록을_연다(ComboKey key)
    {
        Assert.Equal(ComboKeyAction.OpenList, ComboListKeys.OnKeyDown(key, alt: false, listOpen: false));
    }

    [Theory]
    [InlineData(ComboKey.Enter)]
    [InlineData(ComboKey.Space)]
    public void 닫힌_콤보의_Enter_와_스페이스도_목록을_연다(ComboKey key)
    {
        Assert.Equal(ComboKeyAction.OpenList, ComboListKeys.OnKeyDown(key, alt: false, listOpen: false));
    }

    [Theory]
    [MemberData(nameof(MovingKeys))]
    public void 열린_목록에서는_콤보가_하던_대로_항목만_옮긴다(ComboKey key)
    {
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(key, alt: false, listOpen: true));
    }

    [Fact]
    public void 열린_목록의_Enter_는_콤보가_고르고_닫는다()
    {
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(ComboKey.Enter, alt: false, listOpen: true));
    }

    [Fact]
    public void 열린_목록의_Tab_은_고르지_않고_닫은_뒤_다음_칸으로_간다()
    {
        Assert.Equal(ComboKeyAction.CloseAndMoveFocus, ComboListKeys.OnKeyDown(ComboKey.Tab, alt: false, listOpen: true));
        // 닫혀 있으면 Tab 은 그냥 다음 칸으로 간다.
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(ComboKey.Tab, alt: false, listOpen: false));
    }

    [Fact]
    public void Alt_와_함께_누른_키는_콤보가_열고_닫게_둔다()
    {
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(ComboKey.Down, alt: true, listOpen: false));
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(ComboKey.Up, alt: true, listOpen: true));
    }

    [Fact]
    public void 그_밖의_키는_건드리지_않는다()
    {
        // F4 는 WPF 가 목록을 열고 닫고, Esc 는 열린 목록을 고르지 않고 닫는다.
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(ComboKey.Other, alt: false, listOpen: false));
        Assert.Equal(ComboKeyAction.Default, ComboListKeys.OnKeyDown(ComboKey.Other, alt: false, listOpen: true));
    }

    [Fact]
    public void 닫힌_콤보에_친_글자는_목록을_연다()
    {
        Assert.True(ComboListKeys.TextOpensList("d", listOpen: false));
        Assert.False(ComboListKeys.TextOpensList("d", listOpen: true));
        Assert.False(ComboListKeys.TextOpensList("", listOpen: false));
        Assert.False(ComboListKeys.TextOpensList(null, listOpen: false));
    }
}
