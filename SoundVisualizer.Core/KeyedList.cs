// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer
{
    /// <summary>
    /// 화면 목록의 줄(보기 객체)을 새 항목 목록에 맞춘다. 키가 같은 줄은 버리지 않고 고쳐 쓰고, 없어진 줄만 지우고 새 줄만 넣는다.
    /// 모바일 LazyColumn 의 키와 같은 일을 WPF 의 ObservableCollection 에 한다.
    ///
    /// 통째로 바꿔 끼우면 목록이 줄을 모두 새로 만들어, 종류를 고른 카드가 키보드 초점을 잃고 고급 모드 스위치를 누른 줄도 초점을
    /// 잃는다. 남는 줄끼리의 앞뒤 순서가 그대로이면(소리 탭은 늘 그렇다) 지우기와 넣기만 하고 줄을 옮기지 않는다.
    /// </summary>
    public static class KeyedList
    {
        /// <summary>
        /// <paramref name="rows"/> 를 <paramref name="items"/> 의 차례로 맞춘다. 끝나면 rows[i] 의 키는 items[i] 의 키와 같다.
        /// </summary>
        /// <param name="rows">화면 목록. 키가 겹치지 않아야 한다.</param>
        /// <param name="items">새 항목. 키가 겹치지 않아야 한다.</param>
        /// <param name="itemKey">항목의 키.</param>
        /// <param name="rowKey">줄의 키.</param>
        /// <param name="create">새 키의 줄을 만든다. 만든 줄에 항목을 넣는 것까지 한다.</param>
        /// <param name="update">남는 줄에 새 항목을 넣는다. 바뀐 것이 없으면 아무것도 알리지 않게 하는 것은 줄의 몫이다.</param>
        public static void Sync<TItem, TRow>(
            IList<TRow> rows,
            IReadOnlyList<TItem> items,
            Func<TItem, string> itemKey,
            Func<TRow, string> rowKey,
            Func<TItem, TRow> create,
            Action<TRow, TItem> update)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(itemKey);
            ArgumentNullException.ThrowIfNull(rowKey);
            ArgumentNullException.ThrowIfNull(create);
            ArgumentNullException.ThrowIfNull(update);

            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (TItem item in items)
            {
                if (!wanted.Add(itemKey(item))) throw new ArgumentException("키가 겹칩니다: " + itemKey(item), nameof(items));
            }

            // 1. 없어진 줄을 뒤에서부터 지운다.
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(rowKey(rows[i]))) rows.RemoveAt(i);
            }

            var remaining = new Dictionary<string, TRow>(StringComparer.Ordinal);
            foreach (TRow row in rows) remaining.Add(rowKey(row), row);

            // 2. 앞에서부터 맞춘다. 제자리의 줄은 고쳐 쓰고, 새 키는 그 자리에 넣는다. 남는 줄의 순서가 바뀐 때만(소리 탭에는 없다) 옮긴다.
            for (int i = 0; i < items.Count; i++)
            {
                TItem item = items[i];
                string key = itemKey(item);
                if (i < rows.Count && string.Equals(rowKey(rows[i]), key, StringComparison.Ordinal))
                {
                    update(rows[i], item);
                }
                else if (remaining.TryGetValue(key, out TRow? row))
                {
                    rows.RemoveAt(IndexOf(rows, key, rowKey, i + 1));
                    rows.Insert(i, row);
                    update(row, item);
                }
                else
                {
                    rows.Insert(i, create(item));
                }
            }
        }

        private static int IndexOf<TRow>(IList<TRow> rows, string key, Func<TRow, string> rowKey, int start)
        {
            for (int i = start; i < rows.Count; i++)
            {
                if (string.Equals(rowKey(rows[i]), key, StringComparison.Ordinal)) return i;
            }
            throw new InvalidOperationException("줄을 찾지 못했습니다: " + key);
        }
    }
}
