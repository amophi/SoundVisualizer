// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer
{
    /// <summary>
    /// 같은 너비의 선택지 칸(표현 모드, 사운드 모드)을 한 줄에 몇 개씩 둘지. WPF 의 EqualChoicePanel 이 쓴다.
    /// 안드로이드 판 choicesPerRow(#304)와 같은 규칙이다.
    /// </summary>
    public static class ChoiceRows
    {
        /// <summary>
        /// 모두 한 줄부터 하나씩 줄이며, 가장 넓은 칸이 줄을 바꾸지 않고 들어가는 첫 수를 고른다.
        /// 칸을 좁힌 채 글자를 접으면 WPF 는 단어 가운데서도 끊고("Outli/ne"), 접지 않으면 글자가 잘리기 때문이다.
        /// 마지막 줄에 한 칸만 남는 나눔(네 칸의 3+1, 다섯 칸의 4+1)은 건너뛴다. 어느 수에도 안 들어가면 한 칸씩이다.
        /// </summary>
        /// <param name="count">칸 수</param>
        /// <param name="rowWidth">줄 전체 너비</param>
        /// <param name="gap">칸 사이 간격</param>
        /// <param name="widestCell">가장 넓은 칸이 줄을 바꾸지 않을 때의 너비(안쪽 여백 포함)</param>
        public static int PerRow(int count, double rowWidth, double gap, double widestCell)
        {
            if (count <= 1) return 1;
            for (int perRow = count; perRow >= 1; perRow--)
            {
                bool lonelyLastRow = perRow != count && count % perRow != 0 && count % perRow < perRow - 1;
                if (lonelyLastRow) continue;
                if (widestCell <= CellWidth(rowWidth, perRow, gap)) return perRow;
            }
            return 1;
        }

        /// <summary>한 줄에 perRow 칸을 둘 때 칸 하나의 너비.</summary>
        public static double CellWidth(double rowWidth, int perRow, double gap) => Math.Max(0, (rowWidth - gap * (perRow - 1)) / Math.Max(1, perRow));
    }
}
