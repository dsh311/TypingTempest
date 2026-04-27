using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TypingTempest.Controls.GameBoardView
{
    public static class AvalonEditHelper
    {
        public static readonly DependencyProperty BindableTextProperty =
        DependencyProperty.RegisterAttached(
            "BindableText",
            typeof(string),
            typeof(AvalonEditHelper),
            new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindableTextChanged));

        public static void SetBindableText(DependencyObject element, string value)
            => element.SetValue(BindableTextProperty, value);

        public static string GetBindableText(DependencyObject element)
            => (string)element.GetValue(BindableTextProperty);

        private static void OnBindableTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ICSharpCode.AvalonEdit.TextEditor editor)
            {
                editor.TextChanged -= Editor_TextChanged;
                editor.Text = e.NewValue?.ToString() ?? string.Empty;
                editor.TextChanged += Editor_TextChanged;
            }
        }

        private static void Editor_TextChanged(object sender, EventArgs e)
        {
            if (sender is ICSharpCode.AvalonEdit.TextEditor editor)
            {
                SetBindableText(editor, editor.Text);
            }
        }
    }
}
