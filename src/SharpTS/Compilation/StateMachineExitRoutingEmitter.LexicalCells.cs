using System.Reflection.Emit;

namespace SharpTS.Compilation;

public abstract partial class StateMachineExitRoutingEmitter
{
    protected override FieldBuilder DefineLexicalCellField(string name) =>
        DefineStateMachineField(name, Types.StrongBoxOfObject);
}
