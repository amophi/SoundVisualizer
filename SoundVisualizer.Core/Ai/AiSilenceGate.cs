// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 최근 캡처에 소리가 있는 동안만 실시간 추론을 열어 둔다(Android 의 AiSilenceGate.kt). 조용할 때 CPU 를 쓰지 않으려는 것이다.
    ///
    /// 마지막 소리가 YAMNet 창을 빠져나간 뒤에도, 완전히 조용한 창으로 추론이 두 번 끝나 후처리의 히스테리시스가 정리될
    /// 때까지는 열어 둔다. 그래야 소리가 그친 뒤 마지막 라벨이 남지 않는다. 캡처 스레드가 소리를 알리고 추론 스레드가 결과를
    /// 알리므로 상태는 한 잠금 안에서 함께 바꾼다. 시각은 단조 증가하는 밀리초(<see cref="Environment.TickCount64"/> 등)다.
    /// </summary>
    public sealed class AiSilenceGate
    {
        /// <summary>소리가 있다고 보는 원래 크기. Android 에서 진동과 시각화가 깨어나는 기준(0.01)과 같다.</summary>
        public const float ActiveThreshold = 0.01f;

        /// <summary>실시간으로 계속 들어오는 소리가 YAMNet 창(15,600샘플 = 0.975초)을 다 빠져나가는 데 걸리는 시간.</summary>
        public const long YamnetWindowMs = CaptureAudioMath.RequiredMono16kSamples * 1000L / CaptureAudioMath.TargetSampleRate;

        /// <summary>
        /// 가장 긴 종류 전환 히스테리시스를 정리하는 데 필요한 완전히 조용한 추론 수.
        /// Android 의 AiPostProcessor.COARSE_HYSTERESIS_THRESHOLD(2) 와 같아야 한다.
        /// </summary>
        public const int RequiredSilentInferenceCompletions = 2;

        /// <summary>
        /// 추론이 계속 실패하면 창 네 개만큼 지난 뒤 다시 시도하지 않는다. 성공하면 이 대비를 지우고 정상 횟수를 그대로 요구하므로
        /// 잠깐의 실패로 정리가 짧아지지 않는다.
        /// </summary>
        public const long FailureFallbackMs = YamnetWindowMs * 4;

        private const long NoTimestamp = long.MinValue;

        private readonly object _stateLock = new();
        private readonly float _activeThreshold;
        private long _lastActiveInputMs = NoTimestamp;
        private int _silentInferenceCompletions;
        private long _firstConsecutiveFailureMs = NoTimestamp;

        public AiSilenceGate(float activeThreshold = ActiveThreshold)
        {
            _activeThreshold = activeThreshold;
        }

        /// <summary>
        /// 캡처 버퍼 하나를 본다. 섞기 전 인터리브 PCM 의 최고값으로 재므로 채널 수와 상관없이 어느 한 채널(LFE 포함)만 커도 열린다.
        /// 캡처 스레드에서 부른다.
        /// </summary>
        public void OnInterleavedPcm(ReadOnlySpan<float> interleaved, long nowMs)
        {
            if (CaptureAudioMath.MaxAbsoluteInterleavedPeak(interleaved) > _activeThreshold)
            {
                lock (_stateLock)
                {
                    _lastActiveInputMs = nowMs;
                    _silentInferenceCompletions = 0;
                    _firstConsecutiveFailureMs = NoTimestamp;
                }
            }
        }

        public bool IsOpen(long nowMs)
        {
            lock (_stateLock)
            {
                if (_lastActiveInputMs == NoTimestamp) return false;
                if (nowMs - _lastActiveInputMs < YamnetWindowMs) return true;
                if (_silentInferenceCompletions >= RequiredSilentInferenceCompletions) return false;
                return _firstConsecutiveFailureMs == NoTimestamp ||
                    nowMs - _firstConsecutiveFailureMs < FailureFallbackMs;
            }
        }

        /// <summary><paramref name="snapshotTimeMs"/> 에 가져간 창의 추론이 성공했다.</summary>
        public void OnInferenceCompleted(long snapshotTimeMs)
        {
            lock (_stateLock)
            {
                if (_lastActiveInputMs == NoTimestamp) return;
                if (snapshotTimeMs - _lastActiveInputMs < YamnetWindowMs) return;
                _firstConsecutiveFailureMs = NoTimestamp;
                if (_silentInferenceCompletions < RequiredSilentInferenceCompletions)
                {
                    _silentInferenceCompletions++;
                }
            }
        }

        /// <summary>소리가 다 빠져나간 창에서 연달아 실패할 때만 제한 시간을 잰다.</summary>
        public void OnInferenceFailed(long snapshotTimeMs)
        {
            lock (_stateLock)
            {
                if (_lastActiveInputMs == NoTimestamp) return;
                if (snapshotTimeMs - _lastActiveInputMs < YamnetWindowMs) return;
                if (_firstConsecutiveFailureMs == NoTimestamp)
                {
                    _firstConsecutiveFailureMs = snapshotTimeMs;
                }
            }
        }

        public void Reset()
        {
            lock (_stateLock)
            {
                _lastActiveInputMs = NoTimestamp;
                _silentInferenceCompletions = 0;
                _firstConsecutiveFailureMs = NoTimestamp;
            }
        }
    }
}
