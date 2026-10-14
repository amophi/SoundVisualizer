// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Windows.Data;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 슬라이더 줄의 값 글자. [값, 꼴]을 받아 꼴대로 적는다. 숫자는 Windows 지역 설정과 상관없이 같은 꼴(InvariantCulture)이다.
    /// 꼴은 XAML 에서 슬라이더마다 다르게 주므로(StringFormat 은 바인딩할 수 없다) MultiBinding 으로 받는다.
    /// </summary>
    public sealed class ValueFormatConverter : IMultiValueConverter
    {
        public const string DefaultFormat = "{0:F0}";

        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not double value) return "";
            string format = values.Length > 1 && values[1] is string given && given.Length > 0 ? given : DefaultFormat;
            return string.Format(CultureInfo.InvariantCulture, format, value);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
