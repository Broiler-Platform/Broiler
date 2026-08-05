using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.Code.Core.Hosting;

/// <summary>How a host provides one of the services the Code claim depends on.</summary>
public enum HostServiceQuality
{
    /// <summary>Not provided. The feature is unavailable and says so.</summary>
    Unavailable = 0,

    /// <summary>
    /// Provided by a stand-in that behaves plausibly without doing the real
    /// thing — an in-memory clipboard, an inline dispatcher. Acceptable in a
    /// test, never in a support claim.
    /// </summary>
    Substitute,

    /// <summary>The real platform service.</summary>
    Native,
}

public sealed record HostService(string Name, HostServiceQuality Quality, string Detail);

/// <summary>
/// What a desktop head actually provides, as a value rather than a comment.
///
/// The roadmap forbids carrying Writer's immediate dispatcher, in-memory
/// clipboard fallback, or legacy input adapter into the Code support claim. A
/// prose rule is not enforceable, so each head declares what it has and a test
/// asserts the claim. A substitute is allowed to exist — it just cannot be
/// reported as support.
/// </summary>
public sealed record HostServiceReport(string HostName, IReadOnlyList<HostService> Services)
{
    public const string Dispatcher = "ui-thread dispatcher";
    public const string InputRouting = "input routing";
    public const string Clipboard = "clipboard";
    public const string TextInput = "text input and IME";

    /// <summary>
    /// Asking the user for a file. A head without it cannot run Open or Save
    /// As, so it is part of the claim rather than an implementation detail.
    /// </summary>
    public const string FileDialogs = "file dialogs";

    /// <summary>
    /// The services that are the real platform ones. Only these may appear in a
    /// support statement.
    /// </summary>
    public IEnumerable<HostService> Supported =>
        Services.Where(service => service.Quality == HostServiceQuality.Native);

    /// <summary>
    /// Anything not native. A head with entries here is not making a full
    /// support claim, and the reasons are the text the user is shown.
    /// </summary>
    public IEnumerable<HostService> NotSupported =>
        Services.Where(service => service.Quality != HostServiceQuality.Native);

    public HostServiceQuality QualityOf(string name) =>
        Services.FirstOrDefault(service =>
            string.Equals(service.Name, name, StringComparison.Ordinal))?.Quality
        ?? HostServiceQuality.Unavailable;

    /// <summary>
    /// True when nothing is a substitute. Unavailable is honest; a substitute
    /// reported as support is not, which is why they are distinguished.
    /// </summary>
    public bool HasNoSubstitutes =>
        Services.All(service => service.Quality != HostServiceQuality.Substitute);

    public string Describe()
    {
        IEnumerable<string> lines = Services.Select(service => service.Quality switch
        {
            HostServiceQuality.Native => $"  {service.Name}: native — {service.Detail}",
            HostServiceQuality.Substitute => $"  {service.Name}: SUBSTITUTE — {service.Detail}",
            _ => $"  {service.Name}: unavailable — {service.Detail}",
        });

        return $"{HostName}\n{string.Join('\n', lines)}";
    }
}
