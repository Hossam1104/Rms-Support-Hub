using Microsoft.UI.Xaml;

namespace PosAdminTool.WinUI.Services;

public sealed class ThemeService
{
    private FrameworkElement? _root;

    public event EventHandler<ElementTheme>? ThemeChanged;

    public ElementTheme CurrentTheme => _root?.RequestedTheme ?? ElementTheme.Default;

    public void Initialize(FrameworkElement root)
    {
        _root = root;
    }

    public void Apply(string themeSetting)
    {
        if (_root is null)
        {
            return;
        }

        var theme = string.Equals(themeSetting, "DARK", StringComparison.OrdinalIgnoreCase)
            ? ElementTheme.Dark
            : ElementTheme.Light;

        _root.RequestedTheme = theme;
        ThemeChanged?.Invoke(this, theme);
    }
}
