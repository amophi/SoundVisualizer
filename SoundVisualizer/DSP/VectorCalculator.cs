using System;
using System.Linq;

namespace SoundVisualizer.DSP
{
    public class VectorCalculator
    {
        // 윈도우 7.1채널 표준 인덱스 매핑 (총 8가닥)
        // 0: FL (앞-좌), 1: FR (앞-우), 2: FC (센터), 3: LFE (우퍼)
        // 4: BL (뒤-좌), 5: BR (뒤-우), 6: SL (옆-좌), 7: SR (옆-우)

        public (double L, double R, double F, double B, bool IsActive) CalculateDirection(byte[] rawAudioData, int bytesRecorded, int channelCount = 8)
        {
            int floatCount = bytesRecorded / 4;
            float[] samples = new float[floatCount];
            Buffer.BlockCopy(rawAudioData, 0, samples, 0, bytesRecorded);

            float frontLeft = 0, frontRight = 0, center = 0;
            float backLeft = 0, backRight = 0;
            float sideLeft = 0, sideRight = 0;

            // 채널 수에 따른 다이내믹 루프
            for (int i = 0; i < samples.Length - (channelCount - 1); i += channelCount)
            {
                if (channelCount >= 2)
                {
                    frontLeft = Math.Max(frontLeft, Math.Abs(samples[i + 0]));
                    frontRight = Math.Max(frontRight, Math.Abs(samples[i + 1]));
                }
                
                if (channelCount >= 8)
                {
                    center = Math.Max(center, Math.Abs(samples[i + 2]));
                    // 인덱스 4,5: 사이드(Side) / 6,7: 백(Back)으로 매핑 순서 조정 (일반적인 7.1 가상화 엔진 기준)
                    sideLeft = Math.Max(sideLeft, Math.Abs(samples[i + 4]));
                    sideRight = Math.Max(sideRight, Math.Abs(samples[i + 5]));
                    backLeft = Math.Max(backLeft, Math.Abs(samples[i + 6]));
                    backRight = Math.Max(backRight, Math.Abs(samples[i + 7]));
                }
                else if (channelCount == 2)
                {
                    // 스테레오일 경우 인위적으로 센터 채널(L+R) 생성
                    center = Math.Max(center, (Math.Abs(samples[i + 0]) + Math.Abs(samples[i + 1])) / 2.0f);
                }
            }

            float maxVolume = new[] { frontLeft, frontRight, center, backLeft, backRight, sideLeft, sideRight }.Max();
            if (maxVolume < 0.01f) return (0, 0, 0, 0, false);

            double L = frontLeft + sideLeft + backLeft;
            double R = frontRight + sideRight + backRight;
            double F = frontLeft + center + frontRight;
            double B = backLeft + backRight;

            double percentL = (L + R > 0) ? (L / (L + R)) * 100.0 : 0;
            double percentR = (L + R > 0) ? (R / (L + R)) * 100.0 : 0;
            double percentF = (F + B > 0) ? (F / (F + B)) * 100.0 : 0;
            double percentB = (F + B > 0) ? (B / (F + B)) * 100.0 : 0;

            return (percentL, percentR, percentF, percentB, true);
        }

        public (float FL, float FR, float FC, float BL, float BR, float SL, float SR, float LFE) CalculateVolumes(byte[] rawAudioData, int bytesRecorded, int channelCount = 8)
        {
            int floatCount = bytesRecorded / 4;
            float[] samples = new float[floatCount];
            Buffer.BlockCopy(rawAudioData, 0, samples, 0, bytesRecorded);

            float fl = 0, fr = 0, fc = 0, lfe = 0;
            float bl = 0, br = 0, sl = 0, sr = 0;

            for (int i = 0; i < samples.Length - (channelCount - 1); i += channelCount)
            {
                if (channelCount >= 2)
                {
                    fl = Math.Max(fl, Math.Abs(samples[i + 0]));
                    fr = Math.Max(fr, Math.Abs(samples[i + 1]));
                }

                if (channelCount >= 8)
                {
                    fc = Math.Max(fc, Math.Abs(samples[i + 2]));
                    lfe = Math.Max(lfe, Math.Abs(samples[i + 3]));
                    // 매핑 순서: 4,5(Side) / 6,7(Back)
                    sl = Math.Max(sl, Math.Abs(samples[i + 4]));
                    sr = Math.Max(sr, Math.Abs(samples[i + 5]));
                    bl = Math.Max(bl, Math.Abs(samples[i + 6]));
                    br = Math.Max(br, Math.Abs(samples[i + 7]));
                }
                else if (channelCount == 2)
                {
                    // 스테레오 모드: L/R을 FL/FR에 매핑하고, 센터는 합성
                    fc = (fl + fr) / 2.0f;
                }
            }

            return (fl, fr, fc, bl, br, sl, sr, lfe);
        }
    }
}