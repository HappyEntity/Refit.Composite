# Refit.Composite [![NuGet Version](https://img.shields.io/nuget/v/HappyEntity.Refit.Composite)](https://www.nuget.org/packages/HappyEntity.Refit.Composite/)

A lightweight, high-performance architectural wrapper for [Refit](https://github.com/reactiveui/refit) that orchestrates multiple isolated API contracts into unified, Scoped composite services with sophisticated message handler pipeline management.

## Features

- 🏎️ **High Performance:** A source generator implements every composite at compile time. Clients are cached lock-free, so repeated property access allocates nothing.
- 🧩 **Clean Architecture:** Group isolated Refit interfaces into single unified contracts without boilerplate abstract classes or manual dependency factories.
- 🛡️ **Declarative Pipeline Control:** Manage individual `DelegatingHandler` chains using native C# generic attributes directly over API definitions. Pipelines are resolved at compile time and mistakes are reported as build errors.
- 🚀 **Native AOT & Blazor WASM:** Registration is reflection-free and uses Refit's generated clients, so there is no runtime code generation or trimming warning.
- 🔀 **Isolated Clients:** Every client is keyed by its composite, so the same API interface can live in several composites with different base addresses and handlers.
- 🪵 **Built-in Quality of Life:** Seamless, out-of-the-box integration with high-speed, status-code-aware HTTP logging handlers.

## Requirements

- Refit **15.0 – 16.x** (`Refit.HttpClientFactory`).
- .NET 8, 9, 10 or .NET Framework 4.6.2+.
- .NET SDK 8.0.100+ / Visual Studio 17.8+ (Roslyn 4.8), the same floor as Refit's own source generator.

## Installation

Install the package via NuGet Package Manager CLI:

```bash
dotnet add package HappyEntity.Refit.Composite
```

## Quick Start

### 1. Define Your Refit Contracts & Composite Interface

Declare your single micro-interfaces using standard Refit attributes. Then, compose them into an aggregate interface inheriting from `IRefitComposite`. You can assign HTTP handlers globally or to specific endpoints using contemporary generic attributes:

```csharp
using Refit;
using Refit.Composite;
using Refit.Composite.Attributes;

// Applied globally: executes for both Test and Users endpoints
[ApiHandler<AntiforgeryHandler>]
public interface IMyCompositeApi : IRefitComposite
{
    // Inherits global handlers + built-in ShortLoggingHandler
    ITestApi Test { get; }

    // Overrides pipeline: resets all accumulated handlers and applies ONLY CustomLoggingHandler
    [ApiIgnoreAllHandlers]
    [ApiHandler<CustomLoggingHandler>]
    IUserApi Users { get; }
}

public interface ITestApi
{
    [Get("/posts/1")]
    Task<string> GetPostAsync();
}

public interface IUserApi
{
    [Get("/users/1")]
    Task<string> GetUserAsync();
}
```

Composites may be `public` or `internal`, nested in other types, and may inherit other composites.

### 2. Register via Dependency Injection

Invoke the registration extension within your application bootstrap (`Program.cs`). Every API property gets its own named `HttpClient` and handler pipeline:

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Handlers used in attributes are registered as transient services automatically,
// unless you have already registered them yourself.
services.AddRefitComposite<IMyCompositeApi>(
    "https://jsonplaceholder.typicode.com/",
    configure: builder => builder.ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30)));

var provider = services.BuildServiceProvider();
```

`configure` runs for each API client after the handlers declared with attributes, so it is the place for timeouts, primary handlers or Refit 16.4's `HttpRetryHandler`.

### 3. Resolve and Execute

Consume the unified interface through standard constructor injection like any ordinary Scoped service:

```csharp
var composite = provider.GetRequiredService<IMyCompositeApi>();

string postData = await composite.Test.GetPostAsync();
Console.WriteLine(postData);
```

Each API interface also stays injectable on its own (`provider.GetRequiredService<ITestApi>()`). When several composites share an interface, the first registration wins; resolve `GetRequiredKeyedService<ITestApi>(typeof(IMyCompositeApi))` to pick one explicitly.

## Pipeline Priority Engine

Attributes are processed linearly from top to bottom, providing complete granularity over HTTP message handler setup:

1. `[ApiHandler<T>]` — appends a specific `DelegatingHandler` to the active execution queue.
2. `[ApiIgnoreHandler<T>]` — surgically drops an upstream-declared handler from the active property sequence.
3. `[ApiIgnoreAllHandlers]` — completely flushes the configuration timeline up to its line of declaration.

The first handler in the resulting list is the outermost one.

## Logging

`ShortLoggingHandler` is part of every pipeline unless it is ignored. It logs the method, the URI **without its query string**, the status code and the duration. 5xx responses are logged as errors, 401/403 as warnings, and transport failures and cancellations are logged too.

## Testing

Clients are keyed by their composite type, so a fake replaces one client of one composite:

```csharp
services.AddRefitComposite<IMyCompositeApi>("https://localhost");
services.AddKeyedSingleton<ITestApi>(typeof(IMyCompositeApi), new FakeTestApi());
```

## Diagnostics

| ID | Severity | Meaning |
|----|----------|---------|
| RCOMP001 | Error | The composite declares something other than get-only API properties (methods, setters, events, non-interface properties). |
| RCOMP002 | Error | The same API interface is exposed by two properties of one composite. |
| RCOMP003 | Error | The composite, an API interface or a handler is `private`/`protected` and unreachable from generated code. |
| RCOMP004 | Error | Generic composites are not supported. |
| RCOMP005 | Error | A handler is not a non-abstract class deriving from `DelegatingHandler`. |
| RCOMP006 | Warning | An API interface has no Refit HTTP methods, so Refit cannot generate a client for it. |

## Migrating from 2.x

- Refit **15.0+** is required. Clients are created with `AddKeyedRefitGeneratedClient`, so the `Refit.Reflection` package that Refit 14+ split out is not needed.
- `netstandard2.0` is no longer a target, because Refit dropped it in 12.0.
- The hardcoded 10-minute `HttpClient.Timeout` is gone, so the .NET default of 100 seconds applies. Set your own through `configure`.
- Clients are registered as keyed services. Replace them in tests with `AddKeyedSingleton<TApi>(typeof(TComposite), fake)` instead of a plain registration.
- The generated implementation is now `internal` and lives in the `Refit.Composite.Generated` namespace.
- Calling `AddRefitComposite<T>` twice for the same composite now throws, and a relative base URL is rejected.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
