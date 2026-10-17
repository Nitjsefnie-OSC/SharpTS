using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class SymbolDeleteOperatorTests
{
    public const string IssueSource = """
        const object: any = {}, prototype: any = Function.prototype, key = Symbol("missing");
        console.log(delete object[key], delete prototype[key]);
        object[key] = 1;
        prototype[key] = 2;
        console.log(delete object[key], delete prototype[key]);
        console.log(delete object[key], delete prototype[key]);
        function removeStrict(target: any) { "use strict"; return delete target[key]; }
        console.log(removeStrict(object), removeStrict(prototype));
        class C {}
        const instance: any = new C();
        console.log(delete instance[key], removeStrict(instance));
        instance[key] = 3;
        console.log(delete instance[key], removeStrict(instance));
        """;

    [Theory, ModeData]
    public void MissingAndRemovedSymbolsDeleteSuccessfully(ExecutionMode mode)
        => Assert.Equal("true true\ntrue true\ntrue true\ntrue true\ntrue true\ntrue true\n",
            TestHarness.Run(IssueSource, mode));

    [Theory, ModeData]
    public void SymbolDeletionHonorsOwnDescriptorsAndIntegrityLevels(ExecutionMode mode)
    {
        const string source = """
            const key = Symbol("locked"), missing = Symbol("missing"), prototype: any = {};
            Object.defineProperty(prototype, key, { value: 7, configurable: false });
            const child: any = Object.create(prototype);
            console.log(delete child[key], child[key], Object.hasOwn(child, key));
            const sealed: any = {}, frozen: any = {};
            sealed[key] = 8;
            frozen[key] = 9;
            Object.seal(sealed);
            Object.freeze(frozen);
            console.log(delete sealed[missing], delete frozen[missing]);
            console.log(delete prototype[key], delete sealed[key], delete frozen[key]);
            function removeStrict(target: any, property: any) { "use strict"; return delete target[property]; }
            console.log(removeStrict(child, key), removeStrict(sealed, missing), removeStrict(frozen, missing));
            for (const target of [prototype, sealed, frozen]) {
                try { removeStrict(target, key); }
                catch (error) { console.log(error instanceof TypeError, target[key]); }
            }
            """;
        Assert.Equal("true 7 false\ntrue true\nfalse false false\ntrue true true\ntrue 7\ntrue 8\ntrue 9\n",
            TestHarness.Run(source, mode));
    }
}
