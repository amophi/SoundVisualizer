// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Status
{
    /// <summary>홈의 상태 줄 아래에 보이는 안내. 한 번에 하나만 보인다.</summary>
    public enum HomeNotice
    {
        None,

        /// <summary>실행 중인데 AI 를 쓸 수 없다. 모든 소리가 환경음 색으로 보인다.</summary>
        AiUnavailable,

        /// <summary>실행 중인데 AI 를 쓸 수 없고 환경음 표시도 꺼 두었다. 아무것도 보이지 않는다.</summary>
        AiUnavailableHidden,

        /// <summary>사용자가 끄지 않았는데 멈췄다. 이유는 <see cref="RunStatus.LastUnexpectedStop"/>.</summary>
        Stopped,
    }

    /// <summary>
    /// 런처의 홈 안내와 트레이가 보는 실행 상태. 안드로이드 SettingsManager 의 isServiceRunning·aiAvailable·lastUnexpectedStop 과
    /// HomeTab 의 안내 고르기를 한곳에 모았다. 무엇을 보일지는 <see cref="Notice"/> 가 정한다. UI 스레드에서만 쓴다.
    /// </summary>
    public sealed class RunStatus
    {
        /// <summary>오버레이가 떠 있는지.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>이번 실행에서 AI(소리 종류 구분)를 쓸 수 있는지. 실행 중이 아니면 늘 true 다(알릴 것이 없다).</summary>
        public bool AiAvailable { get; private set; } = true;

        /// <summary>사용자가 끄지 않았는데 멈춘 마지막 이유. 다시 실행하거나 안내를 닫으면 지운다.</summary>
        public StopReason? LastUnexpectedStop { get; private set; }

        /// <summary>
        /// 오버레이가 떴다. 지난 "꺼졌습니다" 안내는 이제 틀린 정보라 지운다. AI 는 만드는 중이라 실패를 알려 올 때까지
        /// 쓸 수 있다고 본다(만드는 0.1초 남짓 동안 안내가 깜빡이지 않게).
        /// </summary>
        public void Started()
        {
            IsRunning = true;
            AiAvailable = true;
            LastUnexpectedStop = null;
        }

        /// <summary>AI 를 만들었거나 만들지 못했다. 멈춘 뒤에 늦게 도착한 알림은 다음 실행의 상태가 되지 않게 버린다.</summary>
        public void SetAiAvailable(bool available)
        {
            if (IsRunning) AiAvailable = available;
        }

        /// <summary>
        /// 오버레이가 멈췄다(시작하지 못한 경우도 여기로 온다).
        /// </summary>
        /// <returns>사용자가 끈 게 아니라 알릴 차례면 true.</returns>
        public bool Stopped(StopReason reason)
        {
            IsRunning = false;
            AiAvailable = true;
            if (reason == StopReason.UserRequested) return false;
            LastUnexpectedStop = reason;
            return true;
        }

        /// <summary>사용자가 "안내 닫기"를 눌렀다.</summary>
        public void Dismiss() => LastUnexpectedStop = null;

        /// <summary>
        /// 상태 줄 아래에 보일 안내. AI 안내는 실행 중에만, 멈춘 이유는 멈춘 뒤에만 보이므로 둘이 겹치지 않는다.
        /// </summary>
        /// <param name="showAmbient">
        /// 환경음을 표시하는지. AI 가 없으면 모든 소리가 환경음이 되는데, 환경음 표시를 꺼 두었으면 아무것도 그리지 않는다.
        /// 그때 "환경음 색으로 보인다"고 하면 거짓으로 안심시키므로 다른 안내를 고른다(안드로이드 AiUnavailableNotice 가 진동을 두고 한 일).
        /// </param>
        public HomeNotice Notice(bool showAmbient)
        {
            if (IsRunning) return AiAvailable ? HomeNotice.None : showAmbient ? HomeNotice.AiUnavailable : HomeNotice.AiUnavailableHidden;
            return LastUnexpectedStop != null ? HomeNotice.Stopped : HomeNotice.None;
        }
    }
}
