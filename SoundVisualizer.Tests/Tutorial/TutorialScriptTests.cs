// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;
using SoundVisualizer.Tutorial;

namespace SoundVisualizer.Tests.Tutorial;

/// <summary>
/// 튜토리얼 그림의 소리 대본(Android TutorialScriptTest). 진동 쪽은 PC 에 없어 그 시험도 옮기지 않았다.
///
/// 그림은 앱의 오버레이 엔진이 그대로 그리므로, 대본이 말하려는 것(왼쪽 소리는 왼쪽이 깊다, 위협음은 위협음이다)이 실제 엔진을
/// 거쳐서도 그렇게 그려지는지 본다(<see cref="TutorialDemoTests"/>). 여기서는 대본 자체를 본다.
/// </summary>
public class TutorialScriptTests
{
    private static readonly TutorialScene[] Scenes = Enum.GetValues<TutorialScene>();

    [Fact]
    public void 피크는_늘_0에서_1_사이다()
    {
        Span<float> lr = stackalloc float[2];
        foreach (var scene in Scenes)
        {
            for (double t = -scene.CycleSec(); t < scene.CycleSec() * 2; t += 0.01)
            {
                TutorialScript.Peaks(scene, t, lr);
                Assert.True(lr[0] is >= 0f and <= 1f, $"{scene} t={t} 왼쪽 {lr[0]}");
                Assert.True(lr[1] is >= 0f and <= 1f, $"{scene} t={t} 오른쪽 {lr[1]}");
            }
        }
    }

    [Fact]
    public void 엔진이_받는_프레임은_앞_좌우에만_대본을_넣는다()
    {
        // 사운드 모드 2.0 은 앞 좌우(FL, FR)만 읽어 둘레로 펼친다. 다른 채널에 남은 값이 있으면 5.1·7.1 처럼 그려질 수 있다.
        var frame = Enumerable.Repeat(0.9f, VisualizerEngine.Channels).ToArray();
        Span<float> lr = stackalloc float[2];
        foreach (var scene in Scenes)
        {
            TutorialScript.PeakFrame(scene, 1.0, frame);
            TutorialScript.Peaks(scene, 1.0, lr);
            Assert.Equal(lr[0], frame[0]);
            Assert.Equal(lr[1], frame[1]);
            Assert.All(frame[2..], value => Assert.Equal(0f, value));
        }
    }

    [Fact]
    public void 방향_쪽은_왼쪽_소리_다음에_오른쪽_소리가_난다()
    {
        Span<float> lr = stackalloc float[2];
        var scene = TutorialScene.Direction;

        TutorialScript.Peaks(scene, 1.0, lr);
        Assert.True(lr[0] > lr[1] * 3, "왼쪽 구간인데 오른쪽이 더 크다");
        Assert.Equal(-1, TutorialScript.SourceSide(scene, 1.0));

        TutorialScript.Peaks(scene, 3.0, lr);
        Assert.True(lr[1] > lr[0] * 3, "오른쪽 구간인데 왼쪽이 더 크다");
        Assert.Equal(1, TutorialScript.SourceSide(scene, 3.0));

        TutorialScript.Peaks(scene, 2.0, lr);
        Assert.Equal(0f, lr[0] + lr[1]); // 쉬는 구간
        Assert.Equal(0, TutorialScript.SourceSide(scene, 2.0));
        Assert.Equal(-1, TutorialScript.SourceRipple(scene, 2.0));
    }

    [Fact]
    public void 소리_표시는_방향_쪽에만_있다()
    {
        foreach (var scene in Scenes.Where(s => s != TutorialScene.Direction))
        {
            Assert.Equal(0, TutorialScript.SourceSide(scene, 1.0));
            Assert.Equal(0, TutorialScript.SourceLevel(scene, 1.0));
            Assert.Equal(-1, TutorialScript.SourceRipple(scene, 1.0));
        }
        // 방향 쪽의 파문은 박자마다 0 에서 1 로 퍼진다.
        double ripple = TutorialScript.SourceRipple(TutorialScene.Direction, 0.25);
        Assert.InRange(ripple, 0.49, 0.51);
    }

