using System;
using System.Windows;
using System.Windows.Media;

namespace SoundVisualizer.Visualizers
{
    public class PadVisualizer : IVisualizerMode
    {
        private const int N = 16; // 폴리곤 해상도 최적화 (기존 30 -> 16)
        
        // 8개 채널 각각의 패드 두께를 독립적으로 저장하여 부드러운 애니메이션 적용
        private readonly double[] _padThicknesses = new double[8];
        
        // 매 프레임 가비지 할당을 차단하기 위해 셰이프 지오메트리 계산용 포인트 배열 캐싱 (GC Free)
        private readonly Point[] _outerPts = new Point[N + 1];
        private readonly Point[] _innerPts = new Point[N + 1];

        public Geometry GenerateGeometry(VisualizerContext context)
        {
            double w = context.Width;
            double h = context.Height;
            double P = 2 * (w + h);
            
            // 0번 모드(Wave)와 동일하게 매핑된 소리 깊이(Volume) 배열을 사용
            // 0:FC, 1:FR, 2:SR, 3:BR, 4:BC(Phantom), 5:BL, 6:SL, 7:FL
            double[] depths = context.ChannelDepths;

            // 각 채널이 모니터 테두리 상에서 위치할 중심점(Distance)
            double[] centerDists = new double[]
            {
                0,                             // 0: 상단 중앙 (FC)
                w / 2.0,                       // 1: 우상단 (FR)
                w / 2.0 + h / 2.0,             // 2: 우측 중앙 (SR)
                w / 2.0 + h,                   // 3: 우하단 (BR)
                w / 2.0 + h + w / 2.0,         // 4: 하단 중앙 (BC)
                w / 2.0 + h + w,               // 5: 좌하단 (BL)
                w / 2.0 + h + w + h / 2.0,     // 6: 좌측 중앙 (SL)
                w / 2.0 + h + w + h            // 7: 좌상단 (FL)
            };

            var geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            bool isAnyVisible = false;

            using (var ctx = geometry.Open())
            {
                for (int c = 0; c < 8; c++)
                {
                    // 파도(Wave) 모드용으로 크게 스케일된 깊이를 패드 두께에 맞게 조정 (크기 상향)
                    double targetThickness = depths[c] * 0.25; 
                    if (targetThickness > 55) targetThickness = 55; // 최대 두께 제한
                    
                    // 부드러운 애니메이션 (Lerp)
                    _padThicknesses[c] += (targetThickness - _padThicknesses[c]) * 0.3;

                    if (_padThicknesses[c] < 0.5) continue; // 소리가 거의 없으면 그리지 않음

                    isAnyVisible = true;
                    double centerDist = centerDists[c];
                    double maxThickness = _padThicknesses[c];
                    
                    // 패드 길이 설정 (디스플레이 높이의 1/4 정도)
                    double barLen = h / 4.0; 
                    double startDist = centerDist - barLen / 2.0;

                    for (int i = 0; i <= N; i++)
                    {
                        double distPos = startDist + (barLen * i) / N;
                        Point edgePoint = GetEdgePosition(distPos, w, h, P);
                        _outerPts[i] = edgePoint;

                        // 패드의 양 끝단을 유선형(물방울 모양)으로 부드럽게 깎음
                        double t = (double)i / N;
                        double ease = Math.Sin(t * Math.PI); 
                        ease = Math.Pow(ease, 0.6); 
                        double currentThickness = maxThickness * ease;

                        double dMod = ((distPos % P) + P) % P;
                        double ix = edgePoint.X, iy = edgePoint.Y;

                        // 테두리 방향에 따라 내측(Inward) 좌표를 밀어넣음
                        if (dMod <= w / 2.0 || dMod > w / 2.0 + h + w + h) // Top
                        { iy += currentThickness; ix = Math.Max(currentThickness, Math.Min(w - currentThickness, ix)); }
                        else if (dMod <= w / 2.0 + h) // Right
                        { ix -= currentThickness; iy = Math.Max(currentThickness, Math.Min(h - currentThickness, iy)); }
                        else if (dMod <= w / 2.0 + h + w) // Bottom
                        { iy -= currentThickness; ix = Math.Max(currentThickness, Math.Min(w - currentThickness, ix)); }
                        else // Left
                        { ix += currentThickness; iy = Math.Max(currentThickness, Math.Min(h - currentThickness, iy)); }

                        _innerPts[i] = new Point(ix, iy);
                    }

                    // 도형 그리기
                    ctx.BeginFigure(_outerPts[0], true, true);
                    for (int i = 1; i <= N; i++) ctx.LineTo(_outerPts[i], false, false);
                    for (int i = N; i >= 0; i--) ctx.LineTo(_innerPts[i], false, false);
                }
            }

            if (!isAnyVisible) return Geometry.Empty;

            geometry.Freeze();
            return geometry;
        }

        private Point GetEdgePosition(double dist, double w, double h, double P)
        {
            dist = ((dist % P) + P) % P;
            if (dist <= w / 2) return new Point(w / 2 + dist, 0); // 상단 중앙 -> 우상단
            if (dist <= w / 2 + h) return new Point(w, dist - w / 2); // 우상단 -> 우하단
            if (dist <= w / 2 + h + w) return new Point(w - (dist - (w / 2 + h)), h); // 우하단 -> 좌하단
            if (dist <= w / 2 + h + w + h) return new Point(0, h - (dist - (w / 2 + h + w))); // 좌하단 -> 좌상단
            return new Point(dist - (w / 2 + h + w + h), 0); // 좌상단 -> 상단 중앙
        }

        public Brush GetFillBrush(Color activeColor)
        {
            var brush = new SolidColorBrush(activeColor);
            brush.Freeze();
            return brush;
        }
    }
}
