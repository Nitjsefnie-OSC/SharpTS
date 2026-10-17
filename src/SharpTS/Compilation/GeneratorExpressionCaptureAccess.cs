using System.Reflection.Emit;

namespace SharpTS.Compilation;

/// <summary>Accesses a generator expression's retained lexical environment from its iterator.</summary>
internal static class GeneratorExpressionCaptureAccess
{
    internal static bool HasCapture(CompilationContext? context, string name) =>
        context?.CurrentGeneratorExpression?.Captures.ContainsKey(name) == true;

    internal static bool TryLoad(ILGenerator il, CompilationContext? context, string name)
    {
        if (context?.CurrentGeneratorExpression is not { } expression
            || !expression.Captures.TryGetValue(name, out var capture)) return false;

        LoadEnvironment(il, expression);
        foreach (var field in capture.TargetPath) il.Emit(OpCodes.Ldfld, field);
        if (capture.IsCell)
        {
            il.Emit(OpCodes.Castclass, context.Types.StrongBoxOfObject);
            il.Emit(OpCodes.Ldfld, context.Types.StrongBoxOfObjectValueField);
        }
        else if (capture.TargetPath[^1].FieldType.IsValueType)
            il.Emit(OpCodes.Box, capture.TargetPath[^1].FieldType);
        context.EmitLexicalTdzValueCheck(il, name);
        return true;
    }

    internal static bool TryStore(ILGenerator il, CompilationContext? context, string name)
    {
        if (context?.CurrentGeneratorExpression is not { } expression
            || !expression.Captures.TryGetValue(name, out var capture)) return false;

        var value = il.DeclareLocal(context.Types.Object);
        il.Emit(OpCodes.Stloc, value);
        // Assignment observes the captured binding's TDZ after evaluating its RHS.
        TryLoad(il, context, name);
        il.Emit(OpCodes.Pop);
        LoadEnvironment(il, expression);
        int ownerPathLength = capture.IsCell ? capture.TargetPath.Count : capture.TargetPath.Count - 1;
        for (int i = 0; i < ownerPathLength; i++) il.Emit(OpCodes.Ldfld, capture.TargetPath[i]);
        if (capture.IsCell) il.Emit(OpCodes.Castclass, context.Types.StrongBoxOfObject);
        il.Emit(OpCodes.Ldloc, value);
        var destination = capture.IsCell ? context.Types.StrongBoxOfObjectValueField : capture.TargetPath[^1];
        if (destination.FieldType == context.Types.Double)
            il.Emit(OpCodes.Call, context.Runtime!.NumericCoercion.ConvertToNumber);
        else if (destination.FieldType.IsValueType) il.Emit(OpCodes.Unbox_Any, destination.FieldType);
        else if (destination.FieldType != context.Types.Object) il.Emit(OpCodes.Castclass, destination.FieldType);
        il.Emit(OpCodes.Stfld, destination);
        return true;
    }

    private static void LoadEnvironment(ILGenerator il, GeneratorExpressionCompilation expression)
    {
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, expression.EnvironmentField);
    }
}
