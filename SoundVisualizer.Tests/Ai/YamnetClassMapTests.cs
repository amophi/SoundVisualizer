// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text;
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>클래스 목록 읽기(모바일 YamnetCoarseClassifier.loadClassNames)와 목록·분류기의 입력 확인.</summary>
public class YamnetClassMapTests
{
    private static IReadOnlyList<string> Load(string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return YamnetClassMap.LoadClassNames(stream);
    }

    [Fact]
    public void 머리글을_건너뛰고_따옴표_칸과_두_번_쓴_따옴표를_읽는다()
    {
        var names = Load(
            "index,mid,display_name\n" +
            "0,/m/09x0r,Speech\n" +
            "1,/m/0ytgt,\"Child speech, kid speaking\"\r\n" +
            "2,/m/x,\"Say \"\"hi\"\"\"\n" +
            "3,/m/y,\"\"\"Quoted\"\"\"\n");

        Assert.Equal(YamnetCoarseClassifier.NumClasses, names.Count);
        Assert.Equal("Speech", names[0]);
        Assert.Equal("Child speech, kid speaking", names[1]);
        Assert.Equal("Say \"hi\"", names[2]);
        Assert.Equal("Quoted", names[3]); // 칸을 푼 뒤에도 양끝이 따옴표면 한 번 더 벗긴다.
        Assert.Equal("class_4", names[4]);
        Assert.Equal("class_520", names[520]);
    }

    [Fact]
    public void 범위_밖_번호와_빈_이름은_건너뛰고_칸이_둘이면_두_번째를_이름으로_쓴다()
    {
        var names = Load(
            "521,/m/a,Overflow\n" +
            "-1,/m/b,Negative\n" +
            "not a number,/m/c,Nope\n" +
            "5,/m/d,   \n" +
            "6,Two columns\n" +
            "\"7\",/m/e,  Padded  \n" +
            "8\n" +
            "\n");

        Assert.Equal("class_5", names[5]);
        Assert.Equal("Two columns", names[6]);
        Assert.Equal("Padded", names[7]);
        Assert.Equal("class_8", names[8]);
        Assert.DoesNotContain("Overflow", names);
        Assert.DoesNotContain("Negative", names);
    }

    [Fact]
    public void 받은_스트림은_닫지_않는다()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("0,/m/x,Speech\n"));
        YamnetClassMap.LoadClassNames(stream);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void 이름은_대소문자까지_같아야_번호를_찾는다()
    {
        var map = AiTestData.ClassMap;
        Assert.Equal(421, map.IndexOf("Gunshot, gunfire"));
        Assert.Equal(-1, map.IndexOf("gunshot, gunfire"));
        Assert.Equal(-1, map.IndexOf(null));
        Assert.Equal(SoundType.Danger, map.DefaultCoarse(421));
    }

    [Fact]
    public void 같은_이름이_또_있으면_앞_번호를_찾는다()
    {
        var map = new YamnetClassMap(new[] { "Speech", "Wind", "Speech" });
        Assert.Equal(0, map.IndexOf("Speech"));
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void 분류기는_521개가_아닌_목록과_확률을_받지_않는다()
    {
        Assert.Throws<ArgumentException>(() => new YamnetCoarseClassifier(new YamnetClassMap(new[] { "Speech", "Wind" })));
        var classifier = new YamnetCoarseClassifier(AiTestData.ClassMap);
        Assert.Throws<ArgumentException>(() => classifier.Classify(new float[YamnetCoarseClassifier.NumClasses - 1]));
    }

    [Fact]
    public void 확률이_같으면_앞_번호가_상위에_남는다()
    {
        var classifier = new YamnetCoarseClassifier(AiTestData.ClassMap);
        var probs = new float[YamnetCoarseClassifier.NumClasses];
        Array.Fill(probs, 0.5f);

        var result = classifier.Classify(probs);

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, result.Top5.Select(hit => hit.Index));
    }
}
