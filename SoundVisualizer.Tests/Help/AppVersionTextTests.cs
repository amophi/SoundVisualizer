// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Help;

namespace SoundVisualizer.Tests.Help;

/// <summary>앱 정보와 제보에 적는 버전. .NET SDK 가 붙이는 커밋(+ 뒤)을 앱 정보에서는 떼고, 제보에서는 7자로 줄인다.</summary>
public class AppVersionTextTests
{
    private const string Built = "1.0.0+a8d9c377c4a44972738beae1cea166def54bc7a5";

    [Fact]
    public void 앱_정보에는_빌드_정보를_떼고_보인다()
    {
        Assert.Equal("1.0.0", AppVersionText.Display(Built));
        Assert.Equal("1.2.3-beta", AppVersionText.Display("1.2.3-beta+abc"));
        Assert.Equal("1.0.0", AppVersionText.Display("1.0.0"));
    }

    [Fact]
    public void 제보에는_커밋을_7자로_남긴다()
    {
        Assert.Equal("1.0.0+a8d9c37", AppVersionText.ForReport(Built));
        Assert.Equal("1.0.0+abc", AppVersionText.ForReport("1.0.0+abc"));
        Assert.Equal("1.0.0", AppVersionText.ForReport("1.0.0"));
        Assert.Equal("1.0.0", AppVersionText.ForReport("1.0.0+"));
    }

    [Fact]
    public void 값이_없으면_빈_문자열이다()
    {
        Assert.Equal("", AppVersionText.Display(null));
        Assert.Equal("", AppVersionText.ForReport(null));
        Assert.Equal("", AppVersionText.Display("  "));
    }
}
