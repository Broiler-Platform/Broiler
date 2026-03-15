using System;
using System.Collections.Generic;

namespace YantraJS.Parser;

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
    void Tolerate(Error error)
    {
        if (Tolerant)
        {
            RecordError(error);
        }
        else
        {
            throw error;
        }
    }
    Error ConstructError(string msg, double column)
    {
        var error = new Error(msg);
        return error;
    }
    Error CreateError(int index, int line, int col, string description)
    {
        var msg = "Line " + line + ": " + description;
        var error = ConstructError(msg, col);
        error.Index = index;
        error.LineNumber = line;
        error.Description = description;
        return error;
    }
    public void ThrowError(int index, int line, int col, string description) => throw CreateError(index, line, col, description);
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