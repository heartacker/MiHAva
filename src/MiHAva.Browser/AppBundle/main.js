import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error("Expected to run in a browser environment");

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

await dotnet.run();
