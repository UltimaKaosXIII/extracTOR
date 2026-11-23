using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace extracTOR
{
    // Minimal stand-in for WPF's MessageBox, which Avalonia doesn't provide.
    public static class MessageDialog
    {
        public static async Task Show(Window owner, string message, string title) =>
            await ShowCore(owner, message, title, yesNo: false);

        public static Task<bool> ShowYesNo(Window owner, string message, string title) =>
            ShowCore(owner, message, title, yesNo: true);

        private static async Task<bool> ShowCore(Window owner, string message, string title, bool yesNo)
        {
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 280,
                MaxWidth = 520,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#333333")),
                Icon = owner.Icon,
            };

            var chosen = false;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };

            Button MakeButton(string text, bool result, bool isDefault)
            {
                var b = new Button { Content = text, MinWidth = 70, IsDefault = isDefault, IsCancel = !isDefault || !yesNo,
                                     HorizontalContentAlignment = HorizontalAlignment.Center };
                b.Click += (_, _) => { chosen = result; dialog.Close(result); };
                return b;
            }

            if (yesNo)
            {
                buttons.Children.Add(MakeButton("Yes", true, isDefault: true));
                buttons.Children.Add(MakeButton("No", false, isDefault: false));
            }
            else
            {
                buttons.Children.Add(MakeButton("OK", true, isDefault: true));
            }

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White },
                    buttons,
                }
            };

            // ShowDialog requires a visible owner; fall back to a plain window if there isn't one yet.
            if (owner.IsVisible)
            {
                await dialog.ShowDialog<bool>(owner);
                return chosen;
            }

            var tcs = new TaskCompletionSource<bool>();
            dialog.Closed += (_, _) => tcs.TrySetResult(chosen);
            dialog.Show();
            return await tcs.Task;
        }
    }
}
