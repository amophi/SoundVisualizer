// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows.Markup;

namespace SoundVisualizer.Localization
{
    /// <summary>
    /// XAML 에서 <c>Text="{l:Loc launcher.tab.home}"</c> 처럼 쓴다. <see cref="Loc"/> 의 인덱서에 OneWay 로 바인딩하므로
    /// 언어를 바꾸면 창을 다시 열지 않아도 글자가 바뀐다.
    /// </summary>
    [MarkupExtensionReturnType(typeof(object))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; } = "";

        public override object ProvideValue(IServiceProvider serviceProvider) => Loc.BindingFor(Key).ProvideValue(serviceProvider);
    }
}
