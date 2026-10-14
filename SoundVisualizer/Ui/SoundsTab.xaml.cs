// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SoundVisualizer.Ai;
using SoundVisualizer.Localization;
using SoundVisualizer.Sounds;

namespace SoundVisualizer.Ui
{
    /// <summary>
    /// 런처의 소리 탭. 안드로이드 ClassifyTab 을 옮겼다. AI 가 알아듣는 소리를 어느 종류(환경음·대화음·위협음)로 볼지 보여 주고 바꾸게 한다.
    ///
    /// - 기본 모드: 비슷한 소리를 묶은 카드에서 묶음째 고른다. 카드는 기본 종류의 구역(위협음 → 대화음 → 환경음)에 놓이고 바꿔도
    ///   제 구역에 그대로 있다. 고르면 찾는 말·거르기와 상관없이 카드의 소리를 모두 한 번에 바꾼다(저장도 한 번). 하나씩 바꿔 둔 소리의
    ///   종류가 바뀔 때만 먼저 묻는다.
    /// - 고급 모드: 같은 차례로 묶음 머리 밑에 소리를 한 줄씩 펴서 하나씩 바꾼다. 하나씩 바꾼 묶음이 있는 채로 끄면 먼저 알린다.
    /// - 거르기를 켠 채로 바꾼 카드(고급 모드는 그 줄)는 거르기에서 빠져도 찾는 말이나 거르기를 바꿀 때까지 남는다(KeptSounds).
    ///
    /// 저장하는 것은 소리마다의 종류(<see cref="AppSettings.SoundTypes"/>)와 모드뿐이고, 묶음의 상태는 그때그때 정한다. 오버레이의 AI 는
    /// 틱마다 그 맵을 읽어 판정에 반영한다. 목록의 줄은 Core 의 <see cref="SoundList"/> 가 정하고, 여기서는 글을 넣고 그린다.
    /// </summary>
    public partial class SoundsTab : UserControl
    {
        private const string LogTag = "sounds";

        /// <summary>종류 목록의 차례(안드로이드 SOUND_TYPES).</summary>
        private static readonly SoundType[] MenuOrder = { SoundType.Ambient, SoundType.Speech, SoundType.Danger };

        /// <summary>종류 단추와 열린 목록 사이.</summary>
        private const double MenuGap = 4;

        // 종류 단추에서 글자를 뺀 너비(왼쪽 여백 12, 점 12 + 6, 화살표 8 + 10, 오른쪽 여백 10)와 단추 너비의 범위.
        private const double PillChrome = 58;
        private const double PillMinWidth = 120;
        private const double PillMaxWidth = 260;

        private static readonly string ClassMapPath = System.IO.Path.Combine(AppContext.BaseDirectory, "AIModel", "yamnet_class_map.csv");

        /// <summary>
        /// 색인 캐시. 클래스 목록(AI 가 읽는 것과 같은 yamnet_class_map.csv)을 처음 쓸 때 화면 스레드 밖에서 한 번 읽는다.
        /// 읽지 못하면 null 이고 탭은 비어 있다(AI 도 같은 파일이 없으면 돌지 않는다).
        /// </summary>
        private static readonly Lazy<Task<SoundIndexCache?>> IndexCache = new(() => Task.Run(CreateIndexCache));

        private readonly ObservableCollection<SoundRow> _rows = new();
        private SoundIndex? _index;
        private string _requestedLanguage = "";
        private string _query = "";
        private SoundFilter _filter = SoundFilter.All;
        private KeptSounds _kept = KeptSounds.Empty;
        private Window? _window;

        public SoundsTab()
        {
            InitializeComponent();
            List.ItemsSource = _rows;
            FilterChips.SelectedIndex = (int)SoundFilter.All;
            ApplyTypeColors();
            UpdatePillWidth();

            // 숨어 있는 동안에도 언어가 바뀌면 색인을 미리 만든다. 탭은 런처와 함께 살므로 런처 창이 닫힐 때 끊는다.
            Loc.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // 런처가 뜰 때 바로 만들기 시작해, 탭을 처음 열 때는 보통 다 만들어져 있다. 그 전에는 목록이 비어 있다.
            LoadIndex();
        }

