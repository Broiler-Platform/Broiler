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
    ErrorIsError = 1 << 5,
    ArrayFromAsync = 1 << 6,
    ObjectMapGroupBy = 1 << 7,
    IteratorConcat = 1 << 8,
    AllExperimentalEs2026 =
        MathSumPrecise |
        Uint8ArrayBase64 |
        JsonParseSourceTextAccess |
        MapUpsert |
        StructuredClone |
        ErrorIsError |
        ArrayFromAsync |
        ObjectMapGroupBy |
        IteratorConcat,
}
