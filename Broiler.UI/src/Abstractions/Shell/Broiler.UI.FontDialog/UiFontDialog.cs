using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.FontDialog;

public abstract class UiFontDialog : UiDialog
{
    private static readonly string[] BuiltInFamilies =
    [
        "sans-serif",
        "serif",
        "monospace",
        "Segoe UI",
        "Arial",
        "Calibri",
        "Times New Roman",
        "Georgia",
        "Verdana",
        "Consolas",
        "Courier New",
        "Noto Sans",
        "Noto Serif",
        "DejaVu Sans",
        "DejaVu Serif",
        "Liberation Sans",
        "Liberation Serif",
    ];

    private string[] _fontFamilies = BuiltInFamilies;
    private BFontStyle _selectedFont = BFontStyle.Default;
    private string _sampleText = "The quick brown fox jumps over the lazy dog";

    public event EventHandler? SelectedFontChanged;

    public IReadOnlyList<string> FontFamilies => _fontFamilies;

    public BFontStyle SelectedFont
    {
        get => _selectedFont;
        set
        {
            ThrowIfDisposed();
            BFontStyle normalized = NormalizeFont(value);
            if (_selectedFont == normalized)
                return;

            _selectedFont = normalized;
            bool familiesChanged = EnsureSelectedFamilyIsListed();
            if (familiesChanged)
                OnFontFamiliesChanged();
            OnSelectedFontChanged();
            SelectedFontChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string SampleText
    {
        get => _sampleText;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_sampleText, value))
                return;

            _sampleText = value;
            OnSampleTextChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public void SetFontFamilies(IEnumerable<string>? families)
    {
        ThrowIfDisposed();
        string[] normalized = NormalizeFamilies(families).ToArray();
        if (normalized.Length == 0)
            normalized = BuiltInFamilies;

        _fontFamilies = normalized;
        EnsureSelectedFamilyIsListed();
        OnFontFamiliesChanged();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool AcceptSelection() => Accept(FormatFontValue(SelectedFont));

    public Task<UiDialogResult> ShowFontModal(UiWindow owner, BRect placement = default) =>
        ShowModal(owner, placement);

    public Task<UiDialogResult> ShowFontModeless(UiWindow owner, BRect placement = default) =>
        ShowModeless(owner, placement);

    public static string FormatFontValue(BFontStyle font)
    {
        font = NormalizeFont(font);
        return string.Join(
            "|",
            Escape(font.FamilyName),
            font.SizeInPixels.ToString("0.###", CultureInfo.InvariantCulture),
            ((int)font.Weight).ToString(CultureInfo.InvariantCulture),
            font.Slant.ToString());
    }

    public static bool TryParseFontValue(string? value, out BFontStyle font)
    {
        font = BFontStyle.Default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] parts = SplitEscaped(value).ToArray();
        if (parts.Length != 4 ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double size) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) ||
            !Enum.TryParse(parts[3], ignoreCase: true, out BFontSlant slant))
        {
            return false;
        }

        font = NormalizeFont(new BFontStyle(Unescape(parts[0]), size, (BFontWeight)weight, slant));
        return true;
    }

    protected virtual void OnFontFamiliesChanged()
    {
    }

    protected virtual void OnSelectedFontChanged()
    {
    }

    protected virtual void OnSampleTextChanged()
    {
    }

    private bool EnsureSelectedFamilyIsListed()
    {
        if (_fontFamilies.Any(family => string.Equals(family, _selectedFont.FamilyName, StringComparison.OrdinalIgnoreCase)))
            return false;

        _fontFamilies = NormalizeFamilies(_fontFamilies.Append(_selectedFont.FamilyName)).ToArray();
        return true;
    }

    private static BFontStyle NormalizeFont(BFontStyle? font)
    {
        font ??= BFontStyle.Default;
        string family = string.IsNullOrWhiteSpace(font.FamilyName)
            ? BFontStyle.Default.FamilyName
            : font.FamilyName.Trim();
        double size = double.IsNaN(font.SizeInPixels) || double.IsInfinity(font.SizeInPixels) || font.SizeInPixels <= 0
            ? BFontStyle.Default.SizeInPixels
            : Math.Clamp(font.SizeInPixels, 1.0, 512.0);
        BFontSlant slant = Enum.IsDefined(font.Slant) ? font.Slant : BFontSlant.Normal;
        return new BFontStyle(family, size, font.Weight, slant);
    }

    private static IEnumerable<string> NormalizeFamilies(IEnumerable<string>? families)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? family in families ?? BuiltInFamilies)
        {
            string normalized = family?.Trim() ?? string.Empty;
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;

            yield return normalized;
        }
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);

    private static string Unescape(string value)
    {
        if (!value.Contains('\\', StringComparison.Ordinal))
            return value;

        var chars = new List<char>(value.Length);
        bool escaping = false;
        foreach (char character in value)
        {
            if (escaping)
            {
                chars.Add(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            chars.Add(character);
        }

        if (escaping)
            chars.Add('\\');
        return new string([.. chars]);
    }

    private static IEnumerable<string> SplitEscaped(string value)
    {
        var chars = new List<char>(value.Length);
        bool escaping = false;
        foreach (char character in value)
        {
            if (escaping)
            {
                chars.Add('\\');
                chars.Add(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            if (character == '|')
            {
                yield return new string([.. chars]);
                chars.Clear();
                continue;
            }

            chars.Add(character);
        }

        if (escaping)
            chars.Add('\\');
        yield return new string([.. chars]);
    }
}
