using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class DeletedObjectMethodDescriptorTests
{
    public const string IssueSource = """
        const O: any = Object, read: any = Object.getOwnPropertyDescriptor;
        const reflected: any = Reflect.getOwnPropertyDescriptor;
        const keys = read(O, "keys"), create = read(O, "create");
        console.log(keys.value === O.keys, create.value === O.create,
            keys.writable && !keys.enumerable && keys.configurable,
            create.writable && !create.enumerable && create.configurable);
        console.log(delete O.keys, typeof O.keys);
        console.log(read(O, "keys") === undefined, reflected(O, "keys") === undefined);
        console.log(delete O.create, typeof O.create,
            read(O, "create") === undefined, reflected(O, "create") === undefined);
        """;

    public const string RestorationSource = """
        const O: any = Object, read: any = Object.getOwnPropertyDescriptor;
        const reflected: any = Reflect.getOwnPropertyDescriptor, define: any = Object.defineProperty;
        const original = read(O, "keys"), state = { reads: 0 };
        function replacement(value: any) { return ["replacement"]; }
        delete O.keys;
        O.keys = replacement;
        console.log(read(O, "keys").value === replacement, reflected(O, "keys").value === replacement,
            O.keys({}).join(","));
        console.log(delete O.keys, read(O, "keys") === undefined, reflected(O, "keys") === undefined);
        define(O, "keys", { value: undefined, writable: true, configurable: true });
        const missing = read(O, "keys");
        console.log(Object.hasOwn(missing, "value"), missing.value === undefined,
            reflected(O, "keys").value === undefined);
        console.log(delete O.keys, read(O, "keys") === undefined, reflected(O, "keys") === undefined);
        delete O.create;
        function getter() { state.reads++; return replacement; }
        define(O, "create", { get: getter, configurable: true });
        const accessor = read(O, "create");
        console.log(accessor.get === getter, reflected(O, "create").get === getter,
            !Object.hasOwn(accessor, "value"), state.reads);
        console.log(delete O.create, read(O, "create") === undefined,
            reflected(O, "create") === undefined, state.reads);
        define(O, "keys", original);
        console.log(read(O, "keys").value === original.value, reflected(O, "keys").value === original.value,
            O.keys({ restored: 1 }).join(","), state.reads);
        """;

    public const string LockedSource = """
        const O: any = Object, read: any = Object.getOwnPropertyDescriptor;
        const reflected: any = Reflect.getOwnPropertyDescriptor, define: any = Object.defineProperty;
        const keys = O.keys, create = O.create;
        delete O.keys;
        define(O, "keys", { value: keys, writable: false, configurable: false });
        console.log(Reflect.deleteProperty(O, "keys"), read(O, "keys").value === keys,
            read(O, "keys").configurable, reflected(O, "keys").configurable);
        delete O.create;
        define(O, "create", { value: create, writable: true, configurable: true });
        Object.freeze(O);
        const frozen = read(O, "create");
        console.log(Reflect.deleteProperty(O, "create"), frozen.value === create,
            reflected(O, "create").value === create, frozen.writable, frozen.configurable);
        """;

    public const string ObjectRestorationSource = """
        const O: any = Object, read: any = Object.getOwnPropertyDescriptor;
        const reflected: any = Reflect.getOwnPropertyDescriptor, define: any = Object.defineProperty;
        const original = read(O, "keys"), state = { reads: 0 };
        function replacement(value: any) { return ["replacement"]; }
        delete O.keys;
        O.keys = replacement;
        console.log(read(O, "keys").value === replacement, O.keys({}).join(","));
        console.log(delete O.keys, read(O, "keys") === undefined, reflected(O, "keys") === undefined);
        define(O, "keys", { value: undefined, writable: true, configurable: true });
        const missing = read(O, "keys");
        console.log(Object.hasOwn(missing, "value"), missing.value === undefined);
        console.log(delete O.keys, read(O, "keys") === undefined, reflected(O, "keys") === undefined);
        delete O.create;
        function getter() { state.reads++; return replacement; }
        define(O, "create", { get: getter, configurable: true });
        const accessor = read(O, "create");
        console.log(accessor.get === getter, !Object.hasOwn(accessor, "value"), state.reads);
        console.log(delete O.create, read(O, "create") === undefined,
            reflected(O, "create") === undefined, state.reads);
        define(O, "keys", original);
        console.log(read(O, "keys").value === original.value, O.keys({ restored: 1 }).join(","), state.reads);
        """;

    public const string NonconfigurableSource = """
        const O: any = Object, read: any = Object.getOwnPropertyDescriptor, keys = O.keys;
        delete O.keys;
        Object.defineProperty(O, "keys", { value: keys, writable: false, configurable: false });
        console.log(Reflect.deleteProperty(O, "keys"), read(O, "keys").value === keys,
            read(O, "keys").configurable);
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["issue-and-both-builders", IssueSource,
            "true true true true\ntrue undefined\ntrue true\ntrue undefined true true\n"];
        yield return ["restoration-and-redelete", RestorationSource,
            "true true replacement\ntrue true true\ntrue true true\ntrue true true\n"
            + "true true true 0\ntrue true true 0\ntrue true restored 0\n"];
        yield return ["locked-and-frozen", LockedSource, "false true false false\nfalse true true false false\n"];
    }

    public static IEnumerable<object[]> SharedCases()
    {
        yield return ["reflect-own-expando-2318", """
            const O: any = Object;
            Object.defineProperty(O, 'custom', { value: 7, enumerable: true, configurable: true });
            console.log(Object.getOwnPropertyDescriptor(O, 'custom')!.value);
            console.log(Reflect.getOwnPropertyDescriptor(O, 'custom') === undefined);
            const reflected: any = Reflect.getOwnPropertyDescriptor(O, 'custom');
            console.log(reflected.value, reflected.writable, reflected.enumerable, reflected.configurable);
            let reads = 0;
            Object.defineProperty(O, 'customGetter', { get() { reads++; return 1; }, configurable: true });
            console.log(typeof Reflect.getOwnPropertyDescriptor(O, 'customGetter')!.get, reads);
            delete O.custom;
            console.log(Reflect.getOwnPropertyDescriptor(O, 'custom') === undefined);
            """, "7\nfalse\n7 false true true\nfunction 0\ntrue\n"];
        yield return Cases().First();
        yield return ["restoration-object-descriptors", ObjectRestorationSource,
            "true replacement\ntrue true true\ntrue true\ntrue true true\n"
            + "true true 0\ntrue true true 0\ntrue restored 0\n"];
        yield return ["nonconfigurable-object-descriptor", NonconfigurableSource, "false true false\n"];
    }

    public static IEnumerable<object[]> ModeCases() => SharedCases().SelectMany(row =>
            new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
                .Select(mode => new object[] { row[0], row[1], row[2], mode }))
        .Concat(Cases().Skip(1).SelectMany(row => new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode })));

    [Theory, MemberData(nameof(ModeCases))]
    public void DeletedObjectMethodsStopSynthesizingOwnDescriptors(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        string output;
        if (mode == ExecutionMode.Compiled && (name == "reflect-own-expando-2318"))
        {
            var (errors, compiledOutput) = TestHarness.CompileVerifyAndRun(source);
            Assert.Empty(errors);
            output = compiledOutput;
        }
        else
        {
            output = TestHarness.Run(source, mode);
        }
        Assert.Equal(expected, output);
    }
}
