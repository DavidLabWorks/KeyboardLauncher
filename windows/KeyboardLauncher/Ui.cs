using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KeyboardLauncher;

internal static class Ui
{
    internal static Image AppIcon(double size) => new() { Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png"))), Width = size, Height = size };
    internal static void GlassButton(Button button, bool bound)
    {
        void Apply()
        {
            bool dark = button.ActualTheme == ElementTheme.Dark;
            var baseColor = dark ? Windows.UI.Color.FromArgb(184, 112, 112, 112) : Windows.UI.Color.FromArgb(255, 255, 255, 255);
            var hoverColor = dark ? Windows.UI.Color.FromArgb(204, 124, 124, 124) : Microsoft.UI.Colors.White;
            var borderColor = dark ? Windows.UI.Color.FromArgb(31, 255, 255, 255) : Windows.UI.Color.FromArgb(31, 0, 0, 0);
            button.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(baseColor);
            button.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
            button.BorderThickness = new Thickness(.75);
            button.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark ? Microsoft.UI.Colors.White : Windows.UI.Color.FromArgb(255, 35, 35, 35));
            button.Resources["ButtonBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(hoverColor);
            button.Resources["ButtonBackgroundPressed"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(baseColor);
            button.Resources["ButtonBorderBrushPointerOver"] = Application.Current.Resources["SystemControlHighlightAccentBrush"];
        }
        button.ActualThemeChanged += (_, _) => Apply(); Apply();
    }
    internal static Button UnbindButton()
    {
        var mark = new Grid { Width = 9, Height = 9 };
        var first = new Microsoft.UI.Xaml.Shapes.Line { X1 = 1, Y1 = 1, X2 = 8, Y2 = 8, StrokeThickness = 1.5 };
        var second = new Microsoft.UI.Xaml.Shapes.Line { X1 = 8, Y1 = 1, X2 = 1, Y2 = 8, StrokeThickness = 1.5 };
        mark.Children.Add(first); mark.Children.Add(second);
        var button = new PointerButton
        {
            Content = mark, Width = 18, Height = 18, MinWidth = 0, MinHeight = 0,
            Padding = new Thickness(0), CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(.75),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -6, -6, 0), Visibility = Visibility.Collapsed,
            // The keycap sits at Z=8. Keep the remove control above it, not behind it.
            Translation = new System.Numerics.Vector3(0, 0, 9), Shadow = new Microsoft.UI.Xaml.Media.ThemeShadow()
        };
        void Paint()
        {
            bool dark = button.ActualTheme == ElementTheme.Dark;
            var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 56, 56, 56) : Microsoft.UI.Colors.White);
            var foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark ? Microsoft.UI.Colors.White : Windows.UI.Color.FromArgb(255, 35, 35, 35));
            var border = new Microsoft.UI.Xaml.Media.SolidColorBrush(dark ? Windows.UI.Color.FromArgb(102, 255, 255, 255) : Windows.UI.Color.FromArgb(31, 0, 0, 0));
            button.Background = background; button.Foreground = foreground; button.BorderBrush = border;
            first.Stroke = second.Stroke = foreground;
            foreach (var state in new[] { "PointerOver", "Pressed" })
            {
                button.Resources["ButtonBackground" + state] = background;
                button.Resources["ButtonBorderBrush" + state] = border;
                button.Resources["ButtonForeground" + state] = foreground;
            }
        }
        button.ActualThemeChanged += (_, _) => Paint(); Paint();
        return button;
    }
    internal static ElementTheme Theme(string theme) => theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
    internal static FrameworkElement KeyIcon(Launcher item, double keyWidth)
    {
        var icon = Icon(item);
        // Windows artwork often fills its bitmap, unlike the inset mac app assets.
        // Reserve 17% per side for app icons; keep template symbols at 24%.
        var isTemplate = icon is TextBlock text && !text.Text.EnumerateRunes().Any(r => r.Value >= 0x1F000 || r.Value == 0xFE0F);
        var padding = keyWidth * (isTemplate ? .24 : .17);
        var size = keyWidth - padding * 2;
        if (icon is Image picture)
        {
            picture.Width = size; picture.Height = size;
            picture.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;
            picture.HorizontalAlignment = HorizontalAlignment.Center;
            picture.VerticalAlignment = VerticalAlignment.Center;
            return picture;
        }
        return new Viewbox
        {
            Width = size, Height = size, Child = icon,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
    internal static UIElement Icon(Launcher? item)
    {
        if (item?.Icon is string path && File.Exists(path))
        {
            try { return new Image { Source = new BitmapImage(new Uri(Path.GetFullPath(path))), Width = 24, Height = 24 }; }
            catch { /* Invalid image paths use a text fallback. */ }
        }
        if (item != null && item.ActionType == "application" && string.IsNullOrWhiteSpace(item.KeyboardShortcut))
        {
            var target = Environment.ExpandEnvironmentVariables(item.Exec);
            if (!Path.IsPathRooted(target))
                target = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Select(p => Path.Combine(p, target)).FirstOrDefault(File.Exists) ?? target;
            if (File.Exists(target) || Directory.Exists(target))
            {
                var picture = new Image { Width = 48, Height = 48 };
                _ = LoadApplicationIcon(picture, target);
                return picture;
            }
        }
        return new TextBlock { Text = item == null ? "+" : !string.IsNullOrWhiteSpace(item.Icon) && item.Icon.Length <= 8 ? item.Icon : item.KeyboardShortcut != null ? "⌨" : item.ActionType == "url" ? "🌐" : "▣", FontSize = 23, HorizontalAlignment = HorizontalAlignment.Center };
    }
    private static async Task LoadApplicationIcon(Image image, string path)
    {
        try
        {
            var shellIcon = await ApplicationIcons.Load(Path.GetFullPath(path));
            if (shellIcon != null) { image.Source = shellIcon; return; }
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
            using var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 96, Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);
            if (thumbnail != null) { var source = new BitmapImage(); await source.SetSourceAsync(thumbnail); image.Source = source; }
            else image.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png")));
        }
        catch { image.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png"))); }
    }
    internal static async Task Error(XamlRoot root, string message)
    {
        await new ContentDialog { XamlRoot = root, Title = L.T("操作未完成"), Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, CloseButtonText = L.T("确定") }.ShowAsync();
    }
    internal static TextBox Field(string label, string value = "", string hint = "") => new() { Header = label, Text = value, PlaceholderText = hint, HorizontalAlignment = HorizontalAlignment.Stretch };
}
