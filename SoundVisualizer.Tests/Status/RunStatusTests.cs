// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Status;

namespace SoundVisualizer.Tests.Status;

/// <summary>
/// 홈의 상태 줄 아래 안내. 안드로이드 HomeTab 의 안내 고르기와 StopReasonTest 의 "사용자가 끈 경우만 알리지 않는다"를 옮겼다.
///
/// 청각장애 사용자는 조용한 장면과 꺼진 상태를 구분하지 못하므로, 사용자가 끈 경우만 빼고 모두 알려야 한다.
/// 반대로 직접 끌 때마다 안내가 남으면 성가셔서 안내 자체를 읽지 않게 된다.
/// </summary>
public class RunStatusTests
{
    private static readonly StopReason[] Unexpected = Enum.GetValues<StopReason>().Where(r => r != StopReason.UserRequested).ToArray();

    [Fact]
    public void 처음에는_멈춘_상태이고_안내가_없다()
    {
        var status = new RunStatus();
        Assert.False(status.IsRunning);
        Assert.Null(status.LastUnexpectedStop);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 사용자가_끈_경우에는_알리지_않는다()
    {
        var status = new RunStatus();
        status.Started();
        Assert.False(status.Stopped(StopReason.UserRequested));
        Assert.Null(status.LastUnexpectedStop);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 사용자가_끈_게_아니면_모든_이유를_멈춘_뒤에_알린다()
    {
        Assert.NotEmpty(Unexpected);
        foreach (var reason in Unexpected)
        {
            var status = new RunStatus();
            status.Started();
            Assert.True(status.Stopped(reason), reason.ToString());
            Assert.Equal(reason, status.LastUnexpectedStop);
            Assert.Equal(HomeNotice.Stopped, status.Notice(showAmbient: true));
        }
    }

    [Fact]
    public void 시작하지_못한_경우도_멈춘_이유로_알린다()
    {
        // 장치를 열지 못하면 오버레이를 띄우지 않으므로 Started 없이 Stopped 만 온다.
        var status = new RunStatus();
        Assert.True(status.Stopped(StopReason.NoPlaybackDevice));
        Assert.False(status.IsRunning);
        Assert.Equal(HomeNotice.Stopped, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 다시_실행하면_지난_안내를_지운다()
    {
        var status = new RunStatus();
        status.Stopped(StopReason.CaptureError);
        status.Started();
        Assert.Null(status.LastUnexpectedStop);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));

        // 이번에는 직접 끄면 옛 이유가 되살아나지 않는다.
        status.Stopped(StopReason.UserRequested);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 안내_닫기를_누르면_지운다()
    {
        var status = new RunStatus();
        status.Stopped(StopReason.NoPlaybackDevice);
        status.Dismiss();
        Assert.Null(status.LastUnexpectedStop);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 나중_이유가_앞_이유를_덮는다()
    {
        // 장치가 없어 시작하지 못한 뒤 다시 눌러 또 실패하면, 지금 실패한 이유를 보인다.
        var status = new RunStatus();
        status.Stopped(StopReason.CaptureError);
        status.Stopped(StopReason.NoPlaybackDevice);
        Assert.Equal(StopReason.NoPlaybackDevice, status.LastUnexpectedStop);
    }

    [Fact]
    public void AI_를_쓸_수_없으면_실행_중에만_알린다()
    {
        var status = new RunStatus();
        status.Started();
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true)); // 만드는 동안은 쓸 수 있다고 본다

        status.SetAiAvailable(false);
        Assert.Equal(HomeNotice.AiUnavailable, status.Notice(showAmbient: true));

        // 장치가 바뀌어 다시 만들면 안내가 사라진다.
        status.SetAiAvailable(true);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));

        status.SetAiAvailable(false);
        status.Stopped(StopReason.UserRequested);
        Assert.True(status.AiAvailable);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 환경음_표시를_꺼_두었으면_아무것도_보이지_않는다고_알린다()
    {
        // AI 가 없으면 모든 소리가 환경음이 된다. 환경음 표시가 꺼져 있으면 "환경음 색으로 보인다"는 거짓이다.
        var status = new RunStatus();
        status.Started();
        status.SetAiAvailable(false);
        Assert.Equal(HomeNotice.AiUnavailableHidden, status.Notice(showAmbient: false));
        Assert.Equal(HomeNotice.AiUnavailable, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 멈춘_뒤에_늦게_온_AI_알림은_다음_실행에_남지_않는다()
    {
        var status = new RunStatus();
        status.Started();
        status.Stopped(StopReason.UserRequested);
        status.SetAiAvailable(false); // 닫히기 직전에 만들기를 끝낸 파이프라인의 알림
        status.Started();
        Assert.True(status.AiAvailable);
        Assert.Equal(HomeNotice.None, status.Notice(showAmbient: true));
    }

    [Fact]
    public void 멈춘_이유가_있으면_AI_안내보다_먼저다()
    {
        var status = new RunStatus();
        status.Started();
        status.SetAiAvailable(false);
        status.Stopped(StopReason.CaptureError);
        Assert.Equal(HomeNotice.Stopped, status.Notice(showAmbient: true));
    }
}

/// <summary>실행 중 캡처 오류 뒤에 오버레이를 멈출지와 그 이유.</summary>
public class CaptureRecoveryTests
{
    [Fact]
    public void 캡처가_돌고_있으면_멈추지_않는다()
    {
        Assert.Null(CaptureRecovery.Check(isCapturing: true, hasDevice: true));
    }

    [Fact]
    public void 장치를_열지_못했으면_출력_장치가_없다고_알린다()
    {
        Assert.Equal(StopReason.NoPlaybackDevice, CaptureRecovery.Check(isCapturing: false, hasDevice: false));
    }

    [Fact]
    public void 장치는_열었는데_멈췄으면_캡처_오류다()
    {
        Assert.Equal(StopReason.CaptureError, CaptureRecovery.Check(isCapturing: false, hasDevice: true));
    }

    [Fact]
    public void 기다리는_시간은_엔진이_장치를_다시_여는_지연보다_넉넉하다()
    {
        // AudioCaptureEngine 은 장치 알림 뒤 0.5초 기다렸다가 다시 연다. 그 안에 멈추면 장치를 바꾸기만 해도 꺼진다.
        Assert.True(CaptureRecovery.GracePeriod >= TimeSpan.FromSeconds(2));
        Assert.True(CaptureRecovery.GracePeriod <= TimeSpan.FromSeconds(10)); // 너무 길면 꺼진 줄 모르는 시간이 길어진다
    }
}
