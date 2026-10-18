using SharpTS.Compilation.Emitters;
using SharpTS.Parsing;
using SharpTS.Parsing.Visitors;

namespace SharpTS.Compilation;

internal sealed partial class ForwardedReturnValueAnalyzer
{
    private readonly Dictionary<Expr.Call, TokenType> _nativeIntrinsics = new(ReferenceEqualityComparer.Instance);

    private void ResolveIntrinsicCalls(IReadOnlyList<Stmt> statements)
    {
        var source = new IntrinsicSourceInventory();
        foreach (var statement in statements) source.Visit(statement);
        if (source.UnknownBindings) return;

        foreach (var call in source.Calls)
        {
            if (call.Optional || call.Arguments.Any(argument => argument is Expr.Spread)) continue;
            TokenType? kind = call.Callee switch
            {
                Expr.Variable { Name.Lexeme: "parseInt" }
                    when !source.Bindings.Contains("parseInt")
                        && features?.UsesGlobalParseIntMutation == false
                        && NumberStaticEmitter.IsDecimalStringParseInt(map, call.Arguments) => TokenType.TYPE_NUMBER,
                Expr.Get { Optional: false, Object: Expr.Variable { Name.Lexeme: "Number" }, Name.Lexeme: "parseInt" }
                    when !source.Bindings.Contains("Number")
                        && features?.UsesNumberConstructorMutation == false
                        && NumberStaticEmitter.IsDecimalStringParseInt(map, call.Arguments) => TokenType.TYPE_NUMBER,
                Expr.Get { Optional: false, Object: Expr.Variable { Name.Lexeme: "Math" } } method
                    when !source.Bindings.Contains("Math")
                        && MathStaticEmitter.EmitsUnboxedFixedArityMinMax(map, features, method.Name.Lexeme, call.Arguments)
                    => TokenType.TYPE_NUMBER,
                Expr.Get { Optional: false } method
                    when StringEmitter.EmitsUnboxedPrimitiveSearch(map, features, method.Object, method.Name.Lexeme, call.Arguments)
                    => method.Name.Lexeme == "includes" ? TokenType.TYPE_BOOLEAN : TokenType.TYPE_NUMBER,
                _ => null
            };
            if (kind is { } native && PrimitiveKind(map.Get(call)) == native) _nativeIntrinsics[call] = native;
        }
    }

    /// <summary>
    /// Refuses a canonical intrinsic for any same-named value binding anywhere in its module.
    /// This deliberately avoids resolving scopes: extra refusals keep the general object ABI.
    /// </summary>
    private sealed class IntrinsicSourceInventory : AstVisitorBase
    {
        internal HashSet<string> Bindings { get; } = new(StringComparer.Ordinal);
        internal HashSet<Expr.Call> Calls { get; } = new(ReferenceEqualityComparer.Instance);
        internal bool UnknownBindings { get; private set; }

        private void Add(Token? name)
        {
            if (name?.Lexeme is "parseInt" or "Number" or "Math") Bindings.Add(name.Lexeme);
        }

        public override void Visit(Stmt statement)
        {
            // Class member traversal calls VisitFunction directly. A method's property name
            // therefore does not become a lexical binding, but a real function declaration does.
            switch (statement)
            {
                case Stmt.Var variable: Add(variable.Name); break;
                case Stmt.Const constant: Add(constant.Name); break;
                case Stmt.Function function: Add(function.Name); break;
                case Stmt.Class declaration: Add(declaration.Name); break;
                case Stmt.Enum declaration: Add(declaration.Name); break;
                case Stmt.Namespace declaration: Add(declaration.Name); break;
                case Stmt.ForOf loop: Add(loop.Variable); break;
                case Stmt.ForIn loop: Add(loop.Variable); break;
                case Stmt.TryCatch handler: Add(handler.CatchParam); break;
                case Stmt.Import import when !import.IsTypeOnly:
                    Add(import.DefaultImport); Add(import.NamespaceImport);
                    foreach (var item in import.NamedImports ?? [])
                        if (!item.IsTypeOnly) Add(item.LocalName ?? item.Imported);
                    break;
                case Stmt.ImportAlias alias: Add(alias.AliasName); UnknownBindings = true; break;
                case Stmt.ImportRequire alias: Add(alias.AliasName); UnknownBindings = true; break;
                case Stmt.Using resource:
                    foreach (var binding in resource.Bindings)
                    {
                        Add(binding.Name);
                        if (binding.DestructuringPattern != null) UnknownBindings = true;
                    }
                    break;
                case Stmt.FileDirective directive: Decorators(directive.Decorators); break;
            }
            base.Visit(statement);
        }

