// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Tests.Sounds;

/// <summary>
/// 분류 탭의 색인(#368). 화면 언어로 한 번 만들어 두고(언어 하나의 것만 둔다), 찾는 결과는 바탕 계산을 그때그때 하는 것과 같아야 한다.
/// 모바일 SoundIndexTest 를 옮겼다. 캐시는 테스트마다 따로 만든다.
/// </summary>
public class SoundIndexTests
{
    private static readonly IReadOnlyList<SoundEntry> Entries = SoundTestData.Entries();

    private static readonly IReadOnlyDictionary<SoundGroup, string> EnglishTitles = SoundData.GroupTitles(SoundLanguages.English);

    /// <summary>앱(<see cref="SoundCatalog.BuildIndex(string?, IReadOnlyList{SoundEntry})"/>)이 넘기는 것과 같은 값으로 색인을 만든다.</summary>
    private static SoundIndex Build(string language, IReadOnlyList<string>? labels = null) =>
        SoundCatalog.BuildIndex(language, Entries, labels ?? SoundData.Names(language), SoundData.GroupTitles(language), EnglishTitles);

    /// <summary>
    /// 색인 없이 그때그때 하는 바탕 계산(모바일 #368 전의 분류 탭이 그릴 때마다 만들던 값): 이름 배열, 소리마다의 찾기 값, 묶음,
    /// 영어 이름과 이은 묶음 이름 값.
    /// </summary>
    private sealed class Before
    {
        public Before(string language, IReadOnlyList<string> labels)
        {
            LabelOf = entry => entry.Index < labels.Count ? labels[entry.Index] : entry.Name;
            SearchKeys = Entries.Select(entry => SoundCatalog.SearchKey(LabelOf(entry), entry.Name)).ToList();
            Sections = SoundGroups.Sections(Entries);
            var local = SoundData.GroupTitles(language);
            TitleKeys = SoundGroups.TitleKeys(local: group => local[group], english: group => EnglishTitles[group]);
        }

        public Func<SoundEntry, string> LabelOf { get; }

        public List<string> SearchKeys { get; }

        public IReadOnlyList<SoundSection> Sections { get; }

        public IReadOnlyDictionary<SoundGroup, string> TitleKeys { get; }
    }

    [Fact]
    public void 색인은_언어마다_한_번만_만들고_다시_쓴다()
    {
        var cache = new SoundIndexCache(SoundTestData.Names);
        int builds = 0;
        var first = cache.GetOrBuild("ko", code => { builds++; return Build(code); });
        Assert.Same(first, cache.CachedOrNull("ko"));
        // 탭에 다시 들어올 때다.
        for (int i = 0; i < 3; i++)
        {
            Assert.Same(first, cache.GetOrBuild("ko", code => { builds++; return Build(code); }));
        }
        Assert.Same(first, cache.GetOrBuild("ko"));
        Assert.Equal(1, builds); // 이름 521개를 읽고 정규화를 1,042번 하는 일은 한 번이다.
    }

    [Fact]
    public void 언어가_다르면_색인을_따로_만들고_다른_언어의_것을_쓰지_않는다()
    {
        var cache = new SoundIndexCache(SoundTestData.Names);
        var ko = cache.GetOrBuild("ko");
        Assert.Null(cache.CachedOrNull("en")); // 영어로는 아직 만들지 않았다.
        int builds = 0;
        var en = cache.GetOrBuild("en", code => { builds++; return Build(code); });
        Assert.Equal(1, builds);
        Assert.NotSame(ko, en);
        Assert.Same(en, cache.CachedOrNull("en"));
        Assert.Null(cache.CachedOrNull("ko")); // 두는 것은 마지막 언어의 것 하나다.

        var siren = Entries.First(entry => entry.Name == "Siren");
        Assert.Equal("사이렌", ko.LabelOf(siren));
        Assert.Equal("Siren", en.LabelOf(siren));
        Assert.Contains(SoundCatalog.Normalize("부엌"), ko.TitleKeys[SoundGroup.Kitchen]); // 한국어 색인은 한국어 묶음 이름으로 찾는다.
        Assert.DoesNotContain(SoundCatalog.Normalize("부엌"), en.TitleKeys[SoundGroup.Kitchen]); // 영어 색인에는 한국어 묶음 이름이 없다.
    }

