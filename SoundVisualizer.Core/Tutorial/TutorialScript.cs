// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;
using SoundVisualizer.Rendering;

namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 튜토리얼 그림 한 쪽의 소리 대본(Android TutorialScene). 쪽마다 <see cref="TutorialScript.CycleSec"/> 초마다 처음부터 되풀이한다.
    /// 안드로이드의 진동 쪽은 PC 에 진동이 없어 옮기지 않았다.
    /// </summary>
    public enum TutorialScene
    {
        /// <summary>무엇을 하는 앱인지. 1초마다 한 번씩 소리가 쿵 울리고, 왼쪽·오른쪽으로 조금씩 번갈아 기운다.</summary>
        Sound,

        /// <summary>방향. 왼쪽에서 나다가 잠깐 쉬고 오른쪽에서 난다.</summary>
        Direction,

        /// <summary>종류. 환경음 → 대화음 → 위협음.</summary>
        Types,
    }

    /// <summary>
    /// 튜토리얼 소리 조각(Android #327). 모두 실제 녹음이다. 원본과 라이선스, 자른 위치는 저장소의 THIRD_PARTY.md(Tutorial sounds)에 있다.
    /// 그림이 이 소리를 어느 종류로 그리는지는 <see cref="TutorialScript.Label(TutorialClip)"/> 이다.
    /// </summary>
    public enum TutorialClip
    {
        /// <summary>새 울음 한 토막(0.45초). 첫 쪽과 방향 쪽의 박자마다 낸다.</summary>
        Chirp,

        /// <summary>새소리(2.2초). 종류 쪽의 환경음.</summary>
        Birds,

        /// <summary>영어 인사말 대화(2.2초). 종류 쪽의 대화음.</summary>
        Talk,

        /// <summary>자동차 경적 한 번(0.5초). 종류 쪽의 위협음.</summary>
        Honk,
    }

    /// <summary>한 바퀴 안의 <paramref name="AtSec"/> 초에 <paramref name="Clip"/> 을 왼쪽 <paramref name="Left"/>·오른쪽 <paramref name="Right"/> 크기(0~1)로 낸다.</summary>
    public readonly record struct TutorialCue(double AtSec, TutorialClip Clip, float Left, float Right);

    /// <summary>
    /// 쪽마다 엔진에 흘려 줄 소리(Android TutorialScript). 캡처가 오버레이 엔진에 주는 것과 같은 모양(채널 피크 0~1, AI 라벨)이라
    /// 튜토리얼의 그림은 앱이 실제로 그리는 모양 그대로다.
    ///
    /// - 피크는 파형이 아니라 <b>그사이 가장 큰 크기</b>다. 그래서 오르내림은 50ms 보다 짧게 만들지 않는다.
    /// - 번쩍임은 1초에 세 번보다 적게 둔다(WCAG 2.3.1). 위협음은 빨간색이라 특히 그렇다.
    ///
    /// 대본의 좌우는 사운드 모드 2.0 의 앞 좌우(FL, FR)다(<see cref="PeakFrame"/>). PC 엔진은 2.0 을 Mid/Side 로 둘레 여덟 자리에
    /// 펼치므로(<see cref="StereoUpmix"/>) 실제 스테레오 소리와 똑같이 그려진다.
    ///
    /// 모두 시각만 받는 순수 함수다. 같은 시각에는 늘 같은 값을 내므로 화면 없이 검사할 수 있다(TutorialScriptTests).
    /// </summary>
    public static class TutorialScript
    {
        /// <summary>쪽이 처음부터 되풀이하는 간격(초).</summary>
        public static double CycleSec(this TutorialScene scene) => scene switch
        {
            TutorialScene.Sound => 4.0,
            TutorialScene.Direction => 4.4,
            _ => 6.6,
        };

        /// <summary>
        /// 그림을 멈춰 둘 때(Windows 에서 애니메이션을 껐거나 멈춤을 눌렀을 때, 쪽을 처음 보일 때) 보여 줄 장면의 시각.
        /// 그 쪽이 말하려는 것이 가장 잘 보이는 순간을 고른다.
        /// </summary>
        public static double StillAtSec(this TutorialScene scene) => scene switch
        {
            TutorialScene.Sound => 2.4,
            TutorialScene.Direction => 1.3,
            _ => 4.75,
        };

        /// <summary>그림이 이 소리 조각을 그리는 종류.</summary>
        public static SoundType Label(this TutorialClip clip) => clip switch
        {
            TutorialClip.Talk => SoundType.Speech,
            TutorialClip.Honk => SoundType.Danger,
            _ => SoundType.Ambient,
        };

        /// <summary>한 대본 안의 시각. 음수나 한 바퀴를 넘는 값도 받는다.</summary>
        public static double LoopTime(TutorialScene scene, double t)
        {
            double c = scene.CycleSec();
            return ((t % c) + c) % c;
        }

        /// <summary><paramref name="leftRight"/> 의 앞 두 칸에 [왼쪽 피크, 오른쪽 피크] 를 채운다.</summary>
        public static void Peaks(TutorialScene scene, double t, Span<float> leftRight)
        {
            double s = LoopTime(scene, t);
            double l, r;
            switch (scene)
            {
                case TutorialScene.Sound:
                    {
                        // 1초에 한 번 0.45초 동안 이어지는 쿵. 짧게 치고 마는 소리는 엔진이 부드럽게 따라가는 사이에 묻힌다.
                        // 한 번은 왼쪽, 한 번은 오른쪽으로 조금 기울여 테두리 전체가 숨 쉬듯 움직이게 한다.
                        int index = (int)Math.Floor(s);
                        double level = 0.06 + 0.72 * Thump(s - index, hold: 0.45, release: 0.12);
                        if (index % 2 == 0)
                        {
                            l = level;
                            r = level * SoundLean;
                        }
                        else
                        {
                            l = level * SoundLean;
                            r = level;
                        }
                        break;
                    }
                case TutorialScene.Direction:
                    {
                        int side = DirectionSide(s);
                        double level = DirectionLevel(s);
                        // 반대쪽에도 조금 들리게 둔다. 한쪽만 있는 소리는 실제로 드물다.
                        l = side < 0 ? level : level * DirectionFar;
                        r = side > 0 ? level : level * DirectionFar;
                        break;
                    }
                default:
                    if (s < TypeSegmentSec)
                    {
                        // 환경음: 잔잔하게 이어지는 소리
                        double level = 0.45 + 0.1 * Math.Sin(2 * Math.PI * 0.8 * s);
                        l = level;
                        r = level;
                    }
                    else if (s < 2 * TypeSegmentSec)
                    {
                        // 대화음: 말소리처럼 오르내린다(초당 두어 번)
                        double u = s - TypeSegmentSec;
                        double syllable = Math.Max(0, Math.Sin(2 * Math.PI * 2.2 * u));
                        double level = 0.3 + 0.35 * syllable * syllable;
                        l = level;
                        r = level * TypeSpeechLean;
                    }
                    else
                    {
                        // 위협음: 자동차 경적 세 번. 오른쪽에서 난다.
                        double level = 0.2 + 0.7 * Hits(s - 2 * TypeSegmentSec, TypeDangerHits);
                        l = level * TypeDangerLean;
                        r = level;
                    }
                    break;
            }
            leftRight[0] = (float)Math.Clamp(l, 0, 1);
            leftRight[1] = (float)Math.Clamp(r, 0, 1);
        }

        /// <summary>
        /// 엔진이 받는 채널 피크 한 프레임(캡처 순서 FL, FR, FC, BL, BR, SL, SR, LFE)을 채운다. 대본의 좌우는 앞 좌우에 넣고 나머지는 비운다.
        /// 그림은 사운드 모드 2.0 으로 그린다. 2.0 은 앞 좌우만 읽는다(<see cref="VisualizerEngine.Loudness"/>).
        /// </summary>
        public static void PeakFrame(TutorialScene scene, double t, Span<float> frame)
        {
            frame[..VisualizerEngine.Channels].Clear();
            Peaks(scene, t, frame);
        }

        /// <summary>그 시각에 AI 가 붙였을 종류.</summary>
        public static SoundType Label(TutorialScene scene, double t)
        {
            if (scene != TutorialScene.Types) return SoundType.Ambient;
            double s = LoopTime(scene, t);
            if (s < TypeSegmentSec) return SoundType.Ambient;
            if (s < 2 * TypeSegmentSec) return SoundType.Speech;
            return SoundType.Danger;
        }

        /// <summary>
        /// 방향 쪽에서 소리가 나는 곳. 왼쪽이면 -1, 오른쪽이면 1, 쉬는 중이면 0.
        /// 그림이 장면 안의 그 자리에 소리 표시를 띄운다.
        /// </summary>
        public static int SourceSide(TutorialScene scene, double t) =>
            scene == TutorialScene.Direction ? DirectionSide(LoopTime(scene, t)) : 0;

        /// <summary>방향 쪽 소리의 세기(0~1). 소리 표시의 진하기로 쓴다.</summary>
        public static double SourceLevel(TutorialScene scene, double t) =>
            scene == TutorialScene.Direction ? DirectionLevel(LoopTime(scene, t)) : 0;

        /// <summary>방향 쪽 소리 표시에서 박자마다 새로 퍼지는 파문이 퍼진 정도(0~1). 쉬는 중이면 -1.</summary>
        public static double SourceRipple(TutorialScene scene, double t)
        {
            if (scene != TutorialScene.Direction) return -1;
            double s = LoopTime(scene, t);
            if (DirectionStart(s) is not { } start) return -1;
            return ((s - start) % DirectionBeatSec) / DirectionBeatSec;
        }

        /// <summary>
        /// 소리를 켰을 때 그 쪽이 한 바퀴 동안 내는 소리(Android #327). 대본의 박자·구간과 같은 순간에, 그림이 그 순간 그리는 종류의
        /// 소리를, 그림이 기우는 쪽으로 낸다. 대본의 상수에서 만들므로 대본을 고치면 소리도 따라간다.
        ///
        /// - 첫 쪽: 1초마다 쿵 할 때 새 울음. 그림처럼 왼쪽·오른쪽으로 번갈아 조금 기운다.
        /// - 방향 쪽: 박자마다 새 울음. 왼쪽 네 번, 쉬고 오른쪽 네 번. 반대쪽은 그림처럼 작게 들린다.
        /// - 종류 쪽: 새소리 → 대화 → 경적 세 번.
        /// </summary>
        public static IReadOnlyList<TutorialCue> Cues(TutorialScene scene) => scene switch
        {
            TutorialScene.Sound => SoundCues,
            TutorialScene.Direction => DirectionCues,
            _ => TypesCues,
        };

        // ---------------- 대본의 조각 ----------------

        /// <summary>첫 쪽: 쿵이 기운 쪽의 반대편 크기.</summary>
        private const float SoundLean = 0.72f;

        /// <summary>방향 쪽: 소리가 나는 반대편에 들리는 크기. 한쪽만 있는 소리는 실제로 드물다.</summary>
        private const float DirectionFar = 0.15f;

        /// <summary>종류 쪽: 대화음의 오른쪽, 위협음의 왼쪽 크기.</summary>
        private const float TypeSpeechLean = 0.95f;
        private const float TypeDangerLean = 0.65f;

        private const double TypeSegmentSec = 2.2;
        private static readonly double[] TypeDangerHits = { 0.1, 0.8, 1.5 };

        private const double DirectionLeftEnd = 1.6;
        private const double DirectionRightStart = 2.2;
        private const double DirectionRightEnd = 3.8;
        private const double DirectionBeatSec = 0.5;

        private static readonly TutorialCue[] SoundCues = BuildSoundCues();
        private static readonly TutorialCue[] DirectionCues = BuildDirectionCues();
        private static readonly TutorialCue[] TypesCues = BuildTypesCues();

        private static TutorialCue[] BuildSoundCues()
        {
            var cues = new List<TutorialCue>();
            for (int beat = 0; beat < (int)TutorialScene.Sound.CycleSec(); beat++)
            {
                cues.Add(beat % 2 == 0
                    ? new TutorialCue(beat, TutorialClip.Chirp, 1f, SoundLean)
                    : new TutorialCue(beat, TutorialClip.Chirp, SoundLean, 1f));
            }
            return cues.ToArray();
        }

        private static TutorialCue[] BuildDirectionCues()
        {
            var cues = new List<TutorialCue>();
            foreach (double at in Beats(0, DirectionLeftEnd)) cues.Add(new TutorialCue(at, TutorialClip.Chirp, 1f, DirectionFar));
            foreach (double at in Beats(DirectionRightStart, DirectionRightEnd)) cues.Add(new TutorialCue(at, TutorialClip.Chirp, DirectionFar, 1f));
            return cues.ToArray();
        }

        private static TutorialCue[] BuildTypesCues()
        {
            var cues = new List<TutorialCue>
            {
                new(0, TutorialClip.Birds, 1f, 1f),
                new(TypeSegmentSec, TutorialClip.Talk, 1f, TypeSpeechLean),
            };
            foreach (double hit in TypeDangerHits) cues.Add(new TutorialCue(2 * TypeSegmentSec + hit, TutorialClip.Honk, TypeDangerLean, 1f));
            return cues.ToArray();
        }

        /// <summary><paramref name="start"/> 부터 <paramref name="end"/> 앞까지 방향 쪽의 박자마다의 시각. 더해 가지 않고 곱해 소수 오차가 쌓이지 않게 한다.</summary>
        private static IEnumerable<double> Beats(double start, double end)
        {
            for (int i = 0; start + i * DirectionBeatSec < end; i++) yield return start + i * DirectionBeatSec;
        }

        private static int DirectionSide(double s)
        {
            if (s < DirectionLeftEnd) return -1;
            if (s >= DirectionRightStart && s < DirectionRightEnd) return 1;
            return 0;
        }

        /// <summary>방향 쪽에서 지금 나는 소리가 시작한 시각. 쉬는 중이면 null.</summary>
        private static double? DirectionStart(double s) => DirectionSide(s) switch
        {
            -1 => 0,
            1 => DirectionRightStart,
            _ => null,
        };

        private static double DirectionLevel(double s)
        {
            if (DirectionStart(s) is not { } start) return 0;
            double u = s - start;
            // 소리가 시작할 때 한 프레임에 튀어 오르지 않게 짧게 키운다.
            double attack = Math.Min(1, u / 0.06);
            return attack * (0.45 + 0.3 * Math.Exp(-(u % DirectionBeatSec) / 0.2));
        }

        /// <summary>쿵 하나: 짧게 올라 <paramref name="hold"/> 초 이어지다 <paramref name="release"/> 초 간격으로 줄어든다. <paramref name="u"/> 는 친 뒤 흐른 시간.</summary>
        private static double Thump(double u, double hold, double release)
        {
            if (u < 0) return 0;
            if (u < hold) return Math.Min(1, u / 0.04);
            return Math.Exp(-(u - hold) / release);
        }

        /// <summary><paramref name="starts"/> 시각마다 한 번씩 크게 치고 0.25초 이어지다 사그라드는 소리. <paramref name="u"/> 가 첫 번째보다 앞이면 0.</summary>
        private static double Hits(double u, double[] starts)
        {
            double last = -1;
            foreach (double start in starts)
            {
                if (u >= start) last = start;
            }
            if (last < 0) return 0;
            return Thump(u - last, hold: 0.25, release: 0.15);
        }
    }
}
