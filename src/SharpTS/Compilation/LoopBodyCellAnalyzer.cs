using SharpTS.Parsing;
using SharpTS.Parsing.Visitors;

namespace SharpTS.Compilation;

/// <summary>
/// Selects captured block lexical declarations inside synchronous for-of and for bodies.
/// Identity is the original declaration token, rather than the callable's name set.
/// </summary>
internal sealed class LoopBodyCellAnalyzer : AstVisitorBase
{
    private sealed class Owner(object node, bool canOwnCells, bool canForward)
    {
        public object Node { get; } = node;
        public bool CanOwnCells { get; } = canOwnCells;
        public bool CanForward { get; } = canForward;
    }

    private sealed class Binding(Token name, Owner owner, bool candidate, Binding? shadowOf)
    {
        public Token Name { get; } = name;
        public Owner Owner { get; } = owner;
        public bool Candidate { get; } = candidate;
        public bool Eligible { get; set; } = candidate;
        public bool Captured { get; set; }
        public Binding? ShadowOf { get; } = shadowOf;
    }

    private readonly List<Owner> _owners = [];
    private readonly Stack<Dictionary<string, Binding>> _scopes = new();
    private readonly Dictionary<Token, Binding> _declarations = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, Dictionary<string, Binding>> _tentative = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Token> _cells = new(ReferenceEqualityComparer.Instance);
    private int _loopBodyDepth;
    private int _classDepth;

    public Dictionary<object, Dictionary<string, Token>> ClosureCaptures { get; } = new(ReferenceEqualityComparer.Instance);
    public IEnumerable<Token> CellDeclarations => _cells;
    public bool IsCellDeclaration(Token token) => _cells.Contains(token);

    public bool TryGetCaptureDeclaration(object closure, string name, out Token declaration)
    {
        if (ClosureCaptures.TryGetValue(closure, out var captures) && captures.TryGetValue(name, out declaration!))
            return true;
        declaration = null!;
        return false;
    }

    public void Analyze(List<Stmt> statements)
    {
        _owners.Clear(); _scopes.Clear(); _declarations.Clear(); _tentative.Clear(); _cells.Clear(); ClosureCaptures.Clear();
        _loopBodyDepth = 0;
        _classDepth = 0;
        _owners.Add(new(new object(), !HasDirectSuspension(statements), false));
        EnterScope(statements, allowCandidates: false, hoistVars: true);
        try { foreach (var statement in statements) Visit(statement); }
        finally { _scopes.Pop(); _owners.Clear(); }

        foreach (var binding in _declarations.Values)
            if (binding.Eligible && binding.Captured && binding.ShadowOf is null) _cells.Add(binding.Name);
        bool added;
        do
        {
            added = false;
            foreach (var binding in _declarations.Values)
                if (binding.Eligible && binding.Captured && binding.ShadowOf is { } parent
                    && _cells.Contains(parent.Name)) added |= _cells.Add(binding.Name);
        } while (added);
        foreach (var (closure, captures) in _tentative)
            foreach (var (name, binding) in captures)
                if (_cells.Contains(binding.Name))
                {
                    if (!ClosureCaptures.TryGetValue(closure, out var output))
                        ClosureCaptures[closure] = output = new(StringComparer.Ordinal);
                    output[name] = binding.Name;
                }
    }

    private Binding? VisibleBinding(string name)
    {
        foreach (var scope in _scopes)
            if (scope.TryGetValue(name, out var binding)) return binding;
        return null;
    }

    private void Declare(Token name, bool candidate = false, bool unsupportedShadow = false, Binding? shadowOf = null)
    {
        if (unsupportedShadow)
            foreach (var scope in _scopes)
                if (scope.TryGetValue(name.Lexeme, out var outer))
                {
                    if (outer.Candidate && !ReferenceEquals(outer.Name, name)) outer.Eligible = false;
                    break;
                }
        if (!_declarations.TryGetValue(name, out var binding))
            _declarations[name] = binding = new(name, _owners[^1], candidate, shadowOf);
        _scopes.Peek()[name.Lexeme] = binding;
    }

    private void EnterScope(IEnumerable<Stmt> statements, bool allowCandidates, bool hoistVars = false, bool unsupportedShadows = false)
    {
        _scopes.Push(new(StringComparer.Ordinal));
        if (hoistVars)
        {
            var vars = new VarCollector();
            foreach (var statement in statements) vars.Visit(statement);
            foreach (var token in vars.Names) Declare(token);
        }
        foreach (var statement in statements) Predeclare(statement, allowCandidates, unsupportedShadows);
    }

