using System.Reflection.Emit;

namespace SharpTS.Compilation;

public partial class ILCompiler
{
    private void DefineAsyncArrowStub(AsyncArrowStateMachineBuilder builder)
    {
        builder.DefineStubMethod(_programType, _runtime, (il, stateMachine, parameterOffset) =>
        {
            if (!builder.Arrow.HasOwnThis || !builder.LocalFields.TryGetValue("arguments", out var field))
                return;
            var context = CreateModuleMemberContext(il, builder.StubMethod);
            var types = builder.StubMethod.GetParameters().Skip(parameterOffset)
                .Select(parameter => parameter.ParameterType).ToArray();
            // A capture/outer-state target occupies only a CLR parameter slot.
            // Invoke publishes the original user array without that target.
            var arguments = EmitArgumentsLocalPrologueCore(il, context, builder.Arrow.Parameters,
                types, parameterOffset);
            il.Emit(OpCodes.Ldloca, stateMachine);
            il.Emit(OpCodes.Ldloc, arguments);
            il.Emit(OpCodes.Stfld, field);
        });
    }
}
