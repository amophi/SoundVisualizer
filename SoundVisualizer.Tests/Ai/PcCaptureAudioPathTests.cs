// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Runtime.InteropServices;
using SoundVisualizer.Ai;
using SoundVisualizer.Audio;

namespace SoundVisualizer.Tests.Ai;

/// <summary>
/// PC 에서 더한 AI 입력 경로(Android 에는 없는 5.1·7.1 섞기와 88.2·96·176.4·192kHz 변환)와,
/// 옮기면서 따로 확인하는 링 버퍼의 보관 규칙과 스레드 계약을 본다.
/// </summary>
public class PcCaptureAudioPathTests
{
    public static TheoryData<int> PcHighRates => new() { 88200, 96000, 176400, 192000 };

    // ── 링 버퍼 ──

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void 링은_2초를_넘는_앞부분을_버리고_가장_최근_샘플을_순서대로_돌려준다(int rate)
    {
        var buffer = new AiAudioBuffer(captureSampleRate: rate, channels: 1);
        int retained = Math.Max(AiAudioBuffer.DefaultCaptureSampleRate, rate) * AiAudioBuffer.RingSeconds;
        int total = AiAudioBuffer.MaxRingSize + 70_000; // 링을 한 바퀴 넘게 돈다
        var ramp = new float[total];
        for (int i = 0; i < total; i++) ramp[i] = i + 1;

        buffer.IngestMono(ramp.AsSpan(0, 1000));
        buffer.IngestMono(ramp.AsSpan(1000));

        Assert.Equal(retained, buffer.AvailableSamples);
        var all = new float[retained + 10];
        Assert.Equal(retained, buffer.CopyTailRightPadded(all));
        Assert.All(all[..10], value => Assert.Equal(0f, value));
        Assert.Equal(ramp[^retained..], all[10..]);

        buffer.Reset();
        Assert.Equal(0, buffer.AvailableSamples);
        Assert.False(buffer.HasEnoughForYamnetWindow());
    }

    [Fact]
    public async Task 캡처_스레드가_넣는_동안_추론_스레드가_가져가도_창이_끊기지_않는다()
    {
        // Android 의 synchronized 와 같은 계약: 한 번 넣은 버퍼는 통째로 보이거나 아예 보이지 않는다.
        var buffer = new AiAudioBuffer(captureSampleRate: 48000, channels: 2);
        const int totalFrames = 3_000_000; // float 가 정수를 정확히 담는 범위(2^24) 안
        var producer = Task.Run(() =>
        {
            var random = new Random(7);
            var chunk = new float[2 * 4096];
            int written = 0;
            while (written < totalFrames)
            {
                int frames = Math.Min(random.Next(1, 4097), totalFrames - written);
                for (int f = 0; f < frames; f++)
                {
                    float value = written + f + 1; // 0 은 왼쪽 채움과 구분한다
                    chunk[2 * f] = value + 0.25f;
                    chunk[2 * f + 1] = value - 0.25f;
                }
                buffer.IngestInterleaved(chunk.AsSpan(0, 2 * frames));
                written += frames;
            }
        });

        var window = new float[CaptureAudioMath.CaptureSamplesForOneYamnetWindow(48000)];
        int snapshots = 0;
        while (!producer.IsCompleted || snapshots == 0)
        {
            int available = buffer.CopyTailRightPadded(window);
            int start = window.Length - Math.Min(available, window.Length);
            int broken = -1;
            for (int i = 0; i < window.Length && broken < 0; i++)
            {
                bool ok = i < start ? window[i] == 0f : i == start || window[i] == window[i - 1] + 1f;
                if (!ok) broken = i;
            }
            Assert.True(broken < 0, $"snapshot {snapshots} broke at {broken}");
            snapshots++;
        }
        await producer;

        buffer.CopyTailRightPadded(window);
        Assert.Equal((float)totalFrames, window[^1]);
    }

    // ── 채널 섞기 ──