    private void Predeclare(Stmt statement, bool allowCandidates, bool unsupportedShadows)
    {
        void Lexical(Token name)
        {
            bool direct = allowCandidates && _loopBodyDepth > 0 && _owners[^1].CanOwnCells;
            var parent = !direct && allowCandidates && _owners[^1].CanOwnCells
                ? VisibleBinding(name.Lexeme) : null;
            bool relatedShadow = parent?.Candidate == true;
            // A callback's inner shadow also needs a cell when it masks an actual
            // outer cell. Final selection waits for that parent's selected status.
            Declare(name, direct || relatedShadow, unsupportedShadows, relatedShadow ? parent : null);
        }
        switch (statement)
        {
            case Stmt.Var variable when !variable.IsVar: Lexical(variable.Name); break;
            case Stmt.Const constant: Lexical(constant.Name); break;
            case Stmt.Function function: Declare(function.Name, unsupportedShadow: true); break;
            case Stmt.Class @class: Declare(@class.Name, unsupportedShadow: true); break;
            case Stmt.Enum @enum: Declare(@enum.Name, unsupportedShadow: true); break;
            case Stmt.Namespace @namespace: Declare(@namespace.Name, unsupportedShadow: true); break;
            case Stmt.Using resource:
                foreach (var binding in resource.Bindings)
                    if (binding.Name is { } name) Declare(name, unsupportedShadow: true);
                    else foreach (var scope in _scopes)
                        foreach (var outer in scope.Values) outer.Eligible = false;
                break;
            case Stmt.Sequence sequence:
                foreach (var child in sequence.Statements) Predeclare(child, allowCandidates, unsupportedShadows);
                break;
        }
    }

    private void Reference(string name)
    {
        var binding = VisibleBinding(name);
        if (binding is null || !binding.Candidate || ReferenceEquals(binding.Owner, _owners[^1])) return;

        binding.Captured = true;
        for (int index = _owners.Count - 1; index >= 0 && !ReferenceEquals(_owners[index], binding.Owner); index--)
        {
            var owner = _owners[index];
            if (!owner.CanForward) { binding.Eligible = false; continue; }
            if (!_tentative.TryGetValue(owner.Node, out var captures))
                _tentative[owner.Node] = captures = new(StringComparer.Ordinal);
            if (captures.TryGetValue(name, out var previous) && !ReferenceEquals(previous, binding))
            {
                // One name-keyed closure field cannot hold two lexical declarations.
                previous.Eligible = false;
                binding.Eligible = false;
            }
            else captures[name] = binding;
        }
    }

    protected override void VisitVariable(Expr.Variable expr) => Reference(expr.Name.Lexeme);
    protected override void VisitAssign(Expr.Assign expr) { Reference(expr.Name.Lexeme); base.VisitAssign(expr); }
    protected override void VisitCompoundAssign(Expr.CompoundAssign expr) { Reference(expr.Name.Lexeme); base.VisitCompoundAssign(expr); }
    protected override void VisitLogicalAssign(Expr.LogicalAssign expr) { Reference(expr.Name.Lexeme); base.VisitLogicalAssign(expr); }

    protected override void VisitBlock(Stmt.Block stmt)
    {
        EnterScope(stmt.Statements, allowCandidates: true);
        try { base.VisitBlock(stmt); }
        finally { _scopes.Pop(); }
    }

    protected override void VisitForOf(Stmt.ForOf stmt)
    {
        Visit(stmt.Iterable); // Iterator acquisition precedes the loop binding's scope.
        _scopes.Push(new(StringComparer.Ordinal));
        if (stmt.IsDeclaration && !stmt.IsVar) Declare(stmt.Variable, unsupportedShadow: true);
        else if (!stmt.IsDeclaration) Reference(stmt.Variable.Lexeme);
        try { VisitLoopBody(stmt.Body, !stmt.IsAsync); }
        finally { _scopes.Pop(); }
    }

    protected override void VisitForIn(Stmt.ForIn stmt)
    {
        Visit(stmt.Object);
        _scopes.Push(new(StringComparer.Ordinal));
        if (stmt.IsDeclaration && !stmt.IsVar) Declare(stmt.Variable, unsupportedShadow: true);
        else if (!stmt.IsDeclaration) Reference(stmt.Variable.Lexeme);
        try { Visit(stmt.Body); }
        finally { _scopes.Pop(); }
    }

