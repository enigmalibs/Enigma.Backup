using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;

namespace Enigma.Backup.Desktop.Services;

/// <summary>The glyphs and brushes the dialog services set on a <c>ContentDialog</c> from code.</summary>
internal static class ThemeAssets
{
    /// <summary>One Phosphor glyph, as the geometry a dialog header draws.</summary>
    /// <param name="icon">Which glyph.</param>
    public static Geometry Glyph(PhosphorIcon icon) =>
        PhosphorIconSet.Instance.GetGlyph(icon, PhosphorWeight.Regular).ToGeometry();

    /// <summary>One of the theme's brushes, by key.</summary>
    /// <param name="key">The <c>Enigma*</c> brush key.</param>
    /// <returns>The brush, or <see langword="null"/> — which leaves the dialog its own default.</returns>
    /// <remarks>
    /// A dynamic resource cannot be assigned from here, but it does not need to be: the theme's brushes
    /// are not theme-scoped, only their colours are, so this one instance keeps following a variant
    /// switch. There is no <c>Application</c> at all in a headless test, hence the null.
    /// </remarks>
    public static IBrush? Brush(string key) =>
        Application.Current?.TryFindResource(key, out object? value) == true ? value as IBrush : null;
}
