using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Lazo
{
    internal static class Theme
    {
        public static readonly Brush Ink = Brush("#202020");
        public static readonly Brush Paper = Brush("#F6F6F4");
        public static readonly Brush White = Brush("#FFFFFF");
        public static readonly Brush Muted = Brush("#777777");
        public static readonly Brush Line = Brush("#D9D9D7");
        public static readonly Brush Rail = Brush("#222222");
        public static readonly Brush RailMuted = Brush("#A3A3A3");
        public static readonly FontFamily Mono = new FontFamily("Cascadia Code, Consolas");

        public static Brush Brush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public static TextBlock Text(string value, double size, Brush color, FontWeight weight)
        {
            return new TextBlock { Text = value, FontFamily = Mono, FontSize = size,
                FontWeight = weight, Foreground = color, TextWrapping = TextWrapping.Wrap };
        }

        public static TextBlock Text(string value, double size, Brush color)
        {
            return Text(value, size, color, FontWeights.Normal);
        }

        public static Border Rule()
        {
            return new Border { Height = 1, Background = Line };
        }

        public static Button Button(string label, bool primary)
        {
            Button button = new Button { Content = label, FontFamily = Mono, FontSize = 12,
                FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand,
                MinHeight = 42, Padding = new Thickness(18, 0, 18, 0),
                Background = primary ? Ink : White,
                Foreground = primary ? White : Ink,
                BorderBrush = primary ? Ink : Line, BorderThickness = new Thickness(1) };
            ControlTemplate template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            FrameworkElementFactory frame = new FrameworkElementFactory(typeof(Border));
            frame.Name = "frame";
            frame.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            frame.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(8, 0, 8, 0));
            frame.AppendChild(content);
            template.VisualTree = frame;
            Trigger over = new Trigger { Property = System.Windows.Controls.Button.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(System.Windows.Controls.Button.OpacityProperty, 0.75));
            template.Triggers.Add(over);
            Trigger disabled = new Trigger { Property = System.Windows.Controls.Button.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(System.Windows.Controls.Button.OpacityProperty, 0.38));
            template.Triggers.Add(disabled);
            button.Template = template;
            return button;
        }

        public static Border Card(UIElement child, Thickness padding)
        {
            return new Border { Background = White, BorderBrush = Line,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = padding, Child = child };
        }

        public static void Enter(Window window, FrameworkElement element)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            TransformGroup transforms = new TransformGroup();
            ScaleTransform scale = new ScaleTransform(0.94, 0.94);
            TranslateTransform translate = new TranslateTransform(0, 24);
            transforms.Children.Add(scale);
            transforms.Children.Add(translate);
            element.RenderTransform = transforms;
            element.Opacity = 0;
            DoubleAnimation fade = Animation(0, 1, 240);
            DoubleAnimation grow = Animation(0.94, 1, 340);
            DoubleAnimation rise = Animation(24, 0, 340);
            element.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow.Clone());
            translate.BeginAnimation(TranslateTransform.YProperty, rise);
        }

        public static void Leave(FrameworkElement element, Action completed)
        {
            TransformGroup transforms = element.RenderTransform as TransformGroup;
            if (transforms == null) { completed(); return; }
            ScaleTransform scale = (ScaleTransform)transforms.Children[0];
            TranslateTransform translate = (TranslateTransform)transforms.Children[1];
            DoubleAnimation fade = Animation(1, 0, 220);
            fade.Completed += (s, e) => completed();
            element.BeginAnimation(UIElement.OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(1, 0.78, 260));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(1, 0.78, 260));
            translate.BeginAnimation(TranslateTransform.YProperty, Animation(0, -36, 260));
        }

        public static DoubleAnimation Animation(double from, double to, int milliseconds)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.HoldEnd };
        }

        public static DropShadowEffect Shadow()
        {
            return new DropShadowEffect { Color = Colors.Black, BlurRadius = 38, ShadowDepth = 14,
                Opacity = 0.22, Direction = 270 };
        }
    }
}
