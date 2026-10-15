using System.Reflection.Emit;

namespace SharpTS.Compilation;

internal static class ClrNumericInteropEmitter
{
    // Only actual boxed CLR numbers cross this boundary as guest Numbers.
    // Null, booleans, strings, enums and objects never enter Convert.ToDouble.
    internal static void EmitNormalizeBoxed(ILGenerator il, TypeProvider types)
    {
        var normalize = il.DefineLabel();
        var done = il.DefineLabel();
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brfalse, done);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Isinst, types.Double);
        il.Emit(OpCodes.Brtrue, done);
        foreach (var numericType in new[]
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(decimal)
        })
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Isinst, numericType);
            il.Emit(OpCodes.Brtrue, normalize);
        }
        il.Emit(OpCodes.Br, done);
        il.MarkLabel(normalize);
        il.Emit(OpCodes.Call, types.GetMethod(types.Convert, nameof(Convert.ToDouble), types.Object));
        il.Emit(OpCodes.Box, types.Double);
        il.MarkLabel(done);
    }
}
