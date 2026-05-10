using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SqliteWasmBlazor;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.WebWorkers;
using SpawnDev.BlazorJS.WebWorkers.Demo;
using SpawnDev.BlazorJS.WebWorkers.Demo.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSqliteWasm(o => o.BaseHref = new Uri(builder.HostEnvironment.BaseAddress).AbsolutePath);

// add BlazorJSRuntime (Javascript interop)
builder.Services.AddBlazorJSRuntime(out var JS);

// writes the global scope to console
Console.WriteLine($">>> BlazorJS Running: {JS.GlobalScope.ToString()}");

// added WebWorkerService with defaults
// WebWorkerService.TaskPool defaults: MaxPoolSize == 1, PoolSize == 0 (starts the TaskPool Worker when first requested)
builder.Services.AddWebWorkerService();

// add services used by unit tests
builder.Services.AddSingleton(builder.Configuration); // used to demo appsettings reading in workers
builder.Services.AddSingleton<IMathsService, MathsService>();
builder.Services.AddKeyedSingleton<ITestService2>("apples", (_, key) => new TestService2((string)key!));
builder.Services.AddKeyedSingleton<ITestService2>("bananas", (_, key) => new TestService2((string)key!));
builder.Services.AddSingleton<AsyncCallDispatcherTest>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// add service(s) that holds unit tests
builder.Services.AddSingleton<UnitTestsService>();

// add root elements if running in the window
if (JS.IsWindow)
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}

// SqliteWasm is initialized lazily by SqliteWasmBlazor.razor on demand.
// Initializing it here would fail in:
//   - worker scopes (TaskPool etc.) - they'd race the main thread for OPFS SAH
//   - additional windows opened via WebWorkerService.OpenWindow() - they'd race
//     the originating window for OPFS SAH (NoModificationAllowedError)
// OPFS SAHPool requires exclusive access to its files, so only the page that
// actually needs the DB should initialize it.
//
// Single-window scenarios (PWA / standalone display mode):
// If your app never opens additional windows programmatically (no
// WebWorkerService.OpenWindow() calls) and runs as a single-instance PWA -
// e.g. installed with display: "standalone" or "fullscreen" in the manifest,
// where the OS launcher reuses the existing window instead of opening a new
// one - you can safely initialize globally and skip the lazy pattern:
//
//     if (JS.IsWindow)
//     {
//         await host.Services.InitializeSqliteWasmAsync();
//     }
//
// The JS.IsWindow gate is still required to keep worker scopes out. The user
// can in theory still open the same URL in a second browser tab manually; in
// PWA installed mode that is unusual but not impossible, so surface a clear
// error in the UI if the SAH acquisition throws.

// start the app using BlazorJSRunAsync
// allows proper startup in non-window scopes
await builder.Build().BlazorJSRunAsync();