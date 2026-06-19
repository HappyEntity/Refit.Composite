# Refit.Composite [![NuGet Version](https://img.shields.io/nuget/v/HappyEntity.Refit.Composite)](https://www.nuget.org/packages/HappyEntity.Refit.Composite/)

A lightweight, high-performance architectural wrapper for [Refit](https://github.com/reactiveui/refit) that orchestrates multiple isolated API contracts into unified, Scoped composite services with sophisticated message handler pipeline management.

## Features

- 🏎️ **High Performance:** Compile-time Source Generator orchestration powered by a hardware-level Lock-Free cache with absolutely zero allocations on subsequent property resolutions.
- 🧩 **Clean Architecture:** Group isolated Refit interfaces into single unified contracts without boilerplate abstract classes or manual dependency factories.
- 🛡️ **Declarative Pipeline Control:** Manage individual `DelegatingHandler` chains using native C# 12+ generic attributes directly over API definitions.
- 🚀 **Native AOT & Blazor WASM:** 100% compatible with Ahead-Of-Time compilation and browser environments out of the box, producing zero runtime compilation or formatting warnings.
- 🪵 **Built-in Quality of Life:** Seamless, out-of-the-box integration with high-speed, status-code-aware HTTP logging handlers.

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

### 2. Register via Dependency Injection

Invoke the registration extension within your application bootstrap (`Program.cs`). The library takes care of building independent HttpClient configurations for every inner interface contract and safely mounts the Dynamic Proxy:

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Explicitly register your custom pipeline dependencies
services.AddTransient<AntiforgeryHandler>();
services.AddTransient<CustomLoggingHandler>();

// Boot up the composite workspace
services.AddRefitComposite<IMyCompositeApi>("https://jsonplaceholder.typicode.com/");

var provider = services.BuildServiceProvider();
```

### 3. Resolve and Execute

Consume the unified interface through standard constructor injection like any ordinary Scoped service:

```csharp
var composite = provider.GetRequiredService<IMyCompositeApi>();

// Seamless execution over high-performance lock-free hardware cache
string postData = await composite.Test.GetPostAsync();
Console.WriteLine(postData);
```

## Pipeline Priority Engine

Attributes are processed linearly from top to bottom, providing complete granularity over HTTP message handler setup:

1. `[ApiHandler<T>]` — appends a specific `DelegatingHandler` to the active execution queue.
2. `[ApiIgnoreHandler<T>]` — surgically drops an upstream-declared handler from the active property sequence.
3. `[ApiIgnoreAllHandlers]` — completely flushes the configuration timeline up to its line of declaration.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
