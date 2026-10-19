using SharpTS.Execution;

namespace SharpTS.Runtime.Types;

/// <summary>
/// Array-like storage for a function's arguments object.
/// </summary>
/// <remarks>
/// Reuses <see cref="SharpTSArray"/>'s indexed-property implementation, while
/// retaining the distinct ECMAScript identity needed by Object.prototype.toString,
/// Array.isArray, and instanceof Array.
/// </remarks>
public sealed class SharpTSArguments(IEnumerable<object?> arguments)
    : SharpTSArray(arguments)
{
    internal static SharpTSArguments Create(IEnumerable<object?> arguments, Interpreter interpreter)
    {
        var result = new SharpTSArguments(arguments);
        result.DefineProperty(SharpTSSymbol.Iterator,
            SharpTSPropertyDescriptor.ForMethod(interpreter.GetArrayPrototype().GetIntrinsicValuesMethod()));
        return result;
    }
}
