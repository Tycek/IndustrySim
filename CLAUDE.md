# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Game Overview

Turn-based strategy sandbox single-player game simulating industries, markets, and AI competitors. The player builds industries, produces materials/goods, sells them on the market, and fulfills contracts.

Implemented:
- **Industries** — extractive mines and processing plants that consume inputs and produce outputs each turn
- **Market** — randomly generated one-time buy/sell offers and recurring contracts; player accepts or ignores them
- **Contracts** — recurring agreements with strike/penalty system for non-delivery
- **Simulation** — headless N-turn analysis at fixed prices to evaluate industry setups
- **Save/Load** — JSON persistence infrastructure (stub; polymorphic IIndustry serialization not yet wired)

Planned / not yet implemented:
- **AI companies** — dynamic competitors (`// TODO` in `GameLoop.ProcessTurn`)

## Solution Structure

Three projects under the repo root:

- **IndustrySim.Core** — Class library (net9.0). All game/simulation domain logic.
- **IndustrySim.UI** — Avalonia desktop app (net9.0, WinExe). Entry point and UI layer.
- **IndustrySim.Tests** — xUnit test project (net9.0). References Core. No tests written yet.

Solution file: `IndustrySim.Core/IndustrySim.Core.sln`

## Build & Run Commands

Run from the repo root:

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

## Core Domain Model

### Namespaces

- `IndustrySim.Core.Models` — `Resource`, `ResourceNames`
- `IndustrySim.Core.Industries` — `IIndustry`, `IndustryBase`, `MineBase`, and concrete industry classes
- `IndustrySim.Core.Market` — `MarketOffer`, `Contract`, `Market`, `OfferType`
- `IndustrySim.Core.Game` — `Player`, `GameState`, `GameLoop`, `TurnEvents`
- `IndustrySim.Core.Simulation` — `SimulationRunner`, `SimulationResult`, `SimulationTurnResult`
- `IndustrySim.Core.SaveLoad` — `ISaveLoadService`, `JsonSaveLoadService`, `SaveData`

### Resource

`Resource(string Name, double Quantity)` — record type. Methods: `WithQuantity`, `Add`, `Scale`.

`ResourceNames` holds string constants: `Coal`, `IronOre`, `CoalCoke`, `SteelIngot`.

### Industries

`IIndustry` exposes `Name`, `BuildCost`, `RunningCost`, `InputsRequired[]`, `OutputsProduced[]`, and `Process(availableResources) → Resource[]`.

`IndustryBase` (abstract) — implements `Process`: checks all inputs are available; returns outputs or empty list.

`MineBase` (abstract, extends `IndustryBase`) — adds `Capacity: double` (reserves) and `IsOpen: bool`. Each turn produces `min(BaseOutput, Capacity)` and depletes capacity. Closed mines produce nothing and incur no running cost.

### Market

`OfferType` enum: `Buy` (market buys from player) or `Sell` (market sells to player).

`MarketOffer` — one-time transaction: `Id`, `Type`, `ResourceName`, `Quantity`, `PricePerUnit`, `TurnsRemaining`. Computed `TotalPrice`.

`Contract` — recurring agreement: same fields as offer plus `QuantityPerTurn`, `DurationTurns`, `TurnsAvailable` (disappears if not accepted), `TurnsRemaining` (countdown once active), `Strikes` (failures; 3 = cancellation). Computed `TotalPerTurn` and `CancellationPenalty` (= 3 × `TotalPerTurn`).

`Market` holds `Offers` and `AvailableContracts`. `GenerateOffers(rng)` runs each turn: decrements timers, removes expired, adds 2–5 new offers plus ~33% chance of one new contract. Base prices: Coal $5, Iron Ore $8, Coal Coke $15, Steel Ingot $40. Offers vary ±20%; contracts ±15%. Offer quantities: 10–100 in steps of 10. Contract quantities: 5–20 in steps of 5.

### Player & Game State

`Player` — `Name`, `Balance: decimal`, `Industries: List<IIndustry>`, `Inventory: Dictionary<string, double>`, `ActiveContracts: List<Contract>`. Method: `AddToInventory(Resource)`.

`GameState` — `TurnNumber`, `Player`, `Market`. Serializable.

`TurnEvents` — record: `(IReadOnlyList<string> DepletedMines, IReadOnlyList<string> CancelledContracts)`.

### Game Loop

`GameLoop` wraps `GameState`. Factory: `GameLoop.StartNew(playerName, startingBalance = 10000)`.

Key methods: `TryBuildIndustry`, `RemoveIndustry`, `TryAcceptOffer`, `TryAcceptContract`.

