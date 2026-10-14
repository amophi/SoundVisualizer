// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Status
{
    /// <summary>
    /// 캡처가 돌고 있지 않을 때 오버레이를 멈출지와 그 이유를 정한다.
    ///
    /// 출력 장치를 바꾸면(헤드폰을 뽑는 등) 캡처 엔진은 옛 장치에서 오류를 낸 뒤 0.5초쯤 지나 새 기본 장치를 다시 연다.
    /// 그래서 실행 중의 오류는 받자마자 멈추지 않고 <see cref="GracePeriod"/> 만큼 기다려, 그때도 받지 못할 때만 멈춘다.
    /// 끝내 되살아나지 않으면 오버레이는 아무것도 그리지 못하는데, 사용자는 그것을 조용한 장면과 구분할 수 없다.
    /// </summary>
    public static class CaptureRecovery
    {
        /// <summary>실행 중 캡처 오류 뒤 저절로 되살아나기를 기다리는 시간. 엔진이 다시 여는 지연(0.5초)보다 넉넉하다.</summary>
        public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);

        /// <param name="isCapturing">캡처가 돌고 있는지.</param>
        /// <param name="hasDevice">출력 장치를 열었는지(캡처 형식이 있는지).</param>
        /// <returns>멈출 이유. 캡처가 돌고 있으면 null.</returns>
        public static StopReason? Check(bool isCapturing, bool hasDevice) =>
            isCapturing ? null
            : hasDevice ? StopReason.CaptureError
            : StopReason.NoPlaybackDevice;
    }
}
