# Broiler.Layout

A .NET 10 CSS box-model and layout engine over the canonical Broiler DOM and computed
styles. The component is graphics-backend independent and is currently integrated with
Broiler.HTML through internal compatibility boundaries.

## Preview status

This is first-preview software. The API, layout behavior, and integration boundaries may
change without compatibility guarantees. Substantial implementation work was AI-assisted.
The component is **not human-approved for preview use** while
[HUMAN_REVIEW.md](HUMAN_REVIEW.md) remains `PENDING`.

Broiler.Layout is an independent Broiler component. Its integration with Broiler.HTML
inherits architectural context from HTML Renderer, but it must not be represented as an
official HTML Renderer component or as endorsed by that project's contributors.

## Build and test

From the main Broiler repository root:

```bash
dotnet build Broiler.Layout/Broiler.Layout/Broiler.Layout.csproj
dotnet test Broiler.Layout/Broiler.Layout.Tests/Broiler.Layout.Tests.csproj
```

## License

Broiler.Layout is licensed under the [Apache License 2.0](LICENSE). Third-party material,
if present, retains the license identified with that material. The license provides the
software on an “AS IS” basis, without warranties or conditions.

