using System;
using System.Collections.Generic;

// Moved from Broiler.JavaScript.Core to Broiler.JavaScript.Parser.
// Rationale: ErrorHandler is parser infrastructure (tolerant error
// recording/throwing during parsing) and logically belongs with the
// parser implementation. Namespace preserved for binary compatibility.

namespace Broiler.JavaScript.Parser;

public class Error(string message) : Exception(message)
{
    public string Name;
    public int Index;
    public int LineNumber;
    public int Column;
    public string Description;
}

public class ErrorHandler
{
    public readonly List<Error> Errors;
    public bool Tolerant;

    public ErrorHandler()
    {
        Errors = [];
        Tolerant = false;
    }

    void RecordError(Error error) => Errors.Add(error);

    static Error ConstructError(string msg, double column)
    {
        var error = new Error(msg);
        return error;
    }

    static Error CreateError(int index, int line, int col, string description)
    {
        var msg = "Line " + line + ": " + description;
        var error = ConstructError(msg, col);
        error.Index = index;
        error.LineNumber = line;
        error.Description = description;
        return error;
    }

    public static void ThrowError(int index, int line, int col, string description) => throw CreateError(index, line, col, description);
    
    public void TolerateError(int index, int line, int col, string description)
    {
        var error = CreateError(index, line, col, description);
        
        if (Tolerant)
        {
            RecordError(error);
        }
        else
        {
            throw error;
        }
    }
}
