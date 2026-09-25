using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Lazo
{
    internal sealed class MainWindow : Window
    {
        private readonly NetworkEngine _network = new NetworkEngine();
        private readonly Border _shell;
        private readonly Border _dropZone;
        private readonly TextBlock _fileName;
        private readonly TextBlock _fileMeta;
        private readonly TextBlock _peerCount;
        private readonly TextBlock _emptyPeers;
        private readonly TextBlock _status;
        private readonly ListBox _peerList;
        private readonly Button _send;
        private readonly Border _progressFill;
        private readonly ScaleTransform _progressScale = new ScaleTransform(0, 1);
        private Hotkeys _hotkeys;
        private System.Windows.Forms.NotifyIcon _tray;
        private string _path;
        private ReceiveWindow _receiveWindow;
        private bool _exiting;
        private bool _animating;

        public MainWindow()
        {
            Title = "Lazo";
            Width = 760;
            Height = 674;
            MinWidth = 760;
            MaxWidth = 760;
            MinHeight = 674;
            MaxHeight = 674;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.Mono;

            Grid full = new Grid { Margin = new Thickness(24) };
            Content = full;
            _shell = new Border { Background = Theme.Paper, BorderBrush = Theme.Ink,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Effect = Theme.Shadow() };
            full.Children.Add(_shell);
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _shell.Child = body;

            Grid rail = BuildRail();
            Grid.SetColumn(rail, 0);
            body.Children.Add(rail);

            Grid main = new Grid { Margin = new Thickness(36, 26, 36, 24) };
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(108) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(146) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            Grid.SetColumn(main, 1);
            body.Children.Add(main);

            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            TextBlock eyebrow = Theme.Text("LOCAL / WINDOWS", 10, Theme.Muted, FontWeights.SemiBold);
            eyebrow.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(eyebrow);
            Button close = Theme.Button("×", false);
            close.MinHeight = 28;
            close.Padding = new Thickness(0);
            close.FontSize = 18;
            close.Click += (s, e) => HideAnimated();
            Grid.SetColumn(close, 1);
            top.Children.Add(close);
            Grid.SetRow(top, 0); main.Children.Add(top);
            top.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            StackPanel hero = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            hero.Children.Add(Theme.Text("Archivos, de aquí a allá.", 24, Theme.Ink, FontWeights.Bold));
            TextBlock sub = Theme.Text("Sin nube. Selecciona un archivo y un equipo cercano.", 11, Theme.Muted);
            sub.Margin = new Thickness(0, 10, 0, 0);
            hero.Children.Add(sub);
            Grid.SetRow(hero, 1); main.Children.Add(hero);

            StackPanel file = new StackPanel();
            file.Children.Add(SectionLabel("01", "ARCHIVO"));
            Grid fileGrid = new Grid();
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition());
            fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            StackPanel fileTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _fileName = Theme.Text("Arrastra un archivo aquí", 12, Theme.Ink, FontWeights.SemiBold);
            _fileName.TextTrimming = TextTrimming.CharacterEllipsis;
            _fileMeta = Theme.Text("o búscalo en tu equipo", 10, Theme.Muted);
            _fileMeta.Margin = new Thickness(0, 6, 0, 0);
            fileTexts.Children.Add(_fileName); fileTexts.Children.Add(_fileMeta);
            fileGrid.Children.Add(fileTexts);
            Button choose = Theme.Button("EXPLORAR", false);
            choose.MinHeight = 36;
            choose.FontSize = 10;
            choose.VerticalAlignment = VerticalAlignment.Center;
            choose.Click += (s, e) => ChooseFile();
            Grid.SetColumn(choose, 1); fileGrid.Children.Add(choose);
            _dropZone = Theme.Card(fileGrid, new Thickness(15, 16, 15, 16));
            _dropZone.MinHeight = 78;
            _dropZone.AllowDrop = true;
            _dropZone.DragEnter += (s, e) => { e.Effects = HasOneFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None; _dropZone.BorderBrush = Theme.Ink; e.Handled = true; };
            _dropZone.DragLeave += (s, e) => _dropZone.BorderBrush = Theme.Line;
            _dropZone.Drop += (s, e) => { _dropZone.BorderBrush = Theme.Line; string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths != null && paths.Length == 1) SelectFile(paths[0]); };
            _dropZone.Margin = new Thickness(0, 12, 0, 0);
            file.Children.Add(_dropZone);
            Grid.SetRow(file, 2); main.Children.Add(file);

            Grid peers = new Grid();
            peers.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            peers.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid peersHead = new Grid();
            peersHead.ColumnDefinitions.Add(new ColumnDefinition());
            peersHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            peersHead.Children.Add(SectionLabel("02", "DESTINATARIO"));
            _peerCount = Theme.Text("0 EN LÍNEA", 9, Theme.Muted);
            _peerCount.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_peerCount, 1); peersHead.Children.Add(_peerCount);
            peers.Children.Add(peersHead);
            Grid peerSpace = new Grid();
            _peerList = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            ScrollViewer.SetVerticalScrollBarVisibility(_peerList, ScrollBarVisibility.Auto);
            Style itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 0, 0, 6)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            ControlTemplate itemTemplate = new ControlTemplate(typeof(ListBoxItem));
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            itemTemplate.VisualTree = presenter;
            itemStyle.Setters.Add(new Setter(ListBoxItem.TemplateProperty, itemTemplate));
            _peerList.ItemContainerStyle = itemStyle;
            _peerList.SelectionChanged += (s, e) => { RefreshPeerStyles(); UpdateSendEnabled(); };
            peerSpace.Children.Add(_peerList);
            _emptyPeers = Theme.Text("Buscando equipos en la misma red privada…\n\nSi no aparecen, habilita Lazo en el firewall de ambos equipos.", 11, Theme.Muted);
            _emptyPeers.VerticalAlignment = VerticalAlignment.Center;
            _emptyPeers.TextAlignment = TextAlignment.Center;
            _emptyPeers.Margin = new Thickness(24);
            peerSpace.Children.Add(_emptyPeers);
            Grid.SetRow(peerSpace, 1); peers.Children.Add(peerSpace);
            Grid.SetRow(peers, 3); main.Children.Add(peers);

            Grid footer = new Grid();
            footer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            footer.RowDefinitions.Add(new RowDefinition());
            footer.Children.Add(Theme.Rule());
            Grid footerBody = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footerBody.ColumnDefinitions.Add(new ColumnDefinition());
            footerBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
            StackPanel statusStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _status = Theme.Text("Listo para enviar", 11, Theme.Ink);
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            statusStack.Children.Add(_status);
            Border track = new Border { Height = 3, Background = Theme.Line, Margin = new Thickness(0, 9, 20, 0) };
            Grid progressGrid = new Grid { ClipToBounds = true };
            _progressFill = new Border { Background = Theme.Ink, HorizontalAlignment = HorizontalAlignment.Stretch,
                RenderTransform = _progressScale, RenderTransformOrigin = new Point(0, 0.5) };
            progressGrid.Children.Add(_progressFill);
            track.Child = progressGrid;
            statusStack.Children.Add(track);
            footerBody.Children.Add(statusStack);
            _send = Theme.Button("ENVIAR  →", true);
            _send.Click += SendClicked;
            _send.IsEnabled = false;
            Grid.SetColumn(_send, 1); footerBody.Children.Add(_send);
            Grid.SetRow(footerBody, 1); footer.Children.Add(footerBody);
            Grid.SetRow(footer, 4); main.Children.Add(footer);

            Loaded += (s, e) => Theme.Enter(this, _shell);
            SourceInitialized += (s, e) => SetupHotkeys();
            Closing += (s, e) => { if (!_exiting) { e.Cancel = true; HideAnimated(); } };
            Closed += (s, e) => Cleanup();
            SetupTray();
            _network.PeersChanged += OnPeersChanged;
            _network.OfferReceived += OnOfferReceived;
            _network.ReceiveProgress += OnReceiveProgress;
            _network.ReceiveFinished += OnReceiveFinished;
            try { _network.Start(); }
            catch (Exception ex) { _status.Text = "Red no disponible: " + ex.Message; }
        }

        private Grid BuildRail()
        {
            Grid rail = new Grid { Background = Theme.Rail };
            rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
            rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(148) });
            StackPanel brand = new StackPanel { Margin = new Thickness(24, 26, 0, 0) };
            brand.Children.Add(Theme.Text("LAZO", 22, Theme.White, FontWeights.Bold));
            TextBlock mark = Theme.Text("/ TRANSFERENCIA LOCAL", 9, Theme.RailMuted);
            mark.Margin = new Thickness(0, 8, 0, 0);
            brand.Children.Add(mark);
            Grid.SetRow(brand, 0); rail.Children.Add(brand);
            StackPanel steps = new StackPanel { Margin = new Thickness(24, 8, 15, 0) };
            steps.Children.Add(Theme.Text("01  ARCHIVO", 11, Theme.White, FontWeights.Bold));
            steps.Children.Add(Step("│"));
            steps.Children.Add(Theme.Text("02  EQUIPO", 11, Theme.White, FontWeights.Bold));
            steps.Children.Add(Step("│"));
            steps.Children.Add(Theme.Text("03  ENVIAR", 11, Theme.White, FontWeights.Bold));
            Grid.SetRow(steps, 1); rail.Children.Add(steps);
            StackPanel bottom = new StackPanel { Margin = new Thickness(24, 0, 18, 22), VerticalAlignment = VerticalAlignment.Bottom };
            Border rule = new Border { Height = 1, Background = Theme.Brush("#545454"), Margin = new Thickness(0, 0, 0, 18) };
            bottom.Children.Add(rule);
            bottom.Children.Add(Theme.Text("ABRIR", 9, Theme.RailMuted));
            TextBlock hotkey = Theme.Text("ALT × 2", 15, Theme.White, FontWeights.Bold);
            hotkey.Margin = new Thickness(0, 6, 0, 0);
            bottom.Children.Add(hotkey);
            TextBlock alternative = Theme.Text("CTRL + ALT + L", 9, Theme.RailMuted);
            alternative.Margin = new Thickness(0, 8, 0, 0);
            bottom.Children.Add(alternative);
            Grid.SetRow(bottom, 2); rail.Children.Add(bottom);
            return rail;
        }

        private static TextBlock Step(string text)
        {
            TextBlock line = Theme.Text(text, 16, Theme.RailMuted);
            line.Margin = new Thickness(12, 10, 0, 10);
            return line;
        }

        private static TextBlock SectionLabel(string number, string title)
        {
            return Theme.Text(number + "  /  " + title, 10, Theme.Ink, FontWeights.Bold);
        }

        private static bool HasOneFile(System.Windows.IDataObject data)
        {
            string[] paths = data.GetData(DataFormats.FileDrop) as string[];
            return paths != null && paths.Length == 1 && File.Exists(paths[0]);
        }

        private void ChooseFile()
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Seleccionar archivo", Multiselect = false };
            if (dialog.ShowDialog(this) == true) SelectFile(dialog.FileName);
        }

        private void SelectFile(string path)
        {
            if (!File.Exists(path)) { _status.Text = "Selecciona un archivo válido."; return; }
            FileInfo info = new FileInfo(path);
            _path = path;
            _fileName.Text = info.Name;
            _fileMeta.Text = FormatSize(info.Length) + "  /  listo para transferir";
            _dropZone.BorderBrush = Theme.Ink;
            _status.Text = "Elige un destinatario";
            _progressScale.ScaleX = 0;
            UpdateSendEnabled();
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double number = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;
            while (number >= 1024 && unit < units.Length - 1) { number /= 1024; unit++; }
            return number.ToString(number < 10 ? "0.0" : "0") + " " + units[unit];
        }

        private void OnPeersChanged(List<Peer> peers)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                Guid selected = _peerList.SelectedItem == null ? Guid.Empty : ((Peer)((ListBoxItem)_peerList.SelectedItem).Tag).Id;
                _peerList.Items.Clear();
                foreach (Peer peer in peers)
                {
                    Grid row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                    TextBlock glyph = Theme.Text("▣", 18, Theme.Ink); glyph.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(glyph);
                    StackPanel names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    names.Children.Add(Theme.Text(peer.Name, 11, Theme.Ink, FontWeights.Bold));
                    names.Children.Add(Theme.Text(peer.Address.ToString(), 9, Theme.Muted));
                    Grid.SetColumn(names, 1); row.Children.Add(names);
                    TextBlock arrow = Theme.Text("›", 18, Theme.Muted); arrow.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
                    Border card = Theme.Card(row, new Thickness(12, 7, 12, 7));
                    card.MinHeight = 50;
                    ListBoxItem item = new ListBoxItem { Content = card, Tag = peer };
                    _peerList.Items.Add(item);
                    if (peer.Id == selected) _peerList.SelectedItem = item;
                }
                _peerCount.Text = peers.Count + " EN LÍNEA";
                _emptyPeers.Visibility = peers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                RefreshPeerStyles();
                UpdateSendEnabled();
            }));
        }

        private void RefreshPeerStyles()
        {
            foreach (ListBoxItem item in _peerList.Items)
            {
                Border card = (Border)item.Content;
                card.BorderBrush = item.IsSelected ? Theme.Ink : Theme.Line;
                card.Background = item.IsSelected ? Theme.Brush("#EAEAE8") : Theme.White;
            }
        }

        private void UpdateSendEnabled()
        {
            _send.IsEnabled = _path != null && _peerList.SelectedItem != null;
        }

        private async void SendClicked(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = _peerList.SelectedItem as ListBoxItem;
            if (item == null || _path == null) return;
            Peer peer = (Peer)item.Tag;
            _send.IsEnabled = false;
            _status.Text = "Esperando aceptación de " + peer.Name + "…";
            _progressScale.ScaleX = 0;
            try
            {
                await _network.SendAsync(peer, _path, value => Dispatcher.BeginInvoke((Action)(() =>
                {
                    _status.Text = "Enviando  " + (value * 100).ToString("0") + "%";
                    _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progressScale.ScaleX, value, 130));
                })));
                _status.Text = "Archivo entregado a " + peer.Name;
                _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progressScale.ScaleX, 1, 180));
            }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { UpdateSendEnabled(); }
        }

        private Task<bool> OnOfferReceived(Offer offer)
        {
            TaskCompletionSource<bool> response = new TaskCompletionSource<bool>();
            Dispatcher.BeginInvoke((Action)(() =>
            {
                _receiveWindow = new ReceiveWindow(offer, accept => response.TrySetResult(accept));
                _receiveWindow.Show();
                _receiveWindow.Activate();
            }));
            return response.Task;
        }

        private void OnReceiveProgress(Guid id, double value)
        {
            Dispatcher.BeginInvoke((Action)(() => { if (_receiveWindow != null && _receiveWindow.OfferId == id) _receiveWindow.SetProgress(value); }));
        }

        private void OnReceiveFinished(Guid id, string message)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (_receiveWindow != null && _receiveWindow.OfferId == id) _receiveWindow.Finish(message);
            }));
        }

        private void SetupHotkeys()
        {
            _hotkeys = new Hotkeys(this, Toggle);
            if (!_hotkeys.DoubleAltAvailable && !_hotkeys.AlternativeAvailable) _status.Text = "Atajos no disponibles; abre Lazo desde la bandeja.";
            else if (!_hotkeys.DoubleAltAvailable) _status.Text = "Doble Alt no disponible; usa Ctrl+Alt+L.";
            else if (!_hotkeys.AlternativeAvailable) _status.Text = "Ctrl+Alt+L está ocupado; usa doble Alt.";
        }

        private void Toggle()
        {
            if (IsVisible) HideAnimated(); else ShowAnimated();
        }

        private void ShowAnimated()
        {
            if (_animating) return;
            Topmost = true;
            Show();
            Activate();
            Theme.Enter(this, _shell);
        }

        public void ActivateFromElsewhere()
        {
            ShowAnimated();
        }

        private void HideAnimated()
        {
            if (_animating || !IsVisible) return;
            _animating = true;
            Theme.Leave(_shell, () => { Hide(); _animating = false; });
        }

        private void SetupTray()
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(32, 32);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
            using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.White, 3))
            {
                g.Clear(System.Drawing.Color.FromArgb(32, 32, 32));
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawArc(pen, 5, 8, 18, 16, 60, 270);
                g.DrawLine(pen, 18, 8, 25, 8);
                g.DrawLine(pen, 25, 8, 25, 15);
            }
            _tray = new System.Windows.Forms.NotifyIcon();
            IntPtr iconHandle = bitmap.GetHicon();
            try { _tray.Icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(iconHandle).Clone(); }
            finally { DestroyIcon(iconHandle); bitmap.Dispose(); }
            _tray.Text = "Lazo · transferencia local";
            _tray.Visible = true;
            _tray.DoubleClick += (s, e) => Dispatcher.BeginInvoke((Action)ShowAnimated);
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Abrir Lazo", null, (s, e) => Dispatcher.BeginInvoke((Action)ShowAnimated));
            menu.Items.Add("Salir", null, (s, e) => Dispatcher.BeginInvoke((Action)(() => { _exiting = true; Close(); Application.Current.Shutdown(); })));
            _tray.ContextMenuStrip = menu;
        }

        private void Cleanup()
        {
            if (_hotkeys != null) _hotkeys.Dispose();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            _network.Dispose();
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
