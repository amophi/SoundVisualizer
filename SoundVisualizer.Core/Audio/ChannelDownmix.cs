// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Runtime.InteropServices;

namespace SoundVisualizer.Audio
{
    /// <summary>
    /// 캡처한 소리의 채널 수를 출력 장치에 맞춘다(VB-CABLE 로 받은 소리를 실제 스피커로 다시 내보낼 때).
    ///
    /// 입력은 이번 버퍼에서 **실제로 녹음된 바이트만** 받는다. NAudio 는 캡처 버퍼를 재사용하므로 배열 전체를 넘기면
    /// 지난 버퍼의 남은 소리까지 스피커로 나간다.
    ///
    /// 7.1·5.1 → 2ch 는 ITU-R BS.775 계수로 섞고 LFE 는 뺀다. 채널 순서는 WASAPI 표준을 따른다.
    /// 7.1 은 FL, FR, FC, LFE, BL, BR, SL, SR 이고, 5.1 은 FL, FR, FC, LFE 뒤에 서라운드 한 쌍이다.
    /// </summary>
    public static class ChannelDownmix
    {
        private const float KCenter = 0.707f; // -3dB
        private const float KSide = 0.707f;   // -3dB
        private const float KBack = 0.500f;   // -6dB

        /// <summary>
        /// <paramref name="input"/> 의 채널 수를 <paramref name="outChannels"/> 로 바꿔 <paramref name="outputBuffer"/> 앞쪽에 쓴다.
        /// 버퍼가 모자라면 넉넉하게 새로 만든다. 쓴 바이트 수를 돌려준다.
        /// 7.1·5.1 float → 2ch 는 섞고, 그 밖에는 앞쪽 채널을 골라 쓰거나 첫 채널을 복제한다.
        /// </summary>
        public static int Convert(ReadOnlySpan<byte> input, int inChannels, int bytesPerSample, int outChannels, ref byte[] outputBuffer)
        {
            if (inChannels <= 0 || outChannels <= 0 || bytesPerSample <= 0) return 0;

            int frames = input.Length / (inChannels * bytesPerSample);
            int neededSize = frames * outChannels * bytesPerSample;
            if (outputBuffer.Length < neededSize)
            {
                outputBuffer = new byte[neededSize * 2]; // 다음 버퍼가 조금 커도 다시 만들지 않게 넉넉하게
            }

            var output = outputBuffer.AsSpan(0, neededSize);
            var usedInput = input[..(frames * inChannels * bytesPerSample)];

            if (bytesPerSample == 4 && outChannels == 2 && inChannels == 8)
            {
                Downmix8To2(MemoryMarshal.Cast<byte, float>(usedInput), MemoryMarshal.Cast<byte, float>(output));
                return neededSize;
            }
            if (bytesPerSample == 4 && outChannels == 2 && inChannels == 6)
            {
                Downmix6To2(MemoryMarshal.Cast<byte, float>(usedInput), MemoryMarshal.Cast<byte, float>(output));
                return neededSize;
            }

            // 샘플 단위로 바이트를 옮긴다. float 가 아닌 형식(16·24bit PCM)도 그대로 맞는다.
            for (int f = 0; f < frames; f++)
            {
                for (int ch = 0; ch < outChannels; ch++)
                {
                    int srcCh = ch < inChannels ? ch : 0;
                    usedInput.Slice((f * inChannels + srcCh) * bytesPerSample, bytesPerSample)
                        .CopyTo(output.Slice((f * outChannels + ch) * bytesPerSample, bytesPerSample));
                }
            }
            return neededSize;
        }

        /// <summary>L = FL + 0.707·FC + 0.707·SL + 0.5·BL, R 도 같은 꼴. LFE 는 뺀다.</summary>
        private static void Downmix8To2(ReadOnlySpan<float> src, Span<float> dst)
        {
            int frames = src.Length / 8;
            for (int i = 0; i < frames; i++)
            {
                int o = i * 8;
                float fl = src[o + 0];
                float fr = src[o + 1];
                float fc = src[o + 2];
                // o + 3 은 LFE
                float bl = src[o + 4];
                float br = src[o + 5];
                float sl = src[o + 6];
                float sr = src[o + 7];

                dst[i * 2 + 0] = Math.Clamp(fl + KCenter * fc + KSide * sl + KBack * bl, -1f, 1f);
                dst[i * 2 + 1] = Math.Clamp(fr + KCenter * fc + KSide * sr + KBack * br, -1f, 1f);
            }
        }

        /// <summary>L = FL + 0.707·FC + 0.707·서라운드 L, R 도 같은 꼴. LFE 는 뺀다.</summary>
        private static void Downmix6To2(ReadOnlySpan<float> src, Span<float> dst)
        {
            int frames = src.Length / 6;
            for (int i = 0; i < frames; i++)
            {
                int o = i * 6;
                float fl = src[o + 0];
                float fr = src[o + 1];
                float fc = src[o + 2];
                // o + 3 은 LFE
                float sl = src[o + 4];
                float sr = src[o + 5];

                dst[i * 2 + 0] = Math.Clamp(fl + KCenter * fc + KSide * sl, -1f, 1f);
                dst[i * 2 + 1] = Math.Clamp(fr + KCenter * fc + KSide * sr, -1f, 1f);
            }
        }
    }
}
