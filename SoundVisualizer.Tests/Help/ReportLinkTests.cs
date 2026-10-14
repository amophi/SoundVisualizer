// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Help;

namespace SoundVisualizer.Tests.Help;

/// <summary>제보 링크가 깨지지 않고, 넘긴 값 말고는 아무것도 붙지 않는지 고정한다(안드로이드 ReportLinkTest 를 옮겼다).</summary>
public class ReportLinkTests
{
    private static readonly string Env = ReportLink.EnvironmentBlock(
        appVersion: "1.0.0+a8d9c37",
        windows: "Microsoft Windows 10.0.26200",
        dotnet: ".NET 10.0.0",
        language: "ko");

    [Fact]
    public void 환경_정보는_네_줄이고_넘긴_값만_담는다()
    {
        Assert.Equal(
            new[]
            {
                "App version: 1.0.0+a8d9c37",
                "Windows: Microsoft Windows 10.0.26200",
                ".NET: .NET 10.0.0",
                "App language: ko",
            },
            Env.Split('\n'));
    }

    [Fact]
    public void 이슈_주소는_제목과_본문을_인코딩해_붙인다()
    {
        string url = ReportLink.IssueUrl("앱에서 보낸 제보", "무슨 일이 있었나요?\n\n무엇을 듣고 있었나요?", Env);

        Assert.StartsWith(ReportLink.IssuesUrl + "?title=", url, StringComparison.Ordinal);
        Assert.Contains("&body=", url, StringComparison.Ordinal);
        // 주소에는 날것의 공백이나 줄바꿈이 남으면 안 된다. 남으면 일부 앱이 링크를 잘라 버린다.
        Assert.DoesNotContain(" ", url, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", url, StringComparison.Ordinal);

        Assert.Equal("앱에서 보낸 제보", Title(url));
        Assert.Equal("무슨 일이 있었나요?\n\n무엇을 듣고 있었나요?\n\n" + Env, BodyOf(url));
    }

    [Fact]
    public void 본문_안내가_비어_있으면_환경_정보만_남는다()
    {
        string body = BodyOf(ReportLink.IssueUrl("제보", "", Env));

        Assert.Equal(Env, body);
        Assert.False(body.StartsWith('\n'));
    }

    [Fact]
    public void 메일과_이슈가_같은_본문을_쓴다()
    {
        const string prompt = "무슨 일이 있었나요?";
        string body = ReportLink.Body(prompt, Env);

        Assert.Equal(body, BodyOf(ReportLink.IssueUrl("제보", prompt, Env)));
        Assert.StartsWith(prompt, body, StringComparison.Ordinal);
        Assert.EndsWith(Env, body, StringComparison.Ordinal);
        // 메일은 줄바꿈만 CRLF 다(RFC 6068). 내용은 같다.
        Assert.Equal(body.Replace("\n", "\r\n", StringComparison.Ordinal), ReportLink.MailBody(prompt, Env));
    }

    [Fact]
    public void 메일_주소에_제목과_본문이_들어간다()
    {
        string uri = ReportLink.MailtoUri("team@example.com", "앱에서 보낸 제보", "무슨 일이 있었나요?", Env);

        Assert.StartsWith("mailto:team@example.com?subject=", uri, StringComparison.Ordinal);
        Assert.Contains("&body=", uri, StringComparison.Ordinal);
        // mailto 쿼리에서 + 는 공백이 아니라 글자 그대로다. 공백은 %20 으로 들어가야 한다.
        Assert.DoesNotContain("+", uri, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", uri, StringComparison.Ordinal);
        Assert.Contains("%0D%0A", uri, StringComparison.Ordinal);

        string subject = Uri.UnescapeDataString(Between(uri, "?subject=", "&body="));
        Assert.Equal("앱에서 보낸 제보", subject);
        Assert.Equal("무슨 일이 있었나요?\r\n\r\n" + Env.Replace("\n", "\r\n", StringComparison.Ordinal), BodyOf(uri));
    }

    [Fact]
    public void 쿼리를_나누는_글자도_값_안에서는_인코딩된다()
    {
        // 제목이나 번역문에 &, #, +, = 가 들어가도 다른 쿼리나 조각으로 읽히지 않아야 한다.
        string url = ReportLink.IssueUrl("A&B #1 + C=D?", "x", Env);

        Assert.Equal(2, url.Split('?').Length); // '?' 는 쿼리를 여는 하나뿐이다
        Assert.Equal(2, url.Split('&').Length); // '&' 는 title 과 body 사이의 하나뿐이다
        Assert.DoesNotContain("#", url, StringComparison.Ordinal);
        Assert.Equal("A&B #1 + C=D?", Title(url));
    }

    [Fact]
    public void 빈_값이_와도_줄_수와_순서는_그대로다()
    {
        string empty = ReportLink.EnvironmentBlock("", "", "", "");

        Assert.Equal(4, empty.Split('\n').Length);
        Assert.StartsWith("App version: ", empty, StringComparison.Ordinal);
        Assert.EndsWith("App language: ", empty, StringComparison.Ordinal);
    }

    [Fact]
    public void 제보는_PC_판_저장소와_안드로이드와_같은_메일로_간다()
    {
        Assert.Equal("https://github.com/amophi/SoundVisualizer/issues/new", ReportLink.IssuesUrl);
        Assert.Equal("nawwhod@gmail.com", ReportLink.ReportEmail);
    }

    private static string Title(string url) => Uri.UnescapeDataString(Between(url, "?title=", "&body="));

    private static string BodyOf(string url) => Uri.UnescapeDataString(url[(url.IndexOf("&body=", StringComparison.Ordinal) + "&body=".Length)..]);

    private static string Between(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return text[from..text.IndexOf(end, from, StringComparison.Ordinal)];
    }
}