        /// <summary>
        /// 설정을 다시 읽는다. 런처가 설정 화면을 다시 채울 때(색 고르기, 전체 초기화, 오버레이에서 바꾼 설정) 부른다.
        /// 종류의 색은 거르기 칸·구역 머리·점에 바로 입혀진다.
        /// </summary>
        public void ReloadSettings()
        {
            ApplyTypeColors();
            Refresh();
        }

        private static SoundIndexCache? CreateIndexCache()
        {
            try
            {
                return new SoundIndexCache(YamnetClassMap.Load(ClassMapPath));
            }
            catch (Exception ex)
            {
                AppLog.Warn(LogTag, $"소리 목록을 읽지 못해 소리 탭을 비워 둡니다: {ClassMapPath}", ex);
                return null;
            }
        }

        // --- 색인 ---

        /// <summary>
        /// 지금 화면 언어의 색인을 얻어 목록을 그린다. 이름 521개를 읽고 다듬는 일은 화면 스레드 밖에서 하고, 그 언어로 만들어 둔 것이
        /// 있으면 바로 쓴다. 기다리는 사이 언어가 또 바뀌었으면 늦게 온 것은 버린다.
        /// </summary>
        private async void LoadIndex()
        {
            string language = SoundLanguages.Resolve(Loc.Instance.Code);
            _requestedLanguage = language;
            try
            {
                SoundIndexCache? cache = await IndexCache.Value;
                if (cache is null || language != _requestedLanguage) return;
                SoundIndex index = cache.CachedOrNull(language) ?? await cache.GetOrBuildAsync(language);
                if (language != _requestedLanguage) return;
                _index = index;
                Refresh();
            }
            catch (Exception ex)
            {
                AppLog.Warn(LogTag, $"소리 탭의 색인을 만들지 못했습니다({language}).", ex);
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdatePillWidth();
            LoadIndex();
        }

        // --- 목록 ---

        /// <summary>
        /// 목록을 지금 상태(찾는 말, 거르기, 남길 소리, 바꾼 종류, 모드)에 맞춘다. 키가 같은 줄은 고쳐 쓰므로, 종류를 고른 카드와
        /// 고급 모드 스위치는 키보드 초점과 자리를 잃지 않는다.
        /// </summary>
        /// <param name="scrollToResults">찾는 말이나 거르기를 바꾼 뒤면 결과를 찾기·거르기 바로 밑으로 올릴지 본다.</param>
        private void Refresh(bool scrollToResults = false)
        {
            if (_index is null) return;
            var items = SoundList.Build(_index, AppSettings.SoundTypes, _query, _filter, _kept, AppSettings.ClassifyAdvanced);
            KeyedList.Sync(_rows, items, item => item.Key, row => row.Key, CreateRow, UpdateRow);
            if (scrollToResults) ScrollToResults();
        }

        private SoundRow CreateRow(SoundListItem item)
        {
            SoundRow row = item switch
            {
                SoundListItem.Intro => new IntroRow(item.Key),
                SoundListItem.Changed => new ChangedRow(item.Key),
                SoundListItem.Empty => new EmptyRow(item.Key),
                SoundListItem.SectionHeading heading => new SectionRow(item.Key, heading.Type),
                SoundListItem.GroupHeading => new GroupRow(item.Key),
                SoundListItem.GroupCard or SoundListItem.Sound => new TypeRow(item.Key, item),
                _ => new EndRow(item.Key),
            };
            UpdateRow(row, item);
            return row;
        }

        private void UpdateRow(SoundRow row, SoundListItem item)
        {
            switch (row, item)
            {
                case (IntroRow intro, _):
                    intro.IsAdvanced = AppSettings.ClassifyAdvanced;
                    break;
                case (ChangedRow changed, SoundListItem.Changed count):
                    changed.CountText = Loc.Format("sounds.changed_count", count.Count);
                    changed.CanReset = count.Count > 0;
                    break;
                case (SectionRow section, SoundListItem.SectionHeading heading):
                    section.Title = TypeName(heading.Type);
                    break;
                case (GroupRow group, SoundListItem.GroupHeading heading):
                    FillGroup(group, heading);
                    break;
                case (TypeRow typeRow, SoundListItem.GroupCard card):
                    FillCard(typeRow, card);
                    break;
                case (TypeRow typeRow, SoundListItem.Sound sound):
                    FillSound(typeRow, sound);
                    break;
            }
        }

        private void FillGroup(GroupRow row, SoundListItem.GroupHeading heading)
        {
            string title = _index!.CardTitle(heading.Card);
            int count = heading.Card.Members.Count;
            row.Title = title;
            row.CountText = count.ToString(CultureInfo.InvariantCulture);
            row.AutomationName = Loc.Format("sounds.cd.group_header", title, count);
        }

        /// <summary>
        /// 기본 모드의 카드(안드로이드 GroupCard). 이름 밑에, 묶음째 바꿨으면 지금 종류를 기본 종류와 함께, 일부만 바꿨으면 그렇다고
        /// 강조색으로 적는다. 화면 읽어주기에는 이름·지금 종류·(바꿨으면) 기본 종류·소리 수·미리보기를 한 번에 읽힌다.
        /// </summary>
        private void FillCard(TypeRow row, SoundListItem.GroupCard item)
        {
            SoundCard card = item.Shown.Card;
            string title = _index!.CardTitle(card);
            SoundType? current = item.State is GroupState.Uniform uniform ? uniform.Type : null;
            string typeName = current is { } type ? TypeName(type) : Loc.Get("sounds.type.mixed");
            string defaultName = TypeName(card.DefaultType);
            var names = item.Preview.Names.Select(_index.LabelOf).ToList();
            int count = card.Members.Count;
            string spoken = string.Join(SoundRowText.SpokenSeparator, names);

            row.Item = item;
            row.Title = title;
            row.DotType = current;
            row.TypeLabel = typeName;
            row.Note = SoundRowText.NoteOf(item) switch
            {
                SoundRowNote.Mixed => Loc.Get("sounds.group.mixed_note"),
                SoundRowNote.GroupChanged => Loc.Format("sounds.group.changed_line", typeName, defaultName),
                _ => null,
            };
            // "+N" 은 마지막 이름 바로 뒤에 붙는다. 미리보기는 줄 수를 막지 않아 "+N" 이 잘리지 않는다(이름은 셋까지다).
            row.Preview = names.Count == 0
                ? null
                : string.Join(SoundRowText.ShownSeparator, names) + (item.Preview.More > 0 ? " " + SoundRowText.More(item.Preview.More) : "");
            row.AutomationName = SoundRowText.SpeechOf(item) switch
            {
                SoundRowSpeech.Sound => Loc.Format("sounds.cd.sound", title, typeName),
                SoundRowSpeech.SoundChanged => Loc.Format("sounds.cd.sound_changed", title, typeName, defaultName),
                SoundRowSpeech.Group => Loc.Format("sounds.cd.group", title, typeName, count, spoken),
                _ => Loc.Format("sounds.cd.group_changed", title, typeName, defaultName, count, spoken),
            };
        }

        /// <summary>고급 모드의 소리 한 줄(안드로이드 SoundTypeRow). 바꾼 소리는 기본 종류를 강조색으로 함께 보인다.</summary>
        private void FillSound(TypeRow row, SoundListItem.Sound item)
        {
            string title = _index!.LabelOf(item.Entry);
            string typeName = TypeName(item.Type);
            string defaultName = TypeName(item.Entry.DefaultType);

            row.Item = item;
            row.Title = title;
            row.DotType = item.Type;
            row.TypeLabel = typeName;
            row.Preview = null;
            row.Note = SoundRowText.NoteOf(item) == SoundRowNote.SoundChanged ? Loc.Format("sounds.changed_from", defaultName) : null;
            row.AutomationName = SoundRowText.SpeechOf(item) == SoundRowSpeech.Sound
                ? Loc.Format("sounds.cd.sound", title, typeName)
                : Loc.Format("sounds.cd.sound_changed", title, typeName, defaultName);
        }

        private static string TypeName(SoundType type) => type switch
        {
            SoundType.Speech => Loc.Get("sounds.type.speech"),
            SoundType.Danger => Loc.Get("sounds.type.danger"),
            _ => Loc.Get("sounds.type.ambient"),
        };

        // --- 찾기와 거르기 ---

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            BtnClearSearch.Visibility = SearchBox.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (SearchBox.Text == _query) return;
            _query = SearchBox.Text;
            OnQueryOrFilterChanged();
        }

