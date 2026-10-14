using SharpTS.Execution;

namespace SharpTS.Runtime.Types;

/// <summary>
/// Function-object state shared by internal receiver views of a generator function.
/// A generator owns a prototype even though it has no [[Construct]] slot.
/// </summary>
internal sealed class GeneratorFunctionProperties
{
    private readonly bool _isAsync;
    private readonly SharpTSObject _properties;
    private readonly SharpTSObject _initialGeneratorPrototype = new([]);
    private object? _functionPrototype;
    private object? _generatorPrototype;
    internal SharpTSObject OwnProperties => _properties;

    internal GeneratorFunctionProperties(bool isAsync, string name, int length, Interpreter? creationRealm = null)
    {
        _isAsync = isAsync;
        _properties = FunctionObjectProperties.Create(name, length);
        _properties.DefineProperty("prototype", new SharpTSPropertyDescriptor(
            value: _initialGeneratorPrototype, writable: true));
        if (creationRealm is not null) InitializeRealm(creationRealm);
    }

    internal SharpTSObject GetProperties(Interpreter interpreter)
    {
        if (_functionPrototype is null) InitializeRealm(interpreter);
        return _properties;
    }

    private void InitializeRealm(Interpreter interpreter)
    {
        (_functionPrototype, _generatorPrototype) = interpreter.GetGeneratorIntrinsics(_isAsync);
        _initialGeneratorPrototype.Prototype = _generatorPrototype;
    }

    internal object GetFunctionPrototype(Interpreter interpreter)
    {
        GetProperties(interpreter);
        return _functionPrototype!;
    }

    internal object GetIteratorPrototype(Interpreter interpreter)
    {
        var value = GetProperties(interpreter).GetProperty("prototype");
        // GetPrototypeFromConstructor uses the intrinsic when prototype is a primitive.
        return value is null or SharpTSUndefined or string or bool or double or int or long
            or SharpTSSymbol or SharpTSBigInt or System.Numerics.BigInteger
            ? _generatorPrototype!
            : value;
    }
}

internal interface ISharpTSGeneratorFunctionValue : ISharpTSFunctionObject
{
    GeneratorFunctionProperties FunctionProperties { get; }
}
