using System.Reflection;
using System.Reflection.Emit;
using SharpTS.Compilation;
using SharpTS.Parsing;
using Xunit;

namespace SharpTS.Tests.CompilerTests;

public sealed class EmittedGeneratorPrototypeRuntimeTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static IEnumerable<object[]> Declarations() => new[]
    {
        (Scope: "root", Type: typeof(EmittedGeneratorPrototypeRuntime)),
        (Scope: "sync", Type: typeof(EmittedGeneratorPrototypeKindRuntime)),
        (Scope: "async", Type: typeof(EmittedGeneratorPrototypeKindRuntime)),
    }.SelectMany(group => Slots(group.Type).Select(property => new object[] { group.Scope, property.Name }));

    [Theory, MemberData(nameof(Declarations))]
    public void MissingDeclarationCanBeRepairedBeforeMetadataFreezes(string scope, string name)
    {
        var fixture = new MetadataFixture(includeAsync: true, missingScope: scope, missingName: name);
        object target = fixture.Scope(scope);
        var property = Slots(target.GetType()).Single(property => property.Name == name);
        Expect<InvalidOperationException>(() => property.GetValue(target));
        Expect<ArgumentNullException>(() => property.SetValue(target, null));

        // Missing fields must not freeze the sync child before async validation fails.
        // A missing method prevents body registration until its shell is repaired.
        if (property.PropertyType != typeof(MethodBuilder)) fixture.MarkBodies();
        Assert.Throws<InvalidOperationException>(fixture.Owner.CompleteEmission);
        Assert.False(fixture.Owner.IsComplete);
        Assert.False(fixture.Owner.Sync.IsComplete);
        Assert.False(fixture.Owner.RequireAsync().IsComplete);

        object handle = fixture.Declare(target, property);
        Assert.Same(handle, property.GetValue(target));
        Expect<InvalidOperationException>(() => property.SetValue(target, handle));
        fixture.MarkBodies();
        fixture.Owner.CompleteEmission();

        Assert.True(fixture.Owner.IsComplete);
        Assert.True(fixture.Owner.Sync.IsComplete);
        Assert.True(fixture.Owner.RequireAsync().IsComplete);
        Assert.Throws<InvalidOperationException>(fixture.Owner.CompleteEmission);
        Assert.Throws<InvalidOperationException>(fixture.Owner.BeginAsyncEmission);
        Assert.Throws<InvalidOperationException>(() => fixture.Owner.MarkBodyEmitted(fixture.Owner.Associate));
        Expect<InvalidOperationException>(() => property.SetValue(target, handle));
    }

    [Fact]
    public void DeclaredShellsRequireTheirOwnBodiesBeforeCompletion()
    {
        var fixture = new MetadataFixture(includeAsync: true);
        MethodBuilder delayed = fixture.Owner.Associate;
        var foreign = fixture.Type.DefineMethod("Foreign", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), Type.EmptyTypes);
        foreign.GetILGenerator().Emit(OpCodes.Ret);
        Assert.Throws<InvalidOperationException>(() => fixture.Owner.MarkBodyEmitted(foreign));

        fixture.MarkBodies(delayed);
        Assert.Throws<InvalidOperationException>(() => fixture.Owner.MarkBodyEmitted(fixture.Owner.Kind));
        Assert.Throws<InvalidOperationException>(fixture.Owner.CompleteEmission);
        Assert.False(fixture.Owner.IsComplete);
        Assert.False(fixture.Owner.Sync.IsComplete);
        Assert.False(fixture.Owner.RequireAsync().IsComplete);
        Assert.Same(delayed, fixture.Owner.Associate);

        fixture.MarkBodies();
        fixture.Owner.CompleteEmission();
        Assert.True(fixture.Owner.IsComplete);
    }

    [Fact]
    public void AsyncAvailabilityIsExplicitAndOwnersBelongToTheirOutput()
    {
        var first = new EmittedRuntime();
        var second = new EmittedRuntime();
        Assert.NotSame(first.GeneratorPrototypes, second.GeneratorPrototypes);
        Assert.NotSame(first.GeneratorPrototypes.Sync, second.GeneratorPrototypes.Sync);
        Assert.Null(typeof(EmittedRuntime).GetProperty(nameof(EmittedRuntime.GeneratorPrototypes))!.SetMethod);
        Assert.Null(first.GeneratorPrototypes.Async);
        Assert.Throws<InvalidOperationException>(() => first.GeneratorPrototypes.RequireAsync());
        first.GeneratorPrototypes.BeginAsyncEmission();
        second.GeneratorPrototypes.BeginAsyncEmission();
        Assert.Same(first.GeneratorPrototypes.Async, first.GeneratorPrototypes.RequireAsync());
        Assert.NotSame(first.GeneratorPrototypes.Async, second.GeneratorPrototypes.Async);
        Assert.Throws<InvalidOperationException>(first.GeneratorPrototypes.BeginAsyncEmission);

        var syncOnly = new MetadataFixture(includeAsync: false);
        syncOnly.MarkBodies();
        syncOnly.Owner.CompleteEmission();
        Assert.True(syncOnly.Owner.IsComplete);
        Assert.True(syncOnly.Owner.Sync.IsComplete);
        Assert.Null(syncOnly.Owner.Async);
        Assert.Throws<InvalidOperationException>(() => syncOnly.Owner.RequireAsync());
        Assert.Throws<InvalidOperationException>(syncOnly.Owner.BeginAsyncEmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReusedEmitterKeepsIntrinsicStateAndAsyncGatesIndependent(bool hosted)
    {
        var emitter = new RuntimeEmitter(TypeProvider.Runtime, emitHosted: hosted);
        var owners = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var nodes = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var methods = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var cases = new[]
        {
            (Source: "const n=1;", Async: false),
            (Source: "async function* values(){yield 1;}", Async: true),
            (Source: "async function run(){for await(const value of [1,2]){}}run();", Async: true),
            (Source: "const n=2;", Async: false),
        };

        foreach (var item in cases)
        {
            var assembly = NewAssembly();
            var features = new RuntimeFeatureDetector().Detect(new Parser(new Lexer(item.Source).ScanTokens()).ParseOrThrow());
            var runtime = emitter.EmitAll(assembly.DefineDynamicModule("main"), features);
            var owner = runtime.GeneratorPrototypes;
            CheckOwner(owner, assembly, owners);
            CheckOwner(owner.Sync, assembly, owners);
            Assert.Equal(item.Async, owner.Async is not null);
            if (owner.Async is { } asyncKind) CheckOwner(asyncKind, assembly, owners);
            else Assert.Throws<InvalidOperationException>(() => owner.RequireAsync());

            var loaded = SaveVerifyLoad(assembly);
            var support = loaded.GetType(owner.Type.FullName!)!;
            var populate = support.GetMethod(owner.Populate.Name)!;
            var read = LoadedMethod(loaded, runtime.ObjectRead.Property);
            var write = LoadedMethod(loaded, runtime.ObjectWrite.Property);
            var undefined = loaded.GetType(runtime.Sentinels.UndefinedInstance.DeclaringType!.FullName!)!
                .GetField(runtime.Sentinels.UndefinedInstance.Name, Members)!.GetValue(null);
            populate.Invoke(null, null);

            var groups = owner.Async is { } enabled ? new[] { owner.Sync, enabled } : [owner.Sync];
            foreach (var group in groups)
            {
                foreach (var field in Slots(typeof(EmittedGeneratorPrototypeKindRuntime))
                    .Where(property => property.PropertyType == typeof(FieldBuilder)))
                {
                    var handle = (FieldBuilder)field.GetValue(group)!;
                    Assert.True(nodes.Add(support.GetField(handle.Name)!.GetValue(null)!));
                }

                var functionPrototype = support.GetField(group.FunctionPrototype.Name)!.GetValue(null)!;
                var generatorPrototype = support.GetField(group.GeneratorPrototype.Name)!.GetValue(null)!;
                Assert.Same(undefined, read.Invoke(null, [functionPrototype, "emissionMarker"]));
                Assert.Same(undefined, read.Invoke(null, [generatorPrototype, "emissionMarker"]));
                var next = read.Invoke(null, [generatorPrototype, "next"]);
                Assert.NotNull(next);
                Assert.True(methods.Add(next));

                write.Invoke(null, [functionPrototype, "emissionMarker", "function"]);
                write.Invoke(null, [generatorPrototype, "emissionMarker", "iterator"]);
                populate.Invoke(null, null);
                Assert.Same(functionPrototype, support.GetField(group.FunctionPrototype.Name)!.GetValue(null));
                Assert.Same(generatorPrototype, support.GetField(group.GeneratorPrototype.Name)!.GetValue(null));
                Assert.Equal("function", read.Invoke(null, [functionPrototype, "emissionMarker"]));
                Assert.Equal("iterator", read.Invoke(null, [generatorPrototype, "emissionMarker"]));
                Assert.Same(next, read.Invoke(null, [generatorPrototype, "next"]));
            }

            Assert.DoesNotContain(loaded.GetReferencedAssemblies(), reference => reference.Name == "SharpTS");
            Assert.Equal(hosted, loaded.GetReferencedAssemblies().Any(reference => reference.Name == "SharpTS.Hosting.Abstractions"));
        }
    }

    private static PropertyInfo[] Slots(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => typeof(MemberInfo).IsAssignableFrom(property.PropertyType)).ToArray();

    private static void CheckOwner(object owner, Assembly assembly, HashSet<object> owners)
    {
        Assert.True(owners.Add(owner));
        Assert.Equal(true, owner.GetType().GetProperty("IsComplete")!.GetValue(owner));
        foreach (var property in Slots(owner.GetType()))
            Assert.Same(assembly, ((MemberInfo)property.GetValue(owner)!).Module.Assembly);
    }

    private sealed class MetadataFixture
    {
        private readonly HashSet<MethodBuilder> _marked = [];
        public EmittedGeneratorPrototypeRuntime Owner { get; } = new();
        public TypeBuilder Type { get; } = NewAssembly().DefineDynamicModule("main").DefineType("Handles");

        public MetadataFixture(bool includeAsync, string? missingScope = null, string? missingName = null)
        {
            if (includeAsync) Owner.BeginAsyncEmission();
            foreach (string scope in includeAsync ? new[] { "root", "sync", "async" } : ["root", "sync"])
            {
                object target = Scope(scope);
                foreach (var property in Slots(target.GetType()))
                    if (scope != missingScope || property.Name != missingName) Declare(target, property);
            }
        }

        public object Scope(string name) => name switch
        {
            "root" => Owner,
            "sync" => Owner.Sync,
            "async" => Owner.RequireAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        public object Declare(object target, PropertyInfo property)
        {
            string prefix = ReferenceEquals(target, Owner) ? "Root" : ReferenceEquals(target, Owner.Sync) ? "Sync" : "Async";
            object handle;
            if (property.PropertyType == typeof(TypeBuilder)) handle = Type;
            else if (property.PropertyType == typeof(FieldBuilder))
                handle = Type.DefineField(prefix + property.Name, typeof(object), FieldAttributes.Public | FieldAttributes.Static);
            else
            {
                var method = Type.DefineMethod(prefix + property.Name, MethodAttributes.Public | MethodAttributes.Static,
                    typeof(void), System.Type.EmptyTypes);
                method.GetILGenerator().Emit(OpCodes.Ret);
                handle = method;
            }
            property.SetValue(target, handle);
            return handle;
        }

        public void MarkBodies(MethodBuilder? except = null)
        {
            IEnumerable<object> groups = Owner.Async is { } enabled ? [Owner, Owner.Sync, enabled] : [Owner, Owner.Sync];
            foreach (object group in groups)
                foreach (var property in Slots(group.GetType()).Where(property => property.PropertyType == typeof(MethodBuilder)))
                {
                    var method = (MethodBuilder)property.GetValue(group)!;
                    if (!ReferenceEquals(method, except) && _marked.Add(method)) Owner.MarkBodyEmitted(method);
                }
        }
    }

    private static MethodInfo LoadedMethod(Assembly assembly, MethodInfo method) =>
        assembly.GetType(method.DeclaringType!.FullName!)!.GetMethod(method.Name, Members)!;

    private static void Expect<T>(Action action) where T : Exception =>
        Assert.IsType<T>(Assert.Throws<TargetInvocationException>(action).InnerException);

    private static PersistedAssemblyBuilder NewAssembly() =>
        new(new AssemblyName($"generator_prototype_{Guid.NewGuid():N}"), typeof(object).Assembly);

    private static Assembly SaveVerifyLoad(PersistedAssemblyBuilder builder)
    {
        using var bytes = new MemoryStream();
        builder.Save(bytes);
        bytes.Position = 0;
        using var verifier = new ILVerifier(extraProbeDirectories: [AppContext.BaseDirectory]);
        Assert.Empty(verifier.Verify(bytes));
        return Assembly.Load(bytes.ToArray());
    }
}
