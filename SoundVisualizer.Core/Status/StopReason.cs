// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Status
{
    /// <summary>
    /// 오버레이(시각화)가 멈춘 이유. 안드로이드 StopReason 에서 PC 에 있는 경우만 남겼다(화면 녹화 동의, 다른 앱 위에 표시 권한은 PC 에 없다).
    ///
    /// 청각장애 사용자는 "조용한 장면"과 "꺼진 상태"를 구분할 수 없다. 오버레이는 소리가 없으면 아무것도 그리지 않으므로
    /// 사용자가 직접 끈 게 아니면 반드시 알린다.
    /// </summary>
    public enum StopReason
    {
        /// <summary>홈·트레이의 실행 종료, 편집 패널의 오버레이 종료, 런처 닫기. 알리지 않는다.</summary>
        UserRequested,

        /// <summary>열 수 있는 출력 장치가 없다. 시작할 때 없었거나, 실행 중에 장치가 빠졌는데 다른 장치가 없을 때다.</summary>
        NoPlaybackDevice,

        /// <summary>장치는 열었지만 캡처가 오류로 멈추고 되살아나지 않았다.</summary>
        CaptureError,
    }
}
