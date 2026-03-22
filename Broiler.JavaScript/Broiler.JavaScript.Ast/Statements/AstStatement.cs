using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Statements;

public class AstStatement(FastToken start, FastNodeType type, FastToken end) : AstNode(start, type, end, isStatement: true) { }
