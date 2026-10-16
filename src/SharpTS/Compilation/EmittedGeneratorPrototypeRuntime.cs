using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace SharpTS.Compilation;

/// <summary>Output-owned generator intrinsic graphs and creation-time iterator association.</summary>
public sealed class EmittedGeneratorPrototypeRuntime
{
    internal EmittedGeneratorPrototypeRuntime() { }
    public bool IsComplete { get; private set; }
    private readonly HashSet<MethodBuilder> _emittedBodies = [];
    public EmittedGeneratorPrototypeKindRuntime Sync { get; } = new();
    public EmittedGeneratorPrototypeKindRuntime? Async { get; private set; }

    internal void BeginAsyncEmission()
    {
        EnsureMutable();
        if (Async != null) throw new InvalidOperationException("Async generator prototype emission has already started.");
        Async = new();
    }

    public EmittedGeneratorPrototypeKindRuntime RequireAsync() => Async
        ?? throw new InvalidOperationException("Async generator prototypes were not enabled for this compilation.");

    private TypeBuilder? _type;
    public TypeBuilder Type
    {
        get => Require(_type);
        internal set => SetHandle(ref _type, value);
    }

    private MethodBuilder? _populate;
    public MethodBuilder Populate
    {
        get => Require(_populate);
        internal set => SetHandle(ref _populate, value);
    }

    private MethodBuilder? _kind;
    public MethodBuilder Kind
    {
        get => Require(_kind);
        internal set => SetHandle(ref _kind, value);
    }

    private MethodBuilder? _functionPrototype;
    public MethodBuilder FunctionPrototype
    {
        get => Require(_functionPrototype);
        internal set => SetHandle(ref _functionPrototype, value);
    }

    private MethodBuilder? _ensureOwnPrototype;
    public MethodBuilder EnsureOwnPrototype
    {
        get => Require(_ensureOwnPrototype);
        internal set => SetHandle(ref _ensureOwnPrototype, value);
    }

    private MethodBuilder? _readInherited;
    public MethodBuilder ReadInherited
    {
        get => Require(_readInherited);
        internal set => SetHandle(ref _readInherited, value);
    }

    private MethodBuilder? _readIteratorProperty;
    public MethodBuilder ReadIteratorProperty
    {
        get => Require(_readIteratorProperty);
        internal set => SetHandle(ref _readIteratorProperty, value);
    }

    private MethodBuilder? _associate;
    public MethodBuilder Associate
    {
        get => Require(_associate);
        internal set => SetHandle(ref _associate, value);
    }

    internal void MarkBodyEmitted(MethodBuilder method)
    {
        EnsureMutable();
        if (!RequiredMethods().Contains(method))
            throw new InvalidOperationException("Method does not belong to generator prototype metadata.");
        if (!_emittedBodies.Add(method))
            throw new InvalidOperationException("Generator prototype method body has already been emitted.");
    }

    private IEnumerable<MethodBuilder> RequiredMethods()
    {
        yield return Populate;
        yield return Kind;
        yield return FunctionPrototype;
        yield return EnsureOwnPrototype;
        yield return ReadInherited;
        yield return ReadIteratorProperty;
        yield return Associate;
        foreach (var method in Sync.RequiredMethods()) yield return method;
        if (Async != null)
            foreach (var method in Async.RequiredMethods()) yield return method;
    }

    private static T Require<T>(T? handle, [CallerMemberName] string name = "") where T : class =>
        handle ?? throw new InvalidOperationException("Generator prototype metadata '" + name + "' has not been declared.");

