// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// Android 의 CaptureAudioPathTest.kt 를 옮겼다. 캡처 섞기, 앨리어싱을 막는 16kHz 변환, 링 스냅샷을 본다(ONNX 없음).
/// 기준 데이터는 Python 기준 구현(tools/ai_reference)이 만든 것이고 오차 기준도 Android 와 같다.
/// </summary>
public class CaptureAudioPathTests
{
    [Fact]
    public void 한_창에_필요한_캡처_샘플_수는_44100과_48000에서_안드로이드와_같다()
    {
        Assert.Equal(42998, CaptureAudioMath.CaptureSamplesForOneYamnetWindow(44100));
        Assert.Equal(46800, CaptureAudioMath.CaptureSamplesForOneYamnetWindow(48000));
    }

    [Fact]
    public void 스테레오는_좌우를_평균하고_짝이_없는_끝_샘플은_버린다()
    {
        float[] interleaved = { 1f, 3f, 2f, 4f, 0.5f }; // 끝의 0.5 는 짝이 없다
        var dest = new float[4];

        int n = CaptureAudioMath.DownmixInterleavedToMono(interleaved, 2, dest);

        Assert.Equal(2, n);
        Assert.Equal(2f, dest[0], 1e-6f);
        Assert.Equal(3f, dest[1], 1e-6f);
    }

    [Fact]
    public void 최고값은_섞기_전의_두_채널에서_잰다()
    {
        Assert.Equal(0.02f, CaptureAudioMath.MaxAbsoluteInterleavedPeak(new[] { 0.02f, -0.02f }));
    }

    [Fact]
    public void 링은_스테레오를_모노로_쌓고_모자란_앞쪽은_0으로_채운다()
    {
        var buf = new AiAudioBuffer(44100, 2);
        buf.IngestInterleaved(new[] { 1f, 1f, 2f, 2f, 3f, 3f }); // 스테레오 3프레임
        Assert.Equal(3, buf.AvailableSamples);

        var dest = new float[5];
        buf.CopyTailRightPadded(dest);

        Assert.Equal(new[] { 0f, 0f, 1f, 2f, 3f }, dest);
    }

    [Theory]
    [InlineData("silence")]
    [InlineData("gunshot")]
    [InlineData("alarm")]
    public void 실제_소리를_1024개씩_링에_넣어_16kHz로_바꾸면_Python_기준과_같다(string name)
    {
        float[] stereo = CaptureAudioProbe.LoadReference($"e2e_{name}_stereo44100.bin");
        float[] expectedMono16 = CaptureAudioProbe.LoadReference($"e2e_{name}_mono16k.bin");

        var buf = new AiAudioBuffer(44100, 2);
        // AudioRecord 가 float 1,024개씩 읽어 오는 것처럼 나눠 넣는다.
        for (int offset = 0; offset < stereo.Length; offset += 1024)
        {
            int n = Math.Min(1024, stereo.Length - offset);
            buf.IngestInterleaved(stereo.AsSpan(offset, n));
        }
        Assert.True(buf.HasEnoughForYamnetWindow(), $"{name} ring filled");

        int need = CaptureAudioMath.CaptureSamplesForOneYamnetWindow(44100);
        var capture = new float[need];
        buf.CopyTailRightPadded(capture);

        var mono16 = new float[CaptureAudioMath.RequiredMono16kSamples];
        CaptureAudioMath.ResampleMonoFloatTo16kCustom(capture, 44100, mono16);

        float maxAbs = CaptureAudioProbe.MaxAbs(mono16, expectedMono16);
        Assert.True(maxAbs < 1e-5f, $"{name} mono16k maxAbs {maxAbs}");
        // Android 는 이어서 이 mono16 의 log-mel 을 e2e_{name}_logmel.bin 과 1e-4 안으로 비교한다.
        // log-mel 전처리는 이 묶음 밖이라 그 절반은 전처리를 옮기는 테스트가 맡는다.
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void 짧은_기준데이터의_FIR_결과가_Python_기준과_같다(int sampleRate)
    {
        float[] source = CaptureAudioProbe.LoadReference($"resample_parity_{sampleRate}_capture.bin");
        float[] expected = CaptureAudioProbe.LoadReference($"resample_parity_{sampleRate}_mono16k.bin");
        var actual = new float[expected.Length];

        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, sampleRate, actual);

        float maxAbs = CaptureAudioProbe.MaxAbs(actual, expected);
        Assert.True(maxAbs < 1e-5f, $"{sampleRate} FIR parity maxAbs {maxAbs}");
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void 통과대역과_저지대역_요건을_채운다(int sampleRate)
    {
        CaptureAudioProbe.AssertFrequencyAcceptance(sampleRate);
    }

    [Fact]
    public void 입력이_16kHz면_비트까지_그대로_통과한다()
    {
        var source = new float[CaptureAudioMath.RequiredMono16kSamples];
        for (int index = 0; index < source.Length; index++)
        {
            source[index] = BitConverter.Int32BitsToSingle(index * 7919);
        }
        var destination = new float[CaptureAudioMath.RequiredMono16kSamples];

        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, CaptureAudioMath.TargetSampleRate, destination);

        Assert.Equal(
            Array.ConvertAll(source, BitConverter.SingleToInt32Bits),
            Array.ConvertAll(destination, BitConverter.SingleToInt32Bits));
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void 한_창_길이의_상수_입력은_끝까지_유한하고_안쪽은_1이다(int sampleRate)
    {
        CaptureAudioProbe.AssertConstantWindowResponse(sampleRate);
    }

    [Fact]
    public void 원본_범위_밖은_0으로_보고_가장자리는_FIR_일부만_더한다()
    {
        var source = new float[512];
        Array.Fill(source, 1f);
        var destination = new float[64];

        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, 48000, destination);

        Assert.All(destination, value => Assert.True(float.IsFinite(value)));
        Assert.True(destination[0] > 0f && destination[0] < 1f, "left edge must use a partial FIR sum");
        Assert.Equal(1f, destination[32], 1e-5f);
    }
}
