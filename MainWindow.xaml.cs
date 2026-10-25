using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace QuickPrompt;

public partial class MainWindow : Window
{
    private readonly LibraryStore _store;
    private readonly Library _library;
    private Forms.NotifyIcon? _tray;
    private HwndSource? _source;
    private IntPtr _handle;
    private IntPtr _previousWindow;
    private int _category;
    private bool _dialogOpen, _menuOpen, _exiting, _copying, _ready, _dragging;
    private string? _registeredHotkey;
    private Window? _dialog;
    private NativeMethods.Rect _monitorWork;
    private double _monitorScale = 1;
    private bool _hasMonitor;
    private List<LauncherItem> _documents = [];
    private CancellationTokenSource? _documentScan;
    private string? _documentWarning;
    private bool _scanningDocuments;

    public MainWindow(LibraryStore store, Library library)
    {
        _store = store;
        _library = library;
        _category = library.Settings.RememberCategory ? library.Settings.LastCategory : library.Settings.CategoryOrder[0];
        InitializeComponent();
        LanguageToggle.IsChecked = library.Settings.Language == "de";
        _ready = true;
        SearchBox.GotKeyboardFocus += (_, _) => UpdateHint();
        SearchBox.LostKeyboardFocus += (_, _) => UpdateHint();
        BuildTabs();
        RefreshPrompts();
    }