    [Fact]
    public void 모노_입력은_그대로_쓴다()
    {
        float[] mono = { 0.25f, -0.5f, 1.5f };
        var dest = new float[3];

        Assert.Equal(3, CaptureAudioMath.DownmixInterleavedToMono(mono, 1, dest));
        Assert.Equal(mono, dest);
    }

    [Fact]
    public void 오점일_채널은_스테레오로_섞은_뒤_좌우를_평균하고_LFE는_뺀다()
    {
        // FL, FR, FC, LFE, SL, SR
        float[] frame = { 0.10f, 0.20f, 0.30f, 0.90f, 0.40f, 0.50f };
        var dest = new float[1];

        Assert.Equal(1, CaptureAudioMath.DownmixInterleavedToMono(frame, 6, dest));

        float left = 0.10f + 0.707f * 0.30f + 0.707f * 0.40f;
        float right = 0.20f + 0.707f * 0.30f + 0.707f * 0.50f;
        Assert.Equal((left + right) / 2f, dest[0], 1e-6f);
    }

    [Fact]
    public void 칠점일_채널은_스테레오로_섞은_뒤_좌우를_평균하고_LFE는_뺀다()
    {
        // FL, FR, FC, LFE, BL, BR, SL, SR
        float[] frame = { 0.10f, 0.20f, 0.30f, 0.90f, 0.40f, 0.50f, 0.06f, 0.07f };
        var dest = new float[1];

        Assert.Equal(1, CaptureAudioMath.DownmixInterleavedToMono(frame, 8, dest));

        float left = 0.10f + 0.707f * 0.30f + 0.707f * 0.06f + 0.5f * 0.40f;
        float right = 0.20f + 0.707f * 0.30f + 0.707f * 0.07f + 0.5f * 0.50f;
        Assert.Equal((left + right) / 2f, dest[0], 1e-6f);
    }