    private void SetHandle<T>(ref T? field, T value, [CallerMemberName] string name = "") where T : class
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(value);
        if (field != null) throw new InvalidOperationException("Generator prototype metadata '" + name + "' has already been declared.");
        field = value;
    }

    private void EnsureMutable()
    {
        if (IsComplete) throw new InvalidOperationException("Generator prototype emission is already complete.");
    }

    internal void CompleteEmission()
    {
        EnsureMutable();
        _ = Type;
        foreach (var method in RequiredMethods())
            if (!_emittedBodies.Contains(method))
                throw new InvalidOperationException("Generator prototype method body '" + method.Name + "' has not been emitted.");
        Sync.ValidateDeclarations();
        Async?.ValidateDeclarations();
        Sync.CompleteEmission();
        Async?.CompleteEmission();
        IsComplete = true;
    }
}

/// <summary>One enabled generator kind's intrinsic fields and protocol helper declarations.</summary>
public sealed class EmittedGeneratorPrototypeKindRuntime
{
    internal EmittedGeneratorPrototypeKindRuntime() { }
    public bool IsComplete { get; private set; }

    private FieldBuilder? _functionPrototype;
    public FieldBuilder FunctionPrototype
    {
        get => Require(_functionPrototype);
        internal set => SetHandle(ref _functionPrototype, value);
    }

    private FieldBuilder? _generatorPrototype;
    public FieldBuilder GeneratorPrototype
    {
        get => Require(_generatorPrototype);
        internal set => SetHandle(ref _generatorPrototype, value);
    }

    private FieldBuilder? _iteratorPrototype;
    public FieldBuilder IteratorPrototype
    {
        get => Require(_iteratorPrototype);
        internal set => SetHandle(ref _iteratorPrototype, value);
    }

    private FieldBuilder? _constructor;
    public FieldBuilder Constructor
    {
        get => Require(_constructor);
        internal set => SetHandle(ref _constructor, value);
    }

    private MethodBuilder? _next;
    public MethodBuilder Next
    {
        get => Require(_next);
        internal set => SetHandle(ref _next, value);
    }

    private MethodBuilder? _return;
    public MethodBuilder Return
    {
        get => Require(_return);
        internal set => SetHandle(ref _return, value);
    }

    private MethodBuilder? _throw;
    public MethodBuilder Throw
    {
        get => Require(_throw);
        internal set => SetHandle(ref _throw, value);
    }

    private MethodBuilder? _identity;
    public MethodBuilder Identity
    {
        get => Require(_identity);
        internal set => SetHandle(ref _identity, value);
    }

    private MethodBuilder? _constructorMethod;
    public MethodBuilder ConstructorMethod
    {
        get => Require(_constructorMethod);
        internal set => SetHandle(ref _constructorMethod, value);
    }

    private MethodBuilder? _emptyFunction;
    public MethodBuilder EmptyFunction
    {
        get => Require(_emptyFunction);
        internal set => SetHandle(ref _emptyFunction, value);
    }

    internal IEnumerable<MethodBuilder> RequiredMethods()
    {
        yield return Next;
        yield return Return;
        yield return Throw;
        yield return Identity;
        yield return ConstructorMethod;
        yield return EmptyFunction;
    }

    private static T Require<T>(T? handle, [CallerMemberName] string name = "") where T : class =>
        handle ?? throw new InvalidOperationException("Generator kind prototype metadata '" + name + "' has not been declared.");

    private void SetHandle<T>(ref T? field, T value, [CallerMemberName] string name = "") where T : class
    {
        if (IsComplete) throw new InvalidOperationException("Generator kind prototype emission is already complete.");
        ArgumentNullException.ThrowIfNull(value);
        if (field != null) throw new InvalidOperationException("Generator kind prototype metadata '" + name + "' has already been declared.");
        field = value;
    }

    internal void CompleteEmission()
    {
        if (IsComplete) throw new InvalidOperationException("Generator kind prototype emission is already complete.");
        ValidateDeclarations();
        IsComplete = true;
    }

    internal void ValidateDeclarations()
    {
        _ = FunctionPrototype;
        _ = GeneratorPrototype;
        _ = IteratorPrototype;
        _ = Constructor;
        foreach (var method in RequiredMethods()) _ = method;
    }
}
