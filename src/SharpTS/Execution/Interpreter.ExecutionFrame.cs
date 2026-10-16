using System.Threading;
using SharpTS.Runtime;
using SharpTS.Runtime.Types;

namespace SharpTS.Execution;

public partial class Interpreter
{
    // Continuations share lexical environments, but must select their active
    // environment independently. Replacing this immutable frame also isolates
    // sibling async flows that inherit the same ExecutionContext value.
    private sealed record ExecutionFrame(RuntimeEnvironment Environment,
        Func<object?, bool, object?>? YieldCallback = null,
        SharpTSAsyncGenerator? AsyncGenerator = null);

    private readonly AsyncLocal<ExecutionFrame?> _executionFrame = new();
    private readonly ExecutionFrame _rootExecutionFrame = new(new RuntimeEnvironment());
    private ExecutionFrame CurrentExecutionFrame
    {
        get => _executionFrame.Value ?? _rootExecutionFrame;
        // Scope guards can unwind after disposal. Do not reinstall a retained
        // environment or generator callback into that execution context.
        set => _executionFrame.Value = _isDisposed ? null : value;
    }

    // Keep the existing scope facade so synchronous and asynchronous evaluators
    // select the same frame without copying the retained lexical environment.
    private RuntimeEnvironment _environment
    {
        get => CurrentExecutionFrame.Environment;
        set => CurrentExecutionFrame = CurrentExecutionFrame with { Environment = value };
    }
}
