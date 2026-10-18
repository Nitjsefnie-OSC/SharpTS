using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public sealed class MethodEnvironmentRelayTests
{
    public const string ExactStaticMethodRelay = """
        class C {
            static run() {
                let count = 1;
                function outer() {
                    function next() { count++; }
                    next();
                }
                count = 5;
                outer(); outer();
                console.log(count);
            }
        }
        C.run();
        """;

    public const string SiblingRelaysAndIndependentOwners = """
        class C {
            static make(seed: number) {
                let count = seed;
                function relay(step: number) {
                    function add() { count += step; return count; }
                    function read() { return count; }
                    return { add, read };
                }
                function direct() { return ++count; }
                count = seed + 10;
                return { relay, direct, read: () => count };
            }
        }
        const first = C.make(1), second = C.make(100);
        const a = first.relay(2), b = first.relay(3), c = second.relay(4);
        console.log(a.add(), b.read(), first.direct(), a.read());
        console.log(c.add(), second.read(), b.add(), first.read());
        """;

    public const string InstanceOwnerAndRelayParameterShadow = """
        class C {
            make(seed: number) {
                let count = seed;
                let marker = seed * 10;
                function relay(count: number) {
                    function next() {
                        marker++;
                        count += 2;
                        return marker + ":" + count;
                    }
                    return { next, read: () => count };
                }
                return { relay, read: () => count, marker: () => marker };
            }
        }
        const first = new C().make(1), second = new C().make(10);
        const a = first.relay(3), b = first.relay(7), c = second.relay(20);
        console.log(a.next(), a.read(), b.next(), b.read(), first.read(), first.marker());
        console.log(c.next(), c.read(), a.next(), a.read(), second.read(), second.marker(), first.marker());
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["exact-static-method-relay", ExactStaticMethodRelay, "7\n"];
        yield return ["sibling-relays-and-independent-owners", SiblingRelaysAndIndependentOwners, "13 13 14 14\n114 114 17 17\n"];
        yield return ["instance-owner-and-relay-parameter-shadow", InstanceOwnerAndRelayParameterShadow, "11:5 5 12:9 9 1 12\n101:22 22 13:7 7 10 101 13\n"];
    }

    public static IEnumerable<object[]> ModeCases() => Cases().SelectMany(row =>
        new[] { ExecutionMode.Interpreted, ExecutionMode.Compiled }
            .Select(mode => new object[] { row[0], row[1], row[2], mode }));

    [Theory, MemberData(nameof(ModeCases))]
    public void OrdinaryRelaysRetainTheOwningMethodEnvironment(
        string name, string source, string expected, ExecutionMode mode)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, TestHarness.Run(source, mode));
    }
}
