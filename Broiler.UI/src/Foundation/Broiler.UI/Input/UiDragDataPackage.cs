using System.Collections.Generic;

namespace Broiler.UI;

public sealed record UiDragDataPackage(
    string? Text = null,
    IReadOnlyDictionary<string, string>? StringData = null);
