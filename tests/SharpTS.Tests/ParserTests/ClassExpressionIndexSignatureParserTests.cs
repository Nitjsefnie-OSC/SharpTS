using SharpTS.Parsing;
using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.ParserTests;

public sealed class ClassExpressionIndexSignatureParserTests
{
    [Theory]
    [InlineData("string", TokenType.TYPE_STRING)]
    [InlineData("number", TokenType.TYPE_NUMBER)]
    [InlineData("symbol", TokenType.TYPE_SYMBOL)]
    public void InstanceAndStaticSignaturesRetainTheirKeysAndValueNodes(string keyType, TokenType tokenType)
    {
        var expression = ParseClass("const Dictionary = class<T> { [key: " + keyType + "]: T; static [index: " + keyType + "]: number; };");
        Assert.Equal("T", Assert.Single(expression.TypeParams!).Name.Lexeme);
        var signatures = Assert.IsType<List<Stmt.IndexSignature>>(expression.IndexSignatures);
        Assert.Equal(2, signatures.Count);
        Assert.Equal("key", signatures[0].KeyName.Lexeme);
        Assert.Equal(tokenType, signatures[0].KeyType);
        Assert.False(signatures[0].IsStatic);
        Assert.Equal("T", signatures[0].ValueType);
        Assert.Equal("T", Assert.IsType<NamedTypeNode>(signatures[0].ValueTypeNode).Name);
        Assert.Equal("index", signatures[1].KeyName.Lexeme);
        Assert.Equal(tokenType, signatures[1].KeyType);
        Assert.True(signatures[1].IsStatic);
        Assert.Equal("number", signatures[1].ValueType);
        Assert.Equal("number", Assert.IsType<NamedTypeNode>(signatures[1].ValueTypeNode).Name);
        Assert.Empty(expression.Fields);
        Assert.Empty(expression.Methods);
    }

    [Fact]
    public void ReadonlySyntaxFollowsTheDeclarationRepresentation()
    {
        var expression = ParseClass("const Dictionary = class { readonly [key: string]: number; };");
        var declaration = Assert.IsType<Stmt.Class>(Assert.Single(
            TestHarness.ParseOrThrow("class Dictionary { readonly [key: string]: number; }")));
        var expressionSignature = Assert.Single(expression.IndexSignatures!);
        var declarationSignature = Assert.Single(declaration.IndexSignatures!);
        Assert.Equal(declarationSignature.KeyType, expressionSignature.KeyType);
        Assert.Equal(declarationSignature.ValueType, expressionSignature.ValueType);
    }

    [Fact]
    public void ComputedFieldsMethodsAndAccessorsRemainDistinctFromIndexSignatures()
    {
        var expression = ParseClass("""
            const Dictionary = class {
                [key: string]: number;
                [fieldKey]: number = 1;
                [methodKey](): number { return 2; }
                get [accessorKey](): number { return 3; }
            };
            """);
        Assert.Single(expression.IndexSignatures!);
        var field = Assert.Single(expression.Fields);
        Assert.Equal("fieldKey", Assert.IsType<Expr.Variable>(field.ComputedKey).Name.Lexeme);
        var method = Assert.Single(expression.Methods);
        Assert.Equal("methodKey", Assert.IsType<Expr.Variable>(method.ComputedKey).Name.Lexeme);
        var accessor = Assert.Single(expression.Accessors!);
        Assert.Equal("accessorKey", Assert.IsType<Expr.Variable>(accessor.ComputedKey).Name.Lexeme);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditorSignatureAndValueAnnotationKeepWrittenSourceRanges(bool expression)
    {
        string source = "// 😀\r\n" + (expression ? "const Dictionary = class<T>" : "class Dictionary<T>") + """
             {
                readonly [key: string]: Box< ( T /*keep*/ | number ) >;
                static [index: number]: string;
            };
            """;
        var document = new SourceDocument("class-index.ts", source);
        var tokens = new Lexer(source).ScanTokens();
        var statements = new Parser(tokens).WithSourceDocument(document).WithEditorSyntax().ParseOrThrow();
        var signatures = expression
            ? Assert.IsType<Expr.ClassExpr>(Assert.IsType<Stmt.Const>(Assert.Single(statements)).Initializer).IndexSignatures!
            : Assert.IsType<Stmt.Class>(Assert.Single(statements, statement => statement is Stmt.Class)).IndexSignatures!;
        var syntax = Assert.IsType<EditorSyntaxIndex>(document.EditorSyntax);
        Assert.Equal(2, signatures.Count);
        Assert.Equal("[key: string]: Box< ( T /*keep*/ | number ) >;", Text(document, document.Spans.GetSpan(signatures[0])!.Value));
        Assert.Equal("[index: number]: string;", Text(document, document.Spans.GetSpan(signatures[1])!.Value));
        foreach (var signature in signatures)
        {
            Assert.Contains(syntax.GetRecords(signature), record => record.Kind == EditorSyntaxKind.Type &&
                record.Role == EditorSyntaxRole.Whole && record.IsAuthoritative);
            var name = Assert.Single(syntax.GetRecords(signature), record => record.Role == EditorSyntaxRole.DeclarationName);
            Assert.Same(signature.KeyName, name.Token);
            Assert.Same(tokens.Single(token => token.Start == signature.KeyName.Start).SourceIdentity, signature.KeyName.SourceIdentity);
            Assert.Equal(signature.KeyName.Lexeme, Text(document, name.Span));
        }
        var annotation = Assert.IsType<NamedTypeNode>(signatures[0].ValueTypeNode);
        Assert.Equal("Box< ( T /*keep*/ | number ) >", Text(document, document.Spans.GetSpan(annotation)!.Value));
        var union = Assert.IsType<UnionTypeNode>(Assert.Single(annotation.TypeArguments!));
        Assert.Contains(syntax.GetRecords(union), record => record.Kind == EditorSyntaxKind.Grouping &&
            Text(document, record.Span) == "( T /*keep*/ | number )");
        Assert.Contains(syntax.GetRecords(annotation), record => record.Role == EditorSyntaxRole.Annotation && record.IsAuthoritative);
    }

    private static Expr.ClassExpr ParseClass(string source) => Assert.IsType<Expr.ClassExpr>(
        Assert.IsType<Stmt.Const>(Assert.Single(TestHarness.ParseOrThrow(source))).Initializer);

    private static string Text(SourceDocument document, SourceSpan span) => document.Text[span.Start..span.End];
}
