
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Parser;

/// <summary>
/// Defines the contract for a JavaScript parser that produces an AST.
/// Implementations take a token stream and produce an <see cref="AstProgram"/>
/// representing the parsed JavaScript program.
/// </summary>
public interface IParser
{
    /// <summary>
    /// Parses a JavaScript program and returns the root AST node.
    /// </summary>
    /// <returns>The parsed program as an <see cref="AstProgram"/>.</returns>
    /// <exception cref="FastParseException">Thrown when the input contains invalid syntax.</exception>
    AstProgram ParseProgram();
}