    public void InitializeLauncher(bool show)
    {
        _handle = new WindowInteropHelper(this).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WindowProc);
        CreateTray();
        if (!TrySetHotkey(_library.Settings.Hotkey))
        {
            // Keep the app reachable if Launchy or another launcher owns Alt+Space.
            string requested = _library.Settings.Hotkey;
            string? fallback = NativeMethods.Hotkeys.FirstOrDefault(TrySetHotkey);
            string message = fallback is null
                ? "The launcher shortcuts are already in use. Open QuickPrompt from its tray icon, then choose Settings."
                : $"{requested} is already in use by another application. QuickPrompt is using {fallback} for this session. You can change it in Settings.\n\nIf Launchy is running, exit it before assigning Alt+Space to QuickPrompt.";
            MessageBox.Show(message, "QuickPrompt — shortcut", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        UpdateHotkeyLabel();
        if (show) ShowLauncher();
        else RefreshDocuments();
    }

    private void CreateTray()
    {
        using var iconStream = typeof(App).Assembly.GetManifestResourceStream("QuickPrompt.Assets.QuickPrompt.ico")!;
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(iconStream), Text = "QuickPrompt", Visible = true };
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new TrayMenuColors()),
            ShowImageMargin = false, BackColor = System.Drawing.Color.FromArgb(23, 25, 30),
            ForeColor = System.Drawing.Color.FromArgb(239, 241, 245)
        };
        menu.Items.Add("Open QuickPrompt", null, (_, _) => Dispatcher.Invoke(ShowLauncher));
        menu.Items.Add("Settings…", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ShowLauncher); };
    }

    internal bool TrySetHotkey(string hotkey)
    {
        if (!NativeMethods.Hotkeys.Contains(hotkey)) return false;
        if (_registeredHotkey == hotkey) return true;
        string? previous = _registeredHotkey;
        if (previous is not null) NativeMethods.UnregisterHotKey(_handle, NativeMethods.HotkeyId);
        if (NativeMethods.RegisterHotKey(_handle, NativeMethods.HotkeyId, NativeMethods.Modifiers(hotkey) | 0x4000, 0x20))
        {
            _registeredHotkey = hotkey;
            UpdateHotkeyLabel();
            return true;
        }
        _registeredHotkey = null;
        if (previous is not null && NativeMethods.RegisterHotKey(_handle, NativeMethods.HotkeyId, NativeMethods.Modifiers(previous) | 0x4000, 0x20)) _registeredHotkey = previous;
        return false;
    }

    private void UpdateHotkeyLabel()
    {
        HotkeyLabel.Text = (_registeredHotkey ?? "tray icon").Replace("+", " + ").ToUpperInvariant();
        if (_tray is not null) _tray.Text = "QuickPrompt · " + (_registeredHotkey ?? "Open from tray");
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == NativeMethods.HotkeyId)
        {
            handled = true;
            if (_dialogOpen) { _dialog?.Activate(); return IntPtr.Zero; }
            if (IsVisible) HideLauncher(true); else ShowLauncher();
        }
        else if ((uint)msg == NativeMethods.ShowMessage) { handled = true; ShowLauncher(); }
        return IntPtr.Zero;
    }

    private void ConfigureLauncherMonitor()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var monitor = NativeMethods.MonitorFromPoint(cursor, 2);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;
        NativeMethods.GetDpiForMonitor(monitor, 0, out uint dpi, out _);
        _monitorScale = (dpi == 0 ? 96 : dpi) / 96.0;
        _monitorWork = info.Work;
        _hasMonitor = true;
        int availableWidth = info.Work.Right - info.Work.Left;
        int availableHeight = info.Work.Bottom - info.Work.Top;
        Width = Math.Min(580, availableWidth / _monitorScale - 24);
        MaxHeight = availableHeight / _monitorScale - 24;
        // At most six compact rows; on small displays leave room for the controls.
        PromptList.MaxHeight = Math.Max(54, Math.Min(324, MaxHeight - 162));
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _dialogOpen) return;
        for (var source = e.OriginalSource as DependencyObject; source is not null && source != LauncherHeader;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source))
            if (source is ButtonBase) return;
        e.Handled = true;
        _dragging = true;
        try { DragMove(); }
        finally { _dragging = false; }
    }

    private void CenterLauncher()
    {
        if (!_hasMonitor || _handle == IntPtr.Zero || !IsVisible) return;
        int width = (int)Math.Round(ActualWidth * _monitorScale);
        int height = (int)Math.Round(ActualHeight * _monitorScale);
        // Only move: WPF measures the content and owns the window's height.
        // Called only on opening; later content changes keep the user's position.
        NativeMethods.SetWindowPos(_handle, IntPtr.Zero,
            _monitorWork.Left + (_monitorWork.Right - _monitorWork.Left - width) / 2,
            _monitorWork.Top + (_monitorWork.Bottom - _monitorWork.Top - height) / 2,
            0, 0, 0x0015); // NOSIZE | NOZORDER | NOACTIVATE
    }

    public void ShowLauncher()
    {
        System.Diagnostics.Trace.WriteLine($"ShowLauncher visible={IsVisible} dialog={_dialogOpen}");
        if (_dialogOpen) { _dialog?.Activate(); return; }
        if (!IsVisible)
        {
            _previousWindow = NativeMethods.GetForegroundWindow();
            if (!_library.Settings.RememberCategory) _category = _library.Settings.CategoryOrder[0];
            SearchBox.Clear();
            BuildTabs();
            RefreshPrompts();
            ConfigureLauncherMonitor();
            Show();
            NativeMethods.ShowWindow(_handle, 5);
            UpdateLayout();
            CenterLauncher();
        }
        Activate();
        bool foreground = NativeMethods.SetForegroundWindow(_handle);
        System.Diagnostics.Trace.WriteLine($"Shown visible={IsVisible} active={IsActive} foreground={foreground} handle={_handle} currentHandle={new WindowInteropHelper(this).Handle} valid={NativeMethods.IsWindow(_handle)} foregroundWindow={NativeMethods.GetForegroundWindow()} size={ActualWidth}x{ActualHeight} pos={Left},{Top}");
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => SearchBox.Focus()));
        RefreshDocuments();
    }

    private void HideLauncher(bool restoreFocus)
    {
        System.Diagnostics.Trace.WriteLine($"HideLauncher restore={restoreFocus} active={IsActive} dialog={_dialogOpen}");
        if (_dialogOpen) return;
        Hide();
        if (restoreFocus && _previousWindow != _handle && NativeMethods.IsWindow(_previousWindow)) NativeMethods.SetForegroundWindow(_previousWindow);
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            // A placeholder dialog pins both windows while the user switches
            // apps to collect text. Only normal launcher use dismisses on blur.
            if (!_dialogOpen && !_menuOpen && !_copying && !_dragging && !IsActive) HideLauncher(false);
        }));
    }
    private void OnMenuOpened(object sender, RoutedEventArgs e) => _menuOpen = true;
    private void OnMenuClosed(object sender, RoutedEventArgs e)
    {
        _menuOpen = false;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!_dialogOpen && !_dragging && !IsActive) HideLauncher(false); }));
    }

    private void BuildTabs()
    {
        CategoryTabs.Children.Clear();
        for (int i = 0; i < 5; i++)
        {
            int index = i == LauncherSearch.DocumentsTab ? LauncherSearch.DocumentsTab : _library.Settings.CategoryOrder[i];
            var button = new Button
            {
                Content = index == LauncherSearch.DocumentsTab ? "Documents" : _library.Settings.Categories[index], Focusable = false,
                FontSize = 12,
                Margin = new Thickness(i == 0 ? 0 : 3, 0, i == 4 ? 0 : 3, 0), Padding = new Thickness(4, 5, 4, 5),
                Background = Brush(index == _category ? "#30394E" : "#17191E"),
                BorderBrush = Brush(index == _category ? "#4A5B7C" : "#32363F"),
                Foreground = (Brush)FindResource(index == _category ? "Text" : "Muted"),
                ToolTip = (index == LauncherSearch.DocumentsTab ? "Documents" : _library.Settings.Categories[index]) + " (Alt+" + (i + 1) + ")"
            };
            button.Click += (_, _) => SwitchCategory(index);
            CategoryTabs.Children.Add(button);
        }
    }
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (_dialogOpen || _copying) { LanguageToggle.IsChecked = _library.Settings.Language == "de"; return; }
        string previous = _library.Settings.Language;
        _library.Settings.Language = LanguageToggle.IsChecked == true ? "de" : "en";
        try { _store.Save(_library); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _library.Settings.Language = previous;
            LanguageToggle.IsChecked = previous == "de";
            Hint.Text = "Could not save language preference.";
            return;
        }
        RefreshPrompts(selectedIdentity: (PromptList.SelectedItem as LauncherItem)?.Identity);
        SearchBox.Focus();
    }

    private void SwitchCategory(int index)
    {
        _category = (index + 5) % 5;
        _library.Settings.LastCategory = _category;
        BuildTabs();
        RefreshPrompts();
        if (!SearchBox.IsKeyboardFocusWithin) FocusList();
        // Save the remembered category only when it actually changes.
        try { _store.Save(_library); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Hint.Text = "Could not save category preference."; }
    }
    private void SwitchCategoryPosition(int position) => SwitchCategory(position == LauncherSearch.DocumentsTab
        ? LauncherSearch.DocumentsTab : _library.Settings.CategoryOrder[position]);
    private void MoveCategorySelection(int delta)
    {
        int position = _category == LauncherSearch.DocumentsTab ? LauncherSearch.DocumentsTab : Array.IndexOf(_library.Settings.CategoryOrder, _category);
        SwitchCategoryPosition((position + delta + 5) % 5);
    }

    private void RefreshPrompts(Guid? select = null, string? selectedIdentity = null)
    {
        if (!_ready) return;
        var prompts = LauncherSearch.Find(_library, _documents, _category, SearchBox.Text);
        PromptList.ItemsSource = prompts;
        PromptList.SelectedItem = prompts.FirstOrDefault(p => p.Prompt?.Id == select && select.HasValue || p.Identity == selectedIdentity) ?? prompts.FirstOrDefault();
        EmptyState.Visibility = prompts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PromptList.Visibility = prompts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyTitle.Text = SearchBox.Text.Length > 0 ? "No matches." : _category == LauncherSearch.DocumentsTab ? "No documents yet." : "A little space for your next idea.";
        EmptyHelp.Text = _category == LauncherSearch.DocumentsTab
            ? _scanningDocuments ? "Looking for documents…" : _documentWarning ?? "Add documents or Windows shortcuts to your folder."
            : "Try another search or add a prompt with Ctrl+N.";
        CountLabel.Text = _scanningDocuments ? "Scanning…" : $"{prompts.Count} result{(prompts.Count == 1 ? "" : "s")}";
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateHint();
    }
    private void OnRefreshDocuments(object sender, RoutedEventArgs e) => RefreshDocuments();
    private async void RefreshDocuments()
    {
        _documentScan?.Cancel();
        var request = new CancellationTokenSource();
        _documentScan = request;
        string folder = _library.Settings.DocumentsFolder;
        bool recursive = _library.Settings.IncludeDocumentSubfolders;
        _scanningDocuments = true;
        RefreshPrompts(selectedIdentity: (PromptList.SelectedItem as LauncherItem)?.Identity);
        try
        {
            var scan = await Task.Run(() => DocumentCatalog.Scan(folder, recursive, request.Token));
            if (request.IsCancellationRequested || _exiting) return;
            _documents = scan.Items;
            _documentWarning = scan.Warning;
            _scanningDocuments = false;
            RefreshPrompts(selectedIdentity: (PromptList.SelectedItem as LauncherItem)?.Identity);
            if (_documentWarning is not null) { Hint.Text = _documentWarning; Hint.ToolTip = folder; }
        }
        catch (OperationCanceledException) { }
        finally { if (_documentScan == request) _documentScan = null; request.Dispose(); }
    }
    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshPrompts();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PromptList.SelectedItem is LauncherItem item) PromptList.ScrollIntoView(item);
        if (_ready) UpdateHint();
    }
    private void FocusList()
    {
        PromptList.Focus();
        if (PromptList.SelectedItem is not null)
        {
            PromptList.UpdateLayout();
            (PromptList.ItemContainerGenerator.ContainerFromItem(PromptList.SelectedItem) as ListBoxItem)?.Focus();
        }
        UpdateHint();
    }
    private void UpdateHint()
    {
        string action = (PromptList.SelectedItem as LauncherItem)?.ActionLabel.ToLowerInvariant() ?? "select";
        Hint.Text = SearchBox.IsKeyboardFocusWithin
            ? $"↑ / ↓  navigate     Tab  category     Enter  {action}     Esc  close"
            : $"I / K  navigate     J / L / Tab  category     Enter  {action}";
        Hint.ToolTip = null;
    }
    private void MoveSelection(int delta)
    {
        if (PromptList.Items.Count == 0) return;
        PromptList.SelectedIndex = (PromptList.SelectedIndex + delta + PromptList.Items.Count) % PromptList.Items.Count;
        if (!SearchBox.IsKeyboardFocusWithin) FocusList();
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_dialogOpen) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        bool ctrl = modifiers.HasFlag(ModifierKeys.Control);
        bool search = SearchBox.IsKeyboardFocusWithin;
        e.Handled = true;
        if (key == Key.Escape) HideLauncher(true);
        else if (ctrl && key == Key.N) EditPrompt(null);
        else if (ctrl && key == Key.OemComma) OpenSettings();
        else if (ctrl && key == Key.F) { SearchBox.Focus(); SearchBox.SelectAll(); }
        else if (key == Key.F2) EditSelected();
        else if (key == Key.F5) RefreshDocuments();
        else if (key == Key.Tab) MoveCategorySelection(modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
        else if (modifiers == ModifierKeys.Alt && key >= Key.D1 && key <= Key.D5) SwitchCategoryPosition(key - Key.D1);
        else if (key == Key.Enter) await ActivateSelected();
        else if (key == Key.Down || (!search && modifiers == ModifierKeys.None && key == Key.K)) MoveSelection(1);
        else if (key == Key.Up || (!search && modifiers == ModifierKeys.None && key == Key.I)) MoveSelection(-1);
        else if (!search && modifiers == ModifierKeys.None && key == Key.L) MoveCategorySelection(1);
        else if (!search && modifiers == ModifierKeys.None && key == Key.J) MoveCategorySelection(-1);
        else if (!search && key == Key.OemQuestion && modifiers == ModifierKeys.None) SearchBox.Focus();
        else if (search && ctrl && key == Key.Back) { SearchBox.Clear(); FocusList(); }
        else e.Handled = false;
    }
    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_dialogOpen || SearchBox.IsKeyboardFocusWithin || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            SearchBox.Focus(); SearchBox.Text += e.Text; SearchBox.CaretIndex = SearchBox.Text.Length; e.Handled = true;
        }
    }
    private async Task ActivateSelected()
    {
        if (_copying || _dialogOpen || PromptList.SelectedItem is not LauncherItem item) return;
        if (item.FilePath is string path)
        {
            try
            {
                if (!DocumentCatalog.IsSupported(path) || !File.Exists(path)) throw new IOException("File no longer exists. Press F5 to refresh.");
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
                HideLauncher(false);
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or UnauthorizedAccessException)
            {
                Hint.Text = "Could not open file. Hover here for details.";
                Hint.ToolTip = ex.Message;
            }
            return;
        }
        if (item.Prompt is not Prompt prompt) return;
        string language = _library.Settings.Language;
        if (prompt.RequiresLanguageWarning(language))
        {
            var warning = new MissingLanguageWindow(prompt, language);
            RunDialog(warning);
            if (warning.EditRequested) { EditPrompt(prompt, true); return; }
            if (warning.DialogResult != true) { FocusList(); return; }
        }
        string content = prompt.GetContent(prompt.AvailableLanguage(language));
        _copying = true;
        try
        {
            if (PromptTemplate.GetNames(content).Count > 0)
            {
                var variables = new PromptVariablesWindow(content) { CopyPrompt = CopyToClipboard };
                RunDialog(variables);
                if (variables.DialogResult == true) HideLauncher(true);
                else FocusList();
            }
            else if (await CopyToClipboard(content)) HideLauncher(true);
            else Hint.Text = "Clipboard is busy. Press Enter to try again.";
        }
        finally { _copying = false; }
    }
    private static async Task<bool> CopyToClipboard(string content)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetDataObject(content, true); return true; }
            catch (ExternalException)
            {
                if (attempt == 4) return false;
                await Task.Delay(60);
            }
        }
        return false;
    }
    private async void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(PromptList, e.OriginalSource as DependencyObject) is ListBoxItem) await ActivateSelected();
    }

    private void OnNew(object sender, RoutedEventArgs e) => EditPrompt(null);
    private void OnEdit(object sender, RoutedEventArgs e) => EditSelected();
    private void EditSelected() { if ((PromptList.SelectedItem as LauncherItem)?.Prompt is Prompt p) EditPrompt(p); }
    private void EditPrompt(Prompt? original, bool focusContent = false)
    {
        if (_dialogOpen) return;
        var editor = new PromptEditor(_library.Settings.Categories, original, _category == LauncherSearch.DocumentsTab ? _library.Settings.CategoryOrder[0] : _category, _library.Settings.Language, focusContent, _library.Settings.CategoryOrder);
        editor.SavePrompt = (draft, delete) =>
        {
            int oldIndex = original is null ? -1 : _library.Prompts.IndexOf(original);
            if (oldIndex >= 0) _library.Prompts.RemoveAt(oldIndex);
            if (!delete) _library.Prompts.Add(draft);
            try { _store.Save(_library); }
            catch
            {
                if (!delete) _library.Prompts.Remove(draft);
                if (oldIndex >= 0) _library.Prompts.Insert(oldIndex, original!);
                throw;
            }
            if (!delete) _category = draft.Category;
        };
        RunDialog(editor);
        BuildTabs();
        SearchBox.Clear();
        RefreshPrompts(editor.SavedId);
        FocusList();
    }
    private void OnSettings(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenSettings()
    {
        if (_dialogOpen) { _dialog?.Activate(); return; }
        ShowLauncher();
        var settings = new SettingsWindow(_library.Settings, _registeredHotkey, _store.FilePath);
        settings.SaveSettings = updated =>
        {
            string? oldHotkey = _registeredHotkey;
            if (!TrySetHotkey(updated.Hotkey)) throw new InvalidOperationException("That shortcut is already in use. Try another shortcut or close the other launcher first.");
            var previous = _library.Settings;
            _library.Settings = updated;
            try { _store.Save(_library); }
            catch { _library.Settings = previous; if (oldHotkey is not null) TrySetHotkey(oldHotkey); throw; }
        };
        RunDialog(settings);
        BuildTabs(); RefreshPrompts(); FocusList();
        _documents = [];
        RefreshDocuments();
    }
    private void RunDialog(Window dialog)
    {
        _dialogOpen = true; _dialog = dialog;
        dialog.Owner = this;
        dialog.Topmost = true;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        try { dialog.ShowDialog(); }
        finally { _dialog = null; _dialogOpen = false; Activate(); }
    }
    private void OnExit(object sender, RoutedEventArgs e) => ExitApp();
    private void ExitApp()
    {
        if (_dialogOpen) { _dialog?.Activate(); return; }
        _exiting = true;
        Close();
        Application.Current.Shutdown();
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting) { e.Cancel = true; HideLauncher(true); return; }
        NativeMethods.UnregisterHotKey(_handle, NativeMethods.HotkeyId);
        _documentScan?.Cancel();
        _source?.RemoveHook(WindowProc);
        if (_tray is not null) { _tray.Visible = false; _tray.Icon?.Dispose(); _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        base.OnClosing(e);
    }
}
