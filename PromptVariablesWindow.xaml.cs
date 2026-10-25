using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace QuickPrompt;

public partial class PromptVariablesWindow : Window
{
    private readonly string _content;
    private readonly IReadOnlyList<string> _names;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private int _index;
    private bool _submitting;

    public Func<string, Task<bool>> CopyPrompt { get; set; } = null!;

    public PromptVariablesWindow(string content)
    {
        _content = content;
        _names = PromptTemplate.GetNames(content);
        if (_names.Count == 0) throw new ArgumentException("The prompt has no placeholders.", nameof(content));
        InitializeComponent();
        ShowCurrent();
        Loaded += (_, _) => ValueBox.Focus();
        Closing += (_, e) => { if (_submitting) e.Cancel = true; };
    }

    private void ShowCurrent()
    {
        string name = _names[_index];
        ProgressLabel.Text = $"Value {_index + 1} of {_names.Count}";
        NameLabel.Text = "{" + name + "}";
        AutomationProperties.SetName(ValueBox, name);
        ValueBox.Clear();
        ErrorLabel.Text = "";
        ValueBox.Focus();
        ValueBox.CaretIndex = ValueBox.Text.Length;
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (!_submitting) DialogResult = false;
            return;
        }
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        await Next();
    }

    private async Task Next()
    {
        if (_submitting) return;
        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ErrorLabel.Text = "Enter a value for {" + _names[_index] + "}.";
            ValueBox.Focus();
            return;
        }
        _values[_names[_index]] = ValueBox.Text;
        if (_index < _names.Count - 1) { _index++; ShowCurrent(); return; }

        _submitting = true;
        ValueBox.IsReadOnly = true;
        bool copied;
        try { copied = await CopyPrompt(PromptTemplate.Fill(_content, _values)); }
        finally
        {
            _submitting = false;
            ValueBox.IsReadOnly = false;
        }
        if (copied) DialogResult = true;
        else
        {
            ErrorLabel.Text = "Clipboard is busy. Press Enter to try again.";
            ValueBox.Focus();
        }
    }
}
