// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using static SoundVisualizer.Tests.Localization.CSharpLiteralScanner;

namespace SoundVisualizer.Tests.Localization;

/// <summary>소스 지킴이가 기대는 어휘 분석기가 주석과 여러 문자열 꼴을 제대로 가르는지.</summary>
public class CSharpLiteralScannerTests
{
    private static string[] Literals(string code) => Tokenize(code).Where(t => t.Kind == Kind.Literal).Select(t => t.Text).ToArray();

    [Fact]
    public void 주석_안의_글자는_리터럴이_아니다()
    {
        const string code = "// \"주석\"\n/* \"여러 줄\n 주석\" */ var a = \"b\"; /// <summary>\"문서\"</summary>";

        Assert.Equal(new[] { "\"b\"" }, Literals(code));
    }

    [Fact]
    public void 이스케이프와_축자_문자열을_끝까지_읽는다()
    {
        const string code = "var a = \"x\\\"y\"; var b = @\"c:\\dir\"\"q\"\"\"; var c = '\\''; var d = '\"';";

        Assert.Equal(new[] { "\"x\\\"y\"", "@\"c:\\dir\"\"q\"\"\"", "'\\''", "'\"'" }, Literals(code));
    }

    [Fact]
    public void 보간_구멍_안의_문자열까지_한_리터럴로_읽는다()
    {
        const string code = "var a = $\"{(ok ? \"한\" : \"b\")} {{x}} {n:F0}\"; var b = 1;";

        var literals = Literals(code);

        Assert.Single(literals);
        Assert.Contains("한", literals[0]);
        Assert.Contains(Tokenize(code), t => t.Kind == Kind.Word && t.Text == "b");
    }

    [Fact]
    public void 원시_문자열을_닫는_따옴표까지_읽는다()
    {
        const string code = "var a = \"\"\"{ \"Language\": \"ko\" }\"\"\"; var z = 2;";

        Assert.Equal(new[] { "\"\"\"{ \"Language\": \"ko\" }\"\"\"" }, Literals(code));
    }

    [Fact]
    public void 줄_번호를_센다()
    {
        const string code = "var a = 1;\n/* x\ny */\nvar b = \"two\";";

        var literal = Assert.Single(Tokenize(code), t => t.Kind == Kind.Literal);
        Assert.Equal(4, literal.Line);
    }

    [Fact]
    public void 보통_문자열만_내용을_꺼낸다()
    {
        Assert.Equal("launcher.tab.home", PlainContent("\"launcher.tab.home\""));
        Assert.Null(PlainContent("$\"{x}\""));
        Assert.Null(PlainContent("'a'"));
    }
}
