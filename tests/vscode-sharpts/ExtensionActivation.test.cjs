const assert = require('node:assert/strict');
const fs = require('node:fs');
const Module = require('node:module');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

test('language-server startup failure preserves interpreter debugging and allows a restart retry', async () => {
    const extensionPath = fs.mkdtempSync(path.join(os.tmpdir(), 'sharpts-extension-activation-'));
    const commands = new Map();
    const factories = new Map();
    const errors = [];
    const starts = [];
    let restarts = 0;
    const subscriptions = [];
    let documentSaves = 0;
    const document = {
        uri: { fsPath: path.join(extensionPath, 'main.ts') },
        isDirty: true,
        async save() { documentSaves++; this.isDirty = false; return true; },
    };
    const folder = { uri: { fsPath: extensionPath } };
    const settings = {
        dotnetPath: 'configured-dotnet', projectFile: 'references.csproj', additionalReferences: ['extra.dll'],
    };
    const disposable = () => ({ dispose() {} });
    const vscode = {
        workspace: {
            getConfiguration() { return { get(name, fallback) { return settings[name] ?? fallback; } }; },
            getWorkspaceFolder() { return folder; },
        },
        window: {
            activeTextEditor: { document },
            createOutputChannel() { return disposable(); },
            showErrorMessage(message) { errors.push(message); return Promise.resolve(); },
        },
        commands: {
            registerCommand(name, handler) { commands.set(name, handler); return disposable(); },
        },
        debug: {
            registerDebugAdapterDescriptorFactory(type, factory) { factories.set(type, factory); return disposable(); },
            async startDebugging(...args) { starts.push(args); return true; },
        },
        DebugAdapterExecutable: class {
            constructor(command, args) { this.command = command; this.args = args; }
        },
    };
    const languageclient = {
        LanguageClient: class {
            async start() {
                assert.ok(commands.has('sharpts.debugInterpreter'), 'register debugger commands before LSP startup');
                throw new Error('language-server bundle is missing');
            }
            async restart() { restarts++; }
            async stop() {}
        },
        TransportKind: { stdio: 0 },
    };
    const originalLoad = Module._load;
    Module._load = function(request, parent, isMain) {
        if (request === 'vscode') return vscode;
        if (request === 'vscode-languageclient/node') return languageclient;
        return originalLoad.call(this, request, parent, isMain);
    };
    let extension;
    try {
        const binaryDirectory = path.join(extensionPath, 'bin', 'server');
        fs.mkdirSync(binaryDirectory, { recursive: true });
        fs.writeFileSync(path.join(binaryDirectory, 'SharpTS.DebugAdapter.dll'), 'test adapter marker');
        extension = require('../../extensions/vscode-sharpts/out/extension.js');
        await extension.activate({ extensionPath, subscriptions });
        assert.equal(errors.length, 1);
        assert.match(errors[0], /language-server bundle is missing/);
        assert.match(errors[0], /sharpts\.dotnetPath/);
        assert.match(errors[0], /Restart Language Server/);
        for (const command of ['sharpts.compile', 'sharpts.run', 'sharpts.compileAndRun', 'sharpts.debug'])
            assert.ok(commands.has(command), command);

        const adapter = factories.get('sharpts-interpreter').createDebugAdapterDescriptor({ configuration: {} });
        assert.equal(adapter.command, 'configured-dotnet');
        assert.deepEqual(adapter.args, [path.join(binaryDirectory, 'SharpTS.DebugAdapter.dll')]);
        await commands.get('sharpts.debugInterpreter')();
        assert.equal(documentSaves, 1);
        assert.equal(starts.length, 1);
        assert.equal(starts[0][1].program, document.uri.fsPath);
        assert.equal(starts[0][1].project, 'references.csproj');
        assert.deepEqual(starts[0][1].references, ['extra.dll']);
        assert.deepEqual(starts[0][2], { suppressSaveBeforeStart: true });
        await commands.get('sharpts.restartServer')();
        assert.equal(restarts, 1);
        assert.equal(errors.length, 1);
    } finally {
        Module._load = originalLoad;
        await extension?.deactivate();
        for (const subscription of subscriptions) subscription.dispose();
        fs.rmSync(extensionPath, { recursive: true, force: true });
    }
});
