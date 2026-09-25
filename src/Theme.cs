using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Lazo
{
    internal enum ThemeKind { Raycast, Glass }

    internal static class Theme
    {
        public static ThemeKind Mode { get; private set; }
        public static bool IsGlass { get { return Mode == ThemeKind.Glass; } }
        public static readonly FontFamily Mono = new FontFamily("Cascadia Code, Consolas");
        private static readonly FontFamily Sans = new FontFamily("Segoe UI");

        public static void Load()
        {
            try { Mode = File.ReadAllText(SettingsPath()).Trim() == "glass" ? ThemeKind.Glass : ThemeKind.Raycast; }
            catch { Mode = ThemeKind.Raycast; }
        }

        public static void SetForPreview(ThemeKind mode) { Mode = mode; }

        public static void Toggle(bool persist)
        {
            Mode = IsGlass ? ThemeKind.Raycast : ThemeKind.Glass;
            if (!persist) return;
            try
            {
                string path = SettingsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, IsGlass ? "glass" : "raycast");
            }
            catch { /* The chosen theme still works for this session. */ }
        }

        private static string SettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lazo", "theme.txt");
        }

        public static Brush Color(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public static Brush Ink { get { return Color("#242424"); } }
        public static Brush Muted { get { return Color(IsGlass ? "#646464" : "#777777"); } }
        public static Brush Line { get { return Color(IsGlass ? "#BFFFFFFF" : "#D3D3D3"); } }
        public static Brush CardSurface { get { return Color(IsGlass ? "#B8FFFFFF" : "#F4F4F4"); } }
        public static Brush SoftSurface { get { return Color(IsGlass ? "#80FFFFFF" : "#DEDEDE"); } }
        public static Brush AvatarSurface { get { return Color(IsGlass ? "#B0FFFFFF" : "#ECECEC"); } }
        public static Brush AvatarHover { get { return Color(IsGlass ? "#E5FFFFFF" : "#CECECE"); } }
        public static Brush Primary { get { return Color(IsGlass ? "#252525" : "#F2F2F2"); } }
        public static Brush PrimaryText { get { return Color(IsGlass ? "#FFFFFF" : "#19191B"); } }
        public static FontFamily Font { get { return Sans; } }
        public static CornerRadius Radius { get { return new CornerRadius(IsGlass ? 20 : 10); } }

        public static Brush ShellSurface()
        {
            if (!IsGlass) return Color("#F1F1F1");
            LinearGradientBrush gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point(0, 0);
            gradient.EndPoint = new Point(1, 1);
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(239, 252, 252, 252), 0));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(222, 231, 231, 231), 0.52));
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(231, 249, 249, 249), 1));
            return gradient;
        }

        public static TextBlock Text(string value, double size, Brush color, FontWeight weight)
        {
            return new TextBlock { Text = value, FontFamily = Font, FontSize = size,
                FontWeight = weight, Foreground = color, TextWrapping = TextWrapping.Wrap };
        }

        public static TextBlock Text(string value, double size, Brush color)
        {
            return Text(value, size, color, FontWeights.Normal);
        }

        public static Button Button(string label, bool primary)
        {
            Button button = new Button { Content = label, FontFamily = Font, FontSize = 12,
                FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand,
                MinHeight = 36, Padding = new Thickness(16, 0, 16, 0),
                Background = primary ? Primary : SoftSurface,
                Foreground = primary ? PrimaryText : Ink,
                BorderBrush = primary ? Primary : Line, BorderThickness = new Thickness(1) };
            ControlTemplate template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            FrameworkElementFactory frame = new FrameworkElementFactory(typeof(Border));
            frame.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(IsGlass ? 17 : 8));
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 0, 8, 0));
            frame.AppendChild(content);
            template.VisualTree = frame;
            Trigger hover = new Trigger { Property = System.Windows.Controls.Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(System.Windows.Controls.Button.OpacityProperty, 0.78));
            template.Triggers.Add(hover);
            Trigger disabled = new Trigger { Property = System.Windows.Controls.Button.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(System.Windows.Controls.Button.OpacityProperty, 0.4));
            template.Triggers.Add(disabled);
            button.Template = template;
            return button;
        }

        public static Border Card(UIElement child, Thickness padding)
        {
            return new Border { Background = CardSurface, BorderBrush = Line,
                BorderThickness = new Thickness(1), CornerRadius = Radius, Padding = padding, Child = child };
        }

        public static DropShadowEffect Shadow()
        {
            return new DropShadowEffect { Color = Colors.Black, BlurRadius = IsGlass ? 44 : 32,
                ShadowDepth = IsGlass ? 17 : 12, Opacity = IsGlass ? 0.20 : 0.38, Direction = 270 };
        }

        public static DoubleAnimation Animation(double from, double to, int milliseconds)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd };
        }

        public static void Enter(FrameworkElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            TransformGroup group = new TransformGroup();
            ScaleTransform scale = new ScaleTransform(IsGlass ? 0.90 : 0.96, IsGlass ? 0.90 : 0.96);
            TranslateTransform translate = new TranslateTransform(0, IsGlass ? 28 : 12);
            group.Children.Add(scale);
            group.Children.Add(translate);
            element.RenderTransform = group;
            element.Opacity = 0;
            element.BeginAnimation(UIElement.OpacityProperty, Animation(0, 1, IsGlass ? 340 : 190));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(scale.ScaleX, 1, IsGlass ? 420 : 260));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(scale.ScaleY, 1, IsGlass ? 420 : 260));
            translate.BeginAnimation(TranslateTransform.YProperty, Animation(translate.Y, 0, IsGlass ? 420 : 260));
        }

        public static void Leave(FrameworkElement element, Action completed)
        {
            TransformGroup group = element.RenderTransform as TransformGroup;
            if (group == null) { completed(); return; }
            ScaleTransform scale = (ScaleTransform)group.Children[0];
            TranslateTransform translate = (TranslateTransform)group.Children[1];
            DoubleAnimation fade = Animation(1, 0, IsGlass ? 270 : 170);
            fade.Completed += (s, e) => completed();
            element.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(1, IsGlass ? 0.72 : 0.89, IsGlass ? 290 : 180));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(1, IsGlass ? 0.72 : 0.89, IsGlass ? 290 : 180));
            translate.BeginAnimation(TranslateTransform.YProperty, Animation(0, IsGlass ? -40 : -16, IsGlass ? 290 : 180));
        }
    }
}