        private void FilterChips_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var filter = (SoundFilter)Math.Max(0, FilterChips.SelectedIndex);
            if (filter == _filter) return;
            _filter = filter;
            OnQueryOrFilterChanged();
        }

        /// <summary>남겨 두던 소리를 지우고 결과를 찾기·거르기 바로 밑으로 올린다(안드로이드 ClassifyTab 의 LaunchedEffect).</summary>
        private void OnQueryOrFilterChanged()
        {
            _kept = KeptSounds.Empty;
            Refresh(scrollToResults: true);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            SearchBox.Focus();
        }

        /// <summary>Esc 는 찾는 말을 지운다. 비어 있으면 그대로 흘려보낸다.</summary>
        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && SearchBox.Text.Length > 0)
            {
                SearchBox.Clear();
                e.Handled = true;
            }
        }

        /// <summary>Ctrl+F 는 이 탭이 보일 때 찾기 칸으로 간다. 런처가 단축키를 받는 중이면 런처가 먼저 키를 가져간다.</summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled || !IsVisible) return;
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 결과가 찾기·거르기 바로 밑에서 시작하게 올린다. 찾는 말이 있으면 맨 위에 있어도 올리고, 거르기만 바꿨으면 소개 줄이 다
        /// 올라가 있을 때만 올린다(SoundList.ShouldScrollToResults). 결과가 짧으면 갈 수 있는 데까지만 간다.
        /// </summary>
        private void ScrollToResults()
        {
            if (List.Template.FindName("Scroller", List) is not ScrollViewer scroller) return;
            List.UpdateLayout();
            if (!SoundList.ShouldScrollToResults(_query, IntroScrolledAway(scroller))) return;

            if (ContainerAt(SoundList.ResultsIndex) is not { } results)
            {
                FindDescendant<VirtualizingStackPanel>(List)?.BringIndexIntoViewPublic(SoundList.ResultsIndex);
                List.UpdateLayout();
                if (ContainerAt(SoundList.ResultsIndex) is not { } realized) return;
                results = realized;
            }
            double top = results.TranslatePoint(new Point(), scroller).Y;
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + top);
        }

        /// <summary>소개 줄이 목록 위로 다 올라갔는지(안드로이드 firstVisibleItemIndex &gt; 1). 멀리 내려가 줄이 만들어져 있지 않아도 그렇다.</summary>
        private bool IntroScrolledAway(ScrollViewer scroller) =>
            ContainerAt(0) is not { IsVisible: true } intro || intro.TranslatePoint(new Point(0, intro.ActualHeight), scroller).Y <= 0.5;

        private FrameworkElement? ContainerAt(int index) => List.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T found) return found;
                if (FindDescendant<T>(child) is { } deeper) return deeper;
            }
            return null;
        }

        // --- 종류 고르기 ---

        private void TypeRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is TypeMenuButton button && button.DataContext is TypeRow row) OpenTypeMenu(button, row);
        }

        /// <summary>
        /// 줄의 종류 단추 밑에 종류 목록을 연다(안드로이드 TypeChooser). 지금 종류에 체크 표시를 하고, 기본 종류에는 "(기본)" 을 붙인다.
        /// 소리마다 종류가 다른 카드는 고른 것이 없다. 화살표로 옮기고 Enter 로 고르며 Esc 는 닫는다. 열면 지금 종류(없으면 첫 칸)에 초점이 간다.
        /// </summary>
        private void OpenTypeMenu(TypeMenuButton button, TypeRow row)
        {
            if (_index is null || button.IsMenuOpen) return;
            (SoundType? current, SoundType defaultType) = row.Item switch
            {
                SoundListItem.GroupCard card => (card.State is GroupState.Uniform uniform ? uniform.Type : (SoundType?)null, card.Shown.Card.DefaultType),
                SoundListItem.Sound sound => (sound.Type, sound.Entry.DefaultType),
                _ => ((SoundType?)null, SoundType.Ambient),
            };

            // 목록은 종류 단추 밑에서 단추 끝에 맞춰 연다. 단추가 줄 끝에 있어, 시작에 맞추면 목록이 런처 밖으로 삐져나간다.
            // 밑에 자리가 없으면 위로 연다. 오른쪽에서 왼쪽으로 쓰는 화면에서 WPF 는 이 점을 단추의 화면 오른쪽 끝에서 오른쪽으로 재어
            // 목록의 화면 왼쪽 끝을 둔다(아랍어로 재 보았다). 그래서 목록의 왼쪽 끝을 단추의 왼쪽 끝(줄의 끝 쪽)에 맞추려면 단추 너비만큼 뺀다.
            bool rightToLeft = Loc.Instance.IsRightToLeft;
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("TypeMenu"),
                PlacementTarget = button.Template.FindName("Pill", button) as UIElement ?? button,
                Placement = PlacementMode.Custom,
                CustomPopupPlacementCallback = (popup, target, _) =>
                {
                    double x = rightToLeft ? -target.Width : target.Width - popup.Width;
                    return new[]
                    {
                        new CustomPopupPlacement(new Point(x, target.Height + MenuGap), PopupPrimaryAxis.Vertical),
                        new CustomPopupPlacement(new Point(x, -popup.Height - MenuGap), PopupPrimaryAxis.Vertical),
                    };
                },
                FlowDirection = Loc.Instance.FlowDirection,
            };
            var itemStyle = (Style)FindResource("TypeMenuItem");
            var dotStyle = (Style)FindResource("TypeDot");
            MenuItem? focus = null;
            foreach (SoundType type in MenuOrder)
            {
                string name = TypeName(type);
                var item = new MenuItem
                {
                    Style = itemStyle,
                    Header = type == defaultType ? Loc.Format("sounds.type.default", name) : name,
                    // 체크 표시는 화면 읽어주기에도 "선택됨" 으로 읽힌다.
                    IsCheckable = true,
                    IsChecked = type == current,
                    // 목록은 이 탭 밖(팝업)이라 색을 직접 넣는다.
                    Icon = new System.Windows.Shapes.Ellipse { Style = dotStyle, Tag = type, Fill = TypeBrush(type) },
                };
                SoundType picked = type;
                // 목록이 다 닫힌 뒤에 고른다. 묻는 창을 목록이 닫히는 중에 띄우지 않게 한다.
                item.Click += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Pick(row, picked)));
                menu.Items.Add(item);
                if (type == current) focus = item;
            }

            menu.Opened += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => (focus ?? (MenuItem)menu.Items[0]).Focus()));
            menu.Closed += (_, _) =>
            {
                button.IsMenuOpen = false;
                button.OpenMenu = null;
            };
            button.OpenMenu = menu;
            button.IsMenuOpen = true;
            menu.IsOpen = true;
        }

        /// <summary>
        /// 종류를 골랐다. 기본 모드의 카드는 찾는 말·거르기와 상관없이 카드의 소리를 모두 바꾸고, 하나씩 바꿔 둔 소리의 종류가 바뀔
        /// 때만 먼저 묻는다(#374). 고급 모드의 줄은 그 소리 하나를 바꾼다. 보이던 소리는 거르기에서 빠져도 남긴다.
        /// </summary>
        private void Pick(TypeRow row, SoundType type)
        {
            if (_index is null) return;
            switch (row.Item)
            {
                case SoundListItem.GroupCard card:
                {
                    SoundCard group = card.Shown.Card;
                    IReadOnlyDictionary<string, string> overrides = AppSettings.SoundTypes;
                    if (SoundGroups.OverwritesOneByOne(group, overrides, type))
                    {
                        string message = Loc.Format("sounds.overwrite.message",
                            _index.CardTitle(group), SoundGroups.OverwriteCount(group, overrides, type), TypeName(type));
                        if (!ConfirmWindow.Ask(this, Loc.Get("sounds.overwrite.title"), message,
                                Loc.Get("sounds.overwrite.confirm"), Loc.Get("sounds.overwrite.cancel"))) return;
                    }
                    // 묻는 사이 목록이 다시 그려졌으면 지금 줄의 것을 쓴다.
                    var shown = (row.Item as SoundListItem.GroupCard ?? card).Shown.Shown;
                    _kept = _kept.Plus(_query, _filter, shown.Select(entry => entry.Name));
                    SetTypes(group.Members, type);
                    break;
                }
                case SoundListItem.Sound sound:
                    _kept = _kept.Plus(_query, _filter, new[] { sound.Entry.Name });
                    SetTypes(new[] { sound.Entry }, type);
                    break;
            }
        }

        /// <summary>
        /// 소리들을 한 종류로 바꾼다. 새 맵을 한 번 만들어 한 번 바꿔 끼우고 한 번 저장한다. AI 는 맵이 바뀌면 판정을 새로 시작하므로,
        /// 소리마다 따로 바꾸면 음악 묶음(140개)을 바꾸는 동안 반쯤 바뀐 맵을 거듭 읽는다.
        /// </summary>
        private void SetTypes(IEnumerable<SoundEntry> sounds, SoundType type)
        {
            IReadOnlyDictionary<string, string> current = AppSettings.SoundTypes;
            IReadOnlyDictionary<string, string> updated = SoundTypeOverrides.WithType(current, sounds, type);
            if (!ReferenceEquals(updated, current))
            {
                AppSettings.SoundTypes = updated;
                AppSettings.Save();
            }
            Refresh();
        }

        // --- 모두 되돌리기, 고급 모드 ---

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            bool hadFocus = sender is UIElement { IsKeyboardFocusWithin: true };
            if (!ConfirmWindow.Ask(this, Loc.Get("sounds.reset.title"), Loc.Get("sounds.reset.message"),
                    Loc.Get("sounds.reset.confirm"), Loc.Get("sounds.reset.cancel"))) return;

            AppSettings.SoundTypes = new Dictionary<string, string>();
            AppSettings.Save();
            // 남겨 두던 소리도 지운다. 되돌린 소리가 "바꾼 소리" 거르기에 남아 있지 않게 한다.
            _kept = KeptSounds.Empty;
            Refresh();
            // 누른 단추는 바꾼 소리가 없어 쓸 수 없게 되면서 초점을 잃는다. 키보드로 쓰던 사람이 길을 잃지 않게 찾기 칸으로 보낸다.
            // 묻는 창이 닫힌 뒤 런처가 앞의 초점을 되살리는 것보다 늦게 한다.
            if (hadFocus) Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => SearchBox.Focus()));
        }

        /// <summary>
        /// 고급 모드 스위치. 켜는 것은 묻지 않는다. 끌 때만, 기본 모드에서 묶음을 고르면 하나씩 바꾼 것이 덮어써질 묶음이 있는지
        /// 찾는 말·거르기와 상관없이 모든 묶음을 저장값으로 보고, 있으면 먼저 알린다. 모드만 바꾸고 바꾼 종류는 그대로다.
        /// Click 이 아니라 켜짐·꺼짐으로 받는다. 화면 읽어주기의 전환(Toggle)은 IsChecked 만 바꾸고 Click 을 일으키지 않는다.
        /// 저장값과 같아진 것(바인딩이 넣은 값, 되돌린 값)은 그냥 넘긴다.
        /// </summary>
        private void AdvancedSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox box) return;
            bool on = box.IsChecked == true;
            if (on == AppSettings.ClassifyAdvanced) return;

            if (!on && _index is not null && SoundGroups.AnyMixed(_index.Sections, AppSettings.SoundTypes))
            {
                // 묻는 동안은 고급 모드에 둔다. 꺼진 스위치를 다시 켜고(바인딩은 그대로 둔다) 묻는 창은 이 처리가 끝난 뒤에 띄운다.
                // 화면 읽어주기의 전환은 이 처리가 끝나야 돌아오는데, 여기서 창을 띄우면 창이 닫힐 때까지 화면 읽어주기가 이 앱을
                // 읽지 못해 창의 글도 단추도 읽히지 않았다.
                box.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                (box.DataContext as IntroRow)?.RestoreSwitch();
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(AskSwitchToBasic));
                return;
            }
            SetAdvanced(on);
        }

        /// <summary>하나씩 바꾼 묶음이 있는 채로 기본 모드로 돌아갈지 묻는다. 확인하면 모드만 바꾼다(바꾼 종류는 그대로다).</summary>
        private void AskSwitchToBasic()
        {
            if (!AppSettings.ClassifyAdvanced) return;
            if (!ConfirmWindow.Ask(this, Loc.Get("sounds.basic_warning.title"), Loc.Get("sounds.basic_warning.message"),
                    Loc.Get("sounds.basic_warning.confirm"), Loc.Get("sounds.basic_warning.cancel"))) return;
            SetAdvanced(false);
        }

        private void SetAdvanced(bool on)
        {
            AppSettings.ClassifyAdvanced = on;
            AppSettings.Save();
            Refresh();
        }

        // --- 색과 모양 ---

        /// <summary>설정의 종류 색을 점·거르기 칸·구역 머리에 입힌다. 흰색이나 검은색도 점의 옅은 테두리로 보인다.</summary>
        private void ApplyTypeColors()
        {
            Resources["SoundsTab.AmbientBrush"] = BrushOf(AppSettings.ColorAmbient, AppSettings.DefaultColorAmbient);
            Resources["SoundsTab.SpeechBrush"] = BrushOf(AppSettings.ColorSpeech, AppSettings.DefaultColorSpeech);
            Resources["SoundsTab.DangerBrush"] = BrushOf(AppSettings.ColorDanger, AppSettings.DefaultColorDanger);
        }

        private Brush TypeBrush(SoundType type) => (Brush)Resources[type switch
        {
            SoundType.Speech => "SoundsTab.SpeechBrush",
            SoundType.Danger => "SoundsTab.DangerBrush",
            _ => "SoundsTab.AmbientBrush",
        }];

        private static Brush BrushOf(string hex, string fallback)
        {
            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException)
            {
                color = (Color)ColorConverter.ConvertFromString(fallback);
            }
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 종류 단추의 너비를 화면 언어의 가장 긴 종류 이름(과 "일부 다름")에 맞춘다. 줄마다 단추 끝이 맞고, 이름 칸은 남는 만큼 넓다.
        /// 너무 긴 이름은 단추 안에서 줄을 바꾼다.
        /// </summary>
        private void UpdatePillWidth()
        {
            var family = TryFindResource(AppFonts.ResourceKey) as FontFamily ?? FontFamily;
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double widest = 0;
            foreach (string label in MenuOrder.Select(TypeName).Append(Loc.Get("sounds.type.mixed")))
            {
                var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 14, Brushes.White, pixelsPerDip);
                widest = Math.Max(widest, text.WidthIncludingTrailingWhitespace);
            }
            Resources["SoundsTab.PillWidth"] = Math.Clamp(Math.Ceiling(widest) + PillChrome, PillMinWidth, PillMaxWidth);
        }

        // --- 붙이고 떼기 ---

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 탭을 옮길 때마다 다시 붙는다. 창에는 한 번만 붙인다.
            var window = Window.GetWindow(this);
            if (!ReferenceEquals(window, _window))
            {
                if (_window is not null) _window.Closed -= OnWindowClosed;
                _window = window;
                if (_window is not null) _window.Closed += OnWindowClosed;
            }
            if (_window is not null)
            {
                _window.PreviewKeyDown -= Window_PreviewKeyDown;
                _window.PreviewKeyDown += Window_PreviewKeyDown;
            }
            ReloadSettings();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_window is not null) _window.PreviewKeyDown -= Window_PreviewKeyDown;
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            Loc.Instance.LanguageChanged -= OnLanguageChanged;
            if (_window is not null)
            {
                _window.PreviewKeyDown -= Window_PreviewKeyDown;
                _window.Closed -= OnWindowClosed;
            }
        }
    }
}
