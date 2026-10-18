using System.Reflection;
using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class RuntimeEmitter
{
    /// <summary>
    /// Emits ArrayFrom: creates an array from an iterable with optional map function.
    /// Signature: <c>$Array ArrayFrom(object iterable, object mapFn, $TSSymbol iteratorSymbol, Type runtimeType)</c>.
    /// Stage E.2 M2: returns <c>$Array</c> (was <c>List&lt;object?&gt;</c>).
    /// </summary>
    private void EmitArrayFrom(TypeBuilder typeBuilder, EmittedRuntime runtime)
    {
        // Signature: (iterable, mapFn, iteratorSymbol, runtimeType, thisArg).
        // thisArg added in #101 session — appended at end for caller-side
        // backward compat: existing callers updated to push $Undefined.
        var method = typeBuilder.DefineMethod(
            "ArrayFrom",
            MethodAttributes.Public | MethodAttributes.Static,
            runtime.ArrayStorage.Type,
            [_types.Object, _types.Object, runtime.Symbols.Type, _types.Type, _types.Object]
        );
        runtime.ArrayOperations.From = method;

        var il = method.GetILGenerator();

        var resultLocal = il.DeclareLocal(_types.ListOfObject);

        // ECMA-262 23.1.2.1 Array.from step 1: ToObject(items). Throws TypeError
        // for null / undefined receivers. Pre-fix tolerated these silently
        // (empty result), but Test262 explicitly tests `Array.from(null)` /
        // `Array.from(undefined)` for the throw.
        var nonNullLabel = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Brtrue, nonNullLabel);
        GuestErrorEmitter.ThrowTypeError(il, runtime, "Array.from requires an array-like object - not null or undefined");
        il.MarkLabel(nonNullLabel);
        var nonUndefLabel = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Isinst, runtime.Sentinels.UndefinedType);
        il.Emit(OpCodes.Brfalse, nonUndefLabel);
        GuestErrorEmitter.ThrowTypeError(il, runtime, "Array.from requires an array-like object - not null or undefined");
        il.MarkLabel(nonUndefLabel);

        // ECMA-262 23.1.2.1 step 2-3: if mapfn is undefined, mapping is false.
        // Otherwise mapfn must be callable; non-callable (null/Symbol/Number/
        // string/Object) → throw TypeError.
        // Callers emit $Undefined.Instance for absent args, so this distinguishes
        // "not provided" from "provided as null".
        var mapFnOkLabel = il.DefineLabel();
        // $Undefined → skip check
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Isinst, runtime.Sentinels.UndefinedType);
        il.Emit(OpCodes.Brtrue, mapFnOkLabel);
        // Validate the callable carriers supported by InvokeMethodValue before
        // consuming even an empty source. Bound wrappers and built-in values
        // have distinct CLR representations from an ordinary $TSFunction.
        List<Type> callableTypes =
        [
            runtime.FunctionValues.Type,
            runtime.FunctionPrototypes.Type,
            runtime.FunctionBindings.BoundType,
            runtime.FunctionBindings.AnyType,
            runtime.FunctionBindings.BindType,
            runtime.FunctionBindings.CallType,
            runtime.FunctionBindings.ApplyType,
            runtime.ArrayOperations.BoundMethodType,
            runtime.ReflectedMethods.CallableType,
            _types.FuncObjectArrayToObject,
            _types.MethodBase,
            _types.Type
        ];
        if (runtime.Map is { } map) callableTypes.Add(map.BoundMethodType);
        if (runtime.Set is { } set) callableTypes.Add(set.BoundMethodType);
        if (runtime.TypedArrays.Implementation is { } typedArrays) callableTypes.Add(typedArrays.BoundMethodType);
        if (runtime.TextEncoding is { } encoding) callableTypes.Add(encoding.DecodeMethodType);
        foreach (var callableType in callableTypes)
        {
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Isinst, callableType);
            il.Emit(OpCodes.Brtrue, mapFnOkLabel);
        }
        GuestErrorEmitter.ThrowTypeError(il, runtime, "Array.from: mapfn argument must be callable");
        il.MarkLabel(mapFnOkLabel);

        // Stream iterable values and keep the actual iterator until each mapper
        // call completes. In particular a native generator must not be exhausted
        // before its first mapper invocation.
        var arrayLikePathLabel = il.DefineLabel();
        il.Emit(OpCodes.Newobj, _types.GetDefaultConstructor(_types.ListOfObject));
        il.Emit(OpCodes.Stloc, resultLocal);
        EmitCollectionConsumerLoop(il, GetCollectionConsumerInputs(runtime),
            () => il.Emit(OpCodes.Ldarg_0), EmitAppendValue, arrayLikePathLabel);
        il.Emit(OpCodes.Ldloc, resultLocal);
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Ret);

        // Without an iterator, snapshot LengthOfArrayLike once and Get each
        // property immediately before mapping it. Reads therefore observe prior
        // mapper mutations, while missing indices become actual undefined values.
        // No iterator was acquired, so this path has no IteratorClose obligation.
        il.MarkLabel(arrayLikePathLabel);
        var (receiver, length) = EmitGenericArrayReceiverAndLength(il, runtime);
        var validArrayLength = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, length);
        il.Emit(OpCodes.Ldc_R8, (double)uint.MaxValue);
        il.Emit(OpCodes.Ble, validArrayLength);
        GuestErrorEmitter.ThrowError(il, runtime.Errors.CreateException,
            runtime.Errors.RangeErrorConstructor, "Invalid array length");
        il.MarkLabel(validArrayLength);
        var indexLocal = il.DeclareLocal(_types.Double);
        var valueLocal = il.DeclareLocal(_types.Object);
        var loopStartLabel = il.DefineLabel();
        var loopEndLabel = il.DefineLabel();
        il.Emit(OpCodes.Ldc_R8, 0.0);
        il.Emit(OpCodes.Stloc, indexLocal);
        il.MarkLabel(loopStartLabel);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Ldloc, length);
        il.Emit(OpCodes.Bge, loopEndLabel);
        il.Emit(OpCodes.Ldloc, receiver);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Box, _types.Double);
        il.Emit(OpCodes.Call, runtime.ObjectRead.Index);
        il.Emit(OpCodes.Stloc, valueLocal);
        EmitAppendValue(valueLocal, indexLocal);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Ldc_R8, 1.0);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, indexLocal);
        il.Emit(OpCodes.Br, loopStartLabel);
        il.MarkLabel(loopEndLabel);
        il.Emit(OpCodes.Ldloc, resultLocal);
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Ret);

        void EmitAppendValue(LocalBuilder value, LocalBuilder index)
        {
            var mapped = il.DeclareLocal(_types.Object);
            var noMapper = il.DefineLabel();
            var ready = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Isinst, runtime.Sentinels.UndefinedType);
            il.Emit(OpCodes.Brtrue, noMapper);
            il.Emit(OpCodes.Ldarg, (short)4);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_2);
            il.Emit(OpCodes.Newarr, _types.Object);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldloc, value);
            il.Emit(OpCodes.Stelem_Ref);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Conv_R8);
            il.Emit(OpCodes.Box, _types.Double);
            il.Emit(OpCodes.Stelem_Ref);
            il.Emit(OpCodes.Call, runtime.Invocation.Method);
            il.Emit(OpCodes.Stloc, mapped);
            il.Emit(OpCodes.Br, ready);
            il.MarkLabel(noMapper);
            il.Emit(OpCodes.Ldloc, value);
            il.Emit(OpCodes.Stloc, mapped);
            il.MarkLabel(ready);
            il.Emit(OpCodes.Ldloc, resultLocal);
            il.Emit(OpCodes.Ldloc, mapped);
            il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.ListOfObject, "Add", _types.Object));
        }
    }

    /// <summary>
    /// Emits ArrayOf: creates an array from arguments.
    /// Signature: <c>$Array ArrayOf(object[] args)</c>.
    /// Stage E.2 M2: returns <c>$Array</c> (was <c>List&lt;object?&gt;</c>).
    /// </summary>
    private void EmitArrayOf(TypeBuilder typeBuilder, EmittedRuntime runtime)
    {
        var method = typeBuilder.DefineMethod(
            "ArrayOf",
            MethodAttributes.Public | MethodAttributes.Static,
            runtime.ArrayStorage.Type,
            [_types.ObjectArray]
        );
        runtime.ArrayOperations.Of = method;

        var il = method.GetILGenerator();

        // return new $Array(new List<object?>(args));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, _types.GetConstructor(_types.ListOfObject, typeof(IEnumerable<object>)));
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Stage 4y: adapter wrapping ArrayFrom for value-form access.
    /// `let f = Array.from; f(iterable, mapFn)` — TSFunction.Invoke maps
    /// args[0]/[1] to (iterable, mapFn) and we supply the spec-fixed
    /// SymbolIterator + runtime-Type internally. Signature: object(object[]).
    /// </summary>
    private void EmitArrayFromAdapter(TypeBuilder typeBuilder, EmittedRuntime runtime)
    {
        var method = typeBuilder.DefineMethod(
            "ArrayFromAdapter",
            MethodAttributes.Public | MethodAttributes.Static,
            _types.Object,
            [_types.ObjectArray]
        );
        runtime.ArrayOperations.FromAdapter = method;

        var il = method.GetILGenerator();
        var argsLocal = il.DeclareLocal(_types.ObjectArray);
        var iterableLocal = il.DeclareLocal(_types.Object);
        var mapFnLocal = il.DeclareLocal(_types.Object);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stloc, argsLocal);

        // iterable = args.Length > 0 ? args[0] : null
        var hasIterable = il.DefineLabel();
        var iterableSet = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, argsLocal);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Bgt, hasIterable);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Stloc, iterableLocal);
        il.Emit(OpCodes.Br, iterableSet);
        il.MarkLabel(hasIterable);
        il.Emit(OpCodes.Ldloc, argsLocal);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, iterableLocal);
        il.MarkLabel(iterableSet);

        // mapFn = args.Length > 1 ? args[1] : $Undefined.Instance
        // (absent → $Undefined so ArrayFrom can distinguish from explicit null
        // per ECMA-262 23.1.2.1 step 2: undefined skips mapping; null throws.)
        var hasMapFn = il.DefineLabel();
        var mapFnSet = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, argsLocal);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Bgt, hasMapFn);
        il.Emit(OpCodes.Ldsfld, runtime.Sentinels.UndefinedInstance);
        il.Emit(OpCodes.Stloc, mapFnLocal);
        il.Emit(OpCodes.Br, mapFnSet);
        il.MarkLabel(hasMapFn);
        il.Emit(OpCodes.Ldloc, argsLocal);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, mapFnLocal);
        il.MarkLabel(mapFnSet);

        // Read thisArg from args[2] if provided, else $Undefined.
        var thisArgLocal = il.DeclareLocal(_types.Object);
        var hasThisArg = il.DefineLabel();
        var thisArgSet = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, argsLocal);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Bgt, hasThisArg);
        il.Emit(OpCodes.Ldsfld, runtime.Sentinels.UndefinedInstance);
        il.Emit(OpCodes.Stloc, thisArgLocal);
        il.Emit(OpCodes.Br, thisArgSet);
        il.MarkLabel(hasThisArg);
        il.Emit(OpCodes.Ldloc, argsLocal);
        il.Emit(OpCodes.Ldc_I4_2);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, thisArgLocal);
        il.MarkLabel(thisArgSet);

        // return ArrayFrom(iterable, mapFn, SymbolIterator, typeof($Runtime), thisArg)
        il.Emit(OpCodes.Ldloc, iterableLocal);
        il.Emit(OpCodes.Ldloc, mapFnLocal);
        il.Emit(OpCodes.Ldsfld, runtime.Symbols.Iterator);
        il.Emit(OpCodes.Ldtoken, runtime.RuntimeClass.Type);
        il.Emit(OpCodes.Call, _types.GetMethod(_types.Type, "GetTypeFromHandle", _types.RuntimeTypeHandle));
        il.Emit(OpCodes.Ldloc, thisArgLocal);
        il.Emit(OpCodes.Call, runtime.ArrayOperations.From);
        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Emits the ECMAScript <c>Array</c> constructor (issue #61):
    /// <list type="bullet">
    /// <item><c>Array()</c> / <c>new Array()</c> → empty <c>$Array</c>.</item>
    /// <item><c>Array(n)</c> where <c>n</c> is a valid uint32 number → empty
    ///   <c>$Array</c> whose length is set via <c>SetLength(n)</c>. Beyond the
    ///   sparse threshold the backing transitions to sparse storage, so
    ///   <c>new Array(10_000_000)</c> allocates O(1) instead of paging the
    ///   heap full of holes.</item>
    /// <item><c>Array(x)</c> where <c>x</c> is not a number → single-element
    ///   <c>$Array([x])</c>.</item>
    /// <item><c>Array(a, b, c, …)</c> → <c>$Array</c> wrapping a list of all args.</item>
    /// </list>
    /// Out-of-range numeric inputs (negative, non-integer, &gt; uint32 max)
    /// throw <c>RangeError</c> — <c>SetLength</c> enforces this, matching the
    /// interpreter and ECMA-262 §23.1.1.1.
    /// Signature: <c>$Array ArrayConstructor(object[] args)</c>.
    /// Stage E.2 M2: the Stage-D 1M guard is gone; sparse storage absorbs huge N.
    /// </summary>
    private void EmitArrayConstructor(TypeBuilder typeBuilder, EmittedRuntime runtime)
    {
        var method = typeBuilder.DefineMethod(
            "ArrayConstructor",
            MethodAttributes.Public | MethodAttributes.Static,
            runtime.ArrayStorage.Type,
            [_types.ObjectArray]
        );
        runtime.ArrayOperations.Constructor = method;

        var il = method.GetILGenerator();
        var lenLocal = il.DeclareLocal(_types.Int32);
        var argLocal = il.DeclareLocal(_types.Object);
        var dLocal = il.DeclareLocal(_types.Double);
        var nLocal = il.DeclareLocal(_types.Int64);
        var arrLocal = il.DeclareLocal(runtime.ArrayStorage.Type);

        var emptyCaseLabel = il.DefineLabel();
        var singleArgLabel = il.DefineLabel();
        var multiArgLabel = il.DefineLabel();
        var notNumberLabel = il.DefineLabel();

        // if (args == null) → empty
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Brfalse, emptyCaseLabel);

        // lenLocal = args.Length
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldlen);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Stloc, lenLocal);

        // if (len == 0) → empty
        il.Emit(OpCodes.Ldloc, lenLocal);
        il.Emit(OpCodes.Brfalse, emptyCaseLabel);

        // if (len == 1) → single-arg branch
        il.Emit(OpCodes.Ldloc, lenLocal);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Beq, singleArgLabel);

        // else → multi-arg branch
        il.Emit(OpCodes.Br, multiArgLabel);

        // --- empty: return new $Array(new List<object?>()) ---
        il.MarkLabel(emptyCaseLabel);
        il.Emit(OpCodes.Newobj, _types.GetDefaultConstructor(_types.ListOfObject));
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Ret);

        // --- single-arg: check numeric-vs-other ---
        il.MarkLabel(singleArgLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Stloc, argLocal);

        // if (!(arg is double)) → single-element $Array([arg])
        il.Emit(OpCodes.Ldloc, argLocal);
        il.Emit(OpCodes.Isinst, _types.Double);
        il.Emit(OpCodes.Brfalse, notNumberLabel);

        // Numeric: validate range per ECMA-262 ToUint32.
        il.Emit(OpCodes.Ldloc, argLocal);
        il.Emit(OpCodes.Unbox_Any, _types.Double);
        il.Emit(OpCodes.Stloc, dLocal);

        var rangeErrorLabel = il.DefineLabel();
        var inRangeLabel = il.DefineLabel();
        // if (d < 0) → throw
        il.Emit(OpCodes.Ldloc, dLocal);
        il.Emit(OpCodes.Ldc_R8, 0.0);
        il.Emit(OpCodes.Blt_Un, rangeErrorLabel);
        // if (d > uint.MaxValue) → throw
        il.Emit(OpCodes.Ldloc, dLocal);
        il.Emit(OpCodes.Ldc_R8, (double)uint.MaxValue);
        il.Emit(OpCodes.Bgt_Un, rangeErrorLabel);
        // if (d != floor(d)) → throw (non-integer)
        il.Emit(OpCodes.Ldloc, dLocal);
        il.Emit(OpCodes.Ldloc, dLocal);
        il.Emit(OpCodes.Call, _types.GetMethod(_types.Math, "Floor", _types.Double));
        il.Emit(OpCodes.Bne_Un, rangeErrorLabel);
        il.Emit(OpCodes.Br, inRangeLabel);

        il.MarkLabel(rangeErrorLabel);
        // Inline-throw pattern: $RangeError(msg) wrapped in CLR Exception with
        // Data["__tsValue"]=err so WrapException returns it on catch. Used here
        // because EmitArrayConstructor runs before EmitCreateException in the
        // emit pipeline (must precede InvokeValue), so runtime.CreateException
        // isn't yet defined.
        var errLocal = il.DeclareLocal(_types.Object);
        var exLocal = il.DeclareLocal(_types.Exception);
        il.Emit(OpCodes.Ldstr, "Invalid array length");
        il.Emit(OpCodes.Newobj, runtime.Errors.RangeErrorConstructor);
        il.Emit(OpCodes.Stloc, errLocal);
        il.Emit(OpCodes.Ldstr, "Invalid array length");
        il.Emit(OpCodes.Newobj, _types.GetConstructor(_types.Exception, _types.String));
        il.Emit(OpCodes.Stloc, exLocal);
        il.Emit(OpCodes.Ldloc, exLocal);
        il.Emit(OpCodes.Callvirt, _types.GetProperty(_types.Exception, "Data").GetGetMethod()!);
        il.Emit(OpCodes.Ldstr, "__tsValue");
        il.Emit(OpCodes.Ldloc, errLocal);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.IDictionary, "set_Item"));
        il.Emit(OpCodes.Ldloc, exLocal);
        il.Emit(OpCodes.Throw);

        il.MarkLabel(inRangeLabel);

        // long n = (long)d;
        il.Emit(OpCodes.Ldloc, dLocal);
        il.Emit(OpCodes.Conv_I8);
        il.Emit(OpCodes.Stloc, nLocal);

        // arr = new $Array(new List<object?>());
        // arr.SetLength(n);
        // return arr;
        il.Emit(OpCodes.Newobj, _types.GetDefaultConstructor(_types.ListOfObject));
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Stloc, arrLocal);
        il.Emit(OpCodes.Ldloc, arrLocal);
        il.Emit(OpCodes.Ldloc, nLocal);
        il.Emit(OpCodes.Callvirt, runtime.ArrayStorage.SetLength);
        il.Emit(OpCodes.Ldloc, arrLocal);
        il.Emit(OpCodes.Ret);

        // --- single-arg, non-numeric: return $Array([arg]) ---
        il.MarkLabel(notNumberLabel);
        il.Emit(OpCodes.Newobj, _types.GetDefaultConstructor(_types.ListOfObject));
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Ldloc, argLocal);
        il.Emit(OpCodes.Callvirt, _types.GetMethod(_types.ListOfObject, "Add")!);
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Ret);

        // --- multi-arg: $Array wrapping new List<object>(args) ---
        il.MarkLabel(multiArgLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, _types.GetConstructor(_types.ListOfObject, typeof(IEnumerable<object>)));
        il.Emit(OpCodes.Newobj, runtime.ArrayStorage.Ctor);
        il.Emit(OpCodes.Ret);
    }
}
