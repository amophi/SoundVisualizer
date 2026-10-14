// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace SoundVisualizer.Tests.Ui;

/// <summary>
/// 화면 목록의 줄을 새 항목에 맞추기(소리 탭 목록). 키가 같은 줄은 같은 객체로 남아야 그 줄의 키보드 초점과 자리가 남는다.
/// 줄은 ObservableCollection 이고, 목록이 받는 바뀜 알림까지 본다.
/// </summary>
public class KeyedListTests
{
    private sealed class Row
    {
        public Row(string key, int value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        public int Value { get; set; }

        public int Updates { get; set; }
    }

    private sealed record Item(string Key, int Value);

    private static List<NotifyCollectionChangedEventArgs> Sync(ObservableCollection<Row> rows, params Item[] items)
    {
        var events = new List<NotifyCollectionChangedEventArgs>();
        void Record(object? sender, NotifyCollectionChangedEventArgs e) => events.Add(e);
        rows.CollectionChanged += Record;
        KeyedList.Sync(rows, items, item => item.Key, row => row.Key, item => new Row(item.Key, item.Value), (row, item) =>
        {
            row.Value = item.Value;
            row.Updates++;
        });
        rows.CollectionChanged -= Record;
        return events;
    }

    private static ObservableCollection<Row> Rows(params string[] keys) => new(keys.Select(key => new Row(key, 0)));

    [Fact]
    public void 같은_키의_줄은_같은_객체를_고쳐_쓰고_목록은_바뀌지_않는다()
    {
        var rows = Rows("a", "b", "c");
        var before = rows.ToList();

        var events = Sync(rows, new Item("a", 1), new Item("b", 2), new Item("c", 3));

        Assert.Empty(events);
        Assert.Equal(before, rows);
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(row => row.Value));
        Assert.All(rows, row => Assert.Equal(1, row.Updates));
    }

    [Fact]
    public void 없어진_줄은_지우고_새_줄은_제자리에_넣으며_남는_줄은_옮기지_않는다()
    {
        var rows = Rows("intro", "changed", "s:danger", "c:sirens", "c:alarms", "end");
        Row intro = rows[0], sirens = rows[3];

        var events = Sync(rows, new Item("intro", 0), new Item("changed", 0), new Item("empty", 0), new Item("s:danger", 0),
            new Item("c:sirens", 0), new Item("c:horns", 0), new Item("end", 0));

        Assert.Equal(new[] { "intro", "changed", "empty", "s:danger", "c:sirens", "c:horns", "end" }, rows.Select(row => row.Key));
        Assert.Same(intro, rows[0]);
        Assert.Same(sirens, rows[4]);
        Assert.All(events, e => Assert.Contains(e.Action, new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Remove }));
        Assert.Equal(1, events.Count(e => e.Action == NotifyCollectionChangedAction.Remove)); // c:alarms
        Assert.Equal(2, events.Count(e => e.Action == NotifyCollectionChangedAction.Add)); // empty, c:horns
    }

    [Fact]
    public void 모두_바뀌어도_새_차례가_된다()
    {
        var rows = Rows("intro", "changed", "s:danger", "c:sirens", "end");

        Sync(rows, new Item("intro", 0), new Item("changed", 0), new Item("s:danger", 0), new Item("g:sirens", 0),
            new Item("r:1", 0), new Item("r:2", 0), new Item("end", 0));

        Assert.Equal(new[] { "intro", "changed", "s:danger", "g:sirens", "r:1", "r:2", "end" }, rows.Select(row => row.Key));

        Sync(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public void 남는_줄의_순서가_바뀌면_옮겨서라도_새_차례를_맞춘다()
    {
        var rows = Rows("a", "b", "c", "d");
        Row b = rows[1];

        Sync(rows, new Item("c", 0), new Item("a", 0), new Item("b", 0), new Item("e", 0));

        Assert.Equal(new[] { "c", "a", "b", "e" }, rows.Select(row => row.Key));
        Assert.Same(b, rows[2]);
    }

    [Fact]
    public void 겹친_키는_받지_않는다()
    {
        var rows = Rows("a");

        Assert.Throws<ArgumentException>(() => Sync(rows, new Item("x", 0), new Item("x", 1)));
        Assert.Equal(new[] { "a" }, rows.Select(row => row.Key)); // 고치기 전에 거른다
    }
}
