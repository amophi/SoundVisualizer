// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 오버레이 편집 패널의 코드 숨김(MainWindow.xaml.cs)을 읽어 확인한다. MainWindow 는 화면 전체를 덮는 WPF 창이라 단위 시험에서 띄우지 않는다.
/// </summary>
public class EditPanelSourceTests
{
    [Fact]
    public void 편집_패널에서_모드_칸을_바꾸면_슬라이더를_새_모드의_값으로_다시_채운다()
    {
        // 크기·진하기·속도 같은 값은 모드마다 따로 저장된다(AppSettings 의 모드별 접근자). 모드를 바꾼 뒤 다시 채우지 않으면
        // 슬라이더가 이전 모드의 값을 보이고, 그 상태로 움직이면 새 모드에 엉뚱한 값이 들어간다. F3 단축키로 바꿀 때는 이미 다시 채운다.
        string body = MethodBody("EditPanelVisualMode_SelectionChanged");

        Assert.Contains("LoadSettingsToEditPanel();", body);
    }

    private static string MethodBody(string name)
    {
        string source = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "SoundVisualizer", "MainWindow.xaml.cs"));
        int start = source.IndexOf("void " + name + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, name + " 를 찾지 못했습니다.");

        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
        }
        throw new InvalidOperationException(name + " 의 끝을 찾지 못했습니다.");
    }
}
