using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    private void DefineGeneratorPrototypeRuntime(ModuleBuilder module, EmittedGeneratorPrototypeRuntime prototypes, bool includeAsync)
    {
        var owner = EmitTypeDefinitions.DefineType(module, "$GeneratorPrototypes",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, _types.Object);
        prototypes.Type = owner;
        prototypes.Populate = Define("Populate", _types.Void, []);
        prototypes.Kind = Define("Kind", _types.Int32, [_types.MethodInfo]);
        prototypes.FunctionPrototype = Define("FunctionPrototype", _types.Object, [_types.Int32]);
        prototypes.EnsureOwnPrototype = Define("EnsureOwnPrototype", _types.Object, [_types.Object, _types.MethodInfo]);
        prototypes.ReadInherited = Define("ReadInherited", _types.Object, [_types.Object, _types.String]);
        prototypes.ReadIteratorProperty = Define("ReadIteratorProperty", _types.Object, [_types.Object, _types.String]);
        prototypes.Associate = Define("Associate", _types.Void, [_types.Object, _types.MethodInfo]);
        DefineKind(prototypes.Sync, "Generator");
        if (includeAsync)
        {
            prototypes.BeginAsyncEmission();
            DefineKind(prototypes.RequireAsync(), "AsyncGenerator");
        }

        MethodBuilder Define(string name, Type result, Type[] parameters) => owner.DefineMethod(
            name, MethodAttributes.Public | MethodAttributes.Static, result, parameters);

        void DefineKind(EmittedGeneratorPrototypeKindRuntime kind, string prefix)
        {
            kind.FunctionPrototype = owner.DefineField(prefix + "FunctionPrototype", _types.Object, FieldAttributes.Public | FieldAttributes.Static);
            kind.GeneratorPrototype = owner.DefineField(prefix + "Prototype", _types.Object, FieldAttributes.Public | FieldAttributes.Static);
            kind.IteratorPrototype = owner.DefineField(prefix + "IteratorPrototype", _types.Object, FieldAttributes.Public | FieldAttributes.Static);
            kind.Constructor = owner.DefineField(prefix + "Constructor", _types.Object, FieldAttributes.Public | FieldAttributes.Static);
            kind.Next = Define(prefix + "Next", _types.Object, [_types.Object, _types.Object]);
            kind.Return = Define(prefix + "Return", _types.Object, [_types.Object, _types.Object]);
            kind.Throw = Define(prefix + "Throw", _types.Object, [_types.Object, _types.Object]);
            kind.Identity = Define(prefix + "Identity", _types.Object, [_types.Object]);
            kind.ConstructorMethod = Define(prefix + "Function", _types.Object, [_types.ObjectArray]);
            kind.EmptyFunction = Define(prefix + "EmptyFunction", _types.Object, []);
            foreach (var method in new[] { kind.Next, kind.Return, kind.Throw, kind.Identity })
                method.DefineParameter(1, ParameterAttributes.None, "__this");
            kind.ConstructorMethod.DefineParameter(1, ParameterAttributes.None, "args")
                .SetCustomAttribute(new CustomAttributeBuilder(typeof(ParamArrayAttribute).GetConstructor(Type.EmptyTypes)!, []));
        }
    }
}
