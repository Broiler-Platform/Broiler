using System;
using System.Collections.Generic;

namespace Broiler.App.Rendering;

/// <summary>
/// Lightweight Content Security Policy (CSP) model used to gate dangerous
/// JavaScript operations such as <c>eval()</c>.  The implementation covers
/// the <c>script-src</c> directive with <c>'unsafe-eval'</c> and
/// <c>'strict-dynamic'</c> tokens as defined by the
/// <see href="https://www.w3.org/TR/CSP3/">CSP Level 3</see> specification.
/// </summary>
public sealed class ContentSecurityPolicy
{
    private readonly HashSet<string> _scriptSrcTokens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <c>eval()</c> and similar dynamic code execution is allowed.
    /// Defaults to <c>true</c> (no policy restricts it).
    /// Set to <c>false</c> by adding a <c>script-src</c> directive that
    /// does <em>not</em> include <c>'unsafe-eval'</c>.
    /// </summary>
    public bool AllowsEval { get; private set; } = true;

    /// <summary>
    /// Whether strict-dynamic is specified in the CSP.
    /// </summary>
    public bool StrictDynamic { get; private set; }

    /// <summary>
    /// The raw <c>script-src</c> tokens parsed from the policy.
    /// </summary>
    public IReadOnlyCollection<string> ScriptSrcTokens => _scriptSrcTokens;

    /// <summary>
    /// Parse a CSP header value (e.g.
    /// <c>"script-src 'self' 'unsafe-eval'"</c>) and apply the relevant
    /// directives.  Only the <c>script-src</c> directive is evaluated;
    /// unrecognised directives are silently ignored.
    /// </summary>
    public void Parse(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
            return;

        // Directives are separated by semicolons.
        var directives = policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directive in directives)
        {
            var tokens = directive.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;

            if (string.Equals(tokens[0], "script-src", StringComparison.OrdinalIgnoreCase))
            {
                _scriptSrcTokens.Clear();
                for (var i = 1; i < tokens.Length; i++)
                    _scriptSrcTokens.Add(tokens[i]);

                // eval is only allowed when 'unsafe-eval' is explicitly listed
                AllowsEval = _scriptSrcTokens.Contains("'unsafe-eval'");
                StrictDynamic = _scriptSrcTokens.Contains("'strict-dynamic'");
            }
        }
    }

    /// <summary>
    /// Guard method: throws <see cref="InvalidOperationException"/> when
    /// <c>eval()</c> is disallowed by the current policy.
    /// </summary>
    public void EnforceEval()
    {
        if (!AllowsEval)
            throw new InvalidOperationException(
                "Refused to evaluate a string as JavaScript because 'unsafe-eval' is not an allowed source in the Content Security Policy.");
    }
}
