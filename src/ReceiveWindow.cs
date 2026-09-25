using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Lazo
{
    internal sealed class ReceiveWindow : Window
    {
        private readonly Border _shell;
        private readonly TextBlock _state;
        private readonly TextBlock _description;
        private readonly Button _accept;
        private readonly Button _reject;
        private readonly ScaleTransform _progress = new ScaleTransform(0, 1);
        private readonly Action<bool> _respond;
        private readonly DispatcherTimer _timeout;
        private bool _responded;
        private bool _closing;
        public Guid OfferId { get; private set; }

        public ReceiveWindow(Offer offer, Action<bool> respond)
        {
            OfferId = offer.Id;
            _respond = respond;
            Title = "Lazo · archivo entrante";
            Width = 558;
            Height = 360;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Topmost = true;
            ShowInTaskbar = true;
            FontFamily = Theme.Font;

            Grid outer = new Grid { Margin = new Thickness(14) };
            Content = outer;
            _shell = new Border { Background = Theme.ShellSurface(), BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.IsGlass ? 27 : 14),
                Effect = Theme.Shadow(), ClipToBounds = true };
            outer.Children.Add(_shell);
            Grid backdrop = new Grid();
            _shell.Child = backdrop;
            if (Theme.IsGlass)
            {
                Ellipse glow = new Ellipse { Width = 310, Height = 260, IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -130, -100, 0), Opacity = 0.48 };
                RadialGradientBrush brush = new RadialGradientBrush();
                brush.GradientStops.Add(new GradientStop(Colors.White, 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
                glow.Fill = brush;
                backdrop.Children.Add(glow);
            }

            Grid main = new Grid { Margin = new Thickness(26, 20, 26, 22) };
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(33) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(87) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            backdrop.Children.Add(main);

            _state = Theme.Text("ARCHIVO ENTRANTE", 10, Theme.Muted, FontWeights.SemiBold);
            main.Children.Add(_state);
            TextBlock title = Theme.Text("¿Quieres recibirlo?", 21, Theme.Ink, FontWeights.Bold);
            Grid.SetRow(title, 1); main.Children.Add(title);

            StackPanel fileDetails = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock fileName = Theme.Text(offer.FileName, 14, Theme.Ink, FontWeights.SemiBold);
            fileName.TextTrimming = TextTrimming.CharacterEllipsis;
            fileDetails.Children.Add(fileName);
            TextBlock size = Theme.Text(MainWindow.FormatSize(offer.Size), 11, Theme.Muted);
            size.Margin = new Thickness(0, 5, 0, 0);
            fileDetails.Children.Add(size);
            Border fileCard = Theme.Card(fileDetails, new Thickness(17, 8, 17, 8));
            Grid.SetRow(fileCard, 2); main.Children.Add(fileCard);

            StackPanel sender = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };
            _description = Theme.Text("De " + offer.Sender + "  ·  " + offer.Address, 11, Theme.Muted);
            sender.Children.Add(_description);
            Border track = new Border { Height = 3, Background = Theme.Line, Margin = new Thickness(0, 15, 0, 0) };
            Grid progressArea = new Grid { ClipToBounds = true };
            progressArea.Children.Add(new Border { Background = Theme.Ink, RenderTransform = _progress,
                RenderTransformOrigin = new Point(0, 0.5) });
            track.Child = progressArea;
            sender.Children.Add(track);
            Grid.SetRow(sender, 3); main.Children.Add(sender);

            Grid buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            _reject = Theme.Button("RECHAZAR", false);
            _reject.Click += (s, e) => { if (!_responded) Respond(false); CloseAnimated(); };
            buttons.Children.Add(_reject);
            _accept = Theme.Button("ACEPTAR ARCHIVO  ↓", true);
            _accept.Click += (s, e) =>
            {
                if (_responded) return;
                Respond(true);
                _state.Text = "RECIBIENDO";
                _description.Text = "Guardando en Descargas\\Lazo…";
                _accept.IsEnabled = false;
                _reject.IsEnabled = false;
            };
            Grid.SetColumn(_accept, 2); buttons.Children.Add(_accept);
            Grid.SetRow(buttons, 4); main.Children.Add(buttons);

            SourceInitialized += (s, e) => WindowPlacement.CenterOnCursor(this);
            Loaded += (s, e) => Theme.Enter(_shell);
            Closing += (s, e) => { if (!_responded) Respond(false); };
            _timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(90) };
            _timeout.Tick += (s, e) => { _timeout.Stop(); if (!_responded) { Respond(false); CloseAnimated(); } };
            _timeout.Start();
        }

        private void Respond(bool accepted)
        {
            _responded = true;
            _timeout.Stop();
            _respond(accepted);
        }

        public void SetProgress(double value)
        {
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            _progress.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progress.ScaleX, value, 130));
            _state.Text = "RECIBIENDO  " + (value * 100).ToString("0") + "%";
        }

        public void Finish(string message)
        {
            bool success = !message.StartsWith("Error:", StringComparison.Ordinal);
            _state.Text = success ? "TRANSFERENCIA COMPLETA" : "TRANSFERENCIA INTERRUMPIDA";
            _description.Text = message;
            _progress.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progress.ScaleX, success ? 1 : 0, 220));
            _reject.Content = "CERRAR";
            _reject.IsEnabled = true;
            _accept.IsEnabled = false;
            if (success)
            {
                DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                timer.Tick += (s, e) => { timer.Stop(); CloseAnimated(); };
                timer.Start();
            }
        }

        private void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            Theme.Leave(_shell, () => Close());
        }
    }
}
