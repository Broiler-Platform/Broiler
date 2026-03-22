using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Patterns;

public class AstBindingPattern(FastToken start, FastNodeType type, FastToken end) : AstExpression(start, type, end, true) { }
