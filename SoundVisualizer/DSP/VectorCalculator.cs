using System;
using System.Linq;

namespace SoundVisualizer.DSP
{
    public class VectorCalculator
    {
        // 윈도우 7.1채널 표준 인덱스 매핑 (총 8가닥)
        // 0: FL (앞-좌), 1: FR (앞-우), 2: FC (센터), 3: LFE (우퍼)
        // 4: BL (뒤-좌), 5: BR (뒤-우), 6: SL (옆-좌), 7: SR (옆-우)

        public (double L, double R, double F, double B, bool IsActive) CalculateDirection(byte[] rawAudioData, int bytesRecorded)
        {
            // 1. byte 배열을 계산하기 쉬운 float(32bit 소수점) 배열로 변환
            // WASAPI Loopback은 기본적으로 32-bit IEEE Float 형태로 데이터를 줍니다.
            int floatCount = bytesRecorded / 4;
            float[] samples = new float[floatCount];
            Buffer.BlockCopy(rawAudioData, 0, samples, 0, bytesRecorded);

            // 각 방향별 최대 볼륨(Peak)을 저장할 변수들
            float frontLeft = 0, frontRight = 0, center = 0;
            float backLeft = 0, backRight = 0;
            float sideLeft = 0, sideRight = 0;

            // 2. 버퍼를 8칸씩 건너뛰며(8채널이니까) 각 방향의 가장 큰 소리(진폭)를 찾습니다.
            for (int i = 0; i < samples.Length - 7; i += 8)
            {
                frontLeft = Math.Max(frontLeft, Math.Abs(samples[i + 0]));
                frontRight = Math.Max(frontRight, Math.Abs(samples[i + 1]));
                center = Math.Max(center, Math.Abs(samples[i + 2]));
                // 우퍼(3번)는 방향성이 없으므로 버립니다.
                backLeft = Math.Max(backLeft, Math.Abs(samples[i + 4]));
                backRight = Math.Max(backRight, Math.Abs(samples[i + 5]));
                sideLeft = Math.Max(sideLeft, Math.Abs(samples[i + 6]));
                sideRight = Math.Max(sideRight, Math.Abs(samples[i + 7]));
            }

            // 3. 임계값(Threshold) 처리: 너무 작은 노이즈(배경음)는 무시!
            float maxVolume = new[] { frontLeft, frontRight, center, backLeft, backRight, sideLeft, sideRight }.Max();
            if (maxVolume < 0.05f)
            {
                // 소리가 너무 작으면 레이더 반응 안 함 (IsActive = false)
                return (0, 0, 0, 0, false);
            }

            // [보완] 스테레오 소스(유튜브 등 2채널) 방어 로직
            float surroundMax = new[] { center, backLeft, backRight, sideLeft, sideRight }.Max();
            bool isStereoOnly = (surroundMax < 0.001f);

            double L = frontLeft + sideLeft + backLeft;
            double R = frontRight + sideRight + backRight;
            double F = frontLeft + center + frontRight;
            double B = backLeft + backRight;

            double percentL = 0, percentR = 0, percentF = 0, percentB = 0;

            // 좌우 계산 (L, R 비율)
            double sumLR = L + R;
            if (sumLR > 0)
            {
                percentL = (L / sumLR) * 100.0;
                percentR = (R / sumLR) * 100.0;
            }

            // 앞뒤 계산 (F, B 비율)
            if (isStereoOnly)
            {
                percentF = 0;
                percentB = 0;
            }
            else
            {
                double sumFB = F + B;
                if (sumFB > 0)
                {
                    percentF = (F / sumFB) * 100.0;
                    percentB = (B / sumFB) * 100.0;
                }
            }

            return (percentL, percentR, percentF, percentB, true);
        }
    }
}