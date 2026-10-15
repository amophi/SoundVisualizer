// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// 실시간 AI 파이프라인의 캡처 입구(Android 의 AiCaptureInferenceGate.kt).
    ///
    /// 소리는 게이트가 닫혀 있어도 늘 링에 넣는다. 그래야 다시 열릴 때 그 앞의 소리까지 한 창으로 본다.
    /// 게이트는 준비된 창으로 추론을 시작해도 되는지만 정한다. Android 에서는 internal 이지만, 파이프라인을 앱 쪽에서
    /// 엮을 수 있게 public 으로 둔다.
    /// </summary>
    public sealed class AiCaptureInferenceGate
    {
        private readonly AiAudioBuffer _audioBuffer;
        private readonly AiSilenceGate _silenceGate;

        public AiCaptureInferenceGate(AiAudioBuffer audioBuffer, AiSilenceGate? silenceGate = null)
        {
            _audioBuffer = audioBuffer ?? throw new ArgumentNullException(nameof(audioBuffer));
            _silenceGate = silenceGate ?? new AiSilenceGate();
        }

        /// <summary>캡처 버퍼 하나를 넣는다. 캡처 스레드 하나에서만 부른다(<see cref="AiAudioBuffer.IngestInterleaved"/>).</summary>
        public void IngestInterleaved(ReadOnlySpan<float> pcm, long nowMs)
        {
            // 링에 먼저 넣고 게이트를 연다. 열린 게이트를 본 추론 스레드는 그 소리가 든 창을 반드시 가져간다.
            _audioBuffer.IngestInterleaved(pcm);
            _silenceGate.OnInterleavedPcm(pcm, nowMs);
        }

        public bool IsInferenceOpen(long nowMs) => _silenceGate.IsOpen(nowMs);

        public void OnInferenceCompleted(long snapshotTimeMs)
        {
            _silenceGate.OnInferenceCompleted(snapshotTimeMs);
        }

        public void OnInferenceFailed(long snapshotTimeMs)
        {
            _silenceGate.OnInferenceFailed(snapshotTimeMs);
        }

        /// <summary>게이트만 비운다. 링은 파이프라인이 따로 비운다(Android 와 같다).</summary>
        public void Reset()
        {
            _silenceGate.Reset();
        }
    }
}
