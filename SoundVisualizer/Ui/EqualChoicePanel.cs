// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Windows;
using System.Windows.Controls;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 같은 너비의 선택지 칸(표현 모드, 사운드 모드)을 줄지어 둔다. 안드로이드 판 EqualChoiceRows 와 같은 생각이다(#304).
    ///
    /// 넓으면 한 줄에 모두 둔다. 번역된 이름이 길어 칸에 한 줄로 들어가지 않으면, 들어갈 때까지 한 줄의 칸 수를 줄여 여러 줄로 둔다
    /// (규칙은 Core 의 <see cref="ChoiceRows.PerRow"/>). 한 칸씩 두어도 안 들어가는 이름은 칸 안에서 줄을 바꾼다.
    /// 한 줄 안의 칸은 높이를 맞추고, 마지막 줄이 덜 차도 칸 너비는 위 줄과 같다.
    /// 오른쪽에서 왼쪽으로 쓰는 언어에서는 WPF 가 배치를 통째로 뒤집으므로 따로 할 일이 없다.
    /// </summary>
    public sealed class EqualChoicePanel : Panel
    {
        public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
            nameof(Gap), typeof(double), typeof(EqualChoicePanel),
            new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>칸 사이 간격(가로·세로 같다).</summary>
        public double Gap
        {
            get => (double)GetValue(GapProperty);
            set => SetValue(GapProperty, value);
        }

        private int _perRow = 1;
        private double _cellWidth;
        private double[] _rowHeights = Array.Empty<double>();

        protected override Size MeasureOverride(Size availableSize)
        {
            var children = InternalChildren;
            int count = children.Count;
            if (count == 0) return new Size();

            // 먼저 줄을 바꾸지 않은 이름의 너비를 잰다.
            double widest = 0;
            foreach (UIElement child in children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                widest = Math.Max(widest, child.DesiredSize.Width);
            }

            double gap = Gap;
            if (double.IsInfinity(availableSize.Width))
            {
                _perRow = count;
                _cellWidth = widest;
            }
            else
            {
                _perRow = ChoiceRows.PerRow(count, availableSize.Width, gap, widest);
                _cellWidth = ChoiceRows.CellWidth(availableSize.Width, _perRow, gap);
            }

            int rows = (count + _perRow - 1) / _perRow;
            _rowHeights = new double[rows];
            for (int i = 0; i < count; i++)
            {
                var child = children[i];
                child.Measure(new Size(_cellWidth, double.PositiveInfinity));
                int row = i / _perRow;
                _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
            }

            double height = _rowHeights.Sum() + gap * (rows - 1);
            double width = double.IsInfinity(availableSize.Width) ? _cellWidth * _perRow + gap * (_perRow - 1) : availableSize.Width;
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = InternalChildren;
            if (children.Count == 0) return finalSize;

            double gap = Gap;
            // 배치 단계의 너비가 재기 단계와 다르면(부모가 늘였을 때) 그 너비로 칸을 다시 나눈다. 칸 수는 재기 단계에서 정한 것을 쓴다.
            double cellWidth = ChoiceRows.CellWidth(finalSize.Width, _perRow, gap);
            double y = 0;
            for (int i = 0; i < children.Count; i++)
            {
                int row = i / _perRow;
                int column = i % _perRow;
                if (column == 0 && row > 0) y += _rowHeights[row - 1] + gap;
                double rowHeight = row < _rowHeights.Length ? _rowHeights[row] : children[i].DesiredSize.Height;
                children[i].Arrange(new Rect(column * (cellWidth + gap), y, cellWidth, rowHeight));
            }
            return finalSize;
        }
    }
}
