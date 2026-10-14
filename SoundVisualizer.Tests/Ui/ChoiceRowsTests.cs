// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 선택지 칸(표현 모드 넷, 사운드 모드 셋)의 한 줄 칸 수. 런처는 넓어 한 줄이고, 380px 편집 패널에서 긴 번역은 줄을 나눈다.
/// 안드로이드 판 choicesPerRow 와 같이 마지막 줄에 한 칸만 남는 나눔은 쓰지 않는다.
/// </summary>
public class ChoiceRowsTests
{
    private const double Gap = 8;

    [Fact]
    public void 넓으면_모두_한_줄에_둔다()
    {
        // 런처 설정 카드 안쪽 너비(약 700px)에 네 칸
        Assert.Equal(4, ChoiceRows.PerRow(4, 700, Gap, 120));
        Assert.Equal(3, ChoiceRows.PerRow(3, 700, Gap, 200));
    }

    [Fact]
    public void 가장_넓은_칸이_들어가지_않으면_칸_수를_줄인다()
    {
        // 편집 패널 카드 안쪽 너비 279px: 네 칸이면 칸마다 63.75px
        Assert.Equal(4, ChoiceRows.PerRow(4, 279, Gap, 63));
        Assert.Equal(2, ChoiceRows.PerRow(4, 279, Gap, 64));
    }

    [Fact]
    public void 마지막_줄에_한_칸만_남는_나눔은_건너뛴다()
    {
        // 네 칸은 3+1 을 건너뛰고 2+2, 다섯 칸은 4+1 을 건너뛰고 3+2
        Assert.Equal(2, ChoiceRows.PerRow(4, 300, Gap, 90));
        Assert.Equal(3, ChoiceRows.PerRow(5, 300, Gap, 90));
        // 세 칸의 2+1 은 마지막 줄이 반이라 쓴다
        Assert.Equal(2, ChoiceRows.PerRow(3, 279, Gap, 120));
    }

    [Fact]
    public void 한_칸도_안_들어가면_한_칸씩_두고_칸_안에서_줄을_바꾼다()
    {
        Assert.Equal(1, ChoiceRows.PerRow(4, 279, Gap, 500));
        Assert.Equal(1, ChoiceRows.PerRow(1, 279, Gap, 500));
        Assert.Equal(1, ChoiceRows.PerRow(0, 279, Gap, 10));
    }

    [Fact]
    public void 칸_너비는_간격을_뺀_줄_너비를_나눈_값이다()
    {
        Assert.Equal(63.75, ChoiceRows.CellWidth(279, 4, Gap), 6);
        Assert.Equal(279, ChoiceRows.CellWidth(279, 1, Gap), 6);
        Assert.Equal(0, ChoiceRows.CellWidth(10, 4, Gap), 6);
    }
}
