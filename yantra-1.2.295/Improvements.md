## Planned Improvements

1. ~~Inverse Switch: visit all cases and convert switch argument to string/number based on case~~ — **Done** (see `FastCompiler.VisitSwitchStatement.cs`)
2. ~~Compare native type directly by converting left/right argument~~ — **Done** (see `FastCompiler.VisitBinaryExpression.cs`)
3. Reduce startup time by replacing LinkedStack with ref Struct based Type Stack — *LinkedStack in YantraJS.Core commented out; ExpressionCompiler version still active*
