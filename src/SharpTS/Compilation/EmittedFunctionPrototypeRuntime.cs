using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace SharpTS.Compilation;

/// <summary>Required Function prototype singleton and staged population declarations.</summary>
public sealed class EmittedFunctionPrototypeRuntime
{
    internal EmittedFunctionPrototypeRuntime() { }
    public bool IsComplete { get; private set; }
    private bool _populateBodyEmitted;
    private bool _prepareApplyArgumentsBodyEmitted;

    private Type? _type;
    public Type Type
    {
        get => Require(_type);
        internal set => SetHandle(ref _type, value);
    }

    private ConstructorBuilder? _constructor;
    public ConstructorBuilder Constructor
    {
        get => Require(_constructor);
        internal set => SetHandle(ref _constructor, value);
    }

    private FieldBuilder? _prototype;
    /// <summary>Function.prototype singleton dict, populated lazily with $TSFunction wrappers for call/apply/bind/toString/constructor (ECMA-262 §20.2.3).</summary>
    public FieldBuilder Prototype
    {
        get => Require(_prototype);
        internal set => SetHandle(ref _prototype, value);
    }

    private MethodBuilder? _populate;
    /// <summary>Idempotent populate for <see cref="Prototype"/>.</summary>
    public MethodBuilder Populate
    {
        get => Require(_populate);
        internal set => SetHandle(ref _populate, value);
    }

    private MethodBuilder? _prepareApplyArguments;
    /// <summary>Validates an apply target and copies its array-like argument list through ordinary property reads.</summary>
    public MethodBuilder PrepareApplyArguments
    {
        get => Require(_prepareApplyArguments);
        internal set => SetHandle(ref _prepareApplyArguments, value);
    }

    internal void MarkPopulateBodyEmitted()
    {
        EnsureMutable();
        _ = Populate;
        if (_populateBodyEmitted)
            throw new InvalidOperationException("Function prototype populate body is already emitted.");
        _populateBodyEmitted = true;
    }

    internal void MarkPrepareApplyArgumentsBodyEmitted()
    {
        EnsureMutable();
        _ = PrepareApplyArguments;
        if (_prepareApplyArgumentsBodyEmitted)
            throw new InvalidOperationException("Function apply argument preparation body is already emitted.");
        _prepareApplyArgumentsBodyEmitted = true;
    }

    private static T Require<T>(T? handle, [CallerMemberName] string name = "") where T : class =>
        handle ?? throw new InvalidOperationException("Function prototype metadata '" + name + "' has not been declared.");

    private void SetHandle<T>(ref T? field, T value, [CallerMemberName] string name = "") where T : class
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(value);
        if (field is not null)
            throw new InvalidOperationException("Function prototype metadata '" + name + "' has already been declared.");
        field = value;
    }

    private void EnsureMutable()
    {
        if (IsComplete)
            throw new InvalidOperationException("Function prototype metadata emission is already complete.");
    }

    internal void CompleteEmission()
    {
        EnsureMutable();
        _ = Type;
        _ = Constructor;
        _ = Prototype;
        _ = Populate;
        _ = PrepareApplyArguments;
        if (!_populateBodyEmitted)
            throw new InvalidOperationException("Function prototype populate body has not been emitted.");
        if (!_prepareApplyArgumentsBodyEmitted)
            throw new InvalidOperationException("Function apply argument preparation body has not been emitted.");
        IsComplete = true;
    }
}
