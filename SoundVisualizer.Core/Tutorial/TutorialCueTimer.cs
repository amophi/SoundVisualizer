// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tutorial
{
    /// <summary>
    /// 한 쪽 그림의 소리 박자(Android TutorialSounds, #327). 그림이 프레임마다 대본 시각을 <see cref="OnFrame"/> 으로 넘기면, 그사이 지난
    /// 소리(<see cref="TutorialScript.Cues"/>)를 낸다. 그래서 소리가 그림의 박자와 같은 프레임에 시작하고, 그림이 멈추면(쪽을 넘김·멈춤 버튼·
    /// 창을 최소화) 다음 소리도 나지 않는다.
    ///
    /// 늦은 소리는 내지 않는다. 창이 잠깐 멈췄다 돌아오면 그림의 시각이 그동안 흐른 만큼 건너뛰는데, 그사이의 소리를 한꺼번에 내면
    /// 소리가 겹쳐 쏟아진다. 소리를 도중에 켰을 때도 지난 소리를 몰아 내지 않는다.
    ///
    /// UI 스레드에서만 쓴다.
    /// </summary>
    public sealed class TutorialCueTimer
    {
        /// <summary>이보다 늦게 알게 된 소리는 내지 않는다. 60fps 의 몇 프레임이 밀려도 낼 만큼.</summary>
        public const double MaxLateSec = 0.15;

        /// <summary>처음 그리는 프레임(0초)의 소리도 내도록 그보다 조금 앞에서 센다.</summary>
        private const double BeforeStart = -0.001;

        private readonly IReadOnlyList<TutorialCue> _cues;
        private readonly double _cycleSec;
        private readonly Func<bool> _canPlay;
        private readonly Action<TutorialCue> _play;
        private readonly Action _cancel;
        private double _lastT = BeforeStart;

        /// <param name="cues">한 바퀴의 소리.</param>
        /// <param name="cycleSec">한 바퀴의 길이(초).</param>
        /// <param name="canPlay">지금 소리를 내도 되는지. 아니면 그 소리는 건너뛴다.</param>
        /// <param name="play">소리 하나를 낸다.</param>
        /// <param name="cancel">나던 소리를 모두 멈춘다.</param>
        public TutorialCueTimer(IReadOnlyList<TutorialCue> cues, double cycleSec, Func<bool> canPlay, Action<TutorialCue> play, Action cancel)
        {
            _cues = cues;
            _cycleSec = cycleSec;
            _canPlay = canPlay;
            _play = play;
            _cancel = cancel;
        }

        /// <summary>그림이 새로 그렸다. <paramref name="t"/> 는 대본의 시각(초)이다. 지난 프레임 뒤로 온 소리 가운데 늦지 않은 것을 낸다.</summary>
        public void OnFrame(double t)
        {
            double from = _lastT;
            _lastT = t;
            if (t <= from) return;
            int first = (int)Math.Floor(Math.Max(from, 0) / _cycleSec);
            int last = (int)Math.Floor(t / _cycleSec);
            for (int cycle = first; cycle <= last; cycle++)
            {
                foreach (var cue in _cues)
                {
                    double at = cycle * _cycleSec + cue.AtSec;
                    if (at > from && at <= t && t - at <= MaxLateSec && _canPlay()) _play(cue);
                }
            }
        }

        /// <summary>나던 소리를 멈추고, 다시 움직이면 처음부터 센다(그 시각의 소리부터 다시 낸다).</summary>
        public void Stop()
        {
            _lastT = BeforeStart;
            _cancel();
        }
    }
}
