using System.Reflection.Emit;
using System.Reflection;
using SharpTS.Parsing;

namespace SharpTS.Compilation;

public abstract partial class ExpressionEmitterBase
{
    protected bool IsRuntimeClassDeclarationReference(Expr expression)
    {
        if (expression is not Expr.Variable || Ctx.RuntimeClassDeclarations == null || Ctx.TypeMap == null)
            return false;
        static int DeclarationId(SharpTS.TypeSystem.TypeInfo? type) => type switch
        {
            SharpTS.TypeSystem.TypeInfo.Class cls => cls.Core.DeclarationId,
            SharpTS.TypeSystem.TypeInfo.GenericClass cls => cls.Core.DeclarationId,
            SharpTS.TypeSystem.TypeInfo.InstantiatedGeneric generic => DeclarationId(generic.GenericDefinition),
            _ => 0
        };
        int declarationId = DeclarationId(Ctx.TypeMap.Get(expression));
        return declarationId != 0 && Ctx.RuntimeClassDeclarations.Keys.Any(declaration =>
            Ctx.TypeMap.GetClassType(declaration)?.Core.DeclarationId == declarationId);
    }

    protected void EmitRuntimeClassConstruction(Expr.New expression)
    {
        var constructor = SpillBoxed(expression.Callee);
        EmitArgsArrayWithSpread(expression.Arguments);
        var arguments = IL.DeclareLocal(Types.ObjectArray);
        IL.Emit(OpCodes.Stloc, arguments);
        IL.Emit(OpCodes.Ldloc, constructor);
        IL.Emit(OpCodes.Ldloc, arguments);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.DynamicConstruction.Value);
        SetStackUnknown();
    }

    protected void PublishRuntimeClassBinding(Stmt.Class declaration, string name, LocalBuilder definition)
    {
        bool shadowsOuter = Ctx.RuntimeClassLexicalShadows.Contains(declaration);
        if (!shadowsOuter && Ctx.CellBindingLocals.TryGetValue(name, out var cell))
        {
            IL.Emit(OpCodes.Ldloc, cell);
            IL.Emit(OpCodes.Ldloc, definition);
            IL.Emit(OpCodes.Stfld, Types.StrongBoxOfObjectValueField);
        }
        else if (!shadowsOuter && Ctx.CapturedFunctionLocals?.Contains(name) == true
            && Ctx.FunctionDisplayClassFields?.TryGetValue(name, out var functionField) == true)
        {
            if (Ctx.FunctionDisplayClassLocal != null)
                IL.Emit(OpCodes.Ldloc, Ctx.FunctionDisplayClassLocal);
            else if (GetFunctionDCField() is { } stateField)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, stateField);
            }
            else if (Ctx.CurrentArrowFunctionDCField != null)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Ctx.CurrentArrowFunctionDCField);
            }
            else return;
            IL.Emit(OpCodes.Ldloc, definition);
            IL.Emit(OpCodes.Stfld, functionField);
        }
        else if (!shadowsOuter && Ctx.CapturedArrowLocals?.Contains(name) == true
            && Ctx.ArrowScopeDisplayClassFields?.TryGetValue(name, out var arrowField) == true
            && Ctx.ArrowScopeDisplayClassLocal != null)
        {
            IL.Emit(OpCodes.Ldloc, Ctx.ArrowScopeDisplayClassLocal);
            IL.Emit(OpCodes.Ldloc, definition);
            IL.Emit(OpCodes.Stfld, arrowField);
        }
        else if (!shadowsOuter && Ctx.IsModuleTopLevel && Ctx.CapturedTopLevelVars?.Contains(name) == true
            && Ctx.EntryPointDisplayClassFields?.TryGetValue(name, out var entryField) == true)
        {
            if (Ctx.EntryPointDisplayClassLocal != null) IL.Emit(OpCodes.Ldloc, Ctx.EntryPointDisplayClassLocal);
            else if (Ctx.EntryPointDisplayClassStaticField != null) IL.Emit(OpCodes.Ldsfld, Ctx.EntryPointDisplayClassStaticField);
            else return;
            IL.Emit(OpCodes.Ldloc, definition);
            IL.Emit(OpCodes.Stfld, entryField);
        }

        if (Ctx.LexicalCaptureWriteBacks.TryGetValue(name, out var writeBacks))
        {
            int start = Ctx.RuntimeClassCaptureWriteBackStarts.GetValueOrDefault(declaration);
            foreach (var (displayClass, field) in writeBacks.Skip(start))
            {
                IL.Emit(OpCodes.Ldloc, displayClass);
                IL.Emit(OpCodes.Ldloc, definition);
                IL.Emit(OpCodes.Stfld, field);
            }
        }
    }

    protected bool TryEmitGuestSuperCall(Expr.Call call)
    {
        if ((!Ctx.HasGuestReceiver && !Ctx.IsStaticSuperContext) || call.Callee is not Expr.Super { Method: not null } super
            || (!Ctx.IsStaticSuperContext && super.Method.Lexeme == "constructor")) return false;
        // Nominal declaration helpers use the existing lexical-class bridge,
        // which keeps the actual receiver and bypasses virtual overrides.
        if (Ctx.UseNominalSuperCallBridge && Ctx.CurrentClassExpr == null && Ctx.CurrentClassBuilder != null
            && !Ctx.IsStaticSuperContext && TryEmitSuperMethodCall(super.Method.Lexeme, call.Arguments))
            return true;
        var receiver = SpillBoxed(new Expr.This(new Token(TokenType.THIS, "this", null, 0)));
        EmitGuestSuperValue(super.Method.Lexeme, receiver);
        EmitGuestMethodInvocation(receiver, call.Arguments);
        return true;
    }

    protected void EmitGuestSuperValue(string name, LocalBuilder receiver)
    {
        if (Ctx.CurrentClassExpr != null && TryEmitOwnedClassDefinition()) { }
        else
        {
            IL.Emit(OpCodes.Ldtoken, Ctx.CurrentClassBuilder!);
            IL.Emit(OpCodes.Call, Types.TypeGetTypeFromHandle);
        }
        IL.Emit(OpCodes.Ldloc, receiver);
        IL.Emit(OpCodes.Ldstr, name);
        IL.Emit(Ctx.IsStaticSuperContext ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ClassDefinitions.ReadSuper);
        SetStackUnknown();
        EmitUnchangedSuperMethodValue(name, receiver);
    }

    private void EmitUnchangedSuperMethodValue(string name, LocalBuilder receiver)
    {
        if (Ctx.IsStaticSuperContext || Ctx.CurrentClassExpr != null
            || Ctx.CurrentClassBuilder is not { } owner || Ctx.ClassRegistry is not { } registry) return;
        string? superclass = Ctx.CurrentSuperclassName
            ?? registry.GetSuperclass(owner.FullName ?? Ctx.CurrentClassName!)
            ?? (Ctx.CurrentClassName is { } current ? registry.GetSuperclass(current) : null);
        if (superclass == null || Ctx.ResolveInstanceMethod(Ctx.ResolveClassName(superclass), name) is not { } parent)
            return;

        // Legacy nominal prototype methods wrap a virtual CLR MethodInfo. Keep
        // that unchanged method value nonvirtual, while live data, getters and
        // replacements retain the value returned by the prototype lookup.
        var value = _helpers.SpillStoreObject();
        var function = IL.DeclareLocal(Ctx.Runtime!.FunctionValues.Type);
        var unchanged = IL.DefineLabel();
        IL.Emit(OpCodes.Ldloc, value);
        IL.Emit(OpCodes.Isinst, Ctx.Runtime.FunctionValues.Type);
        IL.Emit(OpCodes.Stloc, function);
        IL.Emit(OpCodes.Ldloc, function);
        IL.Emit(OpCodes.Brfalse, unchanged);
        IL.Emit(OpCodes.Ldloc, function);
        IL.Emit(OpCodes.Callvirt, Ctx.Runtime.FunctionValues.GetMethodInfo);
        IL.Emit(OpCodes.Callvirt, Types.GetProperty(Types.MethodInfo, "Module")!.GetGetMethod()!);
        IL.Emit(OpCodes.Ldtoken, owner);
        IL.Emit(OpCodes.Call, Types.TypeGetTypeFromHandle);
        IL.Emit(OpCodes.Callvirt, Types.GetProperty(Types.Type, "Module")!.GetGetMethod()!);
        IL.Emit(OpCodes.Call, Types.GetMethod(Types.Object, "Equals", Types.Object, Types.Object));
        IL.Emit(OpCodes.Brfalse, unchanged);
        IL.Emit(OpCodes.Ldloc, function);
        IL.Emit(OpCodes.Callvirt, Ctx.Runtime.FunctionValues.GetMethodInfo);
        IL.Emit(OpCodes.Callvirt, Types.GetProperty(Types.MethodInfo, "MetadataToken")!.GetGetMethod()!);
        IL.Emit(OpCodes.Ldc_I4, parent.MetadataToken);
        IL.Emit(OpCodes.Bne_Un, unchanged);
        var (_, bridge) = registry.GetOrCreateSuperCallBridge(owner, parent, Ctx.Runtime.FunctionAttributes);
        IL.Emit(OpCodes.Ldloc, receiver);
        Types.EmitLoadMethodInfo(IL, bridge);
        IL.Emit(OpCodes.Ldstr, name);
        IL.Emit(OpCodes.Ldloc, function);
        IL.Emit(OpCodes.Callvirt, Ctx.Runtime.FunctionValues.LengthGetter);
        IL.Emit(OpCodes.Newobj, Ctx.Runtime.FunctionConstruction.CachedConstructor);
        IL.Emit(OpCodes.Dup);
        IL.Emit(OpCodes.Ldloc, function);
        IL.Emit(OpCodes.Ldfld, Ctx.Runtime.FunctionValues.CapturesArgumentsField);
        IL.Emit(OpCodes.Stfld, Ctx.Runtime.FunctionValues.CapturesArgumentsField);
        IL.Emit(OpCodes.Stloc, value);
        IL.MarkLabel(unchanged);
        IL.Emit(OpCodes.Ldloc, value);
        SetStackUnknown();
    }

    protected bool TryEmitGuestThisSet(Expr.Set set)
    {
        if (set.Object is not Expr.This || (!Ctx.HasGuestReceiver && Ctx.GuestThisVariableName == null)) return false;
        var receiver = SpillBoxed(set.Object);
        var value = SpillBoxed(set.Value);
        IL.Emit(OpCodes.Ldloc, receiver); IL.Emit(OpCodes.Ldstr, set.Name.Lexeme); IL.Emit(OpCodes.Ldloc, value);
        IL.Emit(OpCodes.Ldc_I4_1);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ObjectWrite.PropertyStrict);
        IL.Emit(OpCodes.Ldloc, value);
        SetStackUnknown();
        return true;
    }

    protected bool TryEmitGuestThisCall(Expr.Call call)
    {
        if (call.Callee is not Expr.Get { Object: Expr.This, Optional: false } get || call.Optional
            || (!Ctx.HasGuestReceiver && Ctx.GuestThisVariableName == null)) return false;
        var receiver = SpillBoxed(get.Object);
        IL.Emit(OpCodes.Ldloc, receiver); IL.Emit(OpCodes.Ldstr, get.Name.Lexeme);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ObjectRead.Property);
        EmitGuestMethodInvocation(receiver, call.Arguments);
        return true;
    }

    protected void EmitGuestMethodInvocation(LocalBuilder receiver, IReadOnlyList<Expr> argumentExpressions)
    {
        var callable = _helpers.SpillStoreObject();
        EmitArgsArrayWithSpread(argumentExpressions);
        var arguments = _helpers.SpillStoreObject();
        IL.Emit(OpCodes.Ldloc, receiver); IL.Emit(OpCodes.Ldloc, callable); IL.Emit(OpCodes.Ldloc, arguments);
        IL.Emit(OpCodes.Castclass, Types.ObjectArray); IL.Emit(OpCodes.Call, Ctx.Runtime!.Invocation.Method);
        SetStackUnknown();
    }

    protected bool TryEmitClassDefinitionSelf(Expr.Variable variable)
    {
        string name = variable.Name.Lexeme;
        var expression = Ctx.CurrentClassExpr ?? Ctx.ClassExprBuilders?
            .FirstOrDefault(entry => ReferenceEquals(entry.Value, Ctx.CurrentClassBuilder)).Key;
        if (expression?.Name?.Lexeme == name)
        {
            if (HasClassDefinitionCaptureShadow(name)) return false;
            if (!TryEmitOwnedClassDefinition()) return false;
        }
        else
        {
            // A named enclosing expression has no outer variable declaration to
            // capture. Its checked constructor binding identifies the lexical
            // self-name, whose evaluated definition travels in the owner chain.
            if (HasClassDefinitionCaptureShadow(name)
                || TryGetClassDefinitionCaptureSlot(name, out _)) return false;
            var constructor = Ctx.TypeMap?.Get(variable);
            if (constructor is not (SharpTS.TypeSystem.TypeInfo.Class or SharpTS.TypeSystem.TypeInfo.GenericClass))
                return false;
            string owner = Ctx.ResolveClassName(constructor, name);
            if (Ctx.EnclosingClassNames?.Contains(owner) != true
                || Ctx.ClassExprBuilders?.Any(entry => entry.Value.FullName == owner
                    && entry.Key.Name?.Lexeme == name) != true
                || !TryEmitPrivateInOwnerDefinition(owner)) return false;
        }
        SetStackUnknown();
        return true;
    }

    protected bool TryEmitOwnedClassDefinition()
    {
        if (Ctx.ClassDefinitionOwnerField is { } ownerField)
        {
            IL.Emit(OpCodes.Ldarg_0);
            if (GetThisField() is { } stateReceiver)
            {
                IL.Emit(OpCodes.Ldfld, stateReceiver);
                IL.Emit(OpCodes.Castclass, ownerField.DeclaringType!);
            }
            IL.Emit(OpCodes.Ldfld, ownerField);
        }
        else if (Ctx.ClassDefinitionVariableName is { } variable)
            EmitVariable(new Expr.Variable(new Token(TokenType.IDENTIFIER, variable, null, 0)));
        else if (Ctx.ClassDefinitionParameterIndex is { } parameter)
            IL.Emit(OpCodes.Ldarg, parameter);
        else
        {
            var expression = Ctx.CurrentClassExpr ?? Ctx.ClassExprBuilders?
                .FirstOrDefault(entry => ReferenceEquals(entry.Value, Ctx.CurrentClassBuilder)).Key;
            if (!Ctx.IsInstanceMethod || expression == null ||
                Ctx.ClassExprDefinitionFields?.TryGetValue(expression, out var field) != true) return false;
            EmitThis();
            // Read the lexical owner's field: a derived instance can carry a
            // distinct child definition while executing this base method.
            var reference = EmitterTypeHelpers.SelfFieldReference(field!);
            if (field!.DeclaringType!.IsGenericTypeDefinition && Ctx.ClassExprFactories?.TryGetValue(expression, out var factory) == true)
                reference = EmitterTypeHelpers.ResolveField(factory.Template, field);
            IL.Emit(OpCodes.Castclass, reference.DeclaringType!);
            IL.Emit(OpCodes.Ldfld, reference);
        }
        IL.Emit(OpCodes.Castclass, Ctx.Runtime!.ClassDefinitions.Type);
        SetStackUnknown();
        return true;
    }

    protected bool TryEmitGuestThis()
    {
        if (Ctx.GuestThisVariableName is not { } name) return false;
        EmitVariable(new Expr.Variable(new Token(TokenType.IDENTIFIER, name, null, 0)));
        EnsureBoxed();
        return true;
    }

    protected bool TryEmitGuestThisGet(Expr.Get get)
    {
        if (get.Object is not Expr.This || get.Optional) return false;
        if (!TryEmitGuestThis())
        {
            if (!Ctx.HasGuestReceiver) return false;
            EmitThis();
            EnsureBoxed();
        }
        IL.Emit(OpCodes.Ldstr, get.Name.Lexeme);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ObjectRead.Property);
        SetStackUnknown();
        return true;
    }

    protected bool TryEmitClassDefinitionCapture(string name)
    {
        if (!TryGetClassDefinitionCaptureSlot(name, out var slot) || !TryEmitOwnedClassDefinition()) return false;
        IL.Emit(OpCodes.Ldfld, Ctx.Runtime!.ClassDefinitions.Captures);
        IL.Emit(OpCodes.Ldc_I4, slot);
        IL.Emit(OpCodes.Ldelem_Ref);
        IL.Emit(OpCodes.Call, Ctx.Runtime.ClassDefinitions.ReadCapture);
        Ctx.EmitLexicalTdzValueCheck(IL, name);
        SetStackUnknown();
        return true;
    }

    protected bool TryGetClassDefinitionCaptureSlot(string name, out int slot)
    {
        slot = -1;
        if (HasClassDefinitionCaptureShadow(name)) return false;
        var expression = Ctx.CurrentClassExpr ?? Ctx.ClassExprBuilders?
            .FirstOrDefault(entry => ReferenceEquals(entry.Value, Ctx.CurrentClassBuilder)).Key;
        if (expression == null || Ctx.ClassExprCaptureSlots?.TryGetValue(expression, out var captures) != true
            || !captures!.TryGetValue(name, out slot)) return false;
        return true;
    }

    protected bool HasClassDefinitionCaptureShadow(string name)
    {
        if (Ctx.TryGetParameter(name, out _) || Ctx.Locals.GetLocal(name) != null
            || Ctx.CellBindingLocals.ContainsKey(name) || GetHoistedVariableField(name) != null
            || Ctx.CapturedFields?.ContainsKey(name) == true) return true;

        // Restoring an arrow's lexical class must not hide its live closure
        // storage. Shared captures can live behind a display-class reference
        // instead of a same-named field on the arrow itself.
        string functionName = Ctx.CurrentArrowFunctionDCFieldRenames?.GetValueOrDefault(name) ?? name;
        if (Ctx.CapturedFunctionLocals?.Contains(functionName) == true
            && Ctx.FunctionDisplayClassFields?.ContainsKey(functionName) == true
            && (Ctx.FunctionDisplayClassLocal != null || Ctx.CurrentArrowFunctionDCField != null)) return true;
        if (Ctx.CapturedArrowLocals?.Contains(name) == true
            && Ctx.ArrowScopeDisplayClassFields?.ContainsKey(name) == true
            && Ctx.ArrowScopeDisplayClassLocal != null) return true;
        if (Ctx.ParentArrowCapturedLocals?.Contains(name) == true
            && Ctx.ParentArrowScopeDisplayClassFields?.ContainsKey(name) == true
            && Ctx.CurrentArrowScopeDCField != null) return true;
        if (Ctx.ExtraArrowScopeBindings?.ContainsKey(name) == true) return true;
        return Ctx.CurrentArrowEntryPointDCField != null
            && Ctx.CapturedTopLevelVars?.Contains(name) == true
            && Ctx.EntryPointDisplayClassFields?.ContainsKey(name) == true;
    }

    protected bool TryEmitStoreClassDefinitionCapture(string name)
    {
        if (!TryGetClassDefinitionCaptureSlot(name, out var slot)) return false;
        var value = IL.DeclareLocal(Types.Object);
        IL.Emit(OpCodes.Stloc, value);
        if (!TryEmitOwnedClassDefinition()) throw new InvalidOperationException("Capture has no class definition owner.");
        IL.Emit(OpCodes.Ldc_I4, slot);
        IL.Emit(OpCodes.Ldloc, value);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ClassDefinitions.WriteCapture);
        return true;
    }

    protected bool TryEmitClassDefinitionEnvironment(Type environment)
    {
        if (!TryEmitOwnedClassDefinition()) return false;
        IL.Emit(OpCodes.Ldtoken, environment);
        IL.Emit(OpCodes.Call, Types.TypeGetTypeFromHandle);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ClassDefinitions.FindEnvironment);
        IL.Emit(OpCodes.Castclass, environment);
        return true;
    }

    protected void EmitGuestClassDefinition(Expr.ClassExpr expression)
    {
        if (Ctx.ClassExprFactories?.TryGetValue(expression, out var factory) != true)
            throw new InvalidOperationException("Class expression factory has not been declared.");
        if (expression.SuperclassExpr is { } heritage)
        {
            EmitClassHeritageValue(heritage, expression.Name?.Lexeme);
            EnsureBoxed();
            if ((factory!.Template.BaseType == null || factory.Template.BaseType == Types.Object)
                && heritage is not Expr.ClassExpr { Methods.Count: 0, Fields.Count: 0 })
                IL.Emit(OpCodes.Call, Ctx.Runtime!.ClassDefinitions.ValidateParent);
        }
        else IL.Emit(OpCodes.Ldnull);
        var parent = _helpers.SpillStoreObject();
        IL.Emit(OpCodes.Ldtoken, factory!.Template);
        IL.Emit(OpCodes.Call, Types.TypeGetTypeFromHandle);
        IL.Emit(OpCodes.Call, Ctx.Runtime!.ClassInitialization.RunDefinition);
        var keys = IL.DeclareLocal(Types.ObjectArray);
        EmitDefinitionKeyArray(factory.Keys);
        IL.Emit(OpCodes.Stloc, keys);
        var captures = IL.DeclareLocal(Types.ObjectArray);
        EmitDefinitionCaptureArray(Ctx.ClassExprCaptureSlots!.GetValueOrDefault(expression)?.Keys.ToArray() ?? []);
        IL.Emit(OpCodes.Stloc, captures);
        IL.Emit(OpCodes.Ldtoken, factory.Template);
        IL.Emit(OpCodes.Call, Types.TypeGetTypeFromHandle);
        IL.Emit(OpCodes.Ldnull);
        IL.Emit(OpCodes.Ldftn, factory.Method);
        IL.Emit(OpCodes.Newobj, Types.GetConstructor(typeof(Func<object[], object, object, object>), Types.Object, Types.IntPtr));
        IL.Emit(OpCodes.Ldstr, factory.Name);
        IL.Emit(OpCodes.Ldc_R8, (double)factory.Length);
        IL.Emit(OpCodes.Ldloc, parent);
        IL.Emit(OpCodes.Ldloc, keys);
        IL.Emit(OpCodes.Ldloc, captures);
        IL.Emit(OpCodes.Call, Ctx.Runtime.ClassDefinitions.Create);
        IL.Emit(OpCodes.Dup);
        IL.Emit(OpCodes.Call, factory.Initializer);
        SetStackUnknown();
    }

    protected void EmitDefinitionComputedKeys(MethodBuilder method, IReadOnlyList<Expr> keys)
    {
        EmitDefinitionKeyArray(keys);
        IL.Emit(OpCodes.Call, method);
    }

    private void EmitDefinitionCaptureArray(IReadOnlyList<string> names)
    {
        var values = new List<LocalBuilder>(names.Count);
        foreach (var name in names)
        {
            if (name == CompilationContext.PrivateOwnerCaptureName)
            {
                if (!TryEmitOwnedClassDefinition())
                    throw new InvalidOperationException("Nested private names require their enclosing class evaluation.");
                values.Add(_helpers.SpillStoreObject());
                continue;
            }
            FieldInfo? field = null;
            if (Ctx.CellBindingLocals.TryGetValue(name, out var cell))
            {
                IL.Emit(OpCodes.Ldloc, cell);
                field = Types.StrongBoxOfObjectValueField;
            }
            else if (Ctx.FunctionDisplayClassFields?.TryGetValue(name, out var functionField) == true
                && Ctx.FunctionDisplayClassLocal != null)
            {
                IL.Emit(OpCodes.Ldloc, Ctx.FunctionDisplayClassLocal);
                field = functionField;
            }
            else if (Ctx.FunctionDisplayClassFields?.TryGetValue(name, out functionField) == true
                && GetFunctionDCField() is { } stateField)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, stateField);
                field = functionField;
            }
            else if (Ctx.FunctionDisplayClassFields?.TryGetValue(name, out functionField) == true
                && Ctx.CurrentArrowFunctionDCField != null)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Ctx.CurrentArrowFunctionDCField);
                field = functionField;
            }
            else if (Ctx.CapturedFields?.TryGetValue(name, out var capturedField) == true)
            {
                IL.Emit(OpCodes.Ldarg_0);
                field = capturedField;
            }
            else if (Ctx.EntryPointDisplayClassFields?.TryGetValue(name, out var entryField) == true
                && (Ctx.EntryPointDisplayClassLocal != null || Ctx.EntryPointDisplayClassStaticField != null))
            {
                if (Ctx.EntryPointDisplayClassLocal != null) IL.Emit(OpCodes.Ldloc, Ctx.EntryPointDisplayClassLocal);
                else IL.Emit(OpCodes.Ldsfld, Ctx.EntryPointDisplayClassStaticField!);
                field = entryField;
            }
            else if (Ctx.TopLevelStaticVars?.TryGetValue(name, out var staticField) == true)
            {
                IL.Emit(OpCodes.Ldnull);
                field = staticField;
            }
            if (field != null)
            {
                IL.Emit(OpCodes.Ldtoken, field);
                IL.Emit(OpCodes.Ldtoken, field.DeclaringType!);
                IL.Emit(OpCodes.Call, Types.GetMethod(Types.FieldInfo, "GetFieldFromHandle",
                    Types.Resolve("System.RuntimeFieldHandle"), Types.Resolve("System.RuntimeTypeHandle")));
                IL.Emit(OpCodes.Newobj, Ctx.Runtime!.ClassDefinitions.CaptureConstructor);
            }
            else if (TryGetClassDefinitionCaptureSlot(name, out var slot) && TryEmitOwnedClassDefinition())
            {
                IL.Emit(OpCodes.Ldfld, Ctx.Runtime!.ClassDefinitions.Captures);
                IL.Emit(OpCodes.Ldc_I4, slot);
                IL.Emit(OpCodes.Ldelem_Ref);
            }
            else
            {
                EmitVariable(new Expr.Variable(new Token(TokenType.IDENTIFIER, name, null, 0)));
                EnsureBoxed();
            }
            values.Add(_helpers.SpillStoreObject());
        }
        IL.Emit(OpCodes.Ldc_I4, values.Count);
        IL.Emit(OpCodes.Newarr, Types.Object);
        for (int i = 0; i < values.Count; i++)
        {
            IL.Emit(OpCodes.Dup);
            IL.Emit(OpCodes.Ldc_I4, i);
            IL.Emit(OpCodes.Ldloc, values[i]);
            IL.Emit(OpCodes.Stelem_Ref);
        }
    }

    protected void EmitDefinitionKeyArray(IReadOnlyList<Expr> keys, bool coerceKeys = true)
    {
        var values = new List<LocalBuilder>(keys.Count);
        foreach (var key in keys)
        {
            EmitExpression(key);
            EnsureBoxed();
            if (coerceKeys)
            {
                var isSymbol = IL.DefineLabel();
                IL.Emit(OpCodes.Dup);
                IL.Emit(OpCodes.Isinst, Ctx.Runtime!.Symbols.Type);
                IL.Emit(OpCodes.Brtrue, isSymbol);
                IL.Emit(OpCodes.Call, Ctx.Runtime.StringCoercion.ToJsString);
                IL.MarkLabel(isSymbol);
            }
            values.Add(_helpers.SpillStoreObject());
        }
        IL.Emit(OpCodes.Ldc_I4, values.Count);
        IL.Emit(OpCodes.Newarr, Types.Object);
        for (int i = 0; i < values.Count; i++)
        {
            IL.Emit(OpCodes.Dup);
            IL.Emit(OpCodes.Ldc_I4, i);
            IL.Emit(OpCodes.Ldloc, values[i]);
            IL.Emit(OpCodes.Stelem_Ref);
        }
    }
}
