namespace Broiler.JavaScript.Ast;

public class AstExpression(FastToken start, FastNodeType type, FastToken end, bool isBinding = false) : AstNode(start, type, end, false, isBinding) { }



