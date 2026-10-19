using SharpTS.Runtime.Types;
using SharpTS.Execution;
using System.Runtime.CompilerServices;

namespace SharpTS.Runtime.BuiltIns;

/// <summary>
/// Callable wrapper for native C# implementations of built-in methods.
/// </summary>
/// <remarks>
/// Implements <see cref="ISharpTSCallable"/> to provide a uniform calling convention for
/// built-in methods (array methods like push/pop/map, string methods, Math functions).
/// Supports variable arity via min/max argument counts. The <see cref="Bind"/> method
/// associates a receiver object (e.g., the array instance for array methods).
/// Used by <see cref="Interpreter"/> when resolving method calls on built-in types.
/// </remarks>
/// <seealso cref="ISharpTSCallable"/>
/// <seealso cref="MathBuiltIns"/>
public class BuiltInMethod : ISharpTSFunctionObject, IBuiltInFunctionMetadata
{
    private sealed class OwnPropertyState
    {
        internal SharpTSObject? Properties;
        internal IReadOnlyDictionary<string, object?>? NativeProperties;
        internal IReadOnlyDictionary<string, object?>? DictionaryView;
    }

    private sealed class OwnPropertyDictionary(BuiltInMethod owner)
        : IReadOnlyDictionary<string, object?>
    {
        public IEnumerable<string> Keys => owner.OwnPropertyStorage.OwnVisibleStringKeys();
        public IEnumerable<object?> Values => Keys.Select(key => this[key]);
        public int Count => Keys.Count();
        public object? this[string key] => TryGetValue(key, out var value)
            ? value : throw new KeyNotFoundException(key);
        public bool ContainsKey(string key)
            => owner.OwnPropertyStorage.GetOwnPropertyDescriptor(key) is not null;
        public bool TryGetValue(string key, out object? value)
        {
            var descriptor = owner.OwnPropertyStorage.GetOwnPropertyDescriptor(key);
            value = descriptor is { HasValue: true }
                ? descriptor.Value : SharpTSUndefined.Instance;
            return descriptor is not null;
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
            => Keys.Select(key => new KeyValuePair<string, object?>(key, this[key])).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private readonly string _name;
    private readonly int _minArity;
    private readonly int _maxArity;
    // Legacy boxed implementation. For V2-only built-ins this stays null and is built
    // lazily on the first boxed Call() (the rare reflective standalone-DLL path) — see
    // GetLegacyImplementation. At least one of _implementation/_implementationV2 is non-null.
    private Func<Interpreter, object?, List<object?>, object?>? _implementation;
    private readonly Func<Interpreter, RuntimeValue, ReadOnlySpan<RuntimeValue>, RuntimeValue>? _implementationV2;
    private object? _receiver;
    // Spec-defined Function.prototype.length value visible to user code as
    // `f.length`. Distinct from MinArity / MaxArity (used internally for arg
    // padding/trimming): per ECMA-262, variadic methods like Array.prototype.push
    // have spec length 1 even though their MinArity is 0 in our registration.
    // -1 = unset (caller didn't specify a spec length); resolvers fall back to
    // MinArity for compatibility with the many call sites that already report
    // the correct value via MinArity.
    private int _specLength = -1;
    // Receiver views share this holder even before any property is materialized.
    // A realm clone gets a fresh holder, so static registration templates remain isolated.
    private OwnPropertyState _ownPropertyState = new();

    // Cache for bound methods - uses weak references to avoid memory leaks
    // Key: receiver object, Value: bound method instance
    private ConditionalWeakTable<object, BuiltInMethod>? _boundMethodCache;

    /// <summary>
    /// The minimum number of arguments this method accepts.
    /// </summary>
    public int MinArity => _minArity;

    /// <summary>
    /// The maximum number of arguments this method accepts.
    /// </summary>
    public int MaxArity => _maxArity;

    /// <summary>
    /// Gets the method name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Returns true if this method has a receiver bound via Bind().
    /// Used by fast-path dispatch to avoid redundant double-binding.
    /// </summary>
    public bool IsBound => _receiver != null;

    /// <summary>
    /// True when this method wraps a constant value (e.g. Number.MAX_VALUE, Math.PI)
    /// rather than a real method. The interpreter invokes such methods on property
    /// access to materialize the underlying value. Distinguishing is necessary because
    /// some real methods also have <c>MinArity == MaxArity == 0</c> (Date.now, Array.isArray-as-value),
    /// and invoking THOSE on property access breaks function-reference aliasing
    /// (<c>var nativeNow = Date.now; nativeNow();</c> is a lodash/polyfill idiom).
    /// </summary>
    public bool IsConstant { get; }

    /// <summary>
    /// Whether this callable implements [[Construct]]. Kept true by default for
    /// compatibility with legacy constructor registrations; method builders
    /// explicitly mark ordinary built-in functions as non-constructors.
    /// </summary>
    public bool IsConstructor { get; private set; } = true;

    /// <summary>
    /// Returns true if this method has a native V2 (RuntimeValue) implementation,
    /// meaning CallV2 can bypass the legacy wrapper for better performance.
    /// </summary>
    public bool HasV2Implementation => _implementationV2 != null;

    internal bool IsRealmOwned { get; private set; }

    // These receiver objects are themselves realm-owned. Retain their method
    // identity if the owner is passed to another interpreter.
    internal bool HasRealmOwnedReceiver => _receiver is SharpTSMath or SharpTSJSON
        or SharpTSDate or SharpTSArrayGlobal;

    internal BuiltInMethod MarkRealmOwned()
    {
        IsRealmOwned = true;
        return this;
    }

    internal SharpTSObject OwnPropertyStorage
    {
        get
        {
            if (_ownPropertyState.Properties is { } properties) return properties;
            properties = FunctionObjectProperties.Create(_name, SpecLength);
            if (_ownPropertyState.NativeProperties is { } nativeProperties)
            {
                foreach (var (key, value) in nativeProperties)
                    DefineNativeProperty(properties, key, value);
            }
            return _ownPropertyState.Properties = properties;
        }
    }

    SharpTSObject ISharpTSFunctionObject.OwnProperties => OwnPropertyStorage;

    internal Type? ExpectedReceiverType { get; private set; }

    internal BuiltInMethod WithExpectedReceiverType(Type receiverType)
    {
        ExpectedReceiverType = receiverType;
        return this;
    }

    /// <summary>
    /// Own (function-object) properties carried by this method, resolved by the
    /// interpreter's function-member path before the Function.prototype surface.
    /// Node models several process APIs as functions with methods hanging off
    /// them (<c>process.hrtime.bigint</c>, <c>process.memoryUsage.rss</c>);
    /// this view exposes the canonical live descriptors without reviving deleted
    /// native properties. Accessor invocation belongs to the interpreter's Get path.
    /// </summary>
    public IReadOnlyDictionary<string, object?> OwnProperties
        => _ownPropertyState.DictionaryView ??= new OwnPropertyDictionary(this);

    /// <summary>
    /// Attaches own function-object properties (see <see cref="OwnProperties"/>).
    /// Returns this method for fluent initialization.
    /// </summary>
    public BuiltInMethod WithOwnProperties(Dictionary<string, object?> properties)
    {
        _ownPropertyState.NativeProperties = new Dictionary<string, object?>(properties);
        if (_ownPropertyState.Properties is { } storage)
        {
            foreach (var (key, value) in properties)
                DefineNativeProperty(storage, key, value);
        }
        return this;
    }

    private static void DefineNativeProperty(SharpTSObject properties, string key, object? value)
        => properties.DefineProperty(key, key == "prototype"
            ? new SharpTSPropertyDescriptor(value, writable: false, enumerable: false, configurable: false)
            : new SharpTSPropertyDescriptor(value, writable: true, enumerable: true, configurable: true));

    // Boxed-delegate constructors. The legacy delegate type survives only as plumbing
    // (CreateConstant + the reflective standalone-DLL Call path), so these are not part of
    // the public surface — they exist for the dispatcher tests, which have InternalsVisibleTo.
    internal BuiltInMethod(string name, int arity, Func<Interpreter, object?, List<object?>, object?> implementation)
        : this(name, arity, arity, implementation) { }

    internal BuiltInMethod(string name, int minArity, int maxArity, Func<Interpreter, object?, List<object?>, object?> implementation)
        : this(name, minArity, maxArity, implementation, isConstant: false) { }

    /// <summary>
    /// Internal constructor with IsConstant flag — use <see cref="CreateConstant"/> to wrap
    /// a property-style constant (Number.MAX_VALUE etc.).
    /// </summary>
    internal BuiltInMethod(string name, int minArity, int maxArity,
        Func<Interpreter, object?, List<object?>, object?> implementation, bool isConstant)
    {
        _name = name;
        _minArity = minArity;
        _maxArity = maxArity;
        _implementation = implementation;
        IsConstant = isConstant;
    }

    /// <summary>
    /// Creates a zero-arity BuiltInMethod whose <see cref="IsConstant"/> is true — the
    /// wrapped value materializes on every invocation, and the interpreter's property-access
    /// fast-path calls it on read instead of returning the method reference.
    /// </summary>
    public static BuiltInMethod CreateConstant(string name, object? value)
        => new(name, 0, 0, (_, _, _) => value, isConstant: true);

    /// <summary>
    /// The wrapped value of an <see cref="IsConstant"/> method (<c>Number.MAX_VALUE</c>).
    /// A constant ignores both the interpreter and the receiver, so unwrapping it needs
    /// neither — which lets a member table materialize the value before handing it out,
    /// instead of each read path having to remember to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The method is not a constant.</exception>
    public object? ConstantValue => IsConstant
        ? _implementation!(null!, null, [])
        : throw new InvalidOperationException($"Built-in '{_name}' is not a constant.");

    /// <summary>
    /// Creates a BuiltInMethod with a RuntimeValue-based implementation.
    /// Use <see cref="CreateV2"/> factory method instead for clearer intent.
    /// </summary>
    private BuiltInMethod(int arity,
        Func<Interpreter, RuntimeValue, ReadOnlySpan<RuntimeValue>, RuntimeValue> implementation,
        string name)
    {
        _name = name;
        _minArity = arity;
        _maxArity = arity;
        _implementationV2 = implementation;
        // Legacy wrapper is built lazily on first boxed Call() — see GetLegacyImplementation.
    }

    /// <summary>
    /// Creates a BuiltInMethod with a RuntimeValue-based implementation.
    /// </summary>
    /// <param name="name">The method name.</param>
    /// <param name="arity">The number of required arguments.</param>
    /// <param name="implementation">The V2 implementation using RuntimeValue.</param>
    /// <returns>A new BuiltInMethod instance.</returns>
    public static BuiltInMethod CreateV2(string name, int arity,
        Func<Interpreter, RuntimeValue, ReadOnlySpan<RuntimeValue>, RuntimeValue> implementation)
    {
        return new BuiltInMethod(arity, implementation, name);
    }

    /// <summary>
    /// Creates a BuiltInMethod with a RuntimeValue-based implementation and variable arity.
    /// </summary>
    /// <param name="name">The method name.</param>
    /// <param name="minArity">The minimum number of arguments.</param>
    /// <param name="maxArity">The maximum number of arguments.</param>
    /// <param name="implementation">The V2 implementation using RuntimeValue.</param>
    /// <returns>A new BuiltInMethod instance.</returns>
    public static BuiltInMethod CreateV2(string name, int minArity, int maxArity,
        Func<Interpreter, RuntimeValue, ReadOnlySpan<RuntimeValue>, RuntimeValue> implementation)
    {
        return new BuiltInMethod(name, minArity, maxArity, null!, implementation, (object?)null)
        {
            // Override the null implementation with the wrapper
        };
    }

    // Private constructor for creating bound instances (no cache needed on bound instances)
    private protected BuiltInMethod(string name, int minArity, int maxArity,
        Func<Interpreter, object?, List<object?>, object?>? implementation,
        Func<Interpreter, RuntimeValue, ReadOnlySpan<RuntimeValue>, RuntimeValue>? implementationV2,
        object? receiver, bool isConstant = false)
    {
        _name = name;
        _minArity = minArity;
        _maxArity = maxArity;
        if (implementation == null && implementationV2 == null)
        {
            throw new ArgumentNullException(nameof(implementation));
        }
        // For V2-only methods _implementation stays null and is built lazily on first
        // boxed Call() (see GetLegacyImplementation).
        _implementation = implementation;
        _implementationV2 = implementationV2;
        _receiver = receiver;
        IsConstant = isConstant;
        // Bound instances don't have their own cache
    }

    public int Arity() => _minArity;

    /// <summary>
    /// ECMA-262 Function.prototype.length value. When unset (-1), falls back
    /// to <see cref="MinArity"/> — matches most methods, where the minimum
    /// argument count equals the spec length. Variadic methods that need an
    /// explicit value (push, slice, splice, …) set it via
    /// <see cref="WithSpecLength"/> at registration time.
    /// </summary>
    public int SpecLength => _specLength >= 0 ? _specLength : _minArity;

    /// <summary>
    /// Returns this same instance with the JS-spec length set. Mutates in
    /// place — safe because BuiltInMethod is set up once at static-init time.
    /// </summary>
    public BuiltInMethod WithSpecLength(int specLength)
    {
        _specLength = specLength;
        return this;
    }

    public BuiltInMethod AsNonConstructor()
    {
        IsConstructor = false;
        return this;
    }

    /// <summary>
    /// Returns whether two built-in function objects wrap the same host
    /// implementation. Realm-local intrinsic functions are copied with
    /// <see cref="CloneForRealm"/>, so reference identity alone cannot be used
    /// by guarded fast paths to recognize an unchanged intrinsic.
    /// </summary>
    internal bool HasSameImplementation(BuiltInMethod other)
        => Equals(_implementationV2, other._implementationV2)
            && Equals(_implementation, other._implementation);

    /// <summary>ECMA-262 §17 `name` value — the registered method name.</summary>
    public string FunctionName => _name;

    public bool HasMetadataProperty(string name)
        => name is "name" or "length"
            && OwnPropertyStorage.GetOwnPropertyDescriptor(name) is not null;

    public bool DeleteMetadataProperty(string name)
    {
        if (name is not ("name" or "length")) return true;
        return OwnPropertyStorage.DeletePropertyStrict(name, false);
    }

    /// <summary>Creates a fresh realm function rather than another receiver view.</summary>
    internal BuiltInMethod CloneForRealm()
        => CloneForRealm(new Dictionary<BuiltInMethod, BuiltInMethod>(System.Collections.Generic.ReferenceEqualityComparer.Instance));

    private BuiltInMethod CloneForRealm(Dictionary<BuiltInMethod, BuiltInMethod> copies)
    {
        if (copies.TryGetValue(this, out var existing)) return existing;
        var copy = CopyConfiguration(_receiver);
        copy.IsRealmOwned = true;
        copies.Add(this, copy);
        if (_ownPropertyState.NativeProperties is { } nativeProperties)
        {
            var copiedProperties = new Dictionary<string, object?>(nativeProperties.Count);
            foreach (var (key, value) in nativeProperties)
                copiedProperties[key] = value is BuiltInMethod method
                    ? method.CloneForRealm(copies) : value;
            copy._ownPropertyState.NativeProperties = copiedProperties;
        }
        return copy;
    }

    private BuiltInMethod CopyConfiguration(object? receiver)
        => new(_name, _minArity, _maxArity, _implementation, _implementationV2, receiver, IsConstant)
        {
            _specLength = _specLength,
            IsConstructor = IsConstructor,
            ExpectedReceiverType = ExpectedReceiverType,
            IsRealmOwned = IsRealmOwned,
        };

    private BuiltInMethod CreateReceiverView(object? receiver)
    {
        var view = CopyConfiguration(receiver);
        view._ownPropertyState = _ownPropertyState;
        return view;
    }

    public BuiltInMethod Bind(object? receiver)
    {
        // Null receivers don't need caching
        if (receiver == null)
            return CreateReceiverView(null);

        // Value types (like double for numbers) can't be cached in ConditionalWeakTable
        // because they're boxed each time, creating new object instances
        if (receiver.GetType().IsValueType)
            return CreateReceiverView(receiver);

        // Initialize cache lazily
        _boundMethodCache ??= new ConditionalWeakTable<object, BuiltInMethod>();

        // Try to get cached bound method
        if (_boundMethodCache.TryGetValue(receiver, out var cached))
        {
            return cached;
        }

        // Create new bound method and cache it
        var bound = CreateReceiverView(receiver);
        _boundMethodCache.AddOrUpdate(receiver, bound);
        return bound;
    }

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count < _minArity || arguments.Count > _maxArity)
        {
            throw new Exception($"Runtime Error: '{_name}' expects {_minArity}-{_maxArity} arguments but got {arguments.Count}.");
        }
        return GetLegacyImplementation()(interpreter, _receiver, arguments);
    }

