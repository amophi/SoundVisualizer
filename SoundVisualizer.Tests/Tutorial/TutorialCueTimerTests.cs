// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Tutorial;

namespace SoundVisualizer.Tests.Tutorial;

/// <summary>
/// 튜토리얼 소리의 박자(Android TutorialSoundsTest, #327). 그림이 프레임마다 시각을 넘기면 대본의 소리를 한 번씩, 제때 낸다.
/// 창이 멈췄다 돌아와 시각이 건너뛰어도 지난 소리를 몰아 내지 않는다.
/// </summary>
public class TutorialCueTimerTests
{
    private readonly List<(double At, TutorialCue Cue)> _played = new();
    private int _cancels;
    private bool _allowed = true;
    private double _now;
    private readonly TutorialScene _scene = TutorialScene.Types;
    private readonly TutorialCueTimer _timer;

    public TutorialCueTimerTests()
    {
        _timer = new TutorialCueTimer(
            TutorialScript.Cues(_scene), _scene.CycleSec(),
            canPlay: () => _allowed,
            play: cue => _played.Add((_now, cue)),
            cancel: () => _cancels++);
    }

    /// <summary><paramref name="fromSec"/> 부터 <paramref name="toSec"/> 앞까지 <paramref name="fps"/> 로 프레임을 넘긴다.</summary>
    private void Frames(double fromSec, double toSec, int fps = 60)
    {
        for (int i = 0; fromSec + i / (double)fps < toSec; i++)
        {
            _now = fromSec + i / (double)fps;
            _timer.OnFrame(_now);
        }
    }

    [Fact]
    public void 바퀴마다_대본의_소리를_한_번씩_제때_낸다()
    {
        Frames(0, _scene.CycleSec() * 2);

        var cues = TutorialScript.Cues(_scene);
        Assert.Equal(cues.Concat(cues), _played.Select(p => p.Cue));
        foreach (var (at, cue) in _played)
        {
            // 첫 바퀴인지 둘째 바퀴인지는 낸 시각으로 안다. 낸 시각은 대본 시각에서 한 프레임 안이다.
            double expected = cue.AtSec + (at >= _scene.CycleSec() ? _scene.CycleSec() : 0);
            Assert.InRange(at - expected, 0, 1.0 / 60 + 1e-4);
        }
    }

    [Fact]
    public void 첫_프레임_0초의_소리도_낸다()
    {
        _timer.OnFrame(0);
        Assert.Equal(TutorialClip.Birds, Assert.Single(_played).Cue.Clip);
    }

    [Fact]
    public void 시각이_건너뛰면_지난_소리는_내지_않는다()
    {
        // 창이 잠깐 멈췄다 돌아오면 그림의 시각이 그만큼 건너뛴다. 그사이의 대화·경적을 한꺼번에 내지 않는다.
        Frames(0, 1);
        _now = 5.5;
        _timer.OnFrame(_now);
        Assert.Equal(new[] { TutorialClip.Birds }, _played.Select(p => p.Cue.Clip));

        // 그다음부터는 다시 제때 낸다(5.9초의 경적, 다음 바퀴의 새소리).
        Frames(5.5 + 1.0 / 60, _scene.CycleSec() + 0.1);
        Assert.Equal(new[] { TutorialClip.Birds, TutorialClip.Honk, TutorialClip.Birds }, _played.Select(p => p.Cue.Clip));
    }

    [Fact]
    public void 내면_안_될_때_시작한_소리는_건너뛰고_멈췄다_다시_움직이면_처음부터_센다()
    {
        _allowed = false;
        Frames(0, 1);
        Assert.Empty(_played);

        _allowed = true;
        _timer.Stop();
        Assert.Equal(1, _cancels); // 멈출 때 나던 소리를 멈춘다

        Frames(0, 0.1);
        Assert.Equal(TutorialClip.Birds, Assert.Single(_played).Cue.Clip);
    }

    [Fact]
    public void 멈춘_자리에서_다시_움직이면_그_뒤의_소리부터_낸다()
    {
        // 쪽을 넘겼다 돌아오거나 멈춤을 풀면 그림은 멈춘 시각에서 이어 간다. 그 앞의 소리는 늦었으므로 내지 않는다.
        Frames(0, 3);
        _timer.Stop();
        _played.Clear();

        Frames(3, 4.6);
        Assert.Equal(new[] { TutorialClip.Honk }, _played.Select(p => p.Cue.Clip)); // 4.5초의 첫 경적
    }

    [Fact]
    public void 시각이_그대로거나_뒤로_가면_아무것도_내지_않는다()
    {
        _timer.OnFrame(0);
        _timer.OnFrame(0);
        _timer.OnFrame(-0.5);
        Assert.Single(_played);
    }
}
