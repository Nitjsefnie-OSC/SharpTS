using SharpTS.Parsing;
using SharpTS.Runtime;
using SharpTS.Runtime.Types;

namespace SharpTS.Execution;

public partial class Interpreter
{
    private object? EvaluateEarlyStaticInitializer(Expr expression)
    {
        // Static auto-accessors and legacy field ASTs initialize before the
        // class value is available, but still have their own initializer context.
        var environment = new RuntimeEnvironment(_environment, strictMode: true)
            { NewTarget = RuntimeValue.Undefined };
        using (PushScope(environment)) return Evaluate(expression);
    }

    internal object? EvaluateClassInitializer(SharpTSClass owner, object receiver, Expr expression)
    {
        var environment = new RuntimeEnvironment(owner.InitializerEnvironment ?? _environment)
        {
            PrivateClass = owner,
            StaticSuper = receiver is SharpTSClass
        };
        environment.NewTarget = RuntimeValue.Undefined;
        environment.Define("this", receiver);
        if (owner.Superclass is { } parent) environment.Define("super", parent);
        using (PushScope(environment)) return Evaluate(expression);
    }
}
