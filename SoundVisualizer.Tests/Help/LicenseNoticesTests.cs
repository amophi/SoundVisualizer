// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.RegularExpressions;
using SoundVisualizer.Help;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tests.Help;

/// <summary>
/// 도움말의 오픈소스 라이선스 창. 안드로이드 LicenseAssetsTest·LicenseRulersTest 를 옮겼다.
/// 고지는 저장소 원본을 제자리에서 넣으므로 복사본과 맞춰 보는 대신, 넣은 리소스가 그 원본인지와 순서를 본다.
/// </summary>
public class LicenseNoticesTests
{
    private static readonly string Ruler80 = new('=', 80);

    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();
        foreach (var code in StringTables.AvailableCodes) data.Add(code);
        return data;
    }

    [Fact]
    public void 넣어_둔_고지는_저장소_원본과_같다()
    {
        Assert.All(LicenseNotices.Documents, document =>
        {
            string path = Path.Combine(RepositoryPaths.Root, document.Path);
            Assert.True(File.Exists(path), "원본이 없습니다: " + path);
            Assert.Equal(Lf(File.ReadAllText(path)), Lf(LicenseNotices.Read(document)));
        });
    }

    [Fact]
    public void 고지는_목록_글꼴_소리_이름_앱_라이선스_순서다()
    {
        Assert.Equal(
            new[] { "THIRD_PARTY.md", "SoundVisualizer/Fonts/Pretendard-LICENSE.txt", "SoundVisualizer.Core/Sounds/Data/NOTICE.txt", "LICENSE" },
            LicenseNotices.Documents.Select(d => d.Path));
    }

    [Fact]
    public void 창에_보일_전문은_고지마다_머리를_달고_순서대로_잇는다()
    {
        string all = LicenseNotices.ReadAll();

        Assert.DoesNotContain("\r", all, StringComparison.Ordinal);
        int previous = -1;
        foreach (var document in LicenseNotices.Documents)
        {
            int at = all.IndexOf("==== " + document.Path + " ====", StringComparison.Ordinal);
            Assert.True(at > previous, document.Path + " 의 머리가 없거나 순서가 틀립니다.");
            previous = at;
        }
        // 고지마다 알아볼 말이 들어 있다
        Assert.Contains("Third-Party Licenses", all, StringComparison.Ordinal);
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", all, StringComparison.Ordinal);
        Assert.Contains("\"AudioSet Ontology\" by Google Inc.", all, StringComparison.Ordinal);
        Assert.Contains("GNU AFFERO GENERAL PUBLIC LICENSE", all, StringComparison.Ordinal);
    }

    [Fact]
    public void 소리_이름_고지는_온톨로지의_각색물과_CC_BY_SA_4_0_을_적는다()
    {
        // 분류 탭의 묶음 표와 소리 이름(번역)은 AudioSet 온톨로지(CC BY-SA 4.0)를 바탕으로 만들었다. 출처, 라이선스와 그 링크,
        // 바꾼 점을 고지에 적는다(안드로이드 #349).
        string notice = Lf(LicenseNotices.Read(LicenseNotices.Documents[2]));
        Assert.All(
            new[]
            {
                "SoundVisualizer.Core/Sounds/Data/sound_groups.tsv",
                "SoundVisualizer.Core/Sounds/Data/SoundNames/*.json",
                "\"AudioSet Ontology\" by Google Inc.",
                "https://github.com/audioset/ontology (commit d417d32)",
                "(CC BY-SA 4.0)",
                "https://creativecommons.org/licenses/by-sa/4.0/",
                "regrouped into everyday groups",
            },
            phrase => Assert.Contains(phrase, notice, StringComparison.Ordinal));
    }

    [Fact]
    public void 온톨로지의_라이선스를_데이터셋의_CC_BY_4_0_으로_적지_않는다()
    {
        // 온톨로지는 CC BY-SA 4.0, 데이터셋은 CC BY 4.0 이다. 안드로이드의 예전 고지는 온톨로지를 CC BY 4.0 이라고 적었다(#342).
        var sentences = LicenseNotices.Documents
            .SelectMany(d => Regex.Split(Regex.Replace(LicenseNotices.Read(d), @"\s+", " "), @"(?<=[.;]) "))
            .Where(s => (s.Contains("온톨로지", StringComparison.Ordinal) || s.Contains("ontology", StringComparison.OrdinalIgnoreCase))
                && s.Contains("(CC BY 4.0)", StringComparison.Ordinal))
            .ToArray();
        Assert.True(sentences.Length == 0, "온톨로지를 CC BY 4.0 으로 적은 문장:\n" + string.Join("\n", sentences));
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void 앱_정보의_라이선스_안내는_AGPL_과_소리_이름의_CC_BY_SA_4_0_을_적는다(string code)
    {
        // PC 판은 AGPL-3.0 이다(안드로이드 판의 Apache License 2.0 문장을 옮기지 않는다). 소리 이름과 묶음 표는 AudioSet
        // 온톨로지의 각색물이라 CC BY-SA 4.0 이고, 자세한 고지는 바로 아래 버튼을 그 언어의 버튼 이름 그대로 가리킨다.
        var table = StringTables.Get(code)!;
        Assert.True(table.TryGet("help.about.license", out var text), code + ": help.about.license 가 없습니다");
        Assert.True(table.TryGet("help.licenses.button", out var button), code + ": help.licenses.button 이 없습니다");
        Assert.All(new[] { "AGPL-3.0", "AudioSet", "CC BY-SA 4.0", button }, phrase => Assert.Contains(phrase, text, StringComparison.Ordinal));
        Assert.DoesNotContain("Apache", text, StringComparison.Ordinal);
    }

    // ===== 구분선 맞추기(안드로이드 LicenseRulersTest, #167) =====

    [Fact]
    public void 긴_구분선은_창에_들어가는_칸_수로_줄인다()
    {
        Assert.Equal(new string('=', 35), LicenseNotices.FitRulers(Ruler80, 35));
    }

    [Fact]
    public void 창이_더_넓으면_구분선을_늘리지_않는다()
    {
        Assert.Equal(Ruler80, LicenseNotices.FitRulers(Ruler80, 120));
    }

    [Fact]
    public void 구분선이_아닌_줄은_그대로_둔다()
    {
        string text = string.Join("\n",
            "1. ONNX Runtime",
            "a = b",
            "== 제목 ==",
            "=====",                         // 짧은 = 줄은 구분선이 아니라 내용으로 본다
            "=-=-=-=-=-=-=-=-=-=-=-",        // 두 글자가 섞이면 구분선이 아니다
            "| --- | --- | --- |",           // Markdown 표의 머리 구분 줄
            "    Permission is hereby granted, free of charge, to any person obtaining a");
        Assert.Equal(text, LicenseNotices.FitRulers(text, 20));
    }

    [Fact]
    public void 빼기표_구분선도_맞춘다()
    {
        // PC 판 고지에는 '-' 구분선도 있다(NOTICE 80칸, Pretendard OFL 59칸).
        Assert.Equal(new string('-', 30), LicenseNotices.FitRulers(new string('-', 59), 30));
    }

    [Fact]
    public void 칸_수가_없으면_손대지_않는다()
    {
        Assert.Equal(Ruler80, LicenseNotices.FitRulers(Ruler80, 0));
    }

    [Fact]
    public void CRLF_로_읽혀도_창에_CR_이_남지_않는다()
    {
        // Windows 작업 폴더(autocrlf)에서 빌드하면 리소스의 고지가 CRLF 다.
        string shown = LicenseNotices.DisplayText("머리말\r\n" + Ruler80 + "\r\n1. ONNX Runtime\r\n", 35);
        Assert.Equal("머리말\n" + new string('=', 35) + "\n1. ONNX Runtime\n", shown);
        Assert.DoesNotContain("\r", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void 실제_고지는_구분선만_바뀌고_나머지_줄은_그대로다()
    {
        string all = LicenseNotices.ReadAll();
        string[] before = all.Split('\n');
        string[] after = LicenseNotices.DisplayText(all, 35).Split('\n');

        Assert.Equal(before.Length, after.Length);
        int rulers = 0;
        for (int i = 0; i < before.Length; i++)
        {
            string line = before[i];
            bool isRuler = line.Length >= 10 && (line[0] == '=' || line[0] == '-') && line.All(c => c == line[0]);
            if (isRuler)
            {
                rulers++;
                Assert.Equal(new string(line[0], Math.Min(35, line.Length)), after[i]);
            }
            else
            {
                Assert.Equal(line, after[i]);
            }
        }
        Assert.True(rulers > 0, "고지에서 구분선을 찾지 못했습니다");
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
