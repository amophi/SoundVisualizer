using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoundVisualizer
{
    public partial class ColorPickerWindow : Window
    {
        public string SelectedHexColor { get; private set; }
        private bool _isUpdating = false;

        private double _h = 0;
        private double _s = 1;
        private double _v = 1;

        private bool _isDraggingHue = false;
        private bool _isDraggingSV = false;

        public ColorPickerWindow(string initialHexColor)
        {
            InitializeComponent();
            SelectedHexColor = initialHexColor;

            if (string.IsNullOrEmpty(initialHexColor) || !initialHexColor.StartsWith("#"))
                initialHexColor = "#FFFF0000";

            TxtHexCode.Text = initialHexColor;
            UpdateFromHex(initialHexColor);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void TxtHexCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || !IsLoaded) return;

            string hex = TxtHexCode.Text.ToUpper();
            if (IsValidHex(hex))
            {
                SelectedHexColor = hex;
                UpdateFromHex(hex);
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating || !IsLoaded) return;

            byte r = (byte)SldRed.Value;
            byte g = (byte)SldGreen.Value;
            byte b = (byte)SldBlue.Value;

            UpdateFromRgb(r, g, b, true);
        }

        private void UpdateFromRgb(byte r, byte g, byte b, bool updateHsv)
        {
            _isUpdating = true;

            string hex = $"#FF{r:X2}{g:X2}{b:X2}";
            SelectedHexColor = hex;
            TxtHexCode.Text = hex;

            if (SldRed != null) SldRed.Value = r;
            if (SldGreen != null) SldGreen.Value = g;
            if (SldBlue != null) SldBlue.Value = b;

            if (TxtRed != null) TxtRed.Text = r.ToString();
            if (TxtGreen != null) TxtGreen.Text = g.ToString();
            if (TxtBlue != null) TxtBlue.Text = b.ToString();

            UpdatePreview(r, g, b);

            if (updateHsv)
            {
                ColorToHsv(Color.FromArgb(255, r, g, b), out _h, out _s, out _v);
                UpdateHsvVisuals();
            }

            _isUpdating = false;
        }

        private void UpdateFromHex(string hex)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(hex);
                UpdateFromRgb(color.R, color.G, color.B, true);
            }
            catch
            {
                // Ignore invalid colors
            }
        }

        private void UpdatePreview(byte r, byte g, byte b)
        {
            if (ColorPreview != null)
            {
                ColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
        }

        private bool IsValidHex(string hex)
        {
            return Regex.IsMatch(hex, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
        }

        // --- HSV Logic ---

        private void Hue_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingHue = true;
            HueBorder.CaptureMouse();
            UpdateHueFromMouse(e.GetPosition(HueBorder));
        }

        private void Hue_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingHue) UpdateHueFromMouse(e.GetPosition(HueBorder));
        }

        private void Hue_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingHue = false;
            HueBorder.ReleaseMouseCapture();
        }

        private void Hue_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Optionally handle leave if not captured
        }

        private void UpdateHueFromMouse(Point p)
        {
            double y = Math.Max(0, Math.Min(p.Y, HueBorder.ActualHeight));
            _h = (y / HueBorder.ActualHeight) * 360.0;
            if (_h >= 360) _h = 359.99; // Keep within bound
            
            UpdateHsvVisuals();
            UpdateRgbFromHsv();
        }

        private void SV_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingSV = true;
            SVBorder.CaptureMouse();
            UpdateSVFromMouse(e.GetPosition(SVBorder));
        }

        private void SV_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingSV) UpdateSVFromMouse(e.GetPosition(SVBorder));
        }

        private void SV_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingSV = false;
            SVBorder.ReleaseMouseCapture();
        }

        private void SV_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
        }

        private void UpdateSVFromMouse(Point p)
        {
            double x = Math.Max(0, Math.Min(p.X, SVBorder.ActualWidth));
            double y = Math.Max(0, Math.Min(p.Y, SVBorder.ActualHeight));
            
            _s = x / SVBorder.ActualWidth;
            _v = 1.0 - (y / SVBorder.ActualHeight);

            UpdateHsvVisuals();
            UpdateRgbFromHsv();
        }

        private void UpdateHsvVisuals()
        {
            if (HueBorder == null || SVBorder == null || HueThumb == null || SVThumb == null) return;

            // Update Hue Thumb
            double hueY = (_h / 360.0) * HueBorder.ActualHeight;
            Canvas.SetTop(HueThumb, hueY);

            // Update SV Background with pure Hue color
            Color pureHue = HsvToColor(_h, 1.0, 1.0);
            SVBackground.Background = new SolidColorBrush(pureHue);

            // Update SV Thumb
            double svX = _s * SVBorder.ActualWidth;
            double svY = (1.0 - _v) * SVBorder.ActualHeight;
            Canvas.SetLeft(SVThumb, svX);
            Canvas.SetTop(SVThumb, svY);
        }

        private void UpdateRgbFromHsv()
        {
            Color c = HsvToColor(_h, _s, _v);
            UpdateFromRgb(c.R, c.G, c.B, false);
        }

        private void ColorToHsv(Color color, out double h, out double s, out double v)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            h = 0;
            if (delta > 0)
            {
                if (max == r) h = 60 * (((g - b) / delta) % 6);
                else if (max == g) h = 60 * (((b - r) / delta) + 2);
                else if (max == b) h = 60 * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360;

            s = max == 0 ? 0 : delta / max;
            v = max;
        }

        private Color HsvToColor(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r = 0, g = 0, b = 0;
            if (h >= 0 && h < 60) { r = c; g = x; b = 0; }
            else if (h >= 60 && h < 120) { r = x; g = c; b = 0; }
            else if (h >= 120 && h < 180) { r = 0; g = c; b = x; }
            else if (h >= 180 && h < 240) { r = 0; g = x; b = c; }
            else if (h >= 240 && h < 300) { r = x; g = 0; b = c; }
            else if (h >= 300 && h < 360) { r = c; g = 0; b = x; }

            return Color.FromArgb(255, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }
}
