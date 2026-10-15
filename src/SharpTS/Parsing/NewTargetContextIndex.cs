using SharpTS.Parsing.Visitors;

namespace SharpTS.Parsing;

/// <summary>Lexical function environments for meta-properties, independent of variable scopes.</summary>
internal sealed class NewTargetContextIndex : AstVisitorBase
{
    internal readonly record struct Context(object? Owner, object? ClassOwner = null)
    {
        public bool IsJavaScriptAllowed => Owner is not null;
        public bool IsTypeScriptAllowed => Owner switch
        {
            Stmt.Function function => ClassOwner is null ||
                (!function.IsStatic && function.ComputedKey is null && function.Name.Lexeme == "constructor"),
            Expr.ArrowFunction function => function.HasOwnThis && !function.IsMethod,
            _ => false
        };
    }

    private Context _context;
    internal Dictionary<Expr.NewTarget, Context> Contexts { get; } = new(ReferenceEqualityComparer.Instance);

    internal static Dictionary<Expr.NewTarget, Context> Collect(IEnumerable<Stmt> statements)
    {
        var visitor = new NewTargetContextIndex();
        foreach (var statement in statements) visitor.Visit(statement);
        return visitor.Contexts;
    }

    protected override void VisitNewTarget(Expr.NewTarget expr) => Contexts[expr] = _context;

    protected override void VisitNew(Expr.New expr)
    {
        Visit(expr.Callee);
        foreach (var argument in expr.Arguments) Visit(argument);
    }

    private void InContext(Context context, Action visit)
    {
        var previous = _context;
        _context = context;
        try { visit(); }
        finally { _context = previous; }
    }

    protected override void VisitFunction(Stmt.Function stmt) => VisitFunction(stmt, null);

    private void VisitFunction(Stmt.Function stmt, object? classOwner)
    {
        if (stmt.ComputedKey is { } key) Visit(key);
        VisitDecorators(stmt.Decorators);
        foreach (var parameter in stmt.Parameters) VisitDecorators(parameter.Decorators);
        InContext(new(stmt, classOwner), () => base.VisitFunction(stmt));
    }

    protected override void VisitArrowFunction(Expr.ArrowFunction expr)
        => InContext(expr.HasOwnThis ? new(expr) : _context, () => base.VisitArrowFunction(expr));

    protected override void VisitField(Stmt.Field stmt)
    {
        if (stmt.ComputedKey is { } key) Visit(key);
        VisitDecorators(stmt.Decorators);
        if (stmt.Initializer is { } initializer)
            InContext(new(stmt), () => Visit(initializer));
    }

    protected override void VisitAutoAccessor(Stmt.AutoAccessor stmt)
    {
        VisitDecorators(stmt.Decorators);
        if (stmt.Initializer is { } initializer)
            InContext(new(stmt), () => Visit(initializer));
    }

    protected override void VisitAccessor(Stmt.Accessor stmt)
    {
        if (stmt.ComputedKey is { } key) Visit(key);
        VisitDecorators(stmt.Decorators);
        InContext(new(stmt), () =>
        {
            if (stmt.SetterParam?.DefaultValue is { } value) Visit(value);
            foreach (var statement in stmt.Body) Visit(statement);
        });
    }

    protected override void VisitStaticBlock(Stmt.StaticBlock stmt)
        => InContext(new(stmt), () => base.VisitStaticBlock(stmt));

    protected override void VisitClass(Stmt.Class stmt)
    {
        VisitDecorators(stmt.Decorators);
        VisitClassMembers(stmt, stmt.SuperclassExpr, stmt.Methods, stmt.Fields,
            stmt.Accessors, stmt.AutoAccessors, stmt.StaticInitializers);
    }

    protected override void VisitClassExpr(Expr.ClassExpr expr)
        => VisitClassMembers(expr, expr.SuperclassExpr, expr.Methods, expr.Fields,
            expr.Accessors, expr.AutoAccessors, expr.StaticInitializers);

    private void VisitClassMembers(object owner, Expr? parent, List<Stmt.Function> methods,
        List<Stmt.Field> fields, List<Stmt.Accessor>? accessors,
        List<Stmt.AutoAccessor>? autoAccessors, List<Stmt>? staticInitializers)
    {
        if (parent is not null) Visit(parent);
        foreach (var field in fields) VisitField(field);
        foreach (var method in methods) VisitFunction(method, owner);
        foreach (var accessor in accessors ?? []) VisitAccessor(accessor);
        foreach (var accessor in autoAccessors ?? []) VisitAutoAccessor(accessor);
        // Fields also occur in this ordered list; only blocks add a new traversal.
        foreach (var block in (staticInitializers ?? []).OfType<Stmt.StaticBlock>()) VisitStaticBlock(block);
    }

    private void VisitDecorators(List<Decorator>? decorators)
    {
        foreach (var decorator in decorators ?? []) Visit(decorator.Expression);
    }
}
