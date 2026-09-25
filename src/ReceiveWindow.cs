using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lazo
{
    internal sealed class ReceiveWindow : Window
    {
        private readonly Border _shell;
        private readonly TextBlock _detail;
        private readonly Button _accept;
        private readonly Button _reject;
        private readonly ScaleTransform _progress = new ScaleTransform(0, 1);
        private readonly Action<bool> _respond;
        private readonly DispatcherTimer _timeout;
        private string _receivedPath;
        private readonly Button _dismiss;
        private bool _responded;
        private bool _closing;
        public Guid OfferId { get; private set; }

        public ReceiveWindow(Offer offer, Action<bool> respond)
        {
            OfferId = offer.Id;
            _respond = respond;
            Title = "Lazo · archivo entrante";
            ImageSource preview = Decode(offer.Preview);
            Width = preview == null ? 312 : 360;
            Height = preview == null ? 118 : 132;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Topmost = true;
            ShowActivated = false;
            ShowInTaskbar = false;
            FontFamily = Theme.Font;

            Grid outer = new Grid { Margin = new Thickness(10) };
            Content = outer;
            _shell = new Border
            {
                Background = Theme.ShellSurface(),
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true,
                Opacity = 0
            };
            outer.Children.Add(_shell);

            Grid main = new Grid { Margin = new Thickness(12, 10, 12, 12) };
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            _shell.Child = main;
            main.SizeChanged += (s, e) =>
                main.Clip = new RectangleGeometry(new Rect(1, 1,
                    Math.Max(0, main.ActualWidth - 2), Math.Max(0, main.ActualHeight - 2)), 12, 12);

            Grid body = new Grid();
            if (preview != null)
            {
                body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                body.ColumnDefinitions.Add(new ColumnDefinition());
                Border frame = new Border
                {
                    Width = 52,
                    Height = 52,
                    CornerRadius = new CornerRadius(6),
                    BorderBrush = Theme.Line,
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new Image { Source = preview, Stretch = Stretch.UniformToFill }
                };
                body.Children.Add(frame);
            }
            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            TextBlock name = Theme.Text(offer.FileName, 13, Theme.Ink, FontWeights.SemiBold);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(name);
            _detail = Theme.Text(MainWindow.FormatSize(offer.Size) + "  ·  " + offer.Sender, 11, Theme.Muted);
            _detail.TextTrimming = TextTrimming.CharacterEllipsis;
            _detail.Margin = new Thickness(0, 3, 0, 0);
            text.Children.Add(_detail);
            if (preview != null) Grid.SetColumn(text, 1);
            body.Children.Add(text);
            main.Children.Add(body);

            Grid buttons = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition());
            _reject = Compact("Rechazar", false);
            _reject.Click += (s, e) => { if (_receivedPath != null) { OpenReceived(true); return; } if (!_responded) Respond(false); CloseAnimated(); };
            buttons.Children.Add(_reject);
            _accept = Compact("Aceptar", true);
            _accept.Click += (s, e) =>
            {
                if (_receivedPath != null) { OpenReceived(false); return; }
                if (_responded) return;
                Respond(true);
                _detail.Text = "Guardando…";
                _accept.IsEnabled = false;
                _reject.IsEnabled = false;
            };
            Grid.SetColumn(_accept, 2);
            buttons.Children.Add(_accept);
            Grid.SetRow(buttons, 1);
            main.Children.Add(buttons);

            Border track = new Border
            {
                Height = 2,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsHitTestVisible = false
            };
            track.Child = new Border
            {
                Background = Theme.Ink,
                RenderTransform = _progress,
                RenderTransformOrigin = new Point(0, 0.5)
            };
            main.Children.Add(track);

            _dismiss = Compact("×", false);
            _dismiss.Width = 22;
            _dismiss.MinWidth = 0;
            _dismiss.MinHeight = 20;
            _dismiss.Height = 20;
            _dismiss.Padding = new Thickness(0);
            _dismiss.HorizontalAlignment = HorizontalAlignment.Right;
            _dismiss.VerticalAlignment = VerticalAlignment.Top;
            _dismiss.Margin = new Thickness(0, 4, 4, 0);
            _dismiss.ToolTip = "Cerrar";
            _dismiss.Visibility = Visibility.Collapsed;
            _dismiss.Click += (s, e) => CloseAnimated();
            outer.Children.Add(_dismiss);
            KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) CloseAnimated(); };
            Closed += (s, e) => _timeout.Stop();
            Loaded += (s, e) =>
            {
                TranslateTransform rise = new TranslateTransform(0, 18);
                _shell.RenderTransform = rise;
                rise.BeginAnimation(TranslateTransform.YProperty, Theme.Animation(18, 0, 190));
                _shell.BeginAnimation(OpacityProperty, Theme.Animation(0, 1, 150));
            };
            Closing += (s, e) => { if (!_responded) Respond(false); };
            _timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(90) };
            _timeout.Tick += (s, e) => { _timeout.Stop(); if (!_responded) { Respond(false); CloseAnimated(); } };
            _timeout.Start();
        }

        private static ImageSource Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(bytes);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 112;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch { return null; }
        }

        private static Button Compact(string label, bool primary)
        {
            Button button = Theme.Button(label, primary);
            button.MinHeight = 28;
            button.FontSize = 11;
            button.Padding = new Thickness(8, 0, 8, 0);
            return button;
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
            _progress.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progress.ScaleX, value, 90));
            _detail.Text = (value * 100).ToString("0") + "%";
        }

        public void Finish(string message, string path)
        {
            _timeout.Stop();
            bool success = !string.IsNullOrEmpty(path);
            _receivedPath = success ? path : null;
            _detail.Text = message;
            _progress.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progress.ScaleX, success ? 1 : 0, 120));
            _reject.Content = success ? "Mostrar en carpeta" : "Cerrar";
            _accept.Content = "Abrir";
            _reject.IsEnabled = true;
            _accept.IsEnabled = success;
            _dismiss.Visibility = Visibility.Visible;
            // Keep the received-file actions available until the user chooses or dismisses.
        }

        private void OpenReceived(bool showInFolder)
        {
            try
            {
                if (!File.Exists(_receivedPath)) throw new FileNotFoundException("El archivo ya no está en la carpeta de recepción.");
                if (showInFolder)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                        Arguments = "/select,\"" + _receivedPath + "\"", UseShellExecute = true });
                else
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = _receivedPath, UseShellExecute = true });
                CloseAnimated();
            }
            catch (Exception ex)
            {
                _detail.Text = "No se pudo abrir el archivo";
                _detail.ToolTip = ex.Message;
            }
        }
        private void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            DoubleAnimation fade = Theme.Animation(1, 0, 120);
            fade.Completed += (s, e) => Close();
            _shell.BeginAnimation(OpacityProperty, fade);
            TranslateTransform rise = _shell.RenderTransform as TranslateTransform;
            if (rise != null) rise.BeginAnimation(TranslateTransform.YProperty, Theme.Animation(0, 14, 130));
        }
    }
}