`ProcessTurn() → TurnEvents` order:
1. Increment turn number
2. Market generates new offers/contracts
3. Mines produce (add to inventory)
4. Processing industries consume inputs, produce outputs
5. Active contracts execute: buy contracts → player delivers inventory, receives payment (strike on failure); sell contracts → player pays, receives resources (strike on failure); 3 strikes → cancelled + penalty
6. Deduct running costs (closed mines excluded)
7. Return events (depleted mines, cancelled contracts)

### Simulation

`SimulationRunner.Run(factories, fixedPrices, turnCount) → SimulationResult` — headless run with no market or contracts. Each turn: industries produce, all inventory auto-sold at fixed prices, running costs deducted. Useful for break-even analysis.

`SimulationTurnResult` record: `Turn`, `Revenue`, `RunningCosts`, `Balance`.

`SimulationResult`: `Turns` list, computed `TotalRevenue`, `TotalRunningCosts`, `NetProfit`.

### Save/Load

`ISaveLoadService`: `Save(GameState, filePath)`, `Load(filePath) → GameState`, `SaveExists(filePath) → bool`. `JsonSaveLoadService` implements this with `System.Text.Json`. `SaveData` envelope wraps `GameState` with `Version` and `SavedAt`. **Known gap**: polymorphic JSON for `IIndustry` not yet configured; serialization will fail without `[JsonDerivedType]` attributes.

## UI Architecture (Avalonia + MVVM)

The UI project follows the Avalonia MVVM pattern using **CommunityToolkit.Mvvm**:

- `ViewModelBase` extends `ObservableObject` — all ViewModels must inherit from it.
- **ViewLocator** resolves Views from ViewModels by convention: replaces `"ViewModel"` with `"View"` in the full type name via reflection.
- Compiled bindings are enabled (`AvaloniaUseCompiledBindingsByDefault=true`), so XAML bindings are type-checked at compile time.
- Use `partial class` + CommunityToolkit source generators (`[ObservableProperty]`, `[RelayCommand]`).
- Avalonia DevTools included in Debug builds only.

### Value Converters

- `FulfillableToBrushConverter` — `bool → Green/Red` brush for offer/contract row backgrounds.
- `BoolToFontWeightConverter` — `bool → Bold/Normal` for summary total rows.

### Views & ViewModels

**Main Window** (`MainWindowViewModel`) — hosts the game loop, player state, and all collections. Has a notification banner for turn events (depleted mines, cancelled contracts) with a Dismiss button.

Six tabs:

**Industries** — Two DataGrids.
- *My Industries*: lists player's built industries. Mines show remaining capacity or "Depleted"; processing industries show "Active". "Close" button available only for depleted mines.
- *Build New Industry*: catalog with inputs/outputs detail on row selection; Build button deducts cost.
- ViewModels: `OwnedIndustryViewModel`, `CatalogIndustryViewModel`.

**Market** — Single DataGrid with Resource (All/Coal/Iron Ore/Coal Coke/Steel Ingot) and Type (All/Buy/Sell) filter dropdowns. Columns: Type, Resource, Quantity, Price/Unit, Total, Expires In, Accept. Row tinted green if player can fulfill, red if not.
- ViewModels: `MarketOfferViewModel` (wraps `MarketOffer`, tracks `CanFulfill`, `AcceptCommand`). Filter state in `MainWindowViewModel`.

**Contracts** — Two DataGrids.
- *Available Contracts*: pending offers with Expires In column; green/red fulfillability tinting; Accept button.
- *Active Contracts*: player's live contracts; shows Turns Left, Strikes, Status ("On Track" / "At Risk" based on current fulfillability).
- ViewModels: `AvailableContractViewModel`, `ActiveContractViewModel`.

**Stockpile** — Read-only DataGrid: Resource, Quantity. Only resources with quantity > 0, sorted alphabetically.
- ViewModel: `StockpileEntryViewModel`.

**Summary** (`SummaryView`) — Two DataGrids.
- *Production per Turn*: per-resource breakdown of Produced, Industry Use, Delivered (buy contracts), Received (sell contracts), Net.
- *Finances per Turn*: per-category income/cost breakdown; Net/Turn row in bold.
- ViewModels: `SummaryViewModel`, `ProductSummaryViewModel`, `FinanceSummaryViewModel`.

**Simulation** (`SimulationView`) — Left config panel (turn count 1–500, 4 price inputs, 4 industry checkboxes, Run button); right results panel (summary bar + per-turn table with Turn, Revenue, Costs, Net, Balance).
- ViewModels: `SimulationViewModel`, `SimulationIndustryOptionViewModel`, `SimulationTurnViewModel`.

## Implementation Notes

- Industries are written modularly; add a new class extending `IndustryBase` or `MineBase` and register it in the catalog to expose it in the UI.
- No graphics — UI uses only tables, lists, buttons, and data grids.
- Contract fulfillment uses `double` quantities but `decimal` prices; be mindful of type conversions when working in this area.
