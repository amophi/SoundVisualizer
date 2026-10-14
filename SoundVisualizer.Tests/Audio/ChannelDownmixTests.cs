// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Runtime.InteropServices;
using SoundVisualizer.Audio;

namespace SoundVisualizer.Tests.Audio;

public class ChannelDownmixTests
{
    private static byte[] Floats(params float[] values) => MemoryMarshal.AsBytes(values.AsSpan()).ToArray();

    private static float[] AsFloats(byte[] bytes, int length) => MemoryMarshal.Cast<byte, float>(bytes.AsSpan(0, length)).ToArray();

    [Fact]
    public void 녹음된_바이트까지만_쓰고_재사용_버퍼의_남은_부분은_버린다()
    {
        // NAudio 는 캡처 버퍼를 재사용한다. 이번 버퍼는 앞의 1프레임(2ch)뿐이고 뒤는 지난 버퍼의 찌꺼기다.
        byte[] reused = Floats(0.1f, 0.2f, /* 찌꺼기 */ 0.9f, 0.9f, 0.9f, 0.9f);
        int bytesRecorded = 2 * sizeof(float);
        var output = Array.Empty<byte>();

        int written = ChannelDownmix.Convert(reused.AsSpan(0, bytesRecorded), inChannels: 2, bytesPerSample: 4, outChannels: 1, ref output);

        Assert.Equal(sizeof(float), written);
        Assert.Equal(new[] { 0.1f }, AsFloats(output, written));
    }

    [Fact]
    public void 칠점일_채널은_WASAPI_순서로_섞고_LFE_는_뺀다()
    {
        // FL, FR, FC, LFE, BL, BR, SL, SR
        byte[] frame = Floats(0.10f, 0.20f, 0.30f, 0.90f, 0.40f, 0.50f, 0.06f, 0.07f);
        var output = Array.Empty<byte>();

        int written = ChannelDownmix.Convert(frame, 8, 4, 2, ref output);
        float[] lr = AsFloats(output, written);

        Assert.Equal(0.10f + 0.707f * 0.30f + 0.707f * 0.06f + 0.5f * 0.40f, lr[0], 5);
        Assert.Equal(0.20f + 0.707f * 0.30f + 0.707f * 0.07f + 0.5f * 0.50f, lr[1], 5);
    }

    [Fact]
    public void 오점일_채널은_서라운드_한_쌍을_섞고_LFE_는_뺀다()
    {
        // FL, FR, FC, LFE, SL, SR
        byte[] frame = Floats(0.10f, 0.20f, 0.30f, 0.90f, 0.40f, 0.50f);
        var output = Array.Empty<byte>();

        int written = ChannelDownmix.Convert(frame, 6, 4, 2, ref output);
        float[] lr = AsFloats(output, written);

        Assert.Equal(0.10f + 0.707f * 0.30f + 0.707f * 0.40f, lr[0], 5);
        Assert.Equal(0.20f + 0.707f * 0.30f + 0.707f * 0.50f, lr[1], 5);
    }

    [Fact]
    public void 섞은_값은_1_을_넘지_않는다()
    {
        byte[] frame = Floats(1f, -1f, 1f, 0f, 1f, -1f, 1f, -1f);
        var output = Array.Empty<byte>();

        int written = ChannelDownmix.Convert(frame, 8, 4, 2, ref output);

        Assert.Equal(new[] { 1f, -1f }, AsFloats(output, written));
    }

    [Fact]
    public void 모노는_두_채널로_복제한다()
    {
        byte[] frames = Floats(0.25f, -0.5f);
        var output = Array.Empty<byte>();

        int written = ChannelDownmix.Convert(frames, 1, 4, 2, ref output);

        Assert.Equal(new[] { 0.25f, 0.25f, -0.5f, -0.5f }, AsFloats(output, written));
    }

    [Fact]
    public void 정수_PCM_도_샘플_단위로_옮긴다()
    {
        // 16bit 스테레오 2프레임 → 모노: 각 프레임의 왼쪽 샘플(2바이트)만 남는다.
        byte[] pcm = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var output = Array.Empty<byte>();

        int written = ChannelDownmix.Convert(pcm, 2, 2, 1, ref output);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x05, 0x06 }, output.AsSpan(0, written).ToArray());
    }

    [Fact]
    public void 자리가_모자라면_버퍼를_늘리고_넉넉하면_그대로_쓴다()
    {
        byte[] frame = Floats(0.1f, 0.2f, 0.3f, 0f, 0.4f, 0.5f, 0.6f, 0.7f);
        var output = new byte[2];

        ChannelDownmix.Convert(frame, 8, 4, 2, ref output);
        Assert.True(output.Length >= 2 * sizeof(float));

        var kept = output;
        ChannelDownmix.Convert(frame, 8, 4, 2, ref output);
        Assert.Same(kept, output);
    }

    [Fact]
    public void 잘못된_형식은_아무것도_쓰지_않는다()
    {
        var output = Array.Empty<byte>();
        Assert.Equal(0, ChannelDownmix.Convert(Floats(0.1f), 0, 4, 2, ref output));
        Assert.Equal(0, ChannelDownmix.Convert(Floats(0.1f), 2, 0, 2, ref output));
    }
}
