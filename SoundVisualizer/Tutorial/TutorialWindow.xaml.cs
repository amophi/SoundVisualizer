// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SoundVisualizer.Ai;
using SoundVisualizer.Localization;

namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 처음 여는 사람에게 앱이 무엇을 하는지 보여 주는 창(Android TutorialScreen). 런처가 처음 열릴 때 한 번 저절로 띄우고, 홈의
    /// "튜토리얼 보기"로 다시 연다(LauncherWindow.Tutorial.cs). 시각화를 켜지는 않는다. 켜는 것은 홈의 실행 버튼이다.
    ///
    /// 쪽: 소리 → 방향 → 종류 → 켜고 끄는 방법. 앞의 세 쪽은 실제 오버레이 엔진과 사용자의 표현 모드·색으로 대본의 소리를 그린다
    /// (TutorialDemo, TutorialDemoRenderer). 안드로이드의 진동 쪽과 외부 사운드 모드(마이크)·권한 문구는 PC 에 없어 뺐다.
    ///
    /// - 넘기기: 아래 이전·다음 버튼, ←/→(오른쪽에서 왼쪽으로 쓰는 언어에서는 반대), PageUp/PageDown. Enter 는 다음(기본 버튼),
    ///   Esc 와 건너뛰기는 닫기다. 쪽이 바뀌면 키보드 초점을 새 쪽의 제목으로 옮겨 화면 읽어주기가 제목부터 읽는다.
    /// - 그림은 창이 보이고 멈추지 않았을 때만 CompositionTarget.Rendering 으로 움직인다. 최소화하거나(런처째 포함) 닫으면 멈춘다.
    ///   저절로 되풀이하는 움직임은 멈출 수 있어야 하므로(WCAG 2.2.2) 멈춤 버튼이 있고, Windows 에서 애니메이션을 꺼 두었으면
    ///   멈춘 채로 시작한다. 멈춘 그림은 그 쪽이 말하려는 장면이다.
    /// - 소리(Android #327): 소리 버튼을 누르면 그림이 그리는 소리를 실제 녹음으로 함께 낸다. 열 때마다 꺼진 채로 시작한다.
    ///   오버레이가 도는 동안은 내지 않는다. PC 의 캡처(WASAPI 루프백)는 이 앱이 내는 소리도 들어, 실제 오버레이와 AI 가 튜토리얼
    ///   소리에 반응하기 때문이다(안드로이드는 같은 이유로 외부 사운드 모드에서 내지 않는다).
    /// </summary>
    public partial class TutorialWindow : Window
    {
        // 쪽. 선언 순서가 보이는 순서다. 마지막 쪽은 소리 그림 대신 홈 그림과 켜고 끄는 단계를 보인다.
        private enum Page { Sound, Direction, Types, Start }

        private static readonly Page[] Pages = Enum.GetValues<Page>();

        /// <summary>그림을 새로 계산하는 가장 짧은 간격(밀리초). 고주사율 모니터에서도 1초에 60번쯤만 계산한다(안드로이드와 같다).</summary>
        private const double MinFrameMs = 16.4;

        /// <summary>한 프레임에 대본을 가장 많이 진행하는 시간(초). 창이 잠깐 멈췄다 돌아와도 그림이 크게 건너뛰지 않는다.</summary>
        private const double MaxFrameSec = 0.1;

        /// <summary>홈 그림에서 실행 버튼 둘레의 테두리가 한 번 퍼지는 시간(초)과 가장 크게 퍼지는 폭(DIP). 멈춰 둔 그림은 그 사이 한 겹을 보인다.</summary>
        private const double PulseSec = 1.6;
        private const double PulseGrow = 7;
        private const double StillPulse = 0.35;

        /// <summary>방향 쪽 소리 표시(기준 화면 DIP): 점의 반지름, 파문의 처음 반지름과 퍼지는 폭. 장면 안의 위치는 가로 20%·80%, 세로 56% 다.</summary>
        private const double SourceDotRadius = 20;
        private const double RippleStart = 28;
        private const double RippleGrow = 92;

        private readonly Dictionary<TutorialScene, TutorialDemo> _demos = new();
        private readonly Dictionary<TutorialScene, TutorialCueTimer> _cueTimers = new();
        private readonly TutorialDemoRenderer _renderer = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Border[] _dots;
        private readonly TextBlock[] _titles;
        private readonly FrameworkElement[] _pagePanels;

        private int _page;
        private bool _paused;
        private bool _soundOn;
        private TutorialSoundPlayer? _player;
        private bool _animating; // CompositionTarget.Rendering 에 붙어 있는지
        private double _lastFrameMs;
        private double _homeTime; // 홈 그림의 시각(초)
        private bool _closed;
        private Window? _watchedOwner;
        private SoundType? _legendLabel;

        // 사용자가 설정 탭에서 고른 색. 같은 값이면 다시 풀지 않는다.
        private string? _ambientHex, _speechHex, _dangerHex;
        private Color _ambient, _speech, _danger;

        public TutorialWindow()
        {
            InitializeComponent();
            _dots = new[] { Dot0, Dot1, Dot2, Dot3 };
            _titles = new[] { TitleSound, TitleDirection, TitleTypes, TitleStart };
            _pagePanels = new FrameworkElement[] { PageSound, PageDirection, PageTypes, PageStart };

            // Windows 의 "애니메이션 효과"를 꺼 두었으면 멈춘 채로 시작한다. 소리는 열 때마다 꺼진 채로 시작한다.
            _paused = !SystemParameters.ClientAreaAnimation;
            _soundOn = false;

            Loaded += OnLoaded;
            Closed += OnClosed;
            IsVisibleChanged += (s, e) => UpdateAnimation();
            StateChanged += (s, e) => UpdateAnimation();
            // 멈춘 홈 그림은 배치가 끝나야 테두리 크기를 안다.
            StartPulse.SizeChanged += (s, e) => { if (Pages[_page] == Page.Start) DrawHome(); };
            Loc.Instance.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// 사용자가 닫았는지(확인, 건너뛰기, Esc, Alt+F4). 런처가 이것으로 튜토리얼을 본 것으로 저장한다.
        /// 앱을 끝내며 함께 닫힌 것은 치지 않는다. 안드로이드도 튜토리얼을 닫지 않고 나가면 다음에 다시 띄운다.
        /// </summary>
        public bool Finished { get; private set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 주인(런처)을 최소화하면 이 창도 함께 숨으므로 그림을 멈춘다.
            _watchedOwner = Owner;
            if (_watchedOwner != null) _watchedOwner.StateChanged += OnOwnerStateChanged;
            ApplyCodeTexts();
            ShowPage();
            UpdateAnimation();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _closed = true;
            UpdateAnimation();
            Loc.Instance.LanguageChanged -= OnLanguageChanged;
            if (_watchedOwner != null) _watchedOwner.StateChanged -= OnOwnerStateChanged;
            _player?.Dispose();
            _player = null;
        }

        private void OnOwnerStateChanged(object? sender, EventArgs e) => UpdateAnimation();

        private void OnLanguageChanged(object? sender, EventArgs e) => ApplyCodeTexts();

        // ==========================================
        // 쪽 넘기기
        // ==========================================

        private static TutorialScene? SceneOf(Page page) => page switch
        {
            Page.Sound => TutorialScene.Sound,
            Page.Direction => TutorialScene.Direction,
            Page.Types => TutorialScene.Types,
            _ => null,
        };

        /// <summary>그 쪽으로 간다. 처음과 끝에서 더 가지 않는다(화살표로 창을 닫지 않는다).</summary>
        private void GoTo(int index)
        {
            index = Math.Clamp(index, 0, Pages.Length - 1);
            if (index == _page) return;
            // 나던 소리를 끊는다. 돌아오면 그 쪽의 그림은 멈췄던 자리에서 이어 간다.
            StopSounds();
            _page = index;
            ShowPage();
        }

        private void ShowPage()
        {
            var page = Pages[_page];
            for (int i = 0; i < _pagePanels.Length; i++) _pagePanels[i].Visibility = i == _page ? Visibility.Visible : Visibility.Collapsed;

            bool scenePage = SceneOf(page) != null;
            DemoFrame.Visibility = scenePage ? Visibility.Visible : Visibility.Collapsed;
            HomeFrame.Visibility = scenePage ? Visibility.Collapsed : Visibility.Visible;
            // 마지막 쪽은 단계 글이 길어 그림을 작게 두고 글에 자리를 더 준다(안드로이드와 같다).
            IllustrationArea.Height = scenePage ? DemoFrame.Height : HomeFrame.Height;

            bool first = _page == 0;
            bool last = _page == Pages.Length - 1;
            // 숨겨도 자리는 남겨 쪽마다 줄이 흔들리지 않게 한다. 숨긴 버튼은 누를 수도, 초점을 받을 수도 없다.
            BtnPrevious.Visibility = first ? Visibility.Hidden : Visibility.Visible;
            BtnSkip.Visibility = last ? Visibility.Hidden : Visibility.Visible;
            // "확인"은 홈의 실행과 헷갈리지 않게 "시작"이라 하지 않는다(안드로이드 tutorial_done).
            BtnNext.SetBinding(ContentControl.ContentProperty, Loc.BindingFor(last ? "tutorial.done" : "tutorial.next"));
            for (int i = 0; i < _dots.Length; i++)
            {
                bool active = i == _page;
                _dots[i].Width = active ? 22 : 8;
                _dots[i].SetResourceReference(Border.BackgroundProperty, active ? "AccentBrush" : "SecondaryTextBrush");
            }
            TxtPageStatus.Text = Loc.Format("tutorial.page_status", _page + 1, Pages.Length);

            TextScroll.ScrollToTop();
            DrawCurrent();
            FocusTitle();
        }

        /// <summary>
        /// 새 쪽의 제목으로 키보드 초점을 옮긴다. 초점이 "다음"에 남으면 화면 읽어주기로 새 쪽을 들으려고 거꾸로 훑어야 한다.
        /// 막 보이게 한 제목은 배치 전이라 초점을 받지 못하므로 배치가 끝난 뒤에 옮긴다.
        /// </summary>
        private void FocusTitle()
        {
            var title = _titles[_page];
            Dispatcher.InvokeAsync(() =>
            {
                if (!_closed && title.IsVisible) Keyboard.Focus(title);
            }, DispatcherPriority.Input);
        }

        private void Finish()
        {
            Finished = true;
            Close();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e) => Finish();

        private void BtnPrevious_Click(object sender, RoutedEventArgs e) => GoTo(_page - 1);

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_page == Pages.Length - 1) Finish();
            else GoTo(_page + 1);
        }

        /// <summary>
        /// ←/→ 와 PageUp/PageDown 으로 넘기고, Esc 로 닫는다. 오른쪽에서 왼쪽으로 쓰는 언어(아랍어)는 쪽이 오른쪽에서 왼쪽으로 놓이므로
        /// ← 가 다음이다(이전 버튼의 화살표 방향, 쪽 표시 점의 순서와 같다). 위아래 화살표는 긴 글을 굴리는 데 남겨 둔다.
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int forward = Loc.Instance.IsRightToLeft ? -1 : 1;
            bool plain = Keyboard.Modifiers == ModifierKeys.None;
            switch (e.Key)
            {
                case Key.Escape:
                    Finish();
                    break;
                // Alt+F4 도 사용자가 닫은 것이다. 그냥 두면 Windows 가 닫아 Finished 를 남기지 못한다.
                case Key.System when e.SystemKey == Key.F4:
                    Finish();
                    break;
                case Key.PageDown when plain:
                    GoTo(_page + 1);
                    break;
                case Key.PageUp when plain:
                    GoTo(_page - 1);
                    break;
                case Key.Right when plain:
                    GoTo(_page + forward);
                    break;
                case Key.Left when plain:
                    GoTo(_page - forward);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        /// <summary>창 아무 데나 끌어 옮긴다(런처와 같다). 버튼은 누름을 먼저 받으므로 여기로 오지 않는다.</summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 누름이 이미 끝났다. 옮길 것이 없다.
            }
        }

        private void TextScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 아래에 가려진 글이 있으면 끝을 흐리게 덮어 이어진다는 것을 보인다. 누름은 받지 않는 그림이라 굴리는 데 방해되지 않는다.
            TextFade.Visibility = TextScroll.VerticalOffset < TextScroll.ScrollableHeight - 0.5 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==========================================
        // 멈춤과 소리
        // ==========================================

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            _paused = !_paused;
            UpdatePauseButton();
            UpdateAnimation();
        }

        private void BtnSound_Click(object sender, RoutedEventArgs e)
        {
            _soundOn = !_soundOn;
            if (_soundOn)
            {
                // 장치는 켤 때 연다. 출력 장치가 없으면 이유를 남기고(AppLog) 그림만 그대로 돈다. 버튼은 켠 채로 둔다.
                _player ??= TutorialSoundPlayer.TryOpen();
                // 켠 순간부터 센다. 지난 소리를 몰아 내지 않는다.
                foreach (var timer in _cueTimers.Values) timer.Stop();
            }
            else
            {
                StopSounds();
                _player?.Dispose();
                _player = null;
            }
            UpdateSoundButton();
        }

        private void UpdatePauseButton()
        {
            // 아이콘은 누르면 할 일을(멈춘 그림이면 재생), 이름도 누르면 할 일을 말한다.
            IconPause.Data = (Geometry)FindResource(_paused ? "Tutorial.Icon.Play" : "Tutorial.Icon.Pause");
            AutomationProperties.SetName(BtnPause, Loc.Get(_paused ? "tutorial.play" : "tutorial.pause"));
        }

        private void UpdateSoundButton()
        {
            // 아이콘은 지금 상태(꺼짐이면 소리 끈 스피커)를, 이름은 누르면 할 일을 말한다. 켜 두면 강조색으로 보인다(안드로이드와 같다).
            IconSound.Data = (Geometry)FindResource(_soundOn ? "Tutorial.Icon.SoundOn" : "Tutorial.Icon.SoundOff");
            AutomationProperties.SetName(BtnSound, Loc.Get(_soundOn ? "tutorial.sound_off" : "tutorial.sound_on"));
            if (_soundOn) BtnSound.SetResourceReference(ForegroundProperty, "AccentBrush");
            else BtnSound.ClearValue(ForegroundProperty);
        }

        /// <summary>나던 소리를 끊고, 쪽마다 소리 박자를 처음부터 다시 세게 한다.</summary>
        private void StopSounds()
        {
            foreach (var timer in _cueTimers.Values) timer.Stop();
            _player?.StopAll();
        }

        private TutorialCueTimer CueTimerFor(TutorialScene scene)
        {
            if (!_cueTimers.TryGetValue(scene, out var timer))
            {
                timer = new TutorialCueTimer(TutorialScript.Cues(scene), scene.CycleSec(),
                    canPlay: () => _player != null && !IsOverlayRunning(),
                    play: cue => _player?.Play(cue),
                    cancel: () => _player?.StopAll());
                _cueTimers[scene] = timer;
            }
            return timer;
        }

        /// <summary>오버레이가 떠 있는지. 떠 있으면 그 캡처가 튜토리얼 소리를 들으므로 소리를 내지 않는다.</summary>
        private static bool IsOverlayRunning() => Application.Current.Windows.OfType<MainWindow>().Any();

        // ==========================================
        // 그림
        // ==========================================

        /// <summary>
        /// 창이 보이고 멈추지 않았을 때만 프레임마다 그림을 움직인다. 최소화하거나 런처째 최소화하거나 닫으면 멈추고, 나던 소리도 끊는다.
        /// </summary>
        private void UpdateAnimation()
        {
            bool run = !_closed && !_paused && IsVisible && WindowState != WindowState.Minimized
                && (_watchedOwner == null || _watchedOwner.WindowState != WindowState.Minimized);
            if (run == _animating) return;
            _animating = run;
            if (run)
            {
                _lastFrameMs = _clock.Elapsed.TotalMilliseconds;
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
                StopSounds();
                // 홈 그림의 테두리는 멈춘 모양으로 둔다. 소리 그림은 멈춘 자리 그대로다.
                if (!_closed) DrawCurrent();
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            double elapsedMs = now - _lastFrameMs;
            if (elapsedMs < MinFrameMs) return;
            _lastFrameMs = now;
            double dt = Math.Min(elapsedMs / 1000.0, MaxFrameSec);

            if (SceneOf(Pages[_page]) is { } scene)
            {
                // 멈춘 시각(처음이면 그 쪽이 말하려는 장면)에서 이어 간다.
                var demo = DemoFor(scene);
                double t = demo.TimeSec + dt;
                demo.Step(t);
                DrawDemo(demo);
                // 그림과 같은 프레임에 소리를 낸다.
                if (_soundOn) CueTimerFor(scene).OnFrame(t);
            }
            else
            {
                _homeTime += dt;
                DrawHome();
            }
        }

        /// <summary>
        /// 그 쪽의 그림 계산. 처음에는 그 쪽이 말하려는 장면까지 미리 돌려 둔다(멈춘 그림, 애니메이션을 꺼 둔 사람에게도 비지 않게).
        /// 쪽을 넘겼다 돌아오면 멈췄던 자리에서 이어 간다. 표현 모드가 바뀌었으면(오버레이의 F3) 그 모드로 다시 만든다.
        /// </summary>
        private TutorialDemo DemoFor(TutorialScene scene)
        {
            int mode = AppSettings.VisualMode;
            if (!_demos.TryGetValue(scene, out var demo) || demo.VisualMode != mode)
            {
                demo = new TutorialDemo(scene, mode);
                _demos[scene] = demo;
            }
            return demo;
        }

        /// <summary>지금 쪽의 그림을 지금 상태 그대로 한 번 그린다(쪽을 보일 때, 멈출 때).</summary>
        private void DrawCurrent()
        {
            if (SceneOf(Pages[_page]) is { } scene) DrawDemo(DemoFor(scene));
            else DrawHome();
        }

        private void DrawDemo(TutorialDemo demo)
        {
            RefreshColors();
            // 색은 그림이 지금 그리는 소리의 종류로 고른다(오버레이와 같다).
            _renderer.Draw(demo, DemoShape, ColorFor(demo.Label));
            DrawSource(demo);
            if (demo.Scene == TutorialScene.Types) UpdateLegend(demo.Label);
        }

        /// <summary>방향 쪽: 장면 안에서 소리가 나는 자리에 점을 띄우고, 박자마다 파문을 퍼뜨린다. 쉬는 동안과 다른 쪽에서는 숨긴다.</summary>
        private void DrawSource(TutorialDemo demo)
        {
            int side = TutorialScript.SourceSide(demo.Scene, demo.TimeSec);
            if (side == 0)
            {
                SourceDot.Visibility = Visibility.Collapsed;
                SourceRipple.Visibility = Visibility.Collapsed;
                return;
            }

            double cx = TutorialDemo.ReferenceWidth * (side < 0 ? 0.2 : 0.8);
            double cy = TutorialDemo.ReferenceHeight * 0.56;
            double level = TutorialScript.SourceLevel(demo.Scene, demo.TimeSec);
            SourceDot.Opacity = Math.Min(1, 0.5 + 0.5 * level);
            Canvas.SetLeft(SourceDot, cx - SourceDotRadius);
            Canvas.SetTop(SourceDot, cy - SourceDotRadius);
            SourceDot.Visibility = Visibility.Visible;

            double ripple = TutorialScript.SourceRipple(demo.Scene, demo.TimeSec);
            if (ripple < 0)
            {
                SourceRipple.Visibility = Visibility.Collapsed;
                return;
            }
            double r = RippleStart + RippleGrow * ripple;
            SourceRipple.Width = 2 * r;
            SourceRipple.Height = 2 * r;
            Canvas.SetLeft(SourceRipple, cx - r);
            Canvas.SetTop(SourceRipple, cy - r);
            SourceRipple.Opacity = 0.6 * (1 - ripple);
            SourceRipple.Visibility = Visibility.Visible;
        }

        /// <summary>홈 그림: 실행 버튼 둘레로 테두리가 퍼진다. 멈춰 둔 그림에서는 한 겹을 멈춰 둔다.</summary>
        private void DrawHome()
        {
            double p = _animating ? (_homeTime % PulseSec) / PulseSec : StillPulse;
            double grow = PulseGrow * p;
            double w = StartPulse.ActualWidth;
            double h = StartPulse.ActualHeight;
            if (w > 0 && h > 0)
            {
                StartPulseScale.ScaleX = (w + 2 * grow) / w;
                StartPulseScale.ScaleY = (h + 2 * grow) / h;
            }
            StartPulse.Opacity = 0.8 * (1 - p);
        }

        /// <summary>
        /// 종류 쪽의 범례에서 지금 그림에 나오는 종류를 밝힌다. 종류가 바뀔 때만 바꾼다. 눈으로만 보는 표시라 화면 읽어주기에 알리지
        /// 않는다(알리면 2초마다 다시 읽는다).
        /// </summary>
        private void UpdateLegend(SoundType label)
        {
            if (_legendLabel == label) return;
            _legendLabel = label;
            SetLegendActive(LegendAmbient, label == SoundType.Ambient);
            SetLegendActive(LegendSpeech, label == SoundType.Speech);
            SetLegendActive(LegendDanger, label == SoundType.Danger);
        }

        private static void SetLegendActive(Border item, bool active)
        {
            if (active) item.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            else item.ClearValue(Border.BackgroundProperty);
        }

        /// <summary>
        /// 사용자 색을 읽는다. 설정 탭에서 보는 색과 그림·범례의 색이 달라지면 안 된다. 튜토리얼을 연 채 오버레이 설정에서 색을 바꿀 수도
        /// 있어 그릴 때마다 확인한다(값이 같으면 다시 풀지 않는다).
        /// </summary>
        private void RefreshColors()
        {
            if (Refresh(AppSettings.ColorAmbient, ref _ambientHex, ref _ambient)) DotAmbient.Fill = Frozen(_ambient);
            if (Refresh(AppSettings.ColorSpeech, ref _speechHex, ref _speech)) DotSpeech.Fill = Frozen(_speech);
            if (Refresh(AppSettings.ColorDanger, ref _dangerHex, ref _danger)) DotDanger.Fill = Frozen(_danger);
        }

        private static bool Refresh(string hex, ref string? cachedHex, ref Color color)
        {
            if (cachedHex == hex) return false;
            cachedHex = hex;
            color = ParseColor(hex);
            return true;
        }

        private static Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch (Exception)
            {
                // 오버레이와 같이 읽을 수 없는 색은 흰색으로 그린다.
                return Colors.White;
            }
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private Color ColorFor(SoundType type) => type switch
        {
            SoundType.Danger => _danger,
            SoundType.Speech => _speech,
            _ => _ambient,
        };

        // ==========================================
        // 글
        // ==========================================

        /// <summary>
        /// 바인딩이 아니라 코드가 넣는 글: 쪽 표시, 소리·멈춤 버튼의 이름, 버튼·단축키 이름을 끼워 넣는 단계 글.
        /// 버튼과 단축키 이름은 그 화면의 글자를 그대로 끼워 넣어, 번역이 바뀌거나 단축키를 바꿔도 튜토리얼과 화면이 어긋나지 않는다.
        /// </summary>
        private void ApplyCodeTexts()
        {
            TxtStep1.Text = Loc.Format("help.start.2", Loc.Get("launcher.home.start"));
            TxtStep3.Text = Loc.Format("tutorial.start.hotkeys",
                LauncherWindow.GetKeysName(AppSettings.StereoUpmixKeyBind),
                LauncherWindow.GetKeysName(AppSettings.VisualModeKeyBind),
                LauncherWindow.GetKeysName(AppSettings.EditModeKeyBind));
            TxtStep4.Text = Loc.Format("help.start.4", Loc.Get("launcher.home.stop"));
            TxtPageStatus.Text = Loc.Format("tutorial.page_status", _page + 1, Pages.Length);
            UpdatePauseButton();
            UpdateSoundButton();
        }
    }
}
