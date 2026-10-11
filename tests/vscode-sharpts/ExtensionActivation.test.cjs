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
    const clients = [];
    const startupOutcomes = [
        new Error('language-server bundle is missing'),
        new Error('language-server still unavailable'),
        undefined,
        new Error('language-server failed during restart'),
        undefined,
        undefined,
    ];
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
    const clientResource = () => ({ disposeCalls: 0, dispose() { this.disposeCalls++; } });
    const assertDisposedResources = (client, attempts) => {
        assert.equal(client.outputChannels.length, attempts, 'do not recreate a disposed client channel');
        assert.equal(client.diagnosticCollections.length, attempts);
        for (const resource of [...client.outputChannels, ...client.diagnosticCollections])
            assert.equal(resource.disposeCalls, 1, 'dispose each owned startup resource exactly once');
    };
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
            constructor() {
                this.state = 'Initial';
                this.disposed = false;
                this.outputChannels = [];
                this.diagnosticCollections = [];
                clients.push(this);
            }
            get outputChannel() {
                if (!this.channel) {
                    this.channel = clientResource();
                    this.outputChannels.push(this.channel);
                }
                return this.channel;
            }
            get diagnostics() { return this.diagnosticCollection; }
            async start() {
                assert.ok(commands.has('sharpts.debugInterpreter'), 'register debugger commands before LSP startup');
                if (this.disposed) throw new Error('Client got disposed and cannot be restarted.');
                // v9 caches a rejected startup promise, so start() cannot retry StartFailed.
                if (this.startPromise) return this.startPromise;
                this.state = 'Starting';
                this.diagnosticCollection = clientResource();
                this.diagnosticCollections.push(this.diagnosticCollection);
                // Client logging can create its owned output channel before startup fails.
                void this.outputChannel;
                const outcome = startupOutcomes.shift();
                this.startPromise = Promise.resolve().then(() => {
                    if (outcome) {
                        this.state = 'StartFailed';
                        throw outcome;
                    }
                    this.state = 'Running';
                });
                return this.startPromise;
            }
            async restart() {
                restarts++;
                await this.stop();
                await this.start();
            }
            async stop() {
                if (this.state === 'StartFailed')
                    throw new Error("Client is not running and can't be stopped. Its current state is: startFailed");
                if (this.state === 'Initial' || this.state === 'Stopped') return;
                this.diagnosticCollection?.dispose();
                this.diagnosticCollection = undefined;
                this.channel?.dispose();
                this.channel = undefined;
                this.state = 'Stopped';
                this.startPromise = undefined;
                const outcome = this.stopOutcome;
                this.stopOutcome = undefined;
                // Running shutdown can reject after its finally block cleans up the resources.
                if (outcome) throw outcome;
            }
            async dispose() {
                this.disposed = true;
                await this.stop();
            }
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
        assertDisposedResources(clients[0], 1);
        assert.equal(clients[1].outputChannels.length, 0, 'replacement stays idle until a retry');
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
        assert.equal(errors.length, 2);
        assert.match(errors[1], /language-server still unavailable/);
        assert.match(errors[1], /Restart Language Server/);
        assert.equal(clients[0].disposed, true);
        assert.equal(clients[1].disposed, true);
        assertDisposedResources(clients[1], 1);
        assert.equal(clients[2].outputChannels.length, 0);
        await commands.get('sharpts.restartServer')();
        assert.equal(restarts, 2);
        assert.equal(errors.length, 2);
        assert.equal(clients.at(-1).state, 'Running');

        // A restart can itself fail while starting; the following retry must also recover.
        await commands.get('sharpts.restartServer')();
        assert.equal(errors.length, 3);
        assert.match(errors[2], /language-server failed during restart/);
        assert.equal(clients[2].disposed, true);
        assertDisposedResources(clients[2], 2);
        assert.equal(clients[3].outputChannels.length, 0);
        await commands.get('sharpts.restartServer')();
        assert.equal(restarts, 4);
        assert.equal(clients.at(-1).state, 'Running');
        assert.equal(errors.length, 3);
        clients[3].stopOutcome = new Error('language-server shutdown failed');
        await commands.get('sharpts.restartServer')();
        assert.equal(errors.length, 4);
        assert.match(errors[3], /language-server shutdown failed/);
        assert.equal(clients[3].disposed, true);
        assertDisposedResources(clients[3], 1);
        assert.equal(clients[4].outputChannels.length, 0);
        await commands.get('sharpts.restartServer')();
        assert.equal(restarts, 6);
        assert.equal(clients.at(-1).state, 'Running');
        assert.equal(errors.length, 4);
        assert.equal(startupOutcomes.length, 0);
        await extension.deactivate();
        assertDisposedResources(clients[4], 1);
    } finally {
        Module._load = originalLoad;
        await extension?.deactivate();
        for (const subscription of subscriptions) subscription.dispose();
        fs.rmSync(extensionPath, { recursive: true, force: true });
    }
});