    [Fact]
    public void 언어_코드는_대소문자를_가리지_않고_없는_언어는_영어로_만든다()
    {
        var cache = new SoundIndexCache(SoundTestData.Names);
        var ptBr = cache.GetOrBuild("PT-br");
        Assert.Equal("pt-BR", ptBr.Language);
        Assert.Same(ptBr, cache.CachedOrNull("pt-BR"));

        var unknown = cache.GetOrBuild("xx-YY");
        Assert.Equal(SoundLanguages.English, unknown.Language);
        Assert.Same(unknown, cache.CachedOrNull(null));
        Assert.Equal("Siren", unknown.LabelOf(Entries.First(entry => entry.Name == "Siren")));
    }

    [Fact]
    public async Task 이미_만든_색인은_기다리지_않고_돌려준다()
    {
        var cache = new SoundIndexCache(SoundTestData.Names);
        var built = await cache.GetOrBuildAsync("ja");
        Assert.Equal("ja", built.Language);
        var again = cache.GetOrBuildAsync("ja");
        Assert.True(again.IsCompletedSuccessfully); // 탭에 다시 들어오면 바로 그린다.
        Assert.Same(built, await again);
    }

    [Fact]
    public void 카드_이름은_그_언어의_소리_이름이거나_새로_지은_묶음_이름이다()
    {
        var cache = new SoundIndexCache(SoundTestData.Names);
        var ko = cache.GetOrBuild("ko");
        SoundCard Card(SoundIndex index, SoundGroup group) =>
            index.Sections.SelectMany(section => section.Cards).First(card => card.Group == group);

        Assert.Equal("사이렌", ko.CardTitle(Card(ko, SoundGroup.Sirens)));
        Assert.Equal("경적·자동차 경보음", ko.CardTitle(Card(ko, SoundGroup.Horns)));
        Assert.Equal("말소리·속삭임", ko.CardTitle(Card(ko, SoundGroup.Talking)));

        var en = cache.GetOrBuild("en");
        Assert.Equal("Siren", en.CardTitle(Card(en, SoundGroup.Sirens)));
        Assert.Equal("Car horns and car alarms", en.CardTitle(Card(en, SoundGroup.Horns)));
    }

    [Fact]
    public void 색인으로_찾은_결과는_바탕_계산을_그때그때_한_것과_같다()
    {
        // SoundGroupSearchTests 와 SoundCatalogTests 가 보는 찾는 말들이다. 한국어·영어 소리 이름, 두 언어의 묶음 이름, 문장 부호와
        // 띄어쓰기, 한글을 치는 중의 모양, 맞지 않는 말.
        string[] queries =
        {
            "", "  ", "사이렌", "노크", "경적", "초인종", "부엌", "부엌 요리", "부엌·요리", "경적 자동차", "TV 라디오",
            "tv radio", "kitchen", "Kitchen and cooking", "knock", "Siren", "SIRENE", "firetruck",
            "fire engine fire truck siren", "Fire engine, fire truck (siren)", "civil defense", "door",
            "ㅅ", "상", "사일", "홪", "화재 경봉", "닭 울", "노크 소리knock", "비rain", "zzzz",
        };
        var overridesList = new IReadOnlyDictionary<string, string>[]
        {
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["Doorbell"] = SoundTypeLabels.Danger, ["Siren"] = SoundTypeLabels.Ambient },
        };
        // 이름 배열이 소리 수보다 짧은 경우(모델이 바뀐 직후 등)도 모델 이름으로 찾는다.
        var cases = new (string Language, IReadOnlyList<string> Labels)[]
        {
            ("ko", SoundData.Names("ko")),
            ("en", SoundData.Names("en")),
            ("ko", SoundData.Names("ko").Take(100).ToList()),
        };
        foreach (var (language, labels) in cases)
        {
            var before = new Before(language, labels);
            var index = Build(language, labels);
            Assert.Same(Entries, index.Entries);
            Assert.Equal(Entries.Select(before.LabelOf), Entries.Select(index.LabelOf));
            Assert.Equal(before.SearchKeys, index.SearchKeys);
            Assert.Equal(before.Sections, index.Sections);
            Assert.Equal(before.TitleKeys.OrderBy(pair => pair.Key), index.TitleKeys.OrderBy(pair => pair.Key));
            foreach (var overrides in overridesList)
            {
                foreach (SoundFilter filter in Enum.GetValues<SoundFilter>())
                {
                    foreach (string query in queries)
                    {
                        var expected = SoundGroups.Filter(before.Sections, before.SearchKeys, before.TitleKeys, overrides, query, filter);
                        var actual = index.Filter(overrides, query, filter);
                        Assert.True(expected.SequenceEqual(actual), $"{language}({labels.Count}) \"{query}\" {filter} {overrides.Count}");
                    }
                }
            }
        }

