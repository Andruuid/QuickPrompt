using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QuickPrompt;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static int Main()
    {
        var app = new App { LaunchOnStartup = false };
        app.InitializeComponent();
        if (Environment.GetCommandLineArgs().Contains("--category-drag-check"))
        {
            var settings = new Preferences();
            var window = new SettingsWindow(settings, settings.Hotkey, "Isolated drag-and-drop test")
            {
                Title = "QuickPrompt Settings — drag test",
                SaveSettings = draft => File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "category-drag-result.json"), System.Text.Json.JsonSerializer.Serialize(draft))
            };
            app.MainWindow = window;
            ((TabItem)window.FindName("CategoriesTab")).IsSelected = true;
            window.ShowDialog();
            app.Shutdown();
            return 0;
        }
        var clipboard = Clipboard.GetDataObject();
        string folder = Path.Combine(Path.GetTempPath(), "QuickPrompt.Ui.Tests." + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new Library { Prompts = [new Prompt { Name = "Template", Content = "Read {FileName}. Explain {Topic}. Repeat {FileName}." }] };
            var main = new MainWindow(new LibraryStore(folder), library);
            app.MainWindow = main;
            main.Show();

            Clipboard.SetText("Unchanged on cancel");
            RunPrompt(main, async dialog =>
            {
                Check(dialog.Topmost && dialog.Owner == main, "Placeholder popup stays above normal windows and belongs to launcher");
                Enter(dialog);
                Check(Label(dialog, "ErrorLabel").Text.Contains("Enter a value"), "Empty value stays on current step");
                Value(dialog).Text = "partial value";
                Invoke(main, "OnDeactivated", main, EventArgs.Empty);
                // Let the launcher's deferred loss-of-focus handler run.
                await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check(main.IsVisible && dialog.IsVisible && Value(dialog).Text == "partial value", $"Loss of launcher focus keeps both windows and current value (launcher={main.IsVisible}, popup={dialog.IsVisible}, value={Value(dialog).Text == "partial value"})");
                PressKey(dialog, Key.Escape);
            });
            Check(main.IsVisible && Clipboard.GetText() == "Unchanged on cancel", "Cancelling returns to launcher without changing clipboard");

            RunPrompt(main, dialog =>
            {
                dialog.Close();
                return Task.CompletedTask;
            });
            Check(main.IsVisible && Clipboard.GetText() == "Unchanged on cancel", "Title-bar close cancels without changing clipboard");

            RunPrompt(main, dialog =>
            {
                Value(dialog).Text = "notes.txt\r\n  second line";
                SavePreview(dialog);
                Enter(dialog);
                Check(Label(dialog, "NameLabel").Text == "{Topic}" && Value(dialog).Text == "", "Enter moves to next distinct placeholder");
                Value(dialog).Text = "Literal {FileName} and $1";
                Enter(dialog);
                return Task.CompletedTask;
            });
            Check(!main.IsVisible, "Successful final Enter closes popup and hides launcher");
            Check(Clipboard.GetText() == "Read notes.txt\r\n  second line. Explain Literal {FileName} and $1. Repeat notes.txt\r\n  second line.", "Final clipboard has all substitutions, repeated names and literal inserted braces");
            Check(library.Prompts[0].Content.Contains("{Topic}"), "Filling does not change saved template");

            int attempts = 0;
            var retryDialog = new PromptVariablesWindow("{Topic}") { CopyPrompt = _ => Task.FromResult(++attempts > 1) };
            Exception? retryError = null;
            retryDialog.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Value(retryDialog).Text = "keep this value";
                    Enter(retryDialog);
                    Check(retryDialog.IsVisible && Value(retryDialog).Text == "keep this value" && Label(retryDialog, "ErrorLabel").Text.Contains("Clipboard is busy"), "Clipboard failure keeps popup and values for retry");
                    Enter(retryDialog);
                }
                catch (Exception ex) { retryError = ex; retryDialog.Close(); }
            }));
            Check(retryDialog.ShowDialog() == true && attempts == 2, "Retry copies successfully and closes dialog");
            if (retryError is not null) throw retryError;

            library.Prompts[0].Content = "Plain prompt unchanged.";
            main.Show();
            ((Task)Invoke(main, "ActivateSelected")!).GetAwaiter().GetResult();
            Check(!main.IsVisible && Clipboard.GetText() == "Plain prompt unchanged.", "Plain prompts still copy directly and hide launcher");

            main.Show();
            var toggle = (ToggleButton)main.FindName("LanguageToggle");
            Check(toggle.IsChecked == false, "Launcher starts in English by default");
            main.UpdateLayout();
            var toggleCenter = toggle.TranslatePoint(new Point(toggle.ActualWidth / 2, 0), main);
            Check(Math.Abs(toggleCenter.X - main.ActualWidth / 2) < 1, "EN/DE knob is centered at the top of the launcher");
            toggle.IsChecked = true;
            toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(library.Settings.Language == "de" && new LibraryStore(folder).Load().Settings.Language == "de", "Knob selects German and remembers language on disk");
            SavePreview(main, "language-launcher.png");

            Clipboard.SetText("Before warning");
            RunActivation<MissingLanguageWindow>(main, warning =>
            {
                Check(Clipboard.GetText() == "Before warning" && warning.Topmost, "Missing German version warns before changing clipboard");
                SavePreview(warning, "missing-language.png");
                warning.DialogResult = false;
            });
            Check(main.IsVisible && Clipboard.GetText() == "Before warning", "Cancelling missing-language warning preserves clipboard and launcher");
            RunActivation<MissingLanguageWindow>(main, warning => GetButton(warning, "UseOtherButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            Check(!main.IsVisible && Clipboard.GetText() == "Plain prompt unchanged.", "Acknowledging warning copies the English fallback and hides launcher");

            main.Show();
            Exception? editError = null;
            RunActivation<MissingLanguageWindow>(main, warning =>
            {
                main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var editor = main.OwnedWindows.OfType<PromptEditor>().Single();
                    try
                    {
                        Check(Label(editor, "ContentLabel").Text.Contains("Deutsch") && ((TextBox)editor.FindName("ContentBox")).Text == "", "Edit now opens the empty German translation");
                        Check(((TextBox)editor.FindName("ContentBox")).IsKeyboardFocused, "Missing translation editor focuses prompt text for pasting");
                        Check(((TextBox)editor.FindName("NameBox")).Text == "Template", "Missing German body reuses the existing English title");
                        ((TextBox)editor.FindName("NameBox")).Text = "Shared template";
                        ((TextBox)editor.FindName("DescriptionBox")).Text = "Shared English description";
                        ((TextBox)editor.FindName("ContentBox")).Text = "Lies {Datei}.\n  Grüße\n";
                        GetButton(editor, "EnglishButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(((TextBox)editor.FindName("NameBox")).Text == "Shared template" && ((TextBox)editor.FindName("DescriptionBox")).Text == "Shared English description", "Language tabs retain one shared title and description");
                        Check(((TextBox)editor.FindName("ContentBox")).Text == "Plain prompt unchanged.", "Switching editor tabs preserves existing English translation");
                        GetButton(editor, "GermanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(((TextBox)editor.FindName("ContentBox")).Text == "Lies {Datei}.\n  Grüße\n", "Switching editor tabs preserves unsaved German text exactly");
                        SavePreview(editor, "german-editor.png");
                        Invoke(editor, "OnSave", editor, new RoutedEventArgs());
                    }
                    catch (Exception ex) { editError = ex; editor.Close(); }
                }));
                GetButton(warning, "EditNowButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            if (editError is not null) throw editError;
            Check(main.IsVisible && library.Prompts[0].Name == "Shared template" && library.Prompts[0].NameDe is null && library.Prompts[0].Content == "Plain prompt unchanged.", "Saving German body retains a shared title and the English body");
            Check(((LauncherItem)((ListBox)main.FindName("PromptList")).SelectedItem).Name == "Shared template", "German launcher displays the shared English title");
            RunPrompt(main, dialog =>
            {
                Check(Label(dialog, "NameLabel").Text == "{Datei}", "Selected German version opens its own placeholders without fallback warning");
                Value(dialog).Text = "notizen.txt";
                Enter(dialog);
                return Task.CompletedTask;
            });
            Check(Clipboard.GetText() == "Lies notizen.txt.\n  Grüße\n" && !main.IsVisible, "German copy uses translated template and preserves whitespace");

            library.Prompts[0].Content = "";
            main.Show();
            toggle.IsChecked = false;
            toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            RunActivation<MissingLanguageWindow>(main, warning =>
            {
                Check(GetButton(warning, "UseOtherButton").Content.ToString() == "Use German", "Missing English translation offers German fallback");
                warning.DialogResult = false;
            });

            RunEditorValidation(library.Settings.Categories);
            RunCategoryOrdering(folder);
            RunDeletionBackups(folder);
            RunNonBilingualPrompts(folder);
            Console.WriteLine($"\n{_checks} WPF integration checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (clipboard is not null) Clipboard.SetDataObject(clipboard, true);
                    else Clipboard.Clear();
                    break;
                }
                catch (System.Runtime.InteropServices.ExternalException) when (attempt < 9) { Thread.Sleep(100); }
            }
            app.Shutdown();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static void RunPrompt(MainWindow main, Func<PromptVariablesWindow, Task> interact)
    {
        Exception? error = null;
        main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            var dialog = (PromptVariablesWindow)typeof(MainWindow).GetField("_dialog", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            try
            {
                if (!dialog.IsLoaded)
                {
                    var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    dialog.Loaded += (_, _) => loaded.TrySetResult();
                    await loaded.Task;
                }
                await interact(dialog);
            }
            catch (Exception ex) { error = ex; dialog.Close(); }
        }));
        ((Task)Invoke(main, "ActivateSelected")!).GetAwaiter().GetResult();
        if (error is not null) throw error;
    }

    private static void RunActivation<T>(MainWindow main, Action<T> interact) where T : Window
    {
        Exception? error = null;
        main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = main.OwnedWindows.OfType<T>().Single();
            try { interact(dialog); }
            catch (Exception ex) { error = ex; dialog.Close(); }
        }));
        ((Task)Invoke(main, "ActivateSelected")!).GetAwaiter().GetResult();
        if (error is not null) throw error;
    }

    private static void RunEditorValidation(string[] categories)
    {
        Prompt? saved = null;
        var editor = new PromptEditor(categories, null, 0, "de") { SavePrompt = (draft, _) => saved = draft };
        Exception? error = null;
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Invoke(editor, "OnSave", editor, new RoutedEventArgs());
                Check(saved is null && editor.IsVisible && Label(editor, "ErrorLabel").Text.Length > 0, "Editor rejects saving without any translation");
                ((TextBox)editor.FindName("NameBox")).Text = "Nur Deutsch";
                ((TextBox)editor.FindName("ContentBox")).Text = "Deutscher Text";
                GetButton(editor, "EnglishButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Invoke(editor, "OnSave", editor, new RoutedEventArgs());
            }
            catch (Exception ex) { error = ex; editor.Close(); }
        }));
        Check(editor.ShowDialog() == true && saved?.Content == "" && saved?.ContentDe == "Deutscher Text", "Editor saves a German-only prompt even from the empty English tab");
        if (error is not null) throw error;
    }

    private static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.Invoke(target, args);
    private static void RunCategoryOrdering(string folder)
    {
        var settings = new Preferences { Language = "de", LastCategory = 2 };
        Preferences? saved = null;
        var window = new SettingsWindow(settings, settings.Hotkey, Path.Combine(folder, "prompts.json")) { SaveSettings = draft => saved = draft };
        window.Show();
        ((TabItem)window.FindName("CategoriesTab")).IsSelected = true;
        window.UpdateLayout();
        var list = (ListBox)window.FindName("CategoryList");
        var code = (SettingsWindow.CategoryRow)list.Items[2];
        var first = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        var data = new DataObject("QuickPrompt.CategoryRow", code);
        // Exercise the same routed drop events as a native drag, including drops onto editable text.
        var nameBox = FindVisual<TextBox>(first);
        var drag = MakeDrag(data, nameBox, new Point(4, 2), DragDrop.PreviewDragOverEvent);
        nameBox.RaiseEvent(drag);
        Check(drag.Effects == DragDropEffects.Move && first.BorderThickness.Top == 2, "Drag over a category text field accepts reordering and shows insertion line");
        nameBox.RaiseEvent(MakeDrag(data, nameBox, new Point(4, 2), DragDrop.PreviewDropEvent));
        Check(((SettingsWindow.CategoryRow)list.Items[0]).Id == 2 && nameBox.Text == "General", "Dropping Code above General reorders categories without replacing name text");
        window.UpdateLayout();
        code.Name = "Development";
        // Moving to the end exercises the downward insertion adjustment.
        var last = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(3);
        last.RaiseEvent(MakeDrag(data, last, new Point(4, last.ActualHeight - 1), DragDrop.PreviewDropEvent));
        Check(list.Items.Cast<SettingsWindow.CategoryRow>().Select(row => row.Id).SequenceEqual(new[] { 0, 1, 3, 2 }), "Dragging downward inserts after the target category");
        Invoke(window, "MoveCategory", code, 0);
        var general = (SettingsWindow.CategoryRow)list.Items[1];
        Invoke(window, "OnCategoryDown", new Button { DataContext = general }, new RoutedEventArgs());
        Check(list.Items.Cast<SettingsWindow.CategoryRow>().Select(row => row.Id).SequenceEqual(new[] { 2, 1, 0, 3 }), "Move-down button uses the same reorder behavior");
        Invoke(window, "OnCategoryUp", new Button { DataContext = general }, new RoutedEventArgs());
        Check(settings.CategoryOrder.SequenceEqual(new[] { 0, 1, 2, 3 }) && settings.Categories[2] == "Code", "Editing category order and names keeps original preferences unchanged until Save");
        SavePreview(window, "category-order-settings.png");
        window.Close();
        Check(saved is null && settings.CategoryOrder.SequenceEqual(new[] { 0, 1, 2, 3 }), "Cancelling category changes discards the draft order");

        var saveWindow = new SettingsWindow(settings, settings.Hotkey, Path.Combine(folder, "prompts.json")) { SaveSettings = draft => saved = draft };
        Exception? error = null;
        saveWindow.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var rows = (ListBox)saveWindow.FindName("CategoryList");
                var row = (SettingsWindow.CategoryRow)rows.Items[2];
                row.Name = "Development";
                Invoke(saveWindow, "MoveCategory", row, 0);
                Invoke(saveWindow, "OnSave", saveWindow, new RoutedEventArgs());
            }
            catch (Exception ex) { error = ex; saveWindow.Close(); }
        }));
        Check(saveWindow.ShowDialog() == true && saved!.CategoryOrder.SequenceEqual(new[] { 2, 0, 1, 3 }) && saved.Categories[2] == "Development", "Save stores order and renamed text against stable category IDs");
        if (error is not null) throw error;
        Check(saved!.Language == "de" && saved.LastCategory == 2, "Saving category settings retains language and last selected category");

        var library = Library.CreateDefault();
        library.Settings = saved;
        var main = new MainWindow(new LibraryStore(Path.Combine(folder, "order")), library);
        main.Show();
        var tabs = (System.Windows.Controls.Primitives.UniformGrid)main.FindName("CategoryTabs");
        Check(tabs.Children.Cast<Button>().Select(button => button.Content.ToString()).SequenceEqual(new[] { "Development", "General", "Writing", "Research", "Documents" }), "Launcher renders saved category order with Documents last");
        Check(((ListBox)main.FindName("PromptList")).Items.Cast<LauncherItem>().All(item => item.Prompt?.Category == 2), "Remembered Code category retains its own prompts after reordering");
        Invoke(main, "MoveCategorySelection", 1);
        Check((int)typeof(MainWindow).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)! == 0, "Next-category navigation follows visible order");
        Invoke(main, "MoveCategorySelection", -1);
        Invoke(main, "MoveCategorySelection", -1);
        Check((int)typeof(MainWindow).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)! == 4, "Previous-category navigation wraps from first category to Documents");
        Invoke(main, "SwitchCategoryPosition", 0);
        Check((int)typeof(MainWindow).GetField("_category", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)! == 2, "Position shortcuts select the category at the configured position");
        main.Hide();

        Prompt? edited = null;
        var editor = new PromptEditor(saved.Categories, library.Prompts.First(p => p.Category == 2), 0, "en", false, saved.CategoryOrder) { SavePrompt = (draft, _) => edited = draft };
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var choices = (ComboBox)editor.FindName("CategoryBox");
                Check(choices.SelectedIndex == 0 && (int)choices.SelectedValue == 2, "Prompt editor shows ordered categories while selecting existing category identity");
                choices.SelectedIndex = 2;
                Invoke(editor, "OnSave", editor, new RoutedEventArgs());
            }
            catch (Exception ex) { error = ex; editor.Close(); }
        }));
        Check(editor.ShowDialog() == true && edited?.Category == 1, "Prompt editor saves selected category identity instead of its display position");
        if (error is not null) throw error;
    }
    private static void RunDeletionBackups(string folder)
    {
        var store = new LibraryStore(Path.Combine(folder, "deletion"));
        var library = new Library { Prompts = [new Prompt { Name = "Delete me", Content = "English original", NameDe = "Löschen", ContentDe = "Deutsches Original" }] };
        store.Save(library);
        var main = new MainWindow(store, library);
        main.Show();
        Exception? error = null;
        main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var editor = main.OwnedWindows.OfType<PromptEditor>().Single();
            try
            {
                editor.SavePrompt!(library.Prompts[0], true);
                editor.DialogResult = true;
            }
            catch (Exception ex) { error = ex; editor.Close(); }
        }));
        Invoke(main, "EditPrompt", library.Prompts[0], false);
        if (error is not null) throw error;
        Check(library.Prompts.Count == 0 && store.Load().Prompts.Count == 0 && Directory.GetFiles(store.BackupDirectory, "*.txt").Length == 1, "Launcher deletion saves the removed prompt in permanent backups before persisting deletion");
        Check(File.ReadAllText(Directory.GetFiles(store.BackupDirectory, "*.txt").Single()).Contains("Deutsches Original"), "Deletion through the editor preserves the other language too");
        main.Hide();

        var blockedStore = new LibraryStore(Path.Combine(folder, "blocked-deletion"));
        var blocked = new Library { Prompts = [new Prompt { Name = "Keep me", Content = "Original" }] };
        blockedStore.Save(blocked);
        File.WriteAllText(blockedStore.BackupDirectory, "Blocked folder");
        var blockedMain = new MainWindow(blockedStore, blocked);
        blockedMain.Show();
        var original = blocked.Prompts[0];
        blockedMain.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var editor = blockedMain.OwnedWindows.OfType<PromptEditor>().Single();
            try
            {
                bool failed = false;
                try { editor.SavePrompt!(original, true); } catch (IOException) { failed = true; }
                Check(failed && blocked.Prompts.Single() == original && blockedStore.Load().Prompts.Count == 1, "Failed backup restores the in-memory prompt and preserves the saved prompt");
                editor.DialogResult = false;
            }
            catch (Exception ex) { error = ex; editor.Close(); }
        }));
        Invoke(blockedMain, "EditPrompt", original, false);
        if (error is not null) throw error;
        blockedMain.Hide();

        var settings = new SettingsWindow(new Preferences(), "Alt+Space", store.FilePath);
        settings.Show();
        ((TabItem)settings.FindName("AboutTab")).IsSelected = true;
        settings.UpdateLayout();
        var button = GetButton(settings, "OpenBackupsButton");
        Check(button.IsVisible && button.ToolTip.ToString() == store.BackupDirectory, "About settings exposes the correct backup folder");
        var scroll = (ScrollViewer)settings.FindName("AboutScroll");
        var help = Label(settings, "BackupHelp");
        var position = help.TranslatePoint(new Point(0, help.ActualHeight), scroll);
        Check(position.Y <= scroll.ViewportHeight, "Open backups and its explanation fit in the visible About tab");
        SavePreview(settings, "backup-settings.png");
        settings.Close();
    }
    private static void RunNonBilingualPrompts(string folder)
    {
        var prompt = new Prompt { Name = "Shared English title", Description = "Shared English description", Content = "Use English everywhere.", NonBilingual = true };
        var library = new Library { Prompts = [prompt], Settings = new Preferences { Language = "de" } };
        var store = new LibraryStore(Path.Combine(folder, "non-bilingual"));
        store.Save(library);
        var main = new MainWindow(store, library);
        main.Show();
        ((Task)Invoke(main, "ActivateSelected")!).GetAwaiter().GetResult();
        Check(!main.IsVisible && Clipboard.GetText() == prompt.Content, "English non-bilingual prompt copies in German mode without a warning");
        prompt.Content = ""; prompt.ContentDe = "Lies {Datei}.";
        library.Settings.Language = "en";
        main.Show();
        RunPrompt(main, dialog =>
        {
            Check(Label(dialog, "NameLabel").Text == "{Datei}", "German non-bilingual prompt goes directly to its placeholders in English mode");
            Value(dialog).Text = "notizen.txt";
            Enter(dialog);
            return Task.CompletedTask;
        });
        Check(Clipboard.GetText() == "Lies notizen.txt." && !main.IsVisible, "Non-bilingual fallback still fills German placeholders and copies");

        Prompt? saved = null;
        var original = new Prompt { Name = "One title", Description = "One description", Content = "English body", ContentDe = "Deutscher Inhalt" };
        var editor = new PromptEditor(library.Settings.Categories, original, 0, "de") { SavePrompt = (draft, _) => saved = draft };
        Exception? error = null;
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var checkbox = (CheckBox)editor.FindName("NonBilingualBox");
                Check(checkbox.IsChecked == false, "Non-bilingual checkbox starts off for ordinary prompts");
                checkbox.IsChecked = true;
                Check(!GetButton(editor, "EnglishButton").IsEnabled && GetButton(editor, "GermanButton").IsEnabled, "Non-bilingual mode keeps the chosen version and disables the other language");
                checkbox.IsChecked = false;
                GetButton(editor, "EnglishButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((TextBox)editor.FindName("ContentBox")).Text == "English body", "Unchecking before saving restores the other unsaved version");
                GetButton(editor, "GermanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                checkbox.IsChecked = true;
                SavePreview(editor, "non-bilingual-editor.png");
                Invoke(editor, "OnSave", editor, new RoutedEventArgs());
            }
            catch (Exception ex) { error = ex; editor.Close(); }
        }));
        Check(editor.ShowDialog() == true && saved!.NonBilingual && saved.Content == "" && saved.ContentDe == "Deutscher Inhalt" && saved.Name == "One title", "Saving non-bilingual keeps exactly the selected body and shared English labels");
        if (error is not null) throw error;
        Check(original.Content == "English body" && !original.NonBilingual, "Editor mode changes do not mutate the original prompt");

        var englishOnly = new Prompt { Name = "English title", Description = "English description", Content = "English only" };
        var fallbackEditor = new PromptEditor(library.Settings.Categories, englishOnly, 0, "de");
        fallbackEditor.Show();
        ((CheckBox)fallbackEditor.FindName("NonBilingualBox")).IsChecked = true;
        Check(((TextBox)fallbackEditor.FindName("ContentBox")).Text == "English only" && Label(fallbackEditor, "ContentLabel").Text.Contains("English"), "Checking from an empty translation selects the existing version");
        fallbackEditor.Close();
        Check(!englishOnly.NonBilingual && englishOnly.ContentDe == "", "Cancelling a non-bilingual edit preserves the original mode and text");

        var newEditor = new PromptEditor(library.Settings.Categories, null, 0);
        Check(((CheckBox)newEditor.FindName("NonBilingualBox")).IsChecked == false, "New prompts default to bilingual-capable mode");
        newEditor.Close();
    }
    private static DragEventArgs MakeDrag(IDataObject data, DependencyObject target, Point point, RoutedEvent routedEvent)
    {
        var args = (DragEventArgs)typeof(DragEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single()
            .Invoke(new object[] { data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, point });
        args.RoutedEvent = routedEvent;
        return args;
    }
    private static T FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            try { return FindVisual<T>(child); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("Visual child not found: " + typeof(T).Name);
    }
    private static TextBox Value(Window dialog) => (TextBox)dialog.FindName("ValueBox");
    private static TextBlock Label(Window dialog, string name) => (TextBlock)dialog.FindName(name);
    private static Button GetButton(Window dialog, string name) => (Button)dialog.FindName(name);
    private static void Enter(Window dialog)
    {
        Value(dialog).Focus();
        PressKey(dialog, Key.Enter);
    }
    private static void PressKey(Window dialog, Key key) =>
        dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
        _checks++;
    }

    private static void SavePreview(Window dialog, string fileName = "placeholder-popup.png")
    {
        dialog.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(dialog.ActualWidth), (int)Math.Ceiling(dialog.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(dialog);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(AppContext.BaseDirectory, fileName));
        encoder.Save(output);
    }
}
