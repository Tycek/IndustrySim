# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution Structure

Three projects under `C:\Users\<User>\source\repos\IndustrySim\`:

- **IndustrySim.Core** — Class library (net9.0). Intended for simulation domain logic. Currently empty.
- **IndustrySim.UI** — Avalonia desktop app (net9.0, WinExe). Entry point and UI layer.
- **IndustrySim.Tests** — xUnit test project (net9.0). Not yet added to the `.sln`.

The solution file (`IndustrySim.Core/IndustrySim.Core.sln`) only includes Core and UI. If you add tests to the solution, run: `dotnet sln IndustrySim.Core/IndustrySim.Core.sln add IndustrySim.Tests/IndustrySim.Tests.csproj`

## Build & Run Commands

Run from the repo root (`C:\Users\<User>\source\repos\IndustrySim\`):

```bash
# Build entire solution
dotnet build IndustrySim.Core/IndustrySim.Core.sln

# Run the UI app
dotnet run --project IndustrySim.UI/IndustrySim.UI.csproj

# Run all tests
dotnet test IndustrySim.Tests/IndustrySim.Tests.csproj

# Run a single test by name
dotnet test IndustrySim.Tests/IndustrySim.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

## UI Architecture (Avalonia + MVVM)

The UI project follows the Avalonia MVVM pattern using **CommunityToolkit.Mvvm**:

- `ViewModelBase` extends `ObservableObject` — all ViewModels must inherit from it.
- **ViewLocator** resolves Views from ViewModels by convention: replaces `"ViewModel"` with `"View"` in the full type name via reflection. So `ViewModels/FooViewModel` maps to `Views/FooView`.
- Compiled bindings are enabled by default (`AvaloniaUseCompiledBindingsByDefault=true`), so XAML bindings are type-checked at compile time — ViewModels need proper types/properties.
- Use `partial class` + CommunityToolkit source generators (`[ObservableProperty]`, `[RelayCommand]`) for reactive properties and commands.
- Avalonia DevTools are included in Debug builds only (via `AvaloniaUI.DiagnosticsSupport`).

## Implementational details
- Industries and industry chains are written modularly so they are easy to extend
- No graphics, only tables, lists, buttons, grids and graphs