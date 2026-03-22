using System.Runtime.CompilerServices;
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;

// Type forwarding for types moved to Broiler.JavaScript.Ast assembly.
// These ensure binary compatibility for downstream consumers that reference
// Broiler.JavaScript.Core.

[assembly: TypeForwardedTo(typeof(AstNode))]
[assembly: TypeForwardedTo(typeof(AstExpression))]
[assembly: TypeForwardedTo(typeof(AstStatement))]
[assembly: TypeForwardedTo(typeof(AstBlock))]
[assembly: TypeForwardedTo(typeof(AstProgram))]
[assembly: TypeForwardedTo(typeof(AstBinaryExpression))]
[assembly: TypeForwardedTo(typeof(AstCallExpression))]
[assembly: TypeForwardedTo(typeof(AstFunctionExpression))]
[assembly: TypeForwardedTo(typeof(AstIdentifier))]
[assembly: TypeForwardedTo(typeof(AstLiteral))]
[assembly: TypeForwardedTo(typeof(AstMemberExpression))]
[assembly: TypeForwardedTo(typeof(AstVariableDeclaration))]
[assembly: TypeForwardedTo(typeof(FastNodeType))]
[assembly: TypeForwardedTo(typeof(FastToken))]
[assembly: TypeForwardedTo(typeof(SpanLocation))]
[assembly: TypeForwardedTo(typeof(StringSpan))]
[assembly: TypeForwardedTo(typeof(TokenTypes))]
[assembly: TypeForwardedTo(typeof(FastKeywords))]
