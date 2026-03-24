using System;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public partial class LinqConverter
{
    private YMemberAssignment Visit(MemberBinding binding)
    {
        return binding switch
        {
            MemberAssignment ma => YExpression.Bind(ma.Member, Visit(ma.Expression)),
            _ => throw new NotSupportedException(),
        };
    }
}
