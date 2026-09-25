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
using System.Windows.Threading;
using Microsoft.Win32;

namespace Lazo
{
    internal sealed class MainWindow : Window
    {
        private readonly NetworkEngine _network = new NetworkEngine();
        private readonly bool _preview;
        private readonly DispatcherTimer _searchDelay;
        private readonly List<string> _results = new List<string>();
        private List<Peer> _peers = new List<Peer>();
        private Border _shell;
        private StackPanel _resultList;
        private TextBox _search;
        private TextBlock _placeholder;
        private TextBlock _empty;
        private TextBlock _count;
        private TextBlock _status;
        private ScaleTransform _progressScale;
        private EverythingSearch _everything;
        private Hotkeys _hotkeys;
        private System.Windows.Forms.NotifyIcon _tray;
        private ReceiveWindow _receiveWindow;
        private string _manualPath;
        private string _activeQuery;
        private int _selectedRow;
        private bool _exiting;
        private bool _animating;
        private bool _sending;

        public MainWindow(bool preview = false, bool previewGlass = false, bool previewLiveSearch = false)
        {
            _preview = preview;
            Theme.Load();
            if (previewGlass) Theme.SetForPreview(ThemeKind.Glass);
            Title = "Lazo";
            Width = MinWidth = MaxWidth = 766;
            Height = MinHeight = MaxHeight = 488;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            _searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); RunSearch(); };
            if (_preview && !previewLiveSearch)
            {
                _peers = new List<Peer> {
                    new Peer { Id = Guid.NewGuid(), Name = "JQUIN" },
                    new Peer { Id = Guid.NewGuid(), Name = "EQUIPO-OFICINA" },
                    new Peer { Id = Guid.NewGuid(), Name = "ATLAS" }
                };
                _results.AddRange(new[] {
                    @"C:\Users\JQUIN\Documents\proyecto-neural-21.pdf",
                    @"C:\Users\JQUIN\Downloads\planos-finales.zip",
                    @"C:\Users\JQUIN\Desktop\presupuesto.xlsx"
                });
            }
            BuildUi();
            if (_preview) { _search.Text = previewLiveSearch ? "Lazo" : "proyecto"; RenderResults(); }
            Loaded += (s, e) => { Theme.Enter(_shell); _search.Focus(); };
            SourceInitialized += (s, e) =>
            {
                WindowPlacement.CenterOnCursor(this);
                if (!_preview || previewLiveSearch)
                {
                    _everything = new EverythingSearch(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                        paths => { if (_search.Text.Trim() != _activeQuery) return; _results.Clear(); _results.AddRange(paths); _empty.Text = paths.Count == 0 ? "No se encontraron archivos." : ""; RenderResults(); },
                        message => { if (_search.Text.Trim() != _activeQuery) return; _results.Clear(); _empty.Text = message; RenderResults(); });
                    if (!_preview) SetupHotkeys();
                }
            };
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
            Grid outer = new Grid { Margin = new Thickness(9) };
            Content = outer;
            _shell = new Border { Background = Theme.ShellSurface(), BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Theme.IsGlass ? 24 : 13),
                Effect = Theme.Shadow(), ClipToBounds = true, AllowDrop = true };
            outer.Children.Add(_shell);
            _shell.DragEnter += (s, e) => { e.Effects = HasOneFile(e.Data) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            _shell.Drop += (s, e) => { string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths != null && paths.Length == 1) SelectFile(paths[0]); };
            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            _shell.Child = layout;

            Grid header = new Grid { Margin = new Thickness(22, 0, 15, 0) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            Grid searchHost = new Grid { Margin = new Thickness(0, 0, 8, 0) };
            _search = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = Theme.Ink, FontFamily = Theme.Font, FontSize = 17,
                VerticalContentAlignment = VerticalAlignment.Center, CaretBrush = Theme.Ink };
            _placeholder = Theme.Text("Buscar archivos…", 17, Theme.Muted);
            _placeholder.VerticalAlignment = VerticalAlignment.Center;
            _placeholder.IsHitTestVisible = false;
            searchHost.Children.Add(_search);
            searchHost.Children.Add(_placeholder);
            _search.TextChanged += (s, e) =>
            {
                _placeholder.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                _searchDelay.Stop();
                if (_search.Text.Trim().Length == 0)
                {
                    _results.Clear();
                    _empty.Text = _manualPath == null ? "Escribe un nombre para buscar archivos." : "";
                    RenderResults();
                }
                else
                {
                    _results.Clear();
                    _empty.Text = "Buscando…";
                    RenderResults();
                    _searchDelay.Start();
                }
            };
            header.Children.Add(searchHost);
            Button theme = TextButton(Theme.IsGlass ? "Minimal" : "Vidrio");
            theme.Click += (s, e) =>
            {
                string query = _search.Text;
                Theme.Toggle(!_preview);
                BuildUi();
                _search.Text = query;
                RenderResults();
                Theme.Enter(_shell);
                _search.Focus();
            };
            Grid.SetColumn(theme, 1); header.Children.Add(theme);
            Button close = TextButton("×");
            close.FontSize = 22;
            close.Click += (s, e) => HideAnimated();
            Grid.SetColumn(close, 2); header.Children.Add(close);
            Grid.SetRow(header, 0); layout.Children.Add(header);
            Border rule = new Border { Height = 1, Background = Theme.Line };
            Grid.SetRow(rule, 1); layout.Children.Add(rule);

            Grid heading = new Grid { Margin = new Thickness(22, 0, 23, 0) };
            heading.ColumnDefinitions.Add(new ColumnDefinition());
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock title = Theme.Text("Archivos", 11, Theme.Muted);
            title.VerticalAlignment = VerticalAlignment.Center;
            heading.Children.Add(title);
            _count = Theme.Text("", 11, Theme.Muted);
            _count.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_count, 1); heading.Children.Add(_count);
            Grid.SetRow(heading, 2); layout.Children.Add(heading);

            Grid resultsArea = new Grid { Margin = new Thickness(8, 0, 8, 0) };
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _resultList = new StackPanel();
            scroll.Content = _resultList;
            resultsArea.Children.Add(scroll);
            _empty = Theme.Text("Escribe un nombre para buscar archivos.", 12, Theme.Muted);
            _empty.HorizontalAlignment = HorizontalAlignment.Center;
            _empty.VerticalAlignment = VerticalAlignment.Center;
            _empty.IsHitTestVisible = false;
            resultsArea.Children.Add(_empty);
            Grid.SetRow(resultsArea, 3); layout.Children.Add(resultsArea);

            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
            Border footerRule = new Border { Height = 1, Background = Theme.Line, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumnSpan(footerRule, 3);
            footer.Children.Add(footerRule);
            _status = Theme.Text("Listo para enviar", 11, Theme.Muted);
            _status.VerticalAlignment = VerticalAlignment.Center;
            _status.Margin = new Thickness(21, 0, 0, 0);
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            footer.Children.Add(_status);
            Button choose = TextButton("Elegir archivo  Ctrl+O");
            choose.Click += (s, e) => ChooseFile();
            Grid.SetColumn(choose, 1); footer.Children.Add(choose);
            TextBlock devices = Theme.Text("", 11, Theme.Muted);
            devices.VerticalAlignment = VerticalAlignment.Center;
            devices.HorizontalAlignment = HorizontalAlignment.Right;
            devices.Margin = new Thickness(0, 0, 20, 0);
            Grid.SetColumn(devices, 2); footer.Children.Add(devices);
            _count.Tag = devices;
            Border progressTrack = new Border { Height = 2, Background = Theme.Line,
                VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetColumnSpan(progressTrack, 3);
            _progressScale = new ScaleTransform(0, 1);
            progressTrack.Child = new Border { Background = Theme.Ink, RenderTransform = _progressScale,
                RenderTransformOrigin = new Point(0, 0.5) };
            footer.Children.Add(progressTrack);
            Grid.SetRow(footer, 4); layout.Children.Add(footer);
            RenderResults();
        }

        private Button TextButton(string label)
        {
            Button button = new Button { Content = label, FontFamily = Theme.Font, FontSize = 11,
                Foreground = Theme.Muted, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Padding = new Thickness(2) };
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = ButtonPresenter() };
            return button;
        }

        private static FrameworkElementFactory ButtonPresenter()
        {
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            return content;
        }

        private void RunSearch()
        {
            if (_everything == null) return;
            string query = _search.Text.Trim();
            if (query.Length > 0) { _activeQuery = query; _everything.Search(query); }
        }

        private void RenderResults()
        {
            if (_resultList == null) return;
            _resultList.Children.Clear();
            List<string> paths = _search.Text.Trim().Length == 0
                ? (_manualPath == null ? new List<string>() : new List<string> { _manualPath })
                : _results.ToList();
            for (int i = 0; i < paths.Count; i++) _resultList.Children.Add(FileRow(paths[i], i));
            _empty.Visibility = paths.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _count.Text = paths.Count > 0 ? paths.Count + (paths.Count == 1 ? " resultado" : " resultados") : "";
            TextBlock devices = _count.Tag as TextBlock;
            if (devices != null) devices.Text = _peers.Count + (_peers.Count == 1 ? " equipo" : " equipos");
        }

        private Border FileRow(string path, int index)
        {
            Grid grid = new Grid { Margin = new Thickness(12, 0, 7, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel labels = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center };
            TextBlock name = Theme.Text(Path.GetFileName(path), 12, Theme.Ink, FontWeights.Medium);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.MaxWidth = 240;
            labels.Children.Add(name);
            TextBlock location = Theme.Text("   " + Path.GetDirectoryName(path), 11, Theme.Muted);
            location.TextTrimming = TextTrimming.CharacterEllipsis;
            location.MaxWidth = 245;
            labels.Children.Add(location);
            grid.Children.Add(labels);
            StackPanel people = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center };
            foreach (Peer peer in _peers)
            {
                Button person = Avatar(peer);
                person.ToolTip = "Enviar a " + peer.Name + (peer.Address == null ? "" : " · " + peer.Address);
                person.Click += async (s, e) => await SendToAsync(path, peer);
                people.Children.Add(person);
            }
            ScrollViewer peopleScroll = new ScrollViewer { Content = people, MaxWidth = 220,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetColumn(peopleScroll, 1); grid.Children.Add(peopleScroll);
            Border row = new Border { Child = grid, Height = 44,
                Background = index == _selectedRow ? Theme.SoftSurface : Brushes.Transparent,
                CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 1, 0, 1) };
            row.MouseLeftButtonDown += (s, e) => { _selectedRow = index; HighlightRows(); };
            row.MouseEnter += (s, e) => { if (index != _selectedRow) row.Background = Theme.SoftSurface; };
            row.MouseLeave += (s, e) => { if (index != _selectedRow) row.Background = Brushes.Transparent; };
            return row;
        }

        private Button Avatar(Peer peer)
        {
            Button button = new Button { Content = Initials(peer.Name), Width = 30, Height = 30,
                Margin = new Thickness(3, 0, 0, 0), FontFamily = Theme.Font, FontSize = 10,
                FontWeight = FontWeights.SemiBold, Foreground = Theme.Ink,
                Background = Theme.AvatarSurface, BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory ellipse = new FrameworkElementFactory(typeof(Border));
            ellipse.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
            ellipse.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            ellipse.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            ellipse.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            ellipse.AppendChild(ButtonPresenter());
            template.VisualTree = ellipse;
            Trigger hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Button.BackgroundProperty, Theme.AvatarHover));
            template.Triggers.Add(hover);
            button.Template = template;
            return button;
        }

        internal static string Initials(string name)
        {
            string[] words = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2) return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant();
        }

        private void HighlightRows()
        {
            for (int i = 0; i < _resultList.Children.Count; i++)
                ((Border)_resultList.Children[i]).Background = i == _selectedRow ? Theme.SoftSurface : Brushes.Transparent;
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
            _manualPath = path;
            _search.Text = "";
            _selectedRow = 0;
            _empty.Text = "";
            _status.Text = "Selecciona un destinatario";
            RenderResults();
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
            Dispatcher.BeginInvoke((Action)(() => { _peers = peers; RenderResults(); }));
        }

        private async Task SendToAsync(string path, Peer peer)
        {
            if (_sending || _preview) return;
            if (!File.Exists(path)) { _status.Text = "El archivo ya no existe."; return; }
            _sending = true;
            _status.Text = "Esperando a " + peer.Name + "…";
            _progressScale.ScaleX = 0;
            try
            {
                await _network.SendAsync(peer, path, value => Dispatcher.BeginInvoke((Action)(() =>
                {
                    _status.Text = "Enviando " + (value * 100).ToString("0") + "% a " + peer.Name;
                    _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progressScale.ScaleX, value, 130));
                })));
                _status.Text = "Entregado a " + peer.Name;
                _progressScale.BeginAnimation(ScaleTransform.ScaleXProperty, Theme.Animation(_progressScale.ScaleX, 1, 180));
            }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { _sending = false; }
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
            else if (e.Key == Key.Down && _resultList.Children.Count > 0)
            {
                _selectedRow = Math.Min(_resultList.Children.Count - 1, _selectedRow + 1);
                HighlightRows(); e.Handled = true;
            }
            else if (e.Key == Key.Up && _resultList.Children.Count > 0)
            {
                _selectedRow = Math.Max(0, _selectedRow - 1);
                HighlightRows(); e.Handled = true;
            }
        }

        private void SetupHotkeys()
        {
            _hotkeys = new Hotkeys(this, Toggle);
            if (!_hotkeys.DoubleAltAvailable && !_hotkeys.AlternativeAvailable) _status.Text = "Abre Lazo desde la bandeja.";
            else if (!_hotkeys.DoubleAltAvailable) _status.Text = "Ctrl+Alt+L para abrir Lazo.";
            else if (!_hotkeys.AlternativeAvailable) _status.Text = "Doble Alt para abrir Lazo.";
        }

        private void Toggle() { if (IsVisible) HideAnimated(); else ShowAnimated(); }
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
            _tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
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
            _searchDelay.Stop();
            if (_everything != null) _everything.Dispose();
            if (_hotkeys != null) _hotkeys.Dispose();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            _network.Dispose();
        }
    }
}
