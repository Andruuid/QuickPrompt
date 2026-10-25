using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;

namespace QuickPrompt;

public partial class PromptEditor : Window
{
    private readonly Prompt? _original;
    private readonly Prompt _draft;
    private string _language;
    private bool _ready;
    public Action<Prompt, bool>? SavePrompt { get; set; }
    public Guid? SavedId { get; private set; }
    public PromptEditor(string[] categories, Prompt? original, int category, string language = "en", bool focusContent = false, int[]? categoryOrder = null)
    {
        _draft = new Prompt
        {
            Id = original?.Id ?? Guid.NewGuid(), Name = original?.GetName("en") ?? "", Description = original?.GetDescription("en") ?? "", Content = original?.Content ?? "",
            ContentDe = original?.ContentDe ?? "", NonBilingual = original?.NonBilingual ?? false
        };
        _language = language == "de" ? "de" : "en";
        InitializeComponent();
        _original = original;
        CategoryBox.ItemsSource = (categoryOrder ?? [0, 1, 2, 3]).Select(id => new KeyValuePair<int, string>(id, categories[id])).ToArray();
        CategoryBox.DisplayMemberPath = "Value";
        CategoryBox.SelectedValuePath = "Key";
        CategoryBox.SelectedValue = original?.Category ?? category;
        NameBox.Text = _draft.Name;
        DescriptionBox.Text = _draft.Description;
        NonBilingualBox.IsChecked = _draft.NonBilingual;
        if (_draft.NonBilingual) _language = _draft.AvailableLanguage(_language);
        ShowTranslation();
        _ready = true;
        DeleteButton.Visibility = original is null ? Visibility.Collapsed : Visibility.Visible;
        if (original is not null) { Title = "Edit prompt — QuickPrompt"; Heading.Text = "Make it your own."; }
        Loaded += (_, _) => { if (focusContent) ContentBox.Focus(); else NameBox.Focus(); };
    }
    private void StoreTranslation()
    {
        _draft.Name = NameBox.Text.Trim();
        _draft.Description = DescriptionBox.Text.Trim();
        if (_language == "de") _draft.ContentDe = ContentBox.Text;
        else _draft.Content = ContentBox.Text;
    }
    private void ShowTranslation()
    {
        ContentBox.Text = _draft.GetContent(_language);
        ContentLabel.Text = _language == "de" ? "Prompt · Deutsch" : "Prompt · English";
        EnglishButton.Background = (System.Windows.Media.Brush)FindResource(_language == "en" ? "Accent" : "Surface");
        EnglishButton.Foreground = (System.Windows.Media.Brush)FindResource(_language == "en" ? "Panel" : "Text");
        GermanButton.Background = (System.Windows.Media.Brush)FindResource(_language == "de" ? "Accent" : "Surface");
        GermanButton.Foreground = (System.Windows.Media.Brush)FindResource(_language == "de" ? "Panel" : "Text");
        TranslationStatus.Text = string.IsNullOrWhiteSpace(ContentBox.Text) ? "Translation not added" : "Translation added";
        bool single = NonBilingualBox.IsChecked == true;
        EnglishButton.IsEnabled = !single || _language == "en";
        GermanButton.IsEnabled = !single || _language == "de";
        LanguageModeHelp.Text = single
            ? "Uses the selected version without a language warning. Saving keeps only this version; older text stays in backups."
            : "Only the prompt text changes between EN and DE. The title and description stay the same.";
    }
    private void OnLanguageModeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        StoreTranslation();
        if (NonBilingualBox.IsChecked == true && !_draft.HasLanguage(_language) && (_draft.HasLanguage("en") || _draft.HasLanguage("de")))
            _language = _draft.AvailableLanguage(_language);
        ShowTranslation();
    }
    private void OnLanguage(object sender, RoutedEventArgs e)
    {
        if (NonBilingualBox.IsChecked == true && (string)((Button)sender).Tag != _language) return;
        StoreTranslation();
        _language = (string)((Button)sender).Tag;
        ShowTranslation();
        ErrorLabel.Text = "";
        ContentBox.Focus();
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { e.Handled = true; Save(); }
    }
    private void OnContentChanged(object sender, TextChangedEventArgs e) =>
        TranslationStatus.Text = string.IsNullOrWhiteSpace(ContentBox.Text) ? "Translation not added" : "Translation added";
    private void OnSave(object sender, RoutedEventArgs e) => Save();
    private void Save()
    {
        StoreTranslation();
        if (string.IsNullOrWhiteSpace(_draft.Name))
        { ErrorLabel.Text = "Give your prompt a shared English title."; NameBox.Focus(); return; }
        if (!_draft.HasLanguage("en") && !_draft.HasLanguage("de"))
        { ErrorLabel.Text = "Add prompt text in English or German before saving."; ContentBox.Focus(); return; }
        bool single = NonBilingualBox.IsChecked == true;
        if (single && !_draft.HasLanguage(_language))
        { ErrorLabel.Text = "Add the prompt text for this non-bilingual prompt."; ContentBox.Focus(); return; }
        var prompt = new Prompt
        {
            Id = _draft.Id, Category = (int)CategoryBox.SelectedValue, Name = _draft.Name, Description = _draft.Description,
            Content = single && _language == "de" ? "" : _draft.Content,
            ContentDe = single && _language == "en" ? "" : _draft.ContentDe,
            NonBilingual = single
        };
        try { SavePrompt!(prompt, false); SavedId = prompt.Id; DialogResult = true; }
        catch (Exception ex) { ErrorLabel.Text = "Couldn't save your prompt: " + ex.Message; }
    }
    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_original is null || MessageBox.Show(this, $"Delete “{_original.GetName(_language)}” in both languages?", "Delete prompt", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { SavePrompt!(_original, true); DialogResult = true; }
        catch (Exception ex) { ErrorLabel.Text = "Couldn't delete your prompt: " + ex.Message; }
    }
}
