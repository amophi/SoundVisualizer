using System.Windows.Media;

namespace SoundVisualizer.Visualizers
{
    public interface IVisualizerMode
    {
        Geometry GenerateGeometry(VisualizerContext context);
        Brush GetFillBrush(Color activeColor);
    }

    public class VisualizerContext
    {
        public double Width { get; set; }
        public double Height { get; set; }
        // 0:TopCenter, 1:TopRight, 2:RightCenter, 3:BottomRight, 4:BottomCenter, 5:BottomLeft, 6:LeftCenter, 7:TopLeft
        public double[] ChannelDepths { get; set; } = new double[8];
        public double[] ChannelDists { get; set; } = new double[8];
        public float TotalVolume { get; set; }
        public double AnimationTime { get; set; }
    }
}