    protected override void VisitFor(Stmt.For stmt)
    {
        EnterScope(stmt.Initializer is null ? [] : [stmt.Initializer], allowCandidates: false, unsupportedShadows: true);
        try
        {
            // Header bindings keep the separate copy-forward cell policy (#650).
            if (stmt.Initializer is { } initializer) Visit(initializer);
            if (stmt.Condition is { } condition) Visit(condition);
            if (stmt.Increment is { } increment) Visit(increment);
            VisitLoopBody(stmt.Body);
        }
        finally { _scopes.Pop(); }
    }

    private void VisitLoopBody(Stmt body, bool synchronous = true)
    {
        int previousDepth = _loopBodyDepth;
        _loopBodyDepth = synchronous && _owners[^1].CanOwnCells && !HasDirectSuspension([body])
            ? previousDepth + 1 : 0;
        try { Visit(body); }
        finally { _loopBodyDepth = previousDepth; }
    }

    protected override void VisitTryCatch(Stmt.TryCatch stmt)
    {
        VisitStatementScope(stmt.TryBlock);
        if (stmt.CatchBlock is { } catchBlock)
        {
            EnterScope(catchBlock, allowCandidates: false, unsupportedShadows: true);
            if (stmt.CatchParam is { } parameter) Declare(parameter, unsupportedShadow: true);
            try { foreach (var statement in catchBlock) Visit(statement); }
            finally { _scopes.Pop(); }
        }
        if (stmt.FinallyBlock is { } finallyBlock) VisitStatementScope(finallyBlock);
    }

    private void VisitStatementScope(List<Stmt> statements)
    {
        // These AST lists are not emitted through EmitBlock's cell prologue.
        EnterScope(statements, allowCandidates: false, unsupportedShadows: true);
        try { foreach (var statement in statements) Visit(statement); }
        finally { _scopes.Pop(); }
    }

    protected override void VisitSwitch(Stmt.Switch stmt)
    {
        Visit(stmt.Subject);
        var statements = stmt.Cases.SelectMany(c => c.Body).Concat(stmt.DefaultBody ?? []).ToList();
        EnterScope(statements, allowCandidates: false, unsupportedShadows: true);
        try
        {
            foreach (var @case in stmt.Cases)
            {
                Visit(@case.Value);
                foreach (var statement in @case.Body) Visit(statement);
            }
            if (stmt.DefaultBody is { } defaults) foreach (var statement in defaults) Visit(statement);
        }
        finally { _scopes.Pop(); }
    }

    protected override void VisitArrowFunction(Expr.ArrowFunction expr)
    {
        VisitCallable(expr, expr.Parameters, expr.BlockBody ?? [], expr.ExpressionBody,
            !expr.IsAsync && !expr.IsGenerator, expr.HasOwnThis ? expr.Name : null);
    }

    protected override void VisitFunction(Stmt.Function stmt)
    {
        if (stmt.ComputedKey is { } key) Visit(key);
        VisitCallable(stmt, stmt.Parameters, stmt.Body ?? [], null,
            !stmt.IsAsync && !stmt.IsGenerator, stmt.Name);
    }

    private void VisitCallable(object node, List<Stmt.Parameter> parameters, List<Stmt> body,
        Expr? expressionBody, bool synchronous, Token? self)
    {
        int previousDepth = _loopBodyDepth;
        _loopBodyDepth = 0;
        bool directSuspension = HasDirectSuspension(body, expressionBody);
        _owners.Add(new(node, synchronous && _classDepth == 0 && !directSuspension,
            node is Expr.ArrowFunction && synchronous && _classDepth == 0));
        EnterScope([], allowCandidates: false);
        try
        {
            if (self is not null) Declare(self);
            if (node is Stmt.Function or Expr.ArrowFunction { HasOwnThis: true })
                Declare(new Token(TokenType.IDENTIFIER, "arguments", null, 0));
            foreach (var parameter in parameters) Declare(parameter.Name);
            foreach (var parameter in parameters) if (parameter.DefaultValue is { } value) Visit(value);
            EnterScope(body, allowCandidates: false, hoistVars: true);
            try
            {
                if (expressionBody is not null) Visit(expressionBody);
                foreach (var statement in body) Visit(statement);
            }
            finally { _scopes.Pop(); }
        }
        finally { _scopes.Pop(); _owners.RemoveAt(_owners.Count - 1); _loopBodyDepth = previousDepth; }
    }

    protected override void VisitClass(Stmt.Class stmt)
    {
        if (stmt.SuperclassExpr is { } parent) Visit(parent);
        VisitClassBody(stmt.Name, () =>
        {
            foreach (var field in stmt.Fields) VisitField(field);
            foreach (var method in stmt.Methods) VisitFunction(method);
            if (stmt.Accessors is { } accessors) foreach (var accessor in accessors) VisitAccessor(accessor);
            if (stmt.AutoAccessors is { } autoAccessors) foreach (var accessor in autoAccessors) VisitAutoAccessor(accessor);
            if (stmt.StaticInitializers is { } initializers) foreach (var initializer in initializers) Visit(initializer);
        });
    }