    [Theory]
    [InlineData(8, 4, 0.5f)]   // BL
    [InlineData(8, 5, 0.5f)]   // BR
    [InlineData(8, 6, 0.707f)] // SL
    [InlineData(8, 7, 0.707f)] // SR
    [InlineData(6, 4, 0.707f)] // SL
    [InlineData(6, 5, 0.707f)] // SR
    [InlineData(8, 2, 1.414f)] // FC 는 좌우 모두에 들어간다
    [InlineData(8, 3, 0f)]     // LFE
    [InlineData(6, 3, 0f)]     // LFE
    public void 뒤와_옆_채널로만_나는_소리도_AI_입력에_들어간다(int channels, int channel, float stereoGain)
    {
        // 예전 PC 코드는 FL·FR·FC 만 AI 에 넣어서, 뒤·옆 스피커로만 나는 소리는 AI 가 듣지 못했다.
        var frame = new float[channels];
        frame[channel] = 0.5f;
        var mono = new float[1];

        CaptureAudioMath.DownmixInterleavedToMono(frame, channels, mono);

        Assert.Equal(0.5f * stereoGain / 2f, mono[0], 1e-6f);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void AI_섞기는_스피커용과_같은_계수를_쓰되_1을_넘어도_자르지_않는다(int channels)
    {
        // 1 을 넘지 않는 소리는 스피커용 스테레오(ChannelDownmix)의 (L+R)/2 와 비트까지 같다.
        float[] quiet = { 0.10f, -0.20f, 0.05f, 0.90f, 0.15f, -0.25f, 0.03f, 0.04f };
        float[] quietFrame = quiet[..channels];
        float[] quietStereo = SpeakerStereo(quietFrame, channels);
        var mono = new float[1];
        CaptureAudioMath.DownmixInterleavedToMono(quietFrame, channels, mono);
        Assert.Equal((quietStereo[0] + quietStereo[1]) / 2f, mono[0]);

        // 큰 소리(모든 채널 0.9)는 스피커용이 L·R 을 1 로 자르지만, AI 쪽은 BS.775 합을 그대로 둔다(L 과 R 이 같다).
        float[] loudFrame = Enumerable.Repeat(0.9f, channels).ToArray();
        Assert.Equal(new[] { 1f, 1f }, SpeakerStereo(loudFrame, channels));
        float loudSurround = channels == 8 ? 0.707f * 0.9f + 0.5f * 0.9f : 0.707f * 0.9f;
        float loudLeft = 0.9f + 0.707f * 0.9f + loudSurround;
        CaptureAudioMath.DownmixInterleavedToMono(loudFrame, channels, mono);
        Assert.Equal(loudLeft, mono[0], 1e-5f);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void 그_밖의_채널_수는_모든_채널의_평균이다(int channels)
    {
        var interleaved = new float[channels * 2 + 1]; // 끝의 1개는 짝이 없어 버린다
        for (int i = 0; i < interleaved.Length; i++) interleaved[i] = (i + 1) * 0.01f;
        var mono = new float[2];

        Assert.Equal(2, CaptureAudioMath.DownmixInterleavedToMono(interleaved, channels, mono));

        for (int f = 0; f < 2; f++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += interleaved[f * channels + c];
            Assert.Equal(sum / channels, mono[f]);
        }
    }

    [Fact]
    public void 받을_자리가_모자라거나_형식이_잘못되면_쓰지_않는다()
    {
        Assert.Throws<ArgumentException>(() => CaptureAudioMath.DownmixInterleavedToMono(new float[16], 8, new float[1]));
        Assert.Equal(0, CaptureAudioMath.DownmixInterleavedToMono(new float[16], 0, new float[16]));
        Assert.Equal(0, CaptureAudioMath.DownmixInterleavedToMono(new float[7], 8, new float[1]));
        Assert.Equal(0, CaptureAudioMath.DownmixInterleavedToMono(ReadOnlySpan<float>.Empty, 2, new float[1]));
    }

    [Fact]
    public void 링은_칠점일_채널을_섞어서_쌓는다()
    {
        var buffer = new AiAudioBuffer(captureSampleRate: 48000, channels: 8);
        float[] pcm =
        {
            0.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,   // FL 만
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.4f,   // SR 만
        };

        buffer.IngestInterleaved(pcm);

        Assert.Equal(2, buffer.AvailableSamples);
        var dest = new float[2];
        buffer.CopyTailRightPadded(dest);
        Assert.Equal(0.4f / 2f, dest[0], 1e-6f);
        Assert.Equal(0.707f * 0.4f / 2f, dest[1], 1e-6f);
    }

    [Fact]
    public void 임시_버퍼보다_긴_캡처_버퍼도_나눠서_빠짐없이_넣는다()
    {
        const int channels = 8;
        int frames = AiAudioBuffer.MaxRingSize / 2 + 1000; // 섞을 때 쓰는 임시 버퍼(65,536프레임)보다 길다
        var pcm = new float[frames * channels];
        for (int f = 0; f < frames; f++)
        {
            pcm[f * channels] = f / (float)frames;          // FL
            pcm[f * channels + 6] = 0.5f - f / (float)frames; // SL
        }
        var buffer = new AiAudioBuffer(captureSampleRate: 48000, channels: channels);

        buffer.IngestInterleaved(pcm);

        Assert.Equal(frames, buffer.AvailableSamples);
        var expected = new float[frames];
        CaptureAudioMath.DownmixInterleavedToMono(pcm, channels, expected);
        var actual = new float[frames];
        buffer.CopyTailRightPadded(actual);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(3)] // LFE 만
    [InlineData(5)] // BR 만
    [InlineData(7)] // SR 만
    public void 무음_게이트는_채널_수와_상관없이_섞기_전_어느_채널로든_열린다(int channel)
    {
        // LFE 는 AI 입력에서 빠지지만, 소리가 났다는 것은 Android 처럼 섞기 전 최고값으로 본다.
        var quiet = new float[8];
        Array.Fill(quiet, 0.01f);
        var gate = new AiSilenceGate();
        gate.OnInterleavedPcm(quiet, nowMs: 0);
        Assert.False(gate.IsOpen(0));

        var frame = new float[8];
        frame[channel] = -0.02f;
        gate.OnInterleavedPcm(frame, nowMs: 0);
        Assert.True(gate.IsOpen(0));
    }

    // ── 88.2·96·176.4·192kHz ──

    [Theory]
    [InlineData(16000, 96)]
    [InlineData(22050, 96)] // AI 가 받지 않는 레이트는 Android 와 같은 96탭
    [InlineData(44100, 96)]
    [InlineData(48000, 96)]
    [InlineData(88200, 192)]
    [InlineData(96000, 192)]
    [InlineData(176400, 384)]
    [InlineData(192000, 384)]
    public void FIR_탭_수는_전이_대역의_폭이_같도록_레이트에_비례한다(int rate, int taps)
    {
        Assert.Equal(taps, CaptureAudioMath.FirTapsFor(rate));
    }

    [Theory]
    [InlineData(88200, 85995)]
    [InlineData(96000, 93600)]
    [InlineData(176400, 171990)]
    [InlineData(192000, 187200)]
    public void PC_고레이트의_한_창_길이는_16kHz_15600샘플을_빠짐없이_덮는다(int rate, int windowSamples)
    {
        Assert.Equal(windowSamples, CaptureAudioMath.CaptureSamplesForOneYamnetWindow(rate));
        // 마지막 출력 샘플의 원본 위치까지 창 안에 있고, 한 샘플이라도 줄이면 모자란다.
        long lastPosition = (long)(CaptureAudioMath.RequiredMono16kSamples - 1) * rate / CaptureAudioMath.TargetSampleRate;
        Assert.True(lastPosition < windowSamples);
        Assert.True((long)(windowSamples - 1) * CaptureAudioMath.TargetSampleRate < (long)CaptureAudioMath.RequiredMono16kSamples * rate);
    }

    [Theory]
    [MemberData(nameof(PcHighRates))]
    public void PC_고레이트도_한_창_길이의_상수_입력은_끝까지_유한하고_안쪽은_1이다(int rate)
    {
        CaptureAudioProbe.AssertConstantWindowResponse(rate);
    }

    [Theory]
    [MemberData(nameof(PcHighRates))]
    public void PC_고레이트도_원본_범위_밖은_0으로_보고_가장자리는_FIR_일부만_더한다(int rate)
    {
        var source = new float[rate / 125]; // 8ms
        Array.Fill(source, 1f);
        var destination = new float[64];

        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, rate, destination);

        Assert.All(destination, value => Assert.True(float.IsFinite(value)));
        Assert.True(destination[0] > 0f && destination[0] < 1f, "left edge must use a partial FIR sum");
        Assert.Equal(1f, destination[32], 1e-5f);
    }

    [Theory]
    [MemberData(nameof(PcHighRates))]
    public void PC_고레이트에서_1kHz_사인은_거의_그대로_남는다(int rate)
    {
        int windowSamples = CaptureAudioMath.CaptureSamplesForOneYamnetWindow(rate);
        var source = new float[windowSamples];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (float)(0.5 * Math.Sin(2.0 * Math.PI * 1000.0 * i / rate));
        }
        var mono16 = new float[CaptureAudioMath.RequiredMono16kSamples];

        CaptureAudioMath.ResampleMonoFloatTo16kCustom(source, rate, mono16);

        // 창 가장자리 32개는 원본 밖을 0 으로 보는 구간이라 뺀다. 1e-5 는 44.1·48kHz 기준 데이터의 허용 오차와 같다.
        double maxError = 0;
        for (int i = 32; i < mono16.Length - 32; i++)
        {
            double ideal = 0.5 * Math.Sin(2.0 * Math.PI * 1000.0 * i / CaptureAudioMath.TargetSampleRate);
            maxError = Math.Max(maxError, Math.Abs(mono16[i] - ideal));
        }
        Assert.True(maxError < 1e-5, $"{rate} Hz 1 kHz sine max error {maxError}");
    }

