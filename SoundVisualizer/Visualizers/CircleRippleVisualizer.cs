using System;
using System.Windows;
using System.Windows.Media;

namespace SoundVisualizer.Visualizers
{
    public class CircleRippleVisualizer : IVisualizerMode
    {
        private const int N = 64; // 원을 그릴 점의 해상도 최적화 (기존 120 -> 64)
        private readonly double[] _recentTargets = new double[8];
        
        // 매 프레임 가비지 할당을 없애기 위해 포인트 배열 캐싱 (GC Free)
        private readonly Point[] _outerPts = new Point[N];
        private readonly Point[] _innerPts = new Point[N];

        public Geometry GenerateGeometry(VisualizerContext context)
        {
            double w = context.Width;
            double h = context.Height;
            double cx = w / 2.0;
            double cy = h / 2.0;
            
            // 화면 크기에 비례하며 설정값을 반영하는 중앙의 빈 공간
            // AppSettings.CircleRadius 범위: 10 ~ 100
            // 이를 0.05 ~ 0.40 비율 정도로 매핑
            double radiusRatio = 0.05 + (AppSettings.CircleRadius - 10.0) / 90.0 * 0.35;
            double baseRadius = Math.Min(w, h) * radiusRatio; 

            var geometry = new StreamGeometry();
            // FillRule.EvenOdd를 활용해서 바깥쪽 영역과 안쪽 원의 겹치는 부분을 뚫리게 만듭니다.
            geometry.FillRule = FillRule.EvenOdd;

            bool isAnyVisible = false;
            
            for (int i = 0; i < 8; i++) 
            {
                double targetVal = context.ChannelDepths[i];

                // 파도 모드 대비 스케일 조절
                double target = targetVal * 0.35;
                if (target > h * 0.4) target = h * 0.4; // 폭주 방지용 최댓값 설정

                // 부드러운 애니메이션
                _recentTargets[i] += (target - _recentTargets[i]) * 0.25;
                if (_recentTargets[i] > 1.0) isAnyVisible = true;
            }

            if (!isAnyVisible) return Geometry.Empty;

            using (var ctx = geometry.Open())
            {
                for (int i = 0; i < N; i++)
                {
                    double angle = (2 * Math.PI * i) / N; // 0은 우측 3시 방향, 시계 방향으로 증가
                    
                    // 올바른 각도 매핑: angle=0(3시 방향)일 때 2번 인덱스(우측), pi/2(6시 방향)일 때 4번(하단)
                    double mappedIndex = (angle / (2 * Math.PI)) * 8.0 + 2.0;
                    if (mappedIndex >= 8.0) mappedIndex -= 8.0; // 0~8 범위 내로 유지

                    double depth = GetInterpolatedDepth(_recentTargets, mappedIndex);
                    
                    // 정방향 깊이만 반영하여 꿀렁거림 제거
                    double r = baseRadius + depth;
                    
                    double px = cx + Math.Cos(angle) * r;
                    double py = cy + Math.Sin(angle) * r;
                    _outerPts[i] = new Point(px, py);
                }

                // 바깥쪽 출력 외곽선
                ctx.BeginFigure(_outerPts[0], isFilled: true, isClosed: true);
                for (int i = 1; i < N; i++) ctx.LineTo(_outerPts[i], isStroked: false, isSmoothJoin: true);

                // 안쪽에 역방향(반시계)으로 고정된 원을 그려서 EvenOdd 룰로 인해 구멍을 냄
                for (int i = 0; i < N; i++)
                {
                    double angle = 2 * Math.PI * (N - 1 - i) / N;
                    double px = cx + Math.Cos(angle) * baseRadius;
                    double py = cy + Math.Sin(angle) * baseRadius;
                    _innerPts[i] = new Point(px, py);
                }

                ctx.BeginFigure(_innerPts[0], isFilled: true, isClosed: true);
                for (int i = 1; i < N; i++) ctx.LineTo(_innerPts[i], isStroked: false, isSmoothJoin: true);
            }

            geometry.Freeze();
            return geometry;
        }

        // 값들 사이를 부드럽게 이어주는 코사인 보간법
        private double GetInterpolatedDepth(double[] depths, double index)
        {
            int i0 = (int)Math.Floor(index) % 8;
            int i1 = (i0 + 1) % 8;
            double t = index - Math.Floor(index);

            double ft = (1 - Math.Cos(t * Math.PI)) / 2.0;
            return depths[i0] * (1 - ft) + depths[i1] * ft;
        }

        public Brush GetFillBrush(Color activeColor)
        {
            // 중앙에서 바깥으로 투명해지는 예쁜 방사형 그라데이션 가능 여부 고려
            // 여기서는 깔끔한 투명 단색을 유지
            var brush = new SolidColorBrush(activeColor);
            brush.Freeze();
            return brush;
        }
    }
}