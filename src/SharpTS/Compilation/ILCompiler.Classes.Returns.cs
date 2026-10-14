using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    internal static bool ClassConstructorMayReplace(List<Stmt.Function> methods)
    {
        var visitor = new ConstructorReplacementVisitor();
        foreach (var statement in methods.FirstOrDefault(method => !method.IsStatic && method.Name.Lexeme == "constructor" && method.Body != null)?.Body ?? [])
            visitor.Visit(statement);
        return visitor.HasReplacement;
    }

    private static void ConfigureClassConstructorReturns(CompilationContext context, Stmt.Function? constructor, bool derived)
    {
        var visitor = new ConstructorReplacementVisitor();
        foreach (var statement in constructor?.Body ?? [])
            visitor.Visit(statement);
        context.CapturesClassConstructorReturn = visitor.HasReplacement;
        context.IsDerivedClassConstructor = derived;
        if (visitor.HasReplacement)
        {
            context.ClassConstructorReceiver = context.IL.DeclareLocal(context.Types.Object);
            context.ClassConstructorReturnValue = context.IL.DeclareLocal(context.Types.Object);
        }
    }

    private static void InitializeClassConstructorReceiver(CompilationContext context, ILGenerator il)
    {
        if (context.ClassConstructorReceiver == null) return;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, context.Runtime!.DynamicConstruction.ResolveClassReturn);
        il.Emit(OpCodes.Stloc, context.ClassConstructorReceiver);
    }
}
