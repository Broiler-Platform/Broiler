// Namespace anchor: the JSClassGenerator source generator emits a
// "using Broiler.JavaScript.BuiltIns.Function;" directive in every
// generated file.  Core does not reference the BuiltIns assembly, so
// without at least one type or namespace declaration under this
// namespace the generated code would fail to compile with CS0234.
//
// This file satisfies the compiler by declaring the namespace inside
// Core.  It contains no types and can be removed once the generator
// is updated to omit unused using directives.

namespace Broiler.JavaScript.BuiltIns.Function;