using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YantraJS.Core;

namespace YantraJS.Expressions;

public enum BindingType
{
    MemberAssignment,
    MemberListInit,
    ElementInit
}

public class YBinding(MemberInfo member, BindingType bindingType)
{
    public readonly MemberInfo Member = member;
    public readonly BindingType BindingType = bindingType;
}

public class YElementInit: YBinding
{
    public readonly MethodInfo AddMethod;
    public readonly IFastEnumerable<YExpression> Arguments;

    public YElementInit(MethodInfo addMethod, IFastEnumerable<YExpression> arguments)
        : base(addMethod, BindingType.ElementInit)
    {
        AddMethod = addMethod;
        Arguments = arguments;
    }


    public YElementInit(MethodInfo addMethod, params YExpression[] arguments)
        : base(addMethod, BindingType.ElementInit)
    {
        AddMethod = addMethod;
        Arguments = arguments.AsSequence();
    }
}

public class YMemberElementInit : YBinding
{
    public readonly YElementInit[] Elements;

    public YMemberElementInit(MemberInfo member, IEnumerable<YElementInit> inits)
        : base(member, BindingType.MemberListInit) => Elements = inits.ToArray();


    public YMemberElementInit(MemberInfo member, YElementInit[] inits)
        : base(member, BindingType.MemberListInit) => Elements = inits;
}

public class YMemberAssignment(MemberInfo field, YExpression value) : YBinding(field, BindingType.MemberAssignment)
{
    public YExpression Value = value;
}