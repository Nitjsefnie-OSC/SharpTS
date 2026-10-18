using SharpTS.Parsing;
using SharpTS.Parsing.Visitors;
using SharpTS.TypeSystem;

namespace SharpTS.Compilation;

/// <summary>
/// Checked constructor identity, independent of a variable's structural constructor type.
/// Built on the checked AST before lifting and consumed by every expression emitter.
/// </summary>
internal sealed class HeldClassConstructionInfo
{
    private readonly TypeMap _map;
    private readonly Dictionary<int, TypeInfo.Class> _guestClasses = [];
    private readonly Dictionary<BindingSymbol, TypeInfo.Class?> _declarations = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<BindingSymbol> _namespaces = new(ReferenceEqualityComparer.Instance);

    private HeldClassConstructionInfo(TypeMap map)
    {
        _map = map;
        foreach (var (source, type) in map.ClassDeclarations)
        {
            if (source.IsDeclare || type.Core.IsDotNetMapped) continue;
            _guestClasses[type.Core.DeclarationId] = type;
            if (map.GetValueBinding(source.Name) is not { } binding) continue;
            if (_declarations.TryGetValue(binding, out var previous) && previous?.Core.DeclarationId != type.Core.DeclarationId)
                _declarations[binding] = null;
            else _declarations[binding] = type;
        }
        foreach (var (_, type) in map.ClassExpressions)
            if (!type.Core.IsDotNetMapped) _guestClasses[type.Core.DeclarationId] = type;
    }

    internal static HeldClassConstructionInfo Analyze(IEnumerable<Stmt> statements, TypeMap map, TypeMapper mapper)
    {
        var result = new HeldClassConstructionInfo(map);
        var visitor = new Inventory(result);
        foreach (var statement in statements) visitor.Visit(statement);
        foreach (var construction in visitor.Constructions)
            if (result.RequiresValueConstruction(construction))
                foreach (var type in result.CheckedTypes(construction)) result.MarkAncestors(type, mapper);
        return result;
    }

    internal bool RequiresValueConstruction(Expr.New construction)
    {
        int canonical = CanonicalClass(construction.Callee)?.Core.DeclarationId ?? 0;
        bool guest = false;
        foreach (var type in CheckedTypes(construction))
        {
            int id = Declaration(type).Id;
            if (!_guestClasses.ContainsKey(id)) continue;
            guest = true;
            if (id != canonical) return true;
        }
        return guest && canonical == 0;
    }

    internal bool HasCanonicalReference(Expr expression) => CanonicalClass(expression) != null;

    private TypeInfo.Class? CanonicalClass(Expr expression)
    {
        expression = ExpressionUnwrapper.Unwrap(expression);
        Token? name = expression switch
        {
            Expr.Variable variable => variable.Name,
            Expr.Get { Optional: false } member when IsNamespace(member.Object) => member.Name,
            _ => null
        };
        return name is not null && _map.GetValueBinding(name) is { } binding
            ? _declarations.GetValueOrDefault(binding) : null;
    }

    private bool IsNamespace(Expr expression)
    {
        expression = ExpressionUnwrapper.Unwrap(expression);
        return expression switch
        {
            Expr.Variable variable => IsNamespaceBinding(variable.Name),
            Expr.Get { Optional: false } member => IsNamespace(member.Object) && IsNamespaceBinding(member.Name),
            _ => false
        };
    }

    private bool IsNamespaceBinding(Token token) => _map.GetValueBinding(token) is { } symbol && _namespaces.Contains(symbol);

    private IEnumerable<TypeInfo?> CheckedTypes(Expr.New construction)
    {
        yield return _map.Get(construction);
        Expr expression = construction.Callee;
        while (true)
        {
            yield return _map.Get(expression);
            Expr next = expression switch
            {
                Expr.Grouping group => group.Expression,
                Expr.TypeAssertion assertion => assertion.Expression,
                Expr.NonNullAssertion assertion => assertion.Expression,
                Expr.Satisfies satisfies => satisfies.Expression,
                _ => expression
            };
            if (ReferenceEquals(expression, next)) break;
            expression = next;
        }
    }

    private void MarkAncestors(TypeInfo? type, TypeMapper mapper)
    {
        var visited = new HashSet<int>();
        while (true)
        {
            var (id, parent) = Declaration(type);
            if (!_guestClasses.ContainsKey(id) || !visited.Add(id)) return;
            mapper.RegisterDynamicClassInstance(id);
            type = parent;
        }
    }

