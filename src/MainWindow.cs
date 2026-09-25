using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Lazo
{
    internal sealed class MainWindow : Window
    {
        private readonly NetworkEngine _network = new NetworkEngine();
        private readonly bool _preview;
        private List<Peer> _peers = new List<Peer>();
        private Border _shell;
        private Border _fileCard;
        private TextBlock _fileName;
        private TextBlock _fileMeta;
        private TextBlock _peerCount;
        private TextBlock _emptyPeers;
        private TextBlock _status;
        private TextBox _search;
        private TextBlock _searchHint;
        private ListBox _peerList;
        private Button _send;
        private ScaleTransform _progressScale;
        private Hotkeys _hotkeys;
        private System.Windows.Forms.NotifyIcon _tray;
        private string _path;
        private ReceiveWindow _receiveWindow;
        private bool _exiting;
        private bool _animating;
        private bool _sending;

        public MainWindow(bool preview = false, bool previewGlass = false)
        {
            _preview = preview;
            Theme.Load();
            if (previewGlass) Theme.SetForPreview(ThemeKind.Glass);
            Title = "Lazo";
            Width = MinWidth = MaxWidth = 660;
            Height = MinHeight = MaxHeight = 550;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            BuildUi();

            Loaded += (s, e) => { Theme.Enter(_shell); _search.Focus(); };
            SourceInitialized += (s, e) => { WindowPlacement.CenterOnCursor(this); if (!_preview) SetupHotkeys(); };
            Closing += (s, e) => { if (!_exiting && !_preview) { e.Cancel = true; HideAnimated(); } };
            Closed += (s, e) => Cleanup();
            KeyDown += OnWindowKeyDown;
            if (!_preview)
            {
                SetupTray();
                _network.PeersChanged += OnPeersChanged;
                _network.OfferReceived += OnOfferReceived;
                _network.ReceiveProgress += OnReceiveProgress;
                _network.ReceiveFinished += OnReceiveFinished;
                try { _network.Start(); }
                catch (Exception ex) { _status.Text = "Red no disponible: " + ex.Message; }
            }
        }

        private void BuildUi()
        {
            if (_path != null && !File.Exists(_path)) _path = null;
            Grid outer = new Grid { Margin = new Thickness(14) };
            Content = outer;
            _shell = new Border { Background = Theme.ShellSurface(), BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.IsGlass ? 26 : 13),
                Effect = Theme.Shadow(), ClipToBounds = true, AllowDrop = true };
            outer.Children.Add(_shell);
            _shell.DragEnter += OnDragEnter;
            _shell.DragLeave += (s, e) => _fileCard.BorderBrush = Theme.Line;
            _shell.Drop += OnDrop;

            Grid backdrop = new Grid();
            _shell.Child = backdrop;
            if (Theme.IsGlass)
            {
                System.Windows.Shapes.Ellipse glow = new System.Windows.Shapes.Ellipse { Width = 420, Height = 310, IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -160, -150, 0), Opacity = 0.50 };
                RadialGradientBrush light = new RadialGradientBrush();
                light.GradientStops.Add(new GradientStop(Colors.White, 0));
                light.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
                glow.Fill = light;
                backdrop.Children.Add(glow);
            }

            Grid main = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(84) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
            backdrop.Children.Add(main);

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(Theme.Text("Lazo", 19, Theme.Ink, FontWeights.Bold));
            TextBlock tag = Theme.Text("  /  transferencia local", 10, Theme.Muted);
            tag.VerticalAlignment = VerticalAlignment.Bottom;
            tag.Margin = new Thickness(0, 0, 0, 3);
            brand.Children.Add(tag);
            header.Children.Add(brand);
            Button switchTheme = Theme.Button(Theme.IsGlass ? "RAYCAST" : "VIDRIO", false);
            switchTheme.MinHeight = 30;
            switchTheme.FontSize = 10;
            switchTheme.Click += (s, e) =>
            {
                Guid selected = SelectedPeerId();
                string query = _search.Text;
                Theme.Toggle(!_preview);
                BuildUi();
                _search.Text = query;
                FilterPeers(selected);
                Theme.Enter(_shell);
                _search.Focus();
            };
            Grid.SetColumn(switchTheme, 1); header.Children.Add(switchTheme);
            Button close = Theme.Button("×", false);
            close.MinHeight = 30;
            close.Padding = new Thickness(0);
            close.FontSize = 16;
            close.Click += (s, e) => HideAnimated();
            Grid.SetColumn(close, 3); header.Children.Add(close);
            header.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is TextBlock) DragMove(); };
            main.Children.Add(header);

            Grid file = new Grid();
            file.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            file.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            file.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
            TextBlock glyph = Theme.Text("↗", 28, Theme.Ink);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            file.Children.Add(glyph);
            StackPanel fileText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _fileName = Theme.Text(_path == null ? "Arrastra un archivo" : Path.GetFileName(_path), 13, Theme.Ink, FontWeights.SemiBold);
            _fileName.TextTrimming = TextTrimming.CharacterEllipsis;
            _fileMeta = Theme.Text(_path == null ? "o selecciónalo para enviar" : FormatSize(new FileInfo(_path).Length), 10, Theme.Muted);
            _fileMeta.Margin = new Thickness(0, 5, 0, 0);
            fileText.Children.Add(_fileName); fileText.Children.Add(_fileMeta);
            Grid.SetColumn(fileText, 1); file.Children.Add(fileText);
            Button choose = Theme.Button("ELEGIR", false);
            choose.MinHeight = 34;
            choose.VerticalAlignment = VerticalAlignment.Center;
            choose.FontSize = 10;
            choose.Click += (s, e) => ChooseFile();
            Grid.SetColumn(choose, 2); file.Children.Add(choose);
            _fileCard = Theme.Card(file, new Thickness(14, 9, 14, 9));
            _fileCard.Margin = new Thickness(0, 4, 0, 8);
            Grid.SetRow(_fileCard, 1); main.Children.Add(_fileCard);

            Grid searchGrid = new Grid { Margin = new Thickness(0, 7, 0, 7) };
            Border searchCard = Theme.Card(searchGrid, new Thickness(14, 0, 14, 0));
            Grid.SetRow(searchCard, 2); main.Children.Add(searchCard);
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(27) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock searchIcon = Theme.Text("⌕", 22, Theme.Muted);
            searchIcon.VerticalAlignment = VerticalAlignment.Center;
            searchGrid.Children.Add(searchIcon);
            Grid searchContent = new Grid();
            _search = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = Theme.Ink, FontFamily = Theme.Font, FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center, CaretBrush = Theme.Ink };
            _searchHint = Theme.Text("Buscar equipo por nombre o IP…", 12, Theme.Muted);
            _searchHint.VerticalAlignment = VerticalAlignment.Center;
            _searchHint.IsHitTestVisible = false;
            searchContent.Children.Add(_search);
            searchContent.Children.Add(_searchHint);
            _search.TextChanged += (s, e) =>
            {
                _searchHint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                FilterPeers(Guid.Empty);
            };
            Grid.SetColumn(searchContent, 1); searchGrid.Children.Add(searchContent);

            Grid peersArea = new Grid();
            peersArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            peersArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid peerHeading = new Grid();
            peerHeading.ColumnDefinitions.Add(new ColumnDefinition());
            peerHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock caption = Theme.Text("EQUIPOS CERCANOS", 10, Theme.Muted, FontWeights.SemiBold);
            caption.VerticalAlignment = VerticalAlignment.Center;
            peerHeading.Children.Add(caption);
            _peerCount = Theme.Text("0 EN LÍNEA", 10, Theme.Muted);
            _peerCount.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_peerCount, 1); peerHeading.Children.Add(_peerCount);
            peersArea.Children.Add(peerHeading);
            Grid listArea = new Grid();
            _peerList = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            ScrollViewer.SetVerticalScrollBarVisibility(_peerList, ScrollBarVisibility.Auto);
            Style itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 0, 0, 5)));
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
            _peerList.MouseDoubleClick += (s, e) => { if (_send.IsEnabled) SendClicked(this, null); };
            listArea.Children.Add(_peerList);
            _emptyPeers = Theme.Text("Buscando equipos…\n\nAbre Lazo en otro equipo de esta red.", 12, Theme.Muted);
            _emptyPeers.TextAlignment = TextAlignment.Center;
            _emptyPeers.VerticalAlignment = VerticalAlignment.Center;
            _emptyPeers.HorizontalAlignment = HorizontalAlignment.Center;
            _emptyPeers.IsHitTestVisible = false;
            listArea.Children.Add(_emptyPeers);
            Grid.SetRow(listArea, 1); peersArea.Children.Add(listArea);
            Grid.SetRow(peersArea, 3); main.Children.Add(peersArea);

            Grid footer = new Grid();
            footer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            footer.RowDefinitions.Add(new RowDefinition());
            footer.Children.Add(new Border { Height = 1, Background = Theme.Line });
            Grid bottom = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            bottom.ColumnDefinitions.Add(new ColumnDefinition());
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            StackPanel info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _status = Theme.Text(_path == null ? "Listo para enviar" : "Elige un equipo", 11, Theme.Ink);
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            info.Children.Add(_status);
            TextBlock hint = Theme.Text("ALT × 2   ·   CTRL + ALT + L", 9, Theme.Muted);
            hint.Margin = new Thickness(0, 5, 0, 0);
            info.Children.Add(hint);
            Border progressTrack = new Border { Height = 2, Background = Theme.Line, Margin = new Thickness(0, 7, 16, 0) };
            Grid track = new Grid { ClipToBounds = true };
            _progressScale = new ScaleTransform(0, 1);
            track.Children.Add(new Border { Background = Theme.Ink, RenderTransform = _progressScale, RenderTransformOrigin = new Point(0, 0.5) });
            progressTrack.Child = track;
            info.Children.Add(progressTrack);
            bottom.Children.Add(info);
            _send = Theme.Button("ENVIAR  →", true);
            _send.MinHeight = 42;
            _send.Click += SendClicked;
            _send.IsEnabled = false;
            Grid.SetColumn(_send, 1); bottom.Children.Add(_send);
            Grid.SetRow(bottom, 1); footer.Children.Add(bottom);
            Grid.SetRow(footer, 4); main.Children.Add(footer);
            FilterPeers(Guid.Empty);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = HasOneFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
            _fileCard.BorderBrush = Theme.Ink;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            _fileCard.BorderBrush = Theme.Line;
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null && paths.Length == 1) SelectFile(paths[0]);
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
            _fileMeta.Text = FormatSize(info.Length);
            _fileCard.BorderBrush = Theme.Ink;
            _status.Text = "Elige un equipo";
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
                Guid selected = SelectedPeerId();
                _peers = peers;
                FilterPeers(selected);
            }));
        }

        private Guid SelectedPeerId()
        {
            ListBoxItem item = _peerList.SelectedItem as ListBoxItem;
            return item == null ? Guid.Empty : ((Peer)item.Tag).Id;
        }

        private void FilterPeers(Guid preserve)
        {
            if (_peerList == null || _search == null) return;
            string query = _search.Text.Trim();
            _peerList.Items.Clear();
            List<Peer> shown = _peers.Where(p => query.Length == 0 ||
                p.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.Address.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (Peer peer in shown)
            {
                Grid row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                TextBlock dot = Theme.Text(Theme.IsGlass ? "◯" : "●", 15, Theme.Ink);
                dot.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(dot);
                StackPanel labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                labels.Children.Add(Theme.Text(peer.Name, 12, Theme.Ink, FontWeights.SemiBold));
                labels.Children.Add(Theme.Text(peer.Address.ToString(), 10, Theme.Muted));
                Grid.SetColumn(labels, 1); row.Children.Add(labels);
                TextBlock arrow = Theme.Text("↵", 15, Theme.Muted);
                arrow.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
                Border card = Theme.Card(row, new Thickness(11, 6, 11, 6));
                card.MinHeight = 50;
                ListBoxItem item = new ListBoxItem { Content = card, Tag = peer };
                _peerList.Items.Add(item);
                if (peer.Id == preserve) _peerList.SelectedItem = item;
            }
            _peerCount.Text = _peers.Count + " EN LÍNEA";
            _emptyPeers.Text = _peers.Count == 0 ? "Buscando equipos…\n\nAbre Lazo en otro equipo de esta red." : "No hay coincidencias.";
            _emptyPeers.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshPeerStyles();
            UpdateSendEnabled();
        }

        private void RefreshPeerStyles()
        {
            foreach (ListBoxItem item in _peerList.Items)
            {
                Border card = (Border)item.Content;
                card.BorderBrush = item.IsSelected ? Theme.Ink : Theme.Line;
                card.Background = item.IsSelected ? Theme.SoftSurface : Theme.CardSurface;
            }
        }

        private void UpdateSendEnabled()
        {
            if (_send != null) _send.IsEnabled = !_sending && _path != null && _peerList.SelectedItem != null;
        }

        private async void SendClicked(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = _peerList.SelectedItem as ListBoxItem;
            if (item == null || _path == null || !_send.IsEnabled) return;
            Peer peer = (Peer)item.Tag;
            _sending = true;
            _send.IsEnabled = false;
            _status.Text = "Esperando a " + peer.Name + "…";
            _progressScale.ScaleX = 0;
            try
            {
                await _network.SendAsync(peer, _path, value => Dispatcher.BeginInvoke((Action)(() =>
                {
                    _status.Text = "Enviando  " + (value * 100).ToString("0") + "%";
                    _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progressScale.ScaleX, value, 130));
                })));
                _status.Text = "Entregado a " + peer.Name;
                _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progressScale.ScaleX, 1, 180));
            }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { _sending = false; UpdateSendEnabled(); }
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
            Dispatcher.BeginInvoke((Action)(() => { if (_receiveWindow != null && _receiveWindow.OfferId == id) _receiveWindow.Finish(message); }));
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { HideAnimated(); e.Handled = true; }
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { ChooseFile(); e.Handled = true; }
            else if (e.Key == Key.Down && _peerList.Items.Count > 0)
            {
                _peerList.SelectedIndex = Math.Min(_peerList.Items.Count - 1, _peerList.SelectedIndex + 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _peerList.Items.Count > 0)
            {
                _peerList.SelectedIndex = Math.Max(0, _peerList.SelectedIndex - 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _send.IsEnabled) { SendClicked(this, null); e.Handled = true; }
        }

        private void SetupHotkeys()
        {
            _hotkeys = new Hotkeys(this, Toggle);
            if (!_hotkeys.DoubleAltAvailable && !_hotkeys.AlternativeAvailable) _status.Text = "Atajos no disponibles; abre Lazo desde la bandeja.";
            else if (!_hotkeys.DoubleAltAvailable) _status.Text = "Usa Ctrl+Alt+L para abrir Lazo.";
            else if (!_hotkeys.AlternativeAvailable) _status.Text = "Usa doble Alt para abrir Lazo.";
        }

        private void Toggle()
        {
            if (IsVisible) HideAnimated(); else ShowAnimated();
        }

        public void ActivateFromElsewhere() { ShowAnimated(); }

        private void ShowAnimated()
        {
            if (_animating) return;
            Topmost = true;
            Show();
            WindowPlacement.CenterOnCursor(this);
            Activate();
            Theme.Enter(_shell);
            _search.Focus();
        }

        private void HideAnimated()
        {
            if (_animating || !IsVisible) return;
            _animating = true;
            Theme.Leave(_shell, () => { Hide(); _animating = false; });
        }

        private void SetupTray()
        {
            _tray = new System.Windows.Forms.NotifyIcon();
            _tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
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

    }
}
