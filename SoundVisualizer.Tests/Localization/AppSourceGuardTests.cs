// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SoundVisualizer.Localization;
using static SoundVisualizer.Tests.Localization.CSharpLiteralScanner;

namespace SoundVisualizer.Tests.Localization;

/// <summary>
/// 앱(SoundVisualizer 프로젝트)의 .cs·.xaml 을 읽어, 화면 글자가 모두 문자열 표(Localization/Strings)를 거치는지 지킨다.
/// 한국어가 소스에 다시 들어오거나, XAML 에 영어 글자를 그대로 적거나, 표에 없는 키를 쓰면 여기서 걸린다.
/// </summary>
public class AppSourceGuardTests
{
    // 허용 목록 ①: 인자에 비 ASCII 를 써도 되는 호출. 늘릴 때는 왜 번역하지 않는지 적는다.
    // - AppLog.Info/Warn: 개발자용 기록이다. 화면에 나오지 않고, 프로젝트 관례대로 한국어로 남긴다.
    private static readonly HashSet<string> AllowedCalls = new(StringComparer.Ordinal) { "AppLog.Info", "AppLog.Warn" };

    // 허용 목록 ②: XAML 에 그대로 두는 영어 글자. 번역하지 않는 이름과, 코드가 바로 덮어쓰는 처음 값이다.
    // (언어 이름은 Core 의 UiLanguages 에, Pretendard 같은 글꼴 이름은 UiFontChain 에 있어 여기에 없다.)
    private static readonly HashSet<string> AllowedXamlTexts = new(StringComparer.Ordinal)
    {
        "Sound Visualizer", // 앱 이름. 어느 언어에서나 같다
        "FPS: --",          // 개발자 모드 FPS 표시의 처음 값. 렌더 루프가 숫자로 바꾼다
        "144 FPS",          // 편집 패널 최대 프레임 값의 처음 값. 설정을 읽으며 바꾼다
        "#FF0000",          // 색 선택 창 16진 코드의 처음 값. 창을 열며 바꾼다
    };

    // 사람이 읽는 글자를 담는 XAML 속성
    private static readonly HashSet<string> TextAttributes = new(StringComparer.Ordinal) { "Text", "Content", "Header", "Title", "ToolTip" };

    private static readonly Regex XamlComment = new(@"<!--.*?-->", RegexOptions.Singleline);
    private static readonly Regex XamlLocKey = new(@"\{l:Loc\s+([^\s}]+)\s*\}");
    private static readonly Regex KeyPattern = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$");
    private static readonly Regex Word = new("[A-Za-z]{2,}");

    private static readonly string AppDirectory = Path.Combine(RepositoryRoot(), "SoundVisualizer");

    [Fact]
    public void 앱_소스를_찾는다()
    {
        Assert.True(File.Exists(Path.Combine(AppDirectory, "LauncherWindow.xaml")), AppDirectory);
        Assert.NotEmpty(SourceFiles("*.cs"));
        Assert.NotEmpty(SourceFiles("*.xaml"));
    }

