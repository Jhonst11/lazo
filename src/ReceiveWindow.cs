using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            Width = 630;
            Height = 390;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;
            FontFamily = Theme.Mono;

            Grid full = new Grid { Margin = new Thickness(24) };
            Content = full;
            _shell = new Border { Background = Theme.Paper, BorderBrush = Theme.Ink,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Effect = Theme.Shadow() };
            full.Children.Add(_shell);
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
            body.ColumnDefinitions.Add(new ColumnDefinition());
            _shell.Child = body;

            Grid rail = new Grid { Background = Theme.Rail };
            rail.RowDefinitions.Add(new RowDefinition());
            rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(75) });
            StackPanel railTop = new StackPanel { Margin = new Thickness(20, 24, 0, 0) };
            railTop.Children.Add(Theme.Text("LAZO", 18, Theme.White, FontWeights.Bold));
            TextBlock glyph = Theme.Text("↓", 57, Theme.White);
            glyph.Margin = new Thickness(0, 42, 0, 0);
            railTop.Children.Add(glyph);
            rail.Children.Add(railTop);
            TextBlock railBottom = Theme.Text("ENTRANTE\n/ 01", 10, Theme.RailMuted);
            railBottom.Margin = new Thickness(20, 0, 0, 22);
            railBottom.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetRow(railBottom, 1); rail.Children.Add(railBottom);
            Grid.SetColumn(rail, 0); body.Children.Add(rail);

            Grid main = new Grid { Margin = new Thickness(30, 25, 30, 24) };
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(86) });
            Grid.SetColumn(main, 1); body.Children.Add(main);

            _state = Theme.Text("SOLICITUD DE TRANSFERENCIA", 10, Theme.Muted, FontWeights.Bold);
            main.Children.Add(_state);
            TextBlock title = Theme.Text("¿Recibir este archivo?", 22, Theme.Ink, FontWeights.Bold);
            Grid.SetRow(title, 1); main.Children.Add(title);

            StackPanel details = new StackPanel();
            Border fileCard = Theme.Card(new StackPanel
            {
                Children =
                {
                    Theme.Text(offer.FileName, 13, Theme.Ink, FontWeights.Bold),
                    Theme.Text(MainWindow.FormatSize(offer.Size), 10, Theme.Muted)
                }
            }, new Thickness(15, 13, 15, 13));
            details.Children.Add(fileCard);
            _description = Theme.Text("De " + offer.Sender + "  /  " + offer.Address, 10, Theme.Muted);
            _description.Margin = new Thickness(0, 14, 0, 0);
            details.Children.Add(_description);
            Border track = new Border { Height = 3, Background = Theme.Line, Margin = new Thickness(0, 18, 0, 0) };
            Grid progressArea = new Grid { ClipToBounds = true };
            Border fill = new Border { Background = Theme.Ink, RenderTransform = _progress, RenderTransformOrigin = new Point(0, 0.5) };
            progressArea.Children.Add(fill);
            track.Child = progressArea;
            details.Children.Add(track);
            Grid.SetRow(details, 2); main.Children.Add(details);

            Grid buttons = new Grid();
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
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
            Grid.SetRow(buttons, 3); main.Children.Add(buttons);

            Loaded += (s, e) => Theme.Enter(this, _shell);
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
