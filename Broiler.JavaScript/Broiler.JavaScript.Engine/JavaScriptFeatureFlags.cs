using System;

namespace Broiler.JavaScript.Engine;

[Flags]
public enum JavaScriptFeatureFlags
{
    None = 0,
    MathSumPrecise = 1 << 0,
    Uint8ArrayBase64 = 1 << 1,
    JsonParseSourceTextAccess = 1 << 2,
    MapUpsert = 1 << 3,
    StructuredClone = 1 << 4,
    AllExperimentalEs2026 =
        MathSumPrecise |
        Uint8ArrayBase64 |
        JsonParseSourceTextAccess |
        MapUpsert |
        StructuredClone,
}