    [Fact]
    public void 코드의_문자열에는_한글이나_다른_비ASCII_글자가_없다()
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var tokens = Tokenize(File.ReadAllText(file));
            var openCalls = new Stack<int>(); // 허용 호출의 여는 괄호 깊이
            int depth = 0;
            int pendingCall = -1;
            for (int k = 0; k < tokens.Count; k++)
            {
                var token = tokens[k];
                if (token.Kind == Kind.Word && k + 3 < tokens.Count && tokens[k + 1].Text == "." && tokens[k + 3].Text == "("
                    && AllowedCalls.Contains(token.Text + "." + tokens[k + 2].Text))
                {
                    pendingCall = k + 3;
                }

                if (token.Kind == Kind.Symbol && token.Text == "(")
                {
                    depth++;
                    if (k == pendingCall) openCalls.Push(depth);
                }
                else if (token.Kind == Kind.Symbol && token.Text == ")")
                {
                    if (openCalls.Count > 0 && openCalls.Peek() == depth) openCalls.Pop();
                    depth--;
                }
                else if (token.Kind == Kind.Literal && openCalls.Count == 0 && token.Text.Any(ch => ch > 127))
                {
                    violations.Add($"{Relative(file)}:{token.Line}: {token.Text}");
                }
            }
        }
        Assert.True(violations.Count == 0, "화면 글자는 Loc 키로 바꾸세요:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void XAML_에는_주석_밖에_비ASCII_글자가_없다()
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            // 주석은 지우되 줄 수는 남겨 줄 번호를 맞춘다. 아이콘 글자는 &#xE921; 처럼 적으므로 원문은 ASCII 다.
            string text = XamlComment.Replace(File.ReadAllText(file), m => new string('\n', m.Value.Count(ch => ch == '\n')));
            var lines = text.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                if (lines[n].Any(ch => ch > 127)) violations.Add($"{Relative(file)}:{n + 1}: {lines[n].Trim()}");
            }
        }
        Assert.True(violations.Count == 0, "XAML 글자는 {l:Loc 키} 로 바꾸세요:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void XAML_의_화면_글자는_문자열_표를_거친다()
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var attribute in document.Descendants().Attributes())
            {
                if (!TextAttributes.Contains(attribute.Name.LocalName)) continue;
                if (IsTranslatableLiteral(attribute.Value))
                {
                    violations.Add($"{Relative(file)}:{((System.Xml.IXmlLineInfo)attribute).LineNumber}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
                }
            }
            foreach (var textNode in document.DescendantNodes().OfType<XText>())
            {
                if (textNode is XCData || textNode.Parent?.Name.LocalName is "FontFamily") continue; // 글꼴 이름 목록은 화면 글자가 아니다
                if (IsTranslatableLiteral(textNode.Value.Trim()))
                {
                    violations.Add($"{Relative(file)}:{((System.Xml.IXmlLineInfo)textNode).LineNumber}: {textNode.Value.Trim()}");
                }
            }
        }
        Assert.True(violations.Count == 0, "XAML 글자는 {l:Loc 키} 로 바꾸세요:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void 앱이_쓰는_문자열_키는_모두_영어_표에_있다()
    {
        var english = StringTables.Get(UiLanguages.DefaultCode)!;
        var missing = ReferencedKeys().Where(key => !english.TryGet(key, out _)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "en.json 에 없는 키: " + string.Join(", ", missing));
    }

    [Fact]
    public void 영어_표의_키는_모두_앱에서_쓴다()
    {
        // 쓰지 않는 키는 번역가가 헛수고를 한다. 키는 코드에서 이어 붙이지 말고 통째로 적는다.
        var referenced = ReferencedKeys();
        var unused = StringTables.Get(UiLanguages.DefaultCode)!.Entries.Keys.Where(key => !referenced.Contains(key)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(unused.Length == 0, "앱에서 쓰지 않는 키: " + string.Join(", ", unused));
    }

    private static bool IsTranslatableLiteral(string value)
    {
        if (value.StartsWith("{}", StringComparison.Ordinal)) value = value[2..]; // {} 로 시작하면 그 뒤는 글자 그대로다
        else if (value.StartsWith('{')) return false; // 바인딩·리소스·{l:Loc}
        return Word.IsMatch(value) && !AllowedXamlTexts.Contains(value);
    }

    /// <summary>XAML 의 {l:Loc 키} 와, 코드에서 키 꼴(화면 이름으로 시작하는 점 찍은 소문자)인 문자열 리터럴.</summary>
    private static HashSet<string> ReferencedKeys()
    {
        var groups = StringTables.Get(UiLanguages.DefaultCode)!.Entries.Keys.Select(key => key[..key.IndexOf('.')]).ToHashSet(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles("*.xaml"))
        {
            string text = XamlComment.Replace(File.ReadAllText(file), "");
            foreach (Match match in XamlLocKey.Matches(text)) keys.Add(match.Groups[1].Value);
        }
        foreach (var file in SourceFiles("*.cs"))
        {
            foreach (var token in Tokenize(File.ReadAllText(file)))
            {
                if (token.Kind != Kind.Literal || PlainContent(token.Text) is not { } content) continue;
                if (KeyPattern.IsMatch(content) && groups.Contains(content[..content.IndexOf('.')])) keys.Add(content);
            }
        }
        return keys;
    }

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppDirectory, pattern, SearchOption.AllDirectories)
            .Where(path => !Relative(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"));

    private static string Relative(string path) => Path.GetRelativePath(AppDirectory, path);

    /// <summary>테스트 실행 폴더에서 위로 올라가며 SoundVisualizer.slnx 가 있는 저장소 뿌리를 찾는다.</summary>
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SoundVisualizer.slnx"))) return directory.FullName;
        }
        throw new InvalidOperationException("SoundVisualizer.slnx 를 찾지 못했습니다: " + AppContext.BaseDirectory);
    }
}