    [Fact]
    public void 종류_쪽은_환경음_대화음_위협음_순서로_바뀐다()
    {
        var scene = TutorialScene.Types;
        Assert.Equal(SoundType.Ambient, TutorialScript.Label(scene, 1));
        Assert.Equal(SoundType.Speech, TutorialScript.Label(scene, 3));
        Assert.Equal(SoundType.Danger, TutorialScript.Label(scene, 5));
        Assert.Equal(SoundType.Ambient, TutorialScript.Label(scene, scene.CycleSec() + 1)); // 한 바퀴 돌면 처음부터
    }

    [Fact]
    public void 첫_쪽과_방향_쪽은_모두_환경음이다()
    {
        foreach (var scene in new[] { TutorialScene.Sound, TutorialScene.Direction })
        {
            for (double t = 0; t < scene.CycleSec(); t += 0.05) Assert.Equal(SoundType.Ambient, TutorialScript.Label(scene, t));
        }
    }

    [Fact]
    public void 소리는_그림이_그_소리를_그리는_순간에_같은_종류로_같은_쪽에서_난다()
    {
        // 소리를 켜면 대본의 박자에 맞춰 실제 녹음을 낸다(Android #327). 듣는 것과 보는 것이 어긋나면 처음 보는 사람을 헷갈리게 한다.
        Span<float> lr = stackalloc float[2];
        foreach (var scene in Scenes)
        {
            var cues = TutorialScript.Cues(scene);
            Assert.NotEmpty(cues);
            foreach (var cue in cues)
            {
                string what = $"{scene} {cue.AtSec}초의 {cue.Clip}";
                Assert.True(cue.AtSec >= 0 && cue.AtSec < scene.CycleSec(), what + ": 한 바퀴 안이어야 한다");
                // 소리가 시작하고 조금 뒤(0.05초)의 그림
                double t = cue.AtSec + 0.05;
                Assert.Equal(cue.Clip.Label(), TutorialScript.Label(scene, t));
                TutorialScript.Peaks(scene, t, lr);
                Assert.True(Math.Max(lr[0], lr[1]) > 0.3f, what + ": 그림이 조용하다");
                if (cue.Left != cue.Right)
                {
                    Assert.True(cue.Left > cue.Right == lr[0] > lr[1], what + ": 그림이 기운 쪽과 다르다");
                }
            }
        }
    }

    [Fact]
    public void 종류_쪽은_새소리_대화_경적_세_번이다()
    {
        Assert.Equal(
            new[] { TutorialClip.Birds, TutorialClip.Talk, TutorialClip.Honk, TutorialClip.Honk, TutorialClip.Honk },
            TutorialScript.Cues(TutorialScene.Types).Select(c => c.Clip));
        // 방향 쪽은 왼쪽 네 번, 오른쪽 네 번
        var direction = TutorialScript.Cues(TutorialScene.Direction);
        Assert.Equal(4, direction.Count(c => c.Left > c.Right));
        Assert.Equal(4, direction.Count(c => c.Right > c.Left));
        // 첫 쪽은 1초마다 한 번, 왼쪽과 오른쪽을 번갈아
        Assert.Equal(new[] { 0.0, 1.0, 2.0, 3.0 }, TutorialScript.Cues(TutorialScene.Sound).Select(c => c.AtSec));
    }

    [Fact]
    public void 소리_조각의_종류()
    {
        Assert.Equal(SoundType.Ambient, TutorialClip.Chirp.Label());
        Assert.Equal(SoundType.Ambient, TutorialClip.Birds.Label());
        Assert.Equal(SoundType.Speech, TutorialClip.Talk.Label());
        Assert.Equal(SoundType.Danger, TutorialClip.Honk.Label());
    }

    [Fact]
    public void 대본_시각은_음수와_한_바퀴를_넘는_값도_한_바퀴_안으로_접는다()
    {
        var scene = TutorialScene.Direction;
        Assert.Equal(1.0, TutorialScript.LoopTime(scene, 1.0), 9);
        Assert.Equal(1.0, TutorialScript.LoopTime(scene, 1.0 + scene.CycleSec() * 3), 9);
        Assert.Equal(scene.CycleSec() - 0.5, TutorialScript.LoopTime(scene, -0.5), 9);
    }
}
