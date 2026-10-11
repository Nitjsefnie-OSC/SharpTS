# SharpTS Debug Adapter

`sharpts-dap` is the client-independent Debug Adapter Protocol server for interpreted SharpTS
programs. It uses DAP framing over stdin/stdout; stdout must not be used for logs.

Most users start it through the SharpTS VS Code extension. Other DAP clients can launch the tool
and send a `launch` request whose `program` property names a saved TypeScript entry file. See
[`docs/debugging-interpreter.md`](../../docs/debugging-interpreter.md) for configuration, security,
capabilities, and limitations.

Install with `dotnet tool install --global SharpTS.DebugAdapter`; use `sharpts-dap --version` to
confirm the adapter and client bundle are from the expected release.

For release validation, `pwsh ./scripts/test-debug-adapter-package.ps1 -PackagePath <candidate.nupkg>`
installs the exact candidate from a local-only feed into a clean tool directory and launches its
installed `sharpts-dap` shim through raw DAP. It checks a multi-file program, a verified source
breakpoint, stack inspection, evaluation, output, and natural disconnect exit with stdin still
open. The release workflow runs this check before the candidate can be uploaded. Version output
alone does not verify that the packaged interpreter and its dependencies can run.

The same client-independent flow can exercise an existing tool directly:

```powershell
pwsh ./scripts/test-debug-adapter-launch.ps1 -AdapterPath sharpts-dap -ResultsDirectory artifacts/dap-launch
```

Package and protocol checks retain their logs, framed-message transcript, and result under
`artifacts/dap-package/run-*/`. Each protocol run has a 30-second total deadline, including I/O,
and process failure cleanup waits at most three additional seconds. Pack, install, and version
checks have a 180-second total deadline that includes redirected stdout/stderr completion;
descendant processes retaining those pipes cannot make the check wait indefinitely.
