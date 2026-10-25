using System.Windows;

namespace QuickPrompt;

public partial class DeleteCategoryWindow : Window
{
    public DeleteCategoryWindow(string name, int promptCount)
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
        Explanation.Text = $"“{name}” contains {promptCount} prompt{(promptCount == 1 ? "" : "s")}. The category and all of these prompts will be removed when you save settings.";
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
