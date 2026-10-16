using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    private void EmitGeneratorPrototypeAccess(EmittedGeneratorPrototypeRuntime prototypes, GeneratorPrototypeAccessInputs inputs)
    {
        var il = prototypes.EnsureOwnPrototype.GetILGenerator();
        var descriptor = il.DeclareLocal(inputs.Descriptors.DescriptorType);
        var create = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldstr, "prototype");
        il.Emit(OpCodes.Call, inputs.Descriptors.GetPropertyDescriptor); il.Emit(OpCodes.Stloc, descriptor);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Brfalse, create);
        EmitDescriptorReadAndReturn(il, inputs.Descriptors, descriptor, () => il.Emit(OpCodes.Ldarg_0),
            inputs.InvokeMethod, inputs.UndefinedInstance, inputs.UndefinedType);
        il.MarkLabel(create);
        il.Emit(OpCodes.Call, prototypes.Populate);
        var kind = il.DeclareLocal(_types.Int32);
        il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, prototypes.Kind); il.Emit(OpCodes.Stloc, kind);
        var candidate = il.DeclareLocal(_types.Object);
        il.Emit(OpCodes.Newobj, _types.DictionaryStringObjectCtor);
        il.Emit(OpCodes.Newobj, inputs.Objects.Constructor); il.Emit(OpCodes.Stloc, candidate);
        il.Emit(OpCodes.Ldloc, candidate); EmitIntrinsicGeneratorPrototype(il, prototypes, kind);
        il.Emit(OpCodes.Call, inputs.Descriptors.SetPrototype);

        // Receiver views share the existing MethodInfo property key. Choose the
        // cache winner before installing the matching initial own descriptor.
        var cacheType = inputs.Functions.PrototypeCacheField.FieldType;
        var getOrAdd = _types.GetMethods(cacheType).Single(method => method.Name == "GetOrAdd"
            && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType == _types.Object);
        il.Emit(OpCodes.Ldsfld, inputs.Functions.PrototypeCacheField); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldloc, candidate);
        il.Emit(OpCodes.Callvirt, getOrAdd); il.Emit(OpCodes.Stloc, candidate);
        il.Emit(OpCodes.Newobj, inputs.Descriptors.DescriptorConstructor); il.Emit(OpCodes.Stloc, descriptor);
        il.Emit(OpCodes.Ldloc, descriptor); il.Emit(OpCodes.Ldloc, candidate);
        il.Emit(OpCodes.Callvirt, inputs.Descriptors.DescriptorValue.GetSetMethod()!);
        foreach (var (property, flag) in new[] { (inputs.Descriptors.DescriptorWritable, true),
                     (inputs.Descriptors.DescriptorEnumerable, false), (inputs.Descriptors.DescriptorConfigurable, false) })
        {
            il.Emit(OpCodes.Ldloc, descriptor); il.Emit(flag ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, property.GetSetMethod()!);
        }
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldstr, "prototype"); il.Emit(OpCodes.Ldloc, descriptor);
        il.Emit(OpCodes.Call, inputs.Descriptors.DefineProperty); il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldloc, candidate); il.Emit(OpCodes.Ret);
        prototypes.MarkBodyEmitted(prototypes.EnsureOwnPrototype);

        EmitOrdinaryInheritedRead(prototypes.ReadInherited);
        var iteratorRead = prototypes.ReadIteratorProperty.GetILGenerator();
        iteratorRead.Emit(OpCodes.Ldarg_0); iteratorRead.Emit(OpCodes.Ldarg_1);
        iteratorRead.Emit(OpCodes.Call, prototypes.ReadInherited); iteratorRead.Emit(OpCodes.Ret);
        prototypes.MarkBodyEmitted(prototypes.ReadIteratorProperty);

        var associate = prototypes.Associate.GetILGenerator();
        var done = associate.DefineLabel();
        var fallback = associate.DefineLabel();
        var selected = associate.DefineLabel();
        var sourceKind = associate.DeclareLocal(_types.Int32);
        var prototype = associate.DeclareLocal(_types.Object);
        associate.Emit(OpCodes.Ldarg_1); associate.Emit(OpCodes.Call, prototypes.Kind);
        associate.Emit(OpCodes.Dup); associate.Emit(OpCodes.Stloc, sourceKind); associate.Emit(OpCodes.Brfalse, done);
        associate.Emit(OpCodes.Call, prototypes.Populate);
        // PDS normalizes callable receiver views to their MethodInfo. Use that
        // key directly instead of allocating an uninvoked callable and all of
        // its invocation metadata for every iterator creation. The generator's
        // nonconfigurable prototype slot is a data property, so no getter needs
        // the original callable as its receiver here.
        associate.Emit(OpCodes.Ldarg_1);
        associate.Emit(OpCodes.Ldarg_1); associate.Emit(OpCodes.Call, prototypes.EnsureOwnPrototype);
        associate.Emit(OpCodes.Stloc, prototype);
        associate.Emit(OpCodes.Ldloc, prototype); associate.Emit(OpCodes.Brfalse, fallback);
        foreach (var primitive in new[] { inputs.UndefinedType, _types.Boolean, _types.String, inputs.SymbolType,
                     _types.BigInteger, _types.Double, _types.Single, _types.Decimal, _types.Int32, _types.Int64,
                     _types.UInt32, _types.UInt64, _types.Int16, _types.UInt16, _types.Byte, _types.SByte })
        {
            associate.Emit(OpCodes.Ldloc, prototype); associate.Emit(OpCodes.Isinst, primitive);
            associate.Emit(OpCodes.Brtrue, fallback);
        }
        associate.Emit(OpCodes.Br, selected);
        associate.MarkLabel(fallback);
        EmitIntrinsicGeneratorPrototype(associate, prototypes, sourceKind);
        associate.Emit(OpCodes.Stloc, prototype);
        associate.MarkLabel(selected);
        associate.Emit(OpCodes.Ldarg_0); associate.Emit(OpCodes.Ldloc, prototype);
        associate.Emit(OpCodes.Call, inputs.Descriptors.SetPrototype);
        associate.MarkLabel(done); associate.Emit(OpCodes.Ret);
        prototypes.MarkBodyEmitted(prototypes.Associate);

        void EmitOrdinaryInheritedRead(System.Reflection.Emit.MethodBuilder method)
        {
            var read = method.GetILGenerator();
            var parent = read.DeclareLocal(_types.Object);
            var found = read.DefineLabel();
            read.Emit(OpCodes.Ldarg_0); read.Emit(OpCodes.Call, inputs.GetPrototypeOf); read.Emit(OpCodes.Stloc, parent);
            read.Emit(OpCodes.Ldloc, parent); read.Emit(OpCodes.Brtrue, found);
            read.Emit(OpCodes.Ldsfld, inputs.UndefinedInstance); read.Emit(OpCodes.Ret);
            read.MarkLabel(found); read.Emit(OpCodes.Ldloc, parent); read.Emit(OpCodes.Ldarg_1);
            read.Emit(OpCodes.Ldarg_0); read.Emit(OpCodes.Call, inputs.ReflectGet); read.Emit(OpCodes.Ret);
            prototypes.MarkBodyEmitted(method);
        }
    }

    private static void EmitIntrinsicGeneratorPrototype(ILGenerator il, EmittedGeneratorPrototypeRuntime prototypes, LocalBuilder kind)
    {
        if (prototypes.Async != null)
        {
            var synchronous = il.DefineLabel();
            var ready = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, kind); il.Emit(OpCodes.Ldc_I4_2); il.Emit(OpCodes.Bne_Un, synchronous);
            il.Emit(OpCodes.Ldsfld, prototypes.RequireAsync().GeneratorPrototype); il.Emit(OpCodes.Br, ready);
            il.MarkLabel(synchronous); il.Emit(OpCodes.Ldsfld, prototypes.Sync.GeneratorPrototype); il.MarkLabel(ready);
        }
        else il.Emit(OpCodes.Ldsfld, prototypes.Sync.GeneratorPrototype);
    }
}
