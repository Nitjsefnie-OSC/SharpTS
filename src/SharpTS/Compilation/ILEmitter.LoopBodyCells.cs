using System.Reflection.Emit;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public partial class ILEmitter
{
    private sealed record LoopBodyCellScopeEntry(string Name, LocalBuilder? Cell, Token? Declaration, Token? Shadow);

    private void EnterLoopBodyCellScope(IEnumerable<Stmt> statements, List<LoopBodyCellScopeEntry> prior)
    {
        foreach (var token in DirectLexicalDeclarations(statements))
        {
            string name = token.Lexeme;
            bool selected = _ctx.ClosureAnalyzer?.IsLoopBodyCellDeclaration(token) == true;
            if (!selected && !_ctx.CellBindingLocals.ContainsKey(name)
                && !_ctx.LoopBodyCellShadows.ContainsKey(name)
                && _ctx.LoopBodyCellCaptures?.ContainsKey(name) != true)
                continue;

            prior.Add(new(name, _ctx.CellBindingLocals.GetValueOrDefault(name),
                _ctx.LoopBodyCellDeclarations.GetValueOrDefault(name),
                _ctx.LoopBodyCellShadows.GetValueOrDefault(name)));
            _ctx.CellBindingLocals.Remove(name);
            _ctx.LoopBodyCellDeclarations.Remove(name);
            _ctx.LoopBodyCellShadows.Remove(name);

            IL.Emit(OpCodes.Ldsfld, _ctx.Runtime!.Sentinels.LexicalUninitializedInstance);
            if (selected)
            {
                IL.Emit(OpCodes.Newobj, _ctx.Types.StrongBoxOfObjectCtor);
                var cell = _ctx.Locals.DeclareLocal(name, _ctx.Types.StrongBoxOfObject, token);
                IL.Emit(OpCodes.Stloc, cell);
                _ctx.CellBindingLocals[name] = cell;
                _ctx.LoopBodyCellDeclarations[name] = token;
            }
            else
            {
                // A plain inner declaration masks the outer box from block entry,
                // including reads before its textual initialization.
                var shadow = _ctx.Locals.DeclareLocal(name, _ctx.Types.Object, token);
                IL.Emit(OpCodes.Stloc, shadow);
                _ctx.LoopBodyCellShadows[name] = token;
            }
        }
    }

    private void ExitLoopBodyCellScope(List<LoopBodyCellScopeEntry> prior)
    {
        for (int i = prior.Count - 1; i >= 0; i--)
        {
            var entry = prior[i];
            if (entry.Cell is { } cell) _ctx.CellBindingLocals[entry.Name] = cell;
            else _ctx.CellBindingLocals.Remove(entry.Name);
            if (entry.Declaration is { } declaration) _ctx.LoopBodyCellDeclarations[entry.Name] = declaration;
            else _ctx.LoopBodyCellDeclarations.Remove(entry.Name);
            if (entry.Shadow is { } shadow) _ctx.LoopBodyCellShadows[entry.Name] = shadow;
            else _ctx.LoopBodyCellShadows.Remove(entry.Name);
        }
    }

    private static IEnumerable<Token> DirectLexicalDeclarations(IEnumerable<Stmt> statements)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case Stmt.Var { IsVar: false } variable: yield return variable.Name; break;
                case Stmt.Const constant: yield return constant.Name; break;
                case Stmt.Sequence sequence:
                    foreach (var token in DirectLexicalDeclarations(sequence.Statements)) yield return token;
                    break;
            }
        }
    }

    private bool TryInitializeLoopBodyBinding(Stmt.Var declaration)
    {
        string name = declaration.Name.Lexeme;
        bool cell = _ctx.LoopBodyCellDeclarations.TryGetValue(name, out var token)
            && ReferenceEquals(token, declaration.Name);
        bool shadow = _ctx.LoopBodyCellShadows.TryGetValue(name, out token)
            && ReferenceEquals(token, declaration.Name);
        if (!cell && !shadow) return false;

        if (cell) IL.Emit(OpCodes.Ldloc, _ctx.CellBindingLocals[name]);
        if (declaration.Initializer is { } initializer)
        {
            EmitExpression(initializer);
            EnsureBoxed();
        }
        else IL.Emit(OpCodes.Ldsfld, _ctx.Runtime!.Sentinels.UndefinedInstance);
        if (cell) IL.Emit(OpCodes.Stfld, _ctx.Types.StrongBoxOfObjectValueField);
        else
        {
            var local = _ctx.Locals.GetLocal(name)!;
            IL.Emit(OpCodes.Stloc, local);
            RefreshLexicalCaptureSnapshots(name, local);
        }
        return true;
    }

    private void RefreshLexicalCaptureSnapshots(string name, LocalBuilder binding)
    {
        if (!_ctx.LexicalCaptureWriteBacks.TryGetValue(name, out var writeBacks)) return;
        foreach (var (displayClass, field) in writeBacks)
        {
            IL.Emit(OpCodes.Ldloc, displayClass);
            IL.Emit(OpCodes.Ldloc, binding);
            IL.Emit(OpCodes.Stfld, field);
        }
    }

    private bool TryEmitLoopBodyCellReference(Expr.ArrowFunction closure, string name)
    {
        if (_ctx.ClosureAnalyzer?.TryGetLoopBodyCellCapture(closure, name, out var declaration) != true)
            return false;
        if (_ctx.LoopBodyCellDeclarations.TryGetValue(name, out var active)
            && ReferenceEquals(active, declaration))
            IL.Emit(OpCodes.Ldloc, _ctx.CellBindingLocals[name]);
        else if (_ctx.LoopBodyCellCaptures?.TryGetValue(name, out var captured) == true
            && ReferenceEquals(captured, declaration)
            && _ctx.CapturedFields?.TryGetValue(name, out var field) == true)
        {
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, field);
        }
        else throw new InvalidOperationException($"Missing lexical cell for capture '{name}'.");
        return true;
    }

    protected override void EmitStoreVariable(string name)
    {
        if (_ctx.HasLoopBodyBindingAccess(name))
        {
            if (!_resolver.TryStoreVariable(name))
                throw new InvalidOperationException($"Missing lexical storage for '{name}'.");
            return;
        }
        base.EmitStoreVariable(name);
    }
}
