// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Globalization;
using System.Windows.Data;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 스크롤 막대 손잡이를 일정 길이보다 짧게 두지 않는다. 막대(Track)는 손잡이 길이를 보이는 비율(ViewportSize)로만 정하고 손잡이의
    /// MinHeight 를 보지 않아, 소리 탭 고급 모드(521줄)에서는 손잡이가 9px 남짓한 점이 되어 마우스로 잡기 어려웠다.
    /// 막대에 넘기는 보이는 비율을 손잡이가 최소 길이가 될 만큼만 키운다. 끌 때의 값은 막대가 손잡이 길이를 빼고 셈하므로 그대로 맞다.
    ///
    /// 값: [보이는 크기(ViewportSize), 최댓값(Maximum, 최솟값은 0), 막대 길이(ActualHeight)]. 매개변수: 손잡이 최소 길이(px).
    /// </summary>
    public sealed class ThumbViewportConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 3 || values[0] is not double viewport) return 0.0;
            if (values[1] is not double maximum || values[2] is not double trackLength) return viewport;
            double minThumb = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double given) ? given : 0;
            if (maximum <= 0 || trackLength <= minThumb || double.IsNaN(viewport)) return viewport;
            // 손잡이 = 막대 길이 x 보이는 크기 / (최댓값 + 보이는 크기) 이 최소 길이가 되는 보이는 크기
            return Math.Max(viewport, minThumb * maximum / (trackLength - minThumb));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
