// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// WPF 화면이 쓰는 색 사전(SoundVisualizer/Themes/Palette.xaml)이 Core 의 <see cref="UiColors"/> 와 같은지.
/// 테스트 프로젝트는 WPF 를 모르므로 XAML 파일을 XML 로 읽는다. 붓 이름은 색 이름 + "Brush" 다(Background → BackgroundBrush).
/// 한쪽만 바꾸면 대비 테스트는 Core 값으로 통과하는데 화면은 다른 색을 그리게 되므로, 둘을 여기서 묶는다.
/// </summary>
public class ThemePaletteXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Regex OpaqueHex = new("^#[0-9A-Fa-f]{6}$");

    private static string PalettePath => Path.Combine(RepositoryPaths.Root, "SoundVisualizer", "Themes", "Palette.xaml");

    [Fact]
    public void 팔레트_XAML_의_붓은_Core_의_색과_같다()
    {
        var core = CoreColors();
        Assert.All(XamlBrushes(), brush =>
        {
            Assert.True(brush.Key.EndsWith("Brush", StringComparison.Ordinal), $"붓 이름은 Brush 로 끝나야 합니다: {brush.Key}");
            string name = brush.Key[..^"Brush".Length];
            Assert.True(core.TryGetValue(name, out uint expected), $"UiColors 에 {name} 가 없습니다({brush.Key}).");
            Assert.True(OpaqueHex.IsMatch(brush.Value), $"{brush.Key} 는 불투명한 #RRGGBB 로 적습니다: {brush.Value}");
            uint actual = uint.Parse(brush.Value[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            Assert.True(expected == actual, $"{brush.Key}: XAML #{actual:X6}, Core #{expected:X6}");
        });
    }

    [Fact]
    public void Core_의_색은_모두_팔레트_XAML_에_붓으로_있다()
    {
        var keys = XamlBrushes().Select(b => b.Key).ToHashSet(StringComparer.Ordinal);
        var missing = CoreColors().Keys.Where(name => !keys.Contains(name + "Brush")).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "Palette.xaml 에 없는 색: " + string.Join(", ", missing));
    }

    [Fact]
    public void 팔레트_XAML_은_이름_붙은_단색_붓만_둔다()
    {
        // 투명도나 다른 리소스가 끼면 같은 이름이라도 다른 색으로 그려진다. 파생 색은 Theme.xaml 에서 만든다.
        var root = XDocument.Load(PalettePath).Root!;
        Assert.Equal(Presentation + "ResourceDictionary", root.Name);
        Assert.All(root.Elements(), element =>
        {
            Assert.Equal(Presentation + "SolidColorBrush", element.Name);
            var attributes = element.Attributes().Select(a => a.Name).ToHashSet();
            Assert.True(attributes.SetEquals(new[] { Xaml + "Key", XName.Get("Color") }), $"{element.Attribute(Xaml + "Key")?.Value}: x:Key 와 Color 만 적습니다.");
        });
    }

    [Fact]
    public void 붓_이름은_겹치지_않는다()
    {
        var keys = XamlBrushes().Select(b => b.Key).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    private static IReadOnlyList<KeyValuePair<string, string>> XamlBrushes() =>
        XDocument.Load(PalettePath).Root!
            .Elements(Presentation + "SolidColorBrush")
            .Select(e => new KeyValuePair<string, string>(e.Attribute(Xaml + "Key")?.Value ?? "", e.Attribute("Color")?.Value ?? ""))
            .ToArray();

    /// <summary>UiColors 의 상수 전부(이름 → 0xRRGGBB). 색을 더하면 XAML 에도 붓을 더해야 이 테스트가 통과한다.</summary>
    private static Dictionary<string, uint> CoreColors() =>
        typeof(UiColors).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(uint))
            .ToDictionary(f => f.Name, f => (uint)f.GetRawConstantValue()!, StringComparer.Ordinal);
}
