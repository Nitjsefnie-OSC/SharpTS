using SharpTS.Parsing;
using SharpTS.TypeSystem;
using SharpTS.TypeSystem.Exceptions;
using Xunit;

namespace SharpTS.Tests.TypeCheckerTests;

public sealed class FunctionHasInstanceIntrinsicTypeTests
{
    [Theory]
    [InlineData("class C {} const fn: (value: any) => boolean = C[Symbol.hasInstance]; const result: boolean = fn({});")]
    [InlineData("class C<T> {} const result: boolean = C[Symbol.hasInstance]({});")]
    [InlineData("const C = class {}; const result: boolean = C[Symbol.hasInstance]({});")]
    [InlineData("class Base { static [Symbol.hasInstance](value: number): string { return 'own'; } } class Child extends Base {} const result: string = Child[Symbol.hasInstance](1);")]
    [InlineData("class C { [Symbol.hasInstance](value: number): string { return 'instance'; } } const constructorResult: boolean = C[Symbol.hasInstance]({}); const instanceResult: string = new C()[Symbol.hasInstance](1);")]
    public void ConstructorIntrinsicAndDeclaredHooksKeepTheirOwnSignatures(string source)
        => new TypeChecker().Check(new Parser(new Lexer(source).ScanTokens()).ParseOrThrow());

    [Theory]
    [InlineData("class C {} const result: string = C[Symbol.hasInstance]({});")]
    [InlineData("class C {} new C()[Symbol.hasInstance]({});")]
    [InlineData("class C { static [Symbol.hasInstance](value: number): string { return 'own'; } } C[Symbol.hasInstance]('wrong');")]
    [InlineData("class C { static [Symbol.hasInstance](value: number): string { return 'own'; } } const result: boolean = C[Symbol.hasInstance](1);")]
    public void MissingInstanceMemberAndIncorrectArgumentOrResultTypesRemainErrors(string source)
        => Assert.ThrowsAny<TypeCheckException>(() => new TypeChecker().Check(
            new Parser(new Lexer(source).ScanTokens()).ParseOrThrow()));
}