        // 위의 비교가 빈 결과끼리만 맞춘 것이 아니다.
        var ko = Build("ko");
        List<SoundGroup?> Groups(string query) =>
            ko.Filter(new Dictionary<string, string>(), query, SoundFilter.All)
                .SelectMany(section => section.Cards).Select(card => card.Card.Group).ToList();
        Assert.Equal(new SoundGroup?[] { SoundGroup.Kitchen }, Groups("부엌 요리"));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Kitchen }, Groups("Kitchen and cooking"));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Background }, Groups("tv radio"));
        Assert.Equal(new SoundGroup?[] { SoundGroup.Sirens }, Groups("fire engine fire truck siren"));
    }

    [Fact]
    public async Task 만드는_중에_탭을_떠나도_다_만든_색인이_남고_다시_연_탭은_그것을_기다려_쓴다()
    {
        var cache = new SoundIndexCache(SoundTestData.Names);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int builds = 0;
        // 탭이 하는 대로 스레드 풀에서 색인을 만든다.
        var first = Task.Run(() => cache.GetOrBuild("ko", code =>
        {
            Interlocked.Increment(ref builds);
            started.Set();
            // 기다림이 실패해도 테스트가 멈추지 않게 끝을 둔다.
            release.Wait(TimeSpan.FromSeconds(30));
            return Build(code);
        }));
        Assert.True(started.Wait(TimeSpan.FromSeconds(10)));

        // 만드는 중에 탭을 떠나 기다리기를 그만둔다.
        using (var leave = new CancellationTokenSource())
        {
            var waiting = first.WaitAsync(leave.Token);
            leave.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }

        // 곧 탭이 다시 열려 색인을 찾는다. 앞의 것이 끝나기를 기다려야 한다(따로 만들기 시작하면 안 된다).
        SoundIndex? second = null;
        var waiter = new Thread(() => second = cache.GetOrBuild("ko", code =>
        {
            Interlocked.Increment(ref builds);
            return Build(code);
        }))
        { IsBackground = true };
        waiter.Start();
        // 확인이 실패해도 앞의 것을 풀어 준다. 풀지 않으면 앞의 것이 자물쇠를 쥔 채 남는다.
        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while ((waiter.ThreadState & ThreadState.WaitSleepJoin) == 0 && Volatile.Read(ref builds) < 2)
            {
                Assert.True(DateTime.UtcNow < deadline, "뒤의 탭이 색인을 찾으러 오지 않았다");
                Thread.Sleep(1);
            }
        }
        finally
        {
            release.Set();
        }
        var built = await first;
        Assert.True(waiter.Join(TimeSpan.FromSeconds(30)));

        Assert.Same(built, cache.CachedOrNull("ko")); // 기다리기를 그만둔 쪽이 만든 색인도 캐시에 남는다.
        Assert.Same(built, second); // 다시 연 탭은 앞에서 만든 색인을 쓴다.
        Assert.Equal(1, Volatile.Read(ref builds)); // 색인은 한 번만 만든다.
    }
}