    [Theory]
    [MemberData(nameof(PcHighRates))]
    public void PC_고레이트도_안드로이드와_같은_통과대역과_저지대역_요건을_채운다(int rate)
    {
        // 96탭을 그대로 쓰면 96kHz 에서 7kHz 가 -1.9dB, 9kHz 가 -20dB 에 그쳐 여기서 걸린다.
        CaptureAudioProbe.AssertFrequencyAcceptance(rate);
    }

    [Theory]
    [InlineData(88200, 12000.0)]
    [InlineData(96000, 12000.0)]
    [InlineData(176400, 12000.0)]
    [InlineData(192000, 12000.0)]
    [InlineData(88200, 30000.0)]  // 2kHz 로 접힌다
    [InlineData(96000, 30000.0)]
    [InlineData(176400, 70000.0)] // 6kHz 로 접힌다
    [InlineData(192000, 70000.0)]
    public void PC_고레이트에서_8kHz_위의_소리는_80dB_넘게_줄어든다(int rate, double frequencyHz)
    {
        // β 8.6 Kaiser 설계의 저지대역 감쇠는 약 86dB(Kaiser 식 β = 0.1102·(A − 8.7))다. 80dB 는 float 계수 반올림에 6dB 여유를
        // 둔 값으로, Android 가 15kHz 에 거는 60dB 보다 엄격하다. 측정값은 12kHz 에서 96~104dB 다.
        // 96탭을 그대로 쓰면 176.4·192kHz 에서 12kHz 가 50·43dB 만 줄어 여기서 걸린다.
        CaptureAudioProbe.AssertGainAtMost(rate, frequencyHz, -80.0);
    }