    private static (int Id, TypeInfo? Parent) Declaration(TypeInfo? type)
    {
        while (true)
        {
            switch (type)
            {
                case TypeInfo.Instance instance: type = instance.ResolvedClassType; continue;
                case TypeInfo.InstantiatedGeneric generic: type = generic.GenericDefinition; continue;
                case TypeInfo.Class cls: return (cls.Core.DeclarationId, cls.Core.Superclass);
                case TypeInfo.GenericClass cls: return (cls.Core.DeclarationId, cls.Core.Superclass);
                case TypeInfo.MutableClass cls: return (cls.DeclarationId, cls.Superclass);
                default: return (0, null);
            }
        }
    }

    private sealed class Inventory(HeldClassConstructionInfo info) : AstVisitorBase
    {
        internal HashSet<Expr.New> Constructions { get; } = new(ReferenceEqualityComparer.Instance);
        private void Namespace(Token token)
        {
            if (info._map.GetValueBinding(token) is { } binding) info._namespaces.Add(binding);
        }
        protected override void VisitNamespace(Stmt.Namespace statement) { Namespace(statement.Name); base.VisitNamespace(statement); }
        protected override void VisitImport(Stmt.Import statement)
        {
            if (!statement.IsTypeOnly && statement.NamespaceImport is { } name) Namespace(name);
            base.VisitImport(statement);
        }
        protected override void VisitNew(Expr.New expression)
        {
            Constructions.Add(expression);
            Visit(expression.Callee);
            base.VisitNew(expression);
        }
        protected override void VisitClass(Stmt.Class statement)
        {
            foreach (var expression in ClassDefinitionExpressions.Enumerate(statement)) Visit(expression);
            foreach (var decorator in statement.Decorators ?? []) Visit(decorator.Expression);
            foreach (var field in statement.Fields)
                foreach (var decorator in field.Decorators ?? []) Visit(decorator.Expression);
            base.VisitClass(statement);
            foreach (var accessor in statement.AutoAccessors ?? []) VisitAutoAccessor(accessor);
            foreach (var initializer in statement.StaticInitializers ?? []) Visit(initializer);
        }
        protected override void VisitClassExpr(Expr.ClassExpr expression)
        {
            foreach (var child in ClassDefinitionExpressions.Enumerate(expression)) Visit(child);
            foreach (var field in expression.Fields)
                foreach (var decorator in field.Decorators ?? []) Visit(decorator.Expression);
            base.VisitClassExpr(expression);
            foreach (var accessor in expression.AutoAccessors ?? []) VisitAutoAccessor(accessor);
            foreach (var initializer in expression.StaticInitializers ?? []) Visit(initializer);
        }
        protected override void VisitFunction(Stmt.Function statement)
        {
            foreach (var decorator in statement.Decorators ?? []) Visit(decorator.Expression);
            foreach (var parameter in statement.Parameters)
            {
                foreach (var decorator in parameter.Decorators ?? []) Visit(decorator.Expression);
                foreach (var property in parameter.DestructuredProperties ?? [])
                    if (property.DefaultValue is { } value) Visit(value);
            }
            base.VisitFunction(statement);
        }
        protected override void VisitArrowFunction(Expr.ArrowFunction expression)
        {
            foreach (var parameter in expression.Parameters)
            {
                foreach (var decorator in parameter.Decorators ?? []) Visit(decorator.Expression);
                foreach (var property in parameter.DestructuredProperties ?? [])
                    if (property.DefaultValue is { } value) Visit(value);
            }
            base.VisitArrowFunction(expression);
        }
        protected override void VisitObjectLiteral(Expr.ObjectLiteral expression)
        {
            foreach (var property in expression.Properties)
                if (property.SetterParam?.DefaultValue is { } value) Visit(value);
            base.VisitObjectLiteral(expression);
        }
        protected override void VisitField(Stmt.Field statement)
        {
            foreach (var decorator in statement.Decorators ?? []) Visit(decorator.Expression);
            base.VisitField(statement);
        }
        protected override void VisitAutoAccessor(Stmt.AutoAccessor statement)
        {
            foreach (var decorator in statement.Decorators ?? []) Visit(decorator.Expression);
            base.VisitAutoAccessor(statement);
        }
        protected override void VisitAccessor(Stmt.Accessor statement)
        {
            foreach (var decorator in statement.Decorators ?? []) Visit(decorator.Expression);
            if (statement.SetterParam?.DefaultValue is { } value) Visit(value);
            base.VisitAccessor(statement);
        }
    }
}
