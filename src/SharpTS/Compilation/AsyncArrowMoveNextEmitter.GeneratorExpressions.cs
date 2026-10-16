using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class AsyncArrowMoveNextEmitter
{
    protected override bool TryEmitGeneratorCaptureOwner(Type owner, string capturedName)
    {
        if (_builder.FunctionDCField?.FieldType == owner)
        {
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, _builder.FunctionDCField);
            return true;
        }
        foreach (var field in _builder.StandaloneCaptureFields.Values)
            if (field.FieldType == owner)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, field);
                return true;
            }
        if (_builder.OuterStateMachineField is not null && Ctx.OuterFunctionDCField?.FieldType == owner)
        {
            AsyncArrowStorageAccess.CapturedField(_builder, capturedName, Ctx.OuterFunctionDCField).EmitLoad(IL);
            return true;
        }
        return base.TryEmitGeneratorCaptureOwner(owner, capturedName);
    }
}
