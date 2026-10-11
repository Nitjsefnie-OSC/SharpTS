using SharpTS.Tests.Infrastructure;
using Xunit;

namespace SharpTS.Tests.SharedTests;

public class ModuleStrictCallbackTests
{
    [Theory, ModeData]
    public void StrictScriptCallbacksInheritTheOwningScriptsDirective(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["main.ts"] = """
                "use strict";
                const values = [1];
                const context = {tag: "context"};
                values.find(function() { console.log(this === undefined); return false; });
                values.findIndex(function() { console.log(this === undefined); return false; });
                values.findLast(function() { console.log(this === undefined); return false; });
                values.findLastIndex(function() { console.log(this === undefined); return false; });
                values.find(function() { console.log(this === null); return false; }, null);
                values.findIndex(function() { console.log(this === context); return false; }, context);
                const invoke = function() {
                    values.find(function() { console.log(this === undefined); return false; });
                };
                invoke();
                """
        };

        Assert.Equal("true\ntrue\ntrue\ntrue\ntrue\ntrue\ntrue\n",
            TestHarness.RunModules(files, "main.ts", mode));
    }

    [Theory, ModeData]
    public void StrictScriptCollectionDoesNotMakeFollowingScriptsStrict(ExecutionMode mode)
    {
        var files = new Dictionary<string, string>
        {
            ["strict.ts"] = """
                "use strict";
                [1].find(function() { console.log("strict", this === undefined); return false; });
                [1].find(function() { console.log("strict null", this === null); return false; }, null);
                """,
            ["sloppy.ts"] = """
                [1].find(function() { console.log("sloppy", this === globalThis); return false; });
                [1].find(function() { console.log("sloppy null", this === globalThis); return false; }, null);
                """,
            ["main.ts"] = """
                /// <reference path="./strict.ts" />
                /// <reference path="./sloppy.ts" />
                [1].find(function() { console.log("main", this === globalThis); return false; });
                """
        };

        Assert.Equal("strict true\nstrict null true\nsloppy true\nsloppy null true\nmain true\n",
            TestHarness.RunModules(files, "main.ts", mode));
    }
}