    /// <summary>
    /// Calls the method with RuntimeValue arguments.
    /// </summary>
    public RuntimeValue CallV2(Interpreter interpreter, ReadOnlySpan<RuntimeValue> arguments)
    {
        if (arguments.Length < _minArity || arguments.Length > _maxArity)
        {
            throw new Exception($"Runtime Error: '{_name}' expects {_minArity}-{_maxArity} arguments but got {arguments.Length}.");
        }

        // Fast path: if we have a V2 implementation, use it directly
        if (_implementationV2 != null)
        {
            return _implementationV2(interpreter, RuntimeValue.FromBoxed(_receiver), arguments);
        }

        // Slow path: convert to legacy call
        var boxedArgs = new List<object?>(arguments.Length);
        foreach (var arg in arguments)
        {
            boxedArgs.Add(arg.ToObject());
        }

        var result = GetLegacyImplementation()(interpreter, _receiver, boxedArgs);
        return RuntimeValue.FromBoxed(result);
    }

    /// <summary>
    /// Returns the legacy boxed implementation, building it lazily from the V2 implementation
    /// on first use. Only the rare boxed <see cref="Call"/> path (reflectively invoked from
    /// standalone compiled DLLs) needs it, so V2-only built-ins avoid allocating the wrapper
    /// closure until then. The wrapper is stateless, so a benign race that builds it twice is
    /// harmless.
    /// </summary>
    private Func<Interpreter, object?, List<object?>, object?> GetLegacyImplementation()
        => _implementation ??= WrapV2Implementation(_implementationV2!);

    /// <summary>
    /// Creates a legacy wrapper for a V2 implementation.
    /// </summary>
    private static Func<Interpreter, object?, List<object?>, object?> WrapV2Implementation(
        Func<Interpreter, RuntimeValue, ReadOnlySpan<RuntimeValue>, RuntimeValue> v2Impl)
    {
        return (interpreter, receiver, arguments) =>
        {
            var rvArgs = new RuntimeValue[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                rvArgs[i] = RuntimeValue.FromBoxed(arguments[i]);
            }

            var result = v2Impl(interpreter, RuntimeValue.FromBoxed(receiver), rvArgs);
            return result.ToObject();
        };
    }

    public override string ToString() => $"<built-in {_name}>";
}