        protected override void VisitCall(Expr.Call expression) { Calls.Add(expression); base.VisitCall(expression); }
        protected override void VisitVariable(Expr.Variable expression)
        {
            if (expression.Name.Lexeme is "eval" or "globalThis" or "Function") UnknownBindings = true;
        }
        protected override void VisitThis(Expr.This expression) => UnknownBindings = true;

        private void Parameters(IEnumerable<Stmt.Parameter> parameters)
        {
            foreach (var parameter in parameters)
            {
                Add(parameter.Name);
                Decorators(parameter.Decorators);
                if (parameter.DefaultValue is { } value) Visit(value);
                foreach (var property in parameter.DestructuredProperties ?? [])
                {
                    Add(property.Binding);
                    if (property.DefaultValue is { } fallback) Visit(fallback);
                }
            }
        }

        private void Decorators(IEnumerable<Decorator>? decorators)
        {
            foreach (var decorator in decorators ?? []) Visit(decorator.Expression);
        }

        protected override void VisitFunction(Stmt.Function function)
        {
            Parameters(function.Parameters); Decorators(function.Decorators);
            if (function.ComputedKey is { } key) Visit(key);
            foreach (var statement in function.Body ?? []) Visit(statement);
        }

        protected override void VisitArrowFunction(Expr.ArrowFunction expression)
        {
            Add(expression.Name); Parameters(expression.Parameters);
            if (expression.ExpressionBody is { } body) Visit(body);
            foreach (var statement in expression.BlockBody ?? []) Visit(statement);
        }

        protected override void VisitObjectLiteral(Expr.ObjectLiteral expression)
        {
            foreach (var property in expression.Properties)
                if (property.SetterParam is { } parameter) Parameters([parameter]);
            base.VisitObjectLiteral(expression);
        }

        protected override void VisitAccessor(Stmt.Accessor accessor)
        {
            if (accessor.SetterParam is { } parameter) Parameters([parameter]);
            Decorators(accessor.Decorators);
            base.VisitAccessor(accessor);
        }

        protected override void VisitField(Stmt.Field field) { Decorators(field.Decorators); base.VisitField(field); }
        protected override void VisitAutoAccessor(Stmt.AutoAccessor accessor)
        {
            Decorators(accessor.Decorators); base.VisitAutoAccessor(accessor);
        }

        protected override void VisitClass(Stmt.Class declaration)
        {
            Decorators(declaration.Decorators);
            Members(declaration.SuperclassExpr, declaration.Fields, declaration.Methods,
                declaration.Accessors, declaration.AutoAccessors, declaration.StaticInitializers);
        }

        protected override void VisitClassExpr(Expr.ClassExpr expression)
        {
            Add(expression.Name);
            Members(expression.SuperclassExpr, expression.Fields, expression.Methods,
                expression.Accessors, expression.AutoAccessors, expression.StaticInitializers);
        }

        private void Members(Expr? parent, IEnumerable<Stmt.Field> fields, IEnumerable<Stmt.Function> methods,
            IEnumerable<Stmt.Accessor>? accessors, IEnumerable<Stmt.AutoAccessor>? autoAccessors,
            IEnumerable<Stmt>? initializers)
        {
            if (parent != null) Visit(parent);
            foreach (var field in fields) Visit(field);
            foreach (var method in methods) VisitFunction(method);
            foreach (var accessor in accessors ?? []) Visit(accessor);
            foreach (var accessor in autoAccessors ?? []) Visit(accessor);
            foreach (var initializer in initializers ?? []) Visit(initializer);
        }
    }
}
