using SharpTS.Execution;
using SharpTS.Runtime.Exceptions;
using SharpTS.Runtime.Types;

namespace SharpTS.Runtime.BuiltIns;

public static partial class ObjectBuiltIns
{
    /// <summary>Defines a class field as an own data property without invoking an inherited setter.</summary>
    internal static void CreateDataPropertyOrThrow(
        Interpreter interpreter, object target, object? key, object? value)
    {
        object propertyKey = key is SharpTSSymbol symbol ? symbol : interpreter.ToPropertyKeyString(key);
        var descriptor = new SharpTSPropertyDescriptor
        {
            Value = value, HasValue = true,
            Writable = true, HasWritable = true,
            Enumerable = true, HasEnumerable = true,
            Configurable = true, HasConfigurable = true,
        };

        bool success;
        if (target is SharpTSProxy proxy)
            success = proxy.TrapDefineField(propertyKey, descriptor.ToObject(), interpreter);
        else if (target is SharpTSInstance instance)
            success = propertyKey is SharpTSSymbol instanceSymbol
                ? instance.DefineDataField(instanceSymbol, value)
                : instance.DefineProperty((string)propertyKey, descriptor);
        else if (target is SharpTSObject obj)
            success = propertyKey is SharpTSSymbol objectSymbol
                ? obj.DefineProperty(objectSymbol, descriptor)
                : obj.DefineProperty((string)propertyKey, descriptor);
        else if (target is SharpTSArray array)
            success = propertyKey is SharpTSSymbol arraySymbol
                ? array.DefineProperty(arraySymbol, descriptor)
                : array.DefineProperty((string)propertyKey, descriptor);
        else if (target is SharpTSPromise promise && propertyKey is string promiseName)
        {
            success = promise.DefineOwnDataField(promiseName, value);
        }
        else
        {
            // Reuse the throwing descriptor dispatcher for functions and existing
            // native object adapters. Never use Reflect.defineProperty's catch-all.
            DefineProperty(interpreter, [target, propertyKey, descriptor.ToObject()]);
            return;
        }

        if (!success)
            throw new ThrowException(new SharpTSTypeError("Cannot define class instance field on the selected receiver"));
    }
}