    [Fact]
    public void 칠점일_96kHz_캡처를_링에서_꺼내_바꾸면_옆_채널의_1kHz가_남는다()
    {
        const int rate = 96000;
        const int channels = 8;
        const int sideLeft = 6;
        var buffer = new AiAudioBuffer(captureSampleRate: rate, channels: channels);
        var ingress = new AiCaptureInferenceGate(buffer);
        int frames10Ms = rate / 100;
        var chunk = new float[frames10Ms * channels];
        int totalFrames = rate * 6 / 5; // 1.2초
        for (int start = 0; start < totalFrames; start += frames10Ms)
        {
            Array.Clear(chunk);
            for (int f = 0; f < frames10Ms; f++)
            {
                chunk[f * channels + sideLeft] = (float)(0.5 * Math.Sin(2.0 * Math.PI * 1000.0 * (start + f) / rate));
            }
            ingress.IngestInterleaved(chunk, nowMs: start * 1000L / rate);
        }
        Assert.True(ingress.IsInferenceOpen(totalFrames * 1000L / rate));
        Assert.True(buffer.HasEnoughForYamnetWindow());

        var capture = new float[CaptureAudioMath.CaptureSamplesForOneYamnetWindow(rate)];
        buffer.CopyTailRightPadded(capture);
        var mono16 = new float[CaptureAudioMath.RequiredMono16kSamples];
        CaptureAudioMath.ResampleMonoFloatTo16kCustom(capture, rate, mono16);

        // 옆 채널은 0.707 로 왼쪽에만 들어가고 좌우 평균에서 다시 절반이 된다.
        int firstFrame = totalFrames - capture.Length;
        double amplitude = 0.5 * 0.707 / 2.0;
        double maxError = 0;
        for (int i = 32; i < mono16.Length - 32; i++)
        {
            double sourcePosition = firstFrame + (double)i * rate / CaptureAudioMath.TargetSampleRate;
            double ideal = amplitude * Math.Sin(2.0 * Math.PI * 1000.0 * sourcePosition / rate);
            maxError = Math.Max(maxError, Math.Abs(mono16[i] - ideal));
        }
        Assert.True(maxError < 1e-5, $"7.1 96 kHz side-channel sine max error {maxError}");
    }

    private static float[] SpeakerStereo(float[] frame, int channels)
    {
        byte[] input = MemoryMarshal.AsBytes(frame.AsSpan()).ToArray();
        var output = Array.Empty<byte>();
        int written = ChannelDownmix.Convert(input, channels, sizeof(float), 2, ref output);
        return MemoryMarshal.Cast<byte, float>(output.AsSpan(0, written)).ToArray();
    }
}
