using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstExpression(FastToken start, FastNodeType type, FastToken end, bool isBinding = false) : AstNode(start, type, end, false, isBinding) { }