    protected override void VisitClassExpr(Expr.ClassExpr expr)
    {
        if (expr.SuperclassExpr is { } parent) Visit(parent);
        VisitClassBody(expr.Name, () =>
        {
            foreach (var field in expr.Fields) VisitField(field);
            foreach (var method in expr.Methods) VisitFunction(method);
            if (expr.Accessors is { } accessors) foreach (var accessor in accessors) VisitAccessor(accessor);
            if (expr.AutoAccessors is { } autoAccessors) foreach (var accessor in autoAccessors) VisitAutoAccessor(accessor);
            if (expr.StaticInitializers is { } initializers) foreach (var initializer in initializers) Visit(initializer);
        });
    }

    protected override void VisitNamespace(Stmt.Namespace stmt) =>
        VisitClassBody(stmt.Name, () => VisitStatementScope(stmt.Members));

    private void VisitClassBody(Token? name, Action visit)
    {
        int previousDepth = _loopBodyDepth;
        _loopBodyDepth = 0;
        _classDepth++;
        _owners.Add(new(new object(), false, false));
        EnterScope([], allowCandidates: false);
        try { if (name is not null) Declare(name); visit(); }
        finally { _scopes.Pop(); _owners.RemoveAt(_owners.Count - 1); _classDepth--; _loopBodyDepth = previousDepth; }
    }

    private static bool HasDirectSuspension(IEnumerable<Stmt> statements, Expr? expression = null)
    {
        var visitor = new SuspensionVisitor();
        foreach (var statement in statements) visitor.Visit(statement);
        if (expression is not null) visitor.Visit(expression);
        return visitor.Found;
    }

    private sealed class SuspensionVisitor : AstVisitorBase
    {
        public bool Found { get; private set; }
        protected override void VisitAwait(Expr.Await expr) => Found = true;
        protected override void VisitYield(Expr.Yield expr) => Found = true;
        protected override void VisitForOf(Stmt.ForOf stmt)
        {
            if (stmt.IsAsync) Found = true;
            else base.VisitForOf(stmt);
        }
        protected override void VisitUsing(Stmt.Using stmt)
        {
            if (stmt.IsAsync) Found = true;
            else base.VisitUsing(stmt);
        }
        protected override void VisitArrowFunction(Expr.ArrowFunction expr) { }
        protected override void VisitFunction(Stmt.Function stmt) { }
        protected override void VisitClass(Stmt.Class stmt) =>
            VisitClassEvaluation(stmt.SuperclassExpr, stmt.Fields, stmt.Methods, stmt.Accessors);
        protected override void VisitClassExpr(Expr.ClassExpr expr) =>
            VisitClassEvaluation(expr.SuperclassExpr, expr.Fields, expr.Methods, expr.Accessors);

        private void VisitClassEvaluation(Expr? parent, List<Stmt.Field> fields,
            List<Stmt.Function> methods, List<Stmt.Accessor>? accessors)
        {
            // Heritage and computed names execute in the containing activation;
            // method bodies and instance initializers do not suspend that owner.
            if (parent is not null) Visit(parent);
            foreach (var field in fields) if (field.ComputedKey is { } key) Visit(key);
            foreach (var method in methods) if (method.ComputedKey is { } key) Visit(key);
            if (accessors is not null)
                foreach (var accessor in accessors) if (accessor.ComputedKey is { } key) Visit(key);
        }
    }

    private sealed class VarCollector : AstVisitorBase
    {
        public List<Token> Names { get; } = [];
        protected override void VisitVar(Stmt.Var stmt) { if (stmt.IsVar) Names.Add(stmt.Name); }
        protected override void VisitForOf(Stmt.ForOf stmt) { if (stmt.IsDeclaration && stmt.IsVar) Names.Add(stmt.Variable); Visit(stmt.Body); }
        protected override void VisitForIn(Stmt.ForIn stmt) { if (stmt.IsDeclaration && stmt.IsVar) Names.Add(stmt.Variable); Visit(stmt.Body); }
        protected override void VisitArrowFunction(Expr.ArrowFunction expr) { }
        protected override void VisitFunction(Stmt.Function stmt) { }
        protected override void VisitClass(Stmt.Class stmt) { }
        protected override void VisitClassExpr(Expr.ClassExpr expr) { }
    }
}
