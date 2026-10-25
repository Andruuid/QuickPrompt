using System.Windows;

namespace QuickPrompt;

public partial class MissingLanguageWindow : Window
{
    public bool EditRequested { get; private set; }

    public MissingLanguageWindow(Prompt prompt, string language)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        string selected = language == "de" ? "German" : "English";
        string other = language == "de" ? "English" : "German";
        Explanation.Text = $"“{prompt.GetName(language)}” is not available in {selected}. Use the {other} version this time, or choose Edit now to add the {selected} translation.";
        UseOtherButton.Content = "Use " + other;
    }

    private void OnEditNow(object sender, RoutedEventArgs e) { EditRequested = true; DialogResult = false; }
    private void OnUseOther(object sender, RoutedEventArgs e) => DialogResult = true;
}
