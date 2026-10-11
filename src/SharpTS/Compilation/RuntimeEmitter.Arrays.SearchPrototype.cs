using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    private MethodBuilder EmitArraySearchPrototypeAdapter(
        TypeBuilder typeBuilder, EmittedRuntime runtime, MethodBuilder helper)
    {
        var method = typeBuilder.DefineMethod(
            helper.Name + "Proto",
            MethodAttributes.Public | MethodAttributes.Static,
            _types.Double,
            [_types.Object, _types.ObjectArray]);
        method.DefineParameter(2, ParameterAttributes.None, "args")
            .SetCustomAttribute(typeof(ParamArrayAttribute).GetConstructor(Type.EmptyTypes)!,
                CustomAttributeEncoder.EmptyBlob);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);

        // The two absent slots have different meanings: searchElement is
        // undefined, while lastIndexOf must distinguish omitted fromIndex
        // (start at length - 1) from explicit undefined (start at zero).
        EmitArgumentOrSentinel(0, runtime.Sentinels.UndefinedInstance);
        EmitArgumentOrSentinel(1, runtime.ArrayStorage.HoleInstance);
        il.Emit(OpCodes.Call, helper);
        il.Emit(OpCodes.Ret);
        return method;

        void EmitArgumentOrSentinel(int index, FieldInfo sentinel)
        {
            var provided = il.DefineLabel();
            var ready = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Conv_I4);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Bgt, provided);
            il.Emit(OpCodes.Ldsfld, sentinel);
            il.Emit(OpCodes.Br, ready);
            il.MarkLabel(provided);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldelem_Ref);
            il.MarkLabel(ready);
        }
    }
}
