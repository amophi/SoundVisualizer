// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Diagnostics
{
    /// <summary>
    /// 오버레이 하나의 개발자 모드 HUD 와 결과 기록을 언제 돌리고 멈출지 정한다(모바일 AiDebugOverlay 의 produceState 와 DisposableEffect).
    ///
    /// - HUD 는 오버레이가 열려 있고 개발자 모드가 켜져 있는 동안만 <see cref="PollIntervalMs"/> 마다 읽는다(<see cref="IsPolling"/>).
    /// - 기록은 거기에 기록 스위치까지 켜져 있을 때만 돈다(<see cref="IsRecording"/>). 셋 가운데 하나라도 바뀌면 그 자리에서 켜고 끈다.
    ///   개발자 모드를 꺼도 기록 스위치의 값은 남아, 다시 켜면 새 파일에 기록한다(모바일과 같다).
    /// - 읽을 때마다 결과를 기록기에 넘긴다. 같은 결과를 거듭 넘겨도 기록기가 한 줄만 남긴다(<see cref="AiDebugLogWriter"/>).
    /// - 오버레이를 닫으면(<see cref="Dispose"/>) 기록기를 닫는다. 그 뒤에는 무엇을 넘겨도 다시 켜지 않는다.
    ///
    /// 화면과 타이머는 오버레이가 맡고, 여기는 무엇을 할 차례인지만 정해 기기 없이 검사한다(AiDebugHudTests). UI 스레드에서만 쓴다.
    /// 기록기는 디스크를 기다리지 않으므로(<see cref="AiDebugRecorder"/>) 읽을 때마다 불러도 된다. 닫을 때만 남은 줄을 쓰기를 1초까지 기다린다.
    /// </summary>
    public sealed class AiDebugHud : IDisposable
    {
        /// <summary>HUD 를 다시 읽는 간격(밀리초, 모바일 DEBUG_POLL_MS). 추론이 250ms 마다라 그보다 촘촘해야 짧은 판정 하나를 건너뛰지 않는다.</summary>
        public const int PollIntervalMs = 100;

        private readonly IAiDebugRecorder _recorder;
        private bool _developerMode;
        private bool _record;
        private bool _closed;

        /// <summary>앱이 쓰는 HUD. 기록은 실행 파일 폴더(쓸 수 없으면 LocalAppData)의 ai-log 에 남는다(<see cref="AiDebugRecorder"/>).</summary>
        public AiDebugHud()
            : this(new AiDebugRecorder())
        {
        }

        /// <param name="recorder">켜고 끌 기록기. 이 HUD 가 넘겨받아 <see cref="Dispose"/> 에서 닫는다. 테스트가 가짜나 임시 폴더의 기록기를 준다.</param>
        internal AiDebugHud(IAiDebugRecorder recorder)
        {
            ArgumentNullException.ThrowIfNull(recorder);
            _recorder = recorder;
        }

        /// <summary>HUD 를 보이고 <see cref="PollIntervalMs"/> 마다 읽을 차례인지. 오버레이가 열려 있고 개발자 모드가 켜져 있다.</summary>
        public bool IsPolling => !_closed && _developerMode;

        /// <summary>
        /// 결과를 기록할 차례인지. 읽는 중이고 기록 스위치도 켜져 있다. 기록기가 상한이나 쓰기 실패로 스스로 멈춘 것은 여기에 나타나지
        /// 않는다(모바일과 같다). 스위치를 껐다 켜면 새 파일로 다시 시작한다.
        /// </summary>
        public bool IsRecording => IsPolling && _record;

        /// <summary>
        /// 지금 설정을 알린다. 오버레이가 연 직후와 프레임마다(쉬는 동안에는 확인할 때마다) 부르므로, 런처·편집 패널·초기화 어디서 바꿔도
        /// 곧바로 따른다. 기록할 차례가 되면 기록기를 켜고, 아니게 되면 끈다. 같은 값이면 아무것도 하지 않는다. 닫은 뒤에는 무시한다.
        /// </summary>
        /// <returns><see cref="IsPolling"/> 이 바뀌었으면 true. 부르는 쪽이 HUD 를 보이거나 숨기고 타이머를 켜고 끈다.</returns>
        public bool Update(bool developerMode, bool record)
        {
            if (_closed) return false;
            bool wasPolling = IsPolling;
            bool wasRecording = IsRecording;
            _developerMode = developerMode;
            _record = record;

            if (IsRecording != wasRecording)
            {
                if (IsRecording) _recorder.Start();
                else _recorder.Stop();
            }
            return IsPolling != wasPolling;
        }

        /// <summary>
        /// HUD 를 한 번 읽어 그 글자를 돌려준다. 기록할 차례이고 결과가 있으면 같은 값으로 기록기에도 넘긴다. 값은 부르는 쪽이 이 순간
        /// 한 번씩 읽어 넘긴다. 쓰는 스레드가 나중에 상태를 읽으면 그사이 바뀐 값이 적힌다.
        /// </summary>
        /// <param name="result">마지막 결과. 아직 없거나 AI 가 돌지 않으면 null.</param>
        /// <param name="nowMs">지금 벽시계 시각(유닉스 밀리초, <see cref="AiClassificationResult.TimestampMs"/> 와 같은 시계).</param>
        /// <param name="level">지금 소리 크기(0..1, <see cref="Audio.CaptureLevel"/>).</param>
        /// <param name="shown">그 결과의 종류를 표시하도록 켜 두었는지. 결과가 없으면 false.</param>
        /// <param name="aiAvailable">AI 를 만들지 못해 분류가 아예 돌지 않으면 false. 만드는 중에는 true 다.</param>
        public AiDebugLines Poll(AiClassificationResult? result, long nowMs, float level, bool shown, bool aiAvailable)
        {
            if (IsRecording && result is not null) _recorder.Offer(result, nowMs, level, shown);
            return AiDebugText.Format(result, nowMs, level, shown, aiAvailable);
        }

        /// <summary>오버레이를 닫았다. 기록기를 닫는다(남은 줄을 쓰고 파일을 닫기를 1초까지 기다린다). 여러 번 불러도 된다.</summary>
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            _recorder.Dispose();
        }
    }

    /// <summary>결과 기록기(<see cref="AiDebugRecorder"/>). <see cref="AiDebugHud"/> 가 켜고 끄고, 테스트는 가짜로 바꿔 끼운다.</summary>
    internal interface IAiDebugRecorder : IDisposable
    {
        /// <inheritdoc cref="AiDebugRecorder.Start"/>
        void Start();

        /// <inheritdoc cref="AiDebugRecorder.Stop"/>
        void Stop();

        /// <inheritdoc cref="AiDebugRecorder.Offer"/>
        void Offer(AiClassificationResult result, long nowMs, float level, bool shown);
    }
}
