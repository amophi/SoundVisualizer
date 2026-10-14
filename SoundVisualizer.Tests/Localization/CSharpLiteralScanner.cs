// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests.Localization;

/// <summary>
/// 소스 지킴이 테스트용 간단한 C# 어휘 분석기. 주석을 건너뛰고 문자열·문자 리터럴, 이름, 기호를 순서대로 내놓는다.
/// 보통 문자열, @ 축자 문자열, $ 보간 문자열(구멍 안의 문자열까지), """ 원시 문자열을 다룬다. 컴파일러만큼 엄밀하지는 않지만
/// 앱 소스에 쓰는 꼴은 모두 읽는다.
/// </summary>
internal static class CSharpLiteralScanner
{
    public enum Kind
    {
        Word,
        Symbol,
        Literal,
    }

    public readonly record struct Token(Kind Kind, string Text, int Line);

    public static List<Token> Tokenize(string code)
    {
        var tokens = new List<Token>();
        int i = 0;
        int line = 1;
        while (i < code.Length)
        {
            char c = code[i];
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '/' && At(code, i + 1) == '/')
            {
                while (i < code.Length && code[i] != '\n') i++;
            }
            else if (c == '/' && At(code, i + 1) == '*')
            {
                int end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? code.Length : end + 2;
                line += Count(code, i, end, '\n');
                i = end;
            }
            else if (c == '\'')
            {
                int start = i;
                i++;
                while (i < code.Length && code[i] != '\'') i += code[i] == '\\' ? 2 : 1;
                i++;
                tokens.Add(new Token(Kind.Literal, code[start..Math.Min(i, code.Length)], line));
            }
            else if (c == '"' || ((c == '$' || c == '@') && IsStringPrefix(code, i)))
            {
                int start = i;
                int startLine = line;
                i = SkipString(code, i);
                line += Count(code, start, i, '\n');
                tokens.Add(new Token(Kind.Literal, code[start..i], startLine));
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                int start = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
                tokens.Add(new Token(Kind.Word, code[start..i], line));
            }
            else
            {
                tokens.Add(new Token(Kind.Symbol, c.ToString(), line));
                i++;
            }
        }
        return tokens;
    }

    /// <summary>문자열 리터럴의 내용(따옴표와 접두어를 뺀 것). 키를 찾는 데만 쓰므로 보통 문자열만 푼다.</summary>
    public static string? PlainContent(string literal) =>
        literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"' && !literal.StartsWith("\"\"\"", StringComparison.Ordinal)
            ? literal[1..^1]
            : null;

    private static char At(string code, int index) => index < code.Length ? code[index] : '\0';

    private static int Count(string code, int start, int end, char wanted)
    {
        int n = 0;
        for (int k = start; k < end && k < code.Length; k++)
        {
            if (code[k] == wanted) n++;
        }
        return n;
    }

    private static bool IsStringPrefix(string code, int i)
    {
        while (i < code.Length && (code[i] == '$' || code[i] == '@')) i++;
        return At(code, i) == '"';
    }

    /// <summary>i 에서 시작하는 문자열 리터럴 바로 뒤의 위치.</summary>
    private static int SkipString(string code, int i)
    {
        bool verbatim = false;
        int dollars = 0;
        while (code[i] == '$' || code[i] == '@')
        {
            if (code[i] == '@') verbatim = true;
            else dollars++;
            i++;
        }

        // 원시 문자열: 여는 따옴표 수만큼의 닫는 따옴표까지
        int quotes = 0;
        while (At(code, i + quotes) == '"') quotes++;
        if (quotes >= 3)
        {
            string fence = new('"', quotes);
            int end = code.IndexOf(fence, i + quotes, StringComparison.Ordinal);
            return end < 0 ? code.Length : end + quotes;
        }

        i++; // 여는 따옴표
        while (i < code.Length)
        {
            char c = code[i];
            if (verbatim && c == '"' && At(code, i + 1) == '"')
            {
                i += 2;
            }
            else if (!verbatim && c == '\\')
            {
                i += 2;
            }
            else if (c == '"')
            {
                return i + 1;
            }
            else if (dollars > 0 && c == '{' && At(code, i + 1) == '{')
            {
                i += 2;
            }
            else if (dollars > 0 && c == '{')
            {
                i = SkipHole(code, i + 1);
            }
            else
            {
                i++;
            }
        }
        return code.Length;
    }

    /// <summary>보간 구멍 {…} 의 닫는 중괄호 바로 뒤. 구멍 안의 문자열·괄호를 건너뛴다.</summary>
    private static int SkipHole(string code, int i)
    {
        int depth = 0;
        while (i < code.Length)
        {
            char c = code[i];
            if (c == '"' || ((c == '$' || c == '@') && IsStringPrefix(code, i)))
            {
                i = SkipString(code, i);
            }
            else if (c == '\'')
            {
                i++;
                while (i < code.Length && code[i] != '\'') i += code[i] == '\\' ? 2 : 1;
                i++;
            }
            else if (c == '(' || c == '[' || c == '{')
            {
                depth++;
                i++;
            }
            else if ((c == ')' || c == ']') && depth > 0)
            {
                depth--;
                i++;
            }
            else if (c == '}')
            {
                if (depth == 0) return i + 1;
                depth--;
                i++;
            }
            else
            {
                i++;
            }
        }
        return code.Length;
    }
}
