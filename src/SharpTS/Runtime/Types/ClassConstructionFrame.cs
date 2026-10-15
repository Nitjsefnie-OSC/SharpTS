using SharpTS.Execution;
using SharpTS.Runtime.Exceptions;

namespace SharpTS.Runtime.Types;

/// <summary>Invocation-local state for one lexical class constructor level.</summary>
internal sealed class ClassConstructionFrame(
    SharpTSClass owner, object allocation, object? newTarget, bool retryingFailedSuper = false)
{
    internal enum ElementProgress { NotStarted, Started, Complete }

    internal SharpTSClass Owner { get; } = owner;
    internal object Allocation { get; } = allocation;
    internal object Receiver { get; private set; } = allocation;
    internal object? NewTarget { get; } = newTarget;
    internal RuntimeEnvironment? ThisBindingHome { get; private set; }
    internal bool SuperCompleted { get; private set; }
    internal bool ThisBound { get; private set; } = owner.Superclass is null;
    internal ElementProgress Elements { get; private set; }
    internal object? ParentResult { get; private set; }
    internal bool RetryingFailedSuper { get; } = retryingFailedSuper;
    private int _superAttempts;
    private Action? _initializeParameterProperties;

    internal void AttachThisBinding(RuntimeEnvironment environment) => ThisBindingHome = environment;

    internal void SetParameterPropertyInitializer(Action initialize)
    {
        _initializeParameterProperties = initialize;
        // A parameter default can complete super before the body prologue.
        // Parameter values must be bound before their synthesized assignments.
        if (SuperCompleted) InitializeParameterProperties();
    }

    private void InitializeParameterProperties()
    {
        var initialize = _initializeParameterProperties;
        _initializeParameterProperties = null;
        initialize?.Invoke();
    }

    internal void EnsureThisBound()
    {
        if (!ThisBound)
            throw new ThrowException(new SharpTSReferenceError("Must call super constructor before accessing 'this'"));
    }

    internal bool BeginSuperAttempt()
    {
        if (SuperCompleted)
            throw new ThrowException(new SharpTSReferenceError("Super constructor may only be called once"));
        // A caught parent failure may legitimately retry. Until fresh parent
        // allocations are supported (#2283), replace only that new attempt's
        // existing owner storage rather than leaking CWT duplicate-key errors.
        return _superAttempts++ != 0 || RetryingFailedSuper;
    }

    internal object? CompleteSuper(Interpreter interpreter, object? parentResult)
    {
        SuperCompleted = true;
        Receiver = parentResult ?? throw new InvalidOperationException("Parent construction must select an object receiver.");
        ThisBindingHome?.DefineRV("this", RuntimeValue.FromBoxed(Receiver));
        ThisBound = true;
        ParentResult = parentResult;
        InitializeOwnElements(interpreter);
        InitializeParameterProperties();
        return Receiver;
    }

    internal void InitializeOwnElements(Interpreter interpreter)
    {
        // Mark before evaluating any initializer. A caught initializer throw leaves
        // its earlier elements installed and must not retry this owner's work.
        if (Elements != ElementProgress.NotStarted)
            throw new ThrowException(new SharpTSReferenceError("Class instance elements have already been initialized"));
        Elements = ElementProgress.Started;
        Owner.InitializeOwnInstanceElements(interpreter, Receiver,
            RetryingFailedSuper && ReferenceEquals(Receiver, Allocation));
        Elements = ElementProgress.Complete;
    }
}
