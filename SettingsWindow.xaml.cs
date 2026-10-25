using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace QuickPrompt;

public partial class SettingsWindow : Window
{
    private readonly Preferences _original;
    private readonly string _backupDirectory;
    private readonly ObservableCollection<CategoryRow> _categoryRows;
    private Point? _dragStart;
    private const string CategoryDragFormat = "QuickPrompt.CategoryRow";
    public sealed class CategoryRow(int id, string name)
    {
        public int Id { get; } = id;
        public string Name { get; set; } = name;
    }
    public Action<Preferences>? SaveSettings { get; set; }
    public SettingsWindow(Preferences settings, string? activeHotkey, string path)
    {
        InitializeComponent();
        VersionLabel.Text = $"Version {typeof(App).Assembly.GetName().Version!.ToString(3)} · A small home for your best prompts.";
        _original = settings;
        _backupDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "bak");
        OpenBackupsButton.ToolTip = _backupDirectory;
        HotkeyBox.ItemsSource = NativeMethods.Hotkeys;
        HotkeyBox.SelectedItem = activeHotkey ?? settings.Hotkey;
        RememberBox.IsChecked = settings.RememberCategory;
        AllCategoriesBox.IsChecked = settings.SearchAllCategories;
        _categoryRows = new(settings.CategoryOrder.Select(id => new CategoryRow(id, settings.Categories[id])));
        CategoryList.ItemsSource = _categoryRows;
        DataPath.Text = path;
        DocumentsFolderBox.Text = settings.DocumentsFolder;
        SubfoldersBox.IsChecked = settings.IncludeDocumentSubfolders;
    }
    private void OnSave(object sender, RoutedEventArgs e)
    {
        string[] categories = _categoryRows.OrderBy(row => row.Id).Select(row => row.Name.Trim()).ToArray();
        if (categories.Any(string.IsNullOrWhiteSpace)) { ErrorLabel.Text = "Give each category a name."; return; }
        if (categories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4) { ErrorLabel.Text = "Use a different name for each category."; return; }
        try
        {
            var updated = new Preferences { Categories = categories, Hotkey = HotkeyBox.SelectedItem as string ?? "Alt+Space", RememberCategory = RememberBox.IsChecked == true, SearchAllCategories = AllCategoriesBox.IsChecked == true, LastCategory = _original.LastCategory,
                CategoryOrder = _categoryRows.Select(row => row.Id).ToArray(), Language = _original.Language, DocumentsFolder = GetDocumentsFolder(), IncludeDocumentSubfolders = SubfoldersBox.IsChecked == true };
            SaveSettings!(updated); DialogResult = true;
        }
        catch (Exception ex) { ErrorLabel.Text = "Couldn't save settings: " + ex.Message; }
    }
    private void OnCategoryDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(CategoryList);
        CategoryList.SelectedItem = ((FrameworkElement)sender).DataContext;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }
    private void OnCategoryDragEnd(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        ((UIElement)sender).ReleaseMouseCapture();
    }
    private void OnCategoryDragMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { _dragStart = null; ((UIElement)sender).ReleaseMouseCapture(); return; }
        if (_dragStart is not Point start || ((FrameworkElement)sender).DataContext is not CategoryRow row) return;
        Point current = e.GetPosition(CategoryList);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragStart = null;
        ((UIElement)sender).ReleaseMouseCapture();
        try { DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(CategoryDragFormat, row), DragDropEffects.Move); }
        finally { ClearDropIndicators(); }
    }
    private CategoryRow? DraggedCategory(DragEventArgs e) => e.Data.GetDataPresent(CategoryDragFormat)
        && e.Data.GetData(CategoryDragFormat) is CategoryRow row && _categoryRows.Contains(row) ? row : null;
    private int DropIndex(DragEventArgs e, out ListBoxItem? target, out bool after)
    {
        target = ItemsControl.ContainerFromElement(CategoryList, e.OriginalSource as DependencyObject) as ListBoxItem;
        after = target is not null && e.GetPosition(target).Y >= target.ActualHeight / 2;
        return target?.DataContext is CategoryRow row ? _categoryRows.IndexOf(row) + (after ? 1 : 0) : _categoryRows.Count;
    }
    private void OnCategoryDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DraggedCategory(e) is null ? DragDropEffects.None : DragDropEffects.Move;
        ClearDropIndicators();
        if (e.Effects == DragDropEffects.None) return;
        DropIndex(e, out var target, out bool after);
        if (target is null) { target = CategoryList.ItemContainerGenerator.ContainerFromIndex(_categoryRows.Count - 1) as ListBoxItem; after = true; }
        if (target is not null) target.BorderThickness = after ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
    }
    private void OnCategoryDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (DraggedCategory(e) is CategoryRow row)
        {
            MoveCategory(row, DropIndex(e, out _, out _));
            e.Effects = DragDropEffects.Move;
        }
        else e.Effects = DragDropEffects.None;
        ClearDropIndicators();
    }
    private void OnCategoryDragLeave(object sender, DragEventArgs e) => ClearDropIndicators();
    private void ClearDropIndicators()
    {
        for (int i = 0; i < _categoryRows.Count; i++)
            if (CategoryList.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item) item.BorderThickness = new Thickness(0);
    }
    private void MoveCategory(CategoryRow row, int insertionIndex)
    {
        int oldIndex = _categoryRows.IndexOf(row);
        if (oldIndex < 0) return;
        int newIndex = Math.Clamp(insertionIndex > oldIndex ? insertionIndex - 1 : insertionIndex, 0, _categoryRows.Count - 1);
        if (newIndex != oldIndex) _categoryRows.Move(oldIndex, newIndex);
        CategoryList.SelectedItem = row;
    }
    private void OnCategoryUp(object sender, RoutedEventArgs e)
    {
        var row = (CategoryRow)((FrameworkElement)sender).DataContext;
        MoveCategory(row, _categoryRows.IndexOf(row) - 1);
    }
    private void OnCategoryDown(object sender, RoutedEventArgs e)
    {
        var row = (CategoryRow)((FrameworkElement)sender).DataContext;
        MoveCategory(row, _categoryRows.IndexOf(row) + 2);
    }
    private string GetDocumentsFolder()
    {
        string folder = DocumentsFolderBox.Text.Trim().Trim('"');
        if (!Path.IsPathFullyQualified(folder)) throw new ArgumentException(@"Enter a full folder path, such as C:\Launchy.");
        return Path.GetFullPath(folder);
    }
    private void OnBrowseDocuments(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose your document folder", Multiselect = false };
        if (Directory.Exists(DocumentsFolderBox.Text)) picker.InitialDirectory = DocumentsFolderBox.Text;
        if (picker.ShowDialog(this) == true) DocumentsFolderBox.Text = picker.FolderName;
    }
    private void OnOpenDocumentsFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = GetDocumentsFolder();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex) { ErrorLabel.Text = "Couldn't open folder: " + ex.Message; }
    }
    private void OnOpenBackups(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_backupDirectory);
            Process.Start(new ProcessStartInfo { FileName = _backupDirectory, UseShellExecute = true });
        }
        catch (Exception ex) { ErrorLabel.Text = "Couldn't open backups: " + ex.Message; }
    }
}
