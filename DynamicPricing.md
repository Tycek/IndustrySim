# Dynamic Pricing & AI Market Behavior

## Goal

Replace the current random price variation (±20% of hardcoded base prices) with a demand-driven price index that responds to visible market activity. AI companies should react to price signals, post their own offers, and occasionally spawn new industries when shortages are detected. The player should be able to see whether the current market price for a resource is cheap or expensive relative to the running index.

---

## Design Principles

- **Visible supply/demand only**: price signals come from offers and contracts on the market, not from hidden stockpiles. A player sitting on 500 coal and not posting creates no downward pressure — that is intentional.
- **Slow drift, not instant reset**: prices move gradually (capped per turn) so the player has time to react.
- **Incremental and testable**: each of the five implementation steps below is independently buildable and testable without breaking the others.

---

## Data Structures

### `Market.PriceIndex`
```
Dictionary<string, decimal> PriceIndex
```
One entry per resource name (Coal, IronOre, CoalCoke, SteelIngot). Initialized to current hardcoded base prices. Persists across turns — it is the running "fair price" signal.

### `Market.PreviousPriceIndex`
```
Dictionary<string, decimal> PreviousPriceIndex
```
Copy of `PriceIndex` from the previous turn, used to compute trend direction (up / down / stable) for the player UI without storing a full history.

### Offer imbalance (transient, computed each turn)
```
Dictionary<string, double> sellPressure   // sum of Sell offer quantities per resource
Dictionary<string, double> buyPressure    // sum of Buy offer quantities + Buy contract quantities per resource
```
Computed inside `GameLoop.ProcessTurn` from `Market.Offers` and all `ActiveContracts` across player and AIs. Passed into `Market.AdjustPrices(...)`.

---

## Price Adjustment Algorithm

Called once per turn **after** contracts execute but **before** `GenerateOffers`.

```
ratio = sellPressure[r] / buyPressure[r]   (default 1.0 if both are zero)

if ratio > 1.2  → price drifts down by up to MaxDriftPerTurn
if ratio < 0.8  → price drifts up   by up to MaxDriftPerTurn
otherwise       → price drifts toward base price by half of MaxDriftPerTurn (mean reversion)
```

**Constants (tunable):**
- `MaxDriftPerTurn = 0.03` (3% per turn maximum move)
- `PriceFloor = basePrice × 0.40` (prices cannot fall below 40% of base)
- `PriceCeiling = basePrice × 2.50` (prices cannot rise above 250% of base)

`GenerateOffers` replaces all hardcoded base price constants with `PriceIndex[resourceName]`. The existing ±20% random variance is applied on top of the index, not on top of the hardcoded values.

---

## Implementation Steps

### Step 1 — Price Index in Market (Core only, no UI changes)

**Files to change:**
- `IndustrySim.Core/Market/Market.cs`
  - Add `PriceIndex` and `PreviousPriceIndex` dictionaries with initialization from hardcoded base prices.
  - Add method `AdjustPrices(Dictionary<string, double> sellPressure, Dictionary<string, double> buyPressure)` that applies the drift algorithm above.
  - Change `GenerateOffers` to use `PriceIndex[r]` instead of inline constants.

- `IndustrySim.Core/Game/GameLoop.cs`
  - In `ProcessTurn`, after contract execution and before `GenerateOffers`, compute sell/buy pressure by iterating `State.Market.Offers` and all active contracts across player + AIs.
  - Call `State.Market.AdjustPrices(sellPressure, buyPressure)`.

**Test coverage:** verify that a market with only Sell offers causes `PriceIndex` to drift down after N turns; only Buy offers causes it to drift up; balanced offers stabilize near base price.

---

### Step 2 — AI Uses Price Index for Acceptance Decisions

**Files to change:**
- `IndustrySim.Core/AiCompanies/AiCompany.cs` (or wherever `AcceptOffers` logic lives)
  - Replace hardcoded `basePrice × 1.15` threshold with `PriceIndex[resourceName] × 1.15`.
  - `ProcessTurn` or the acceptance method must receive or have access to `Market.PriceIndex`.
  - Cleanest approach: pass `IReadOnlyDictionary<string, decimal> priceIndex` as a parameter alongside the existing `participants` dictionary.

**No new data structures needed.** This is a one-line-per-resource swap in acceptance logic.

**Test coverage:** verify that an AI rejects an offer priced above `PriceIndex × 1.15` but accepts one priced below it, even when the absolute price is above the old hardcoded base.

---

### Step 3 — Player Price Indicator (UI only, no Core changes beyond Step 1)

**Goal:** show the player the current `PriceIndex` per resource and whether it is trending up, down, or stable, so they can judge if an offer is cheap or expensive.

**Files to change:**
- `IndustrySim.Core/Market/Market.cs` — expose `PriceIndex` and `PreviousPriceIndex` as public properties (already added in Step 1).

- `IndustrySim.UI/ViewModels/SummaryViewModel.cs` (or a new `PriceIndexViewModel`)
  - Add a collection of rows: Resource | Fair Price | Trend (↑ / ↓ / → based on PriceIndex vs PreviousPriceIndex).
  - Refresh alongside existing summary data after each `ProcessTurn`.

- `IndustrySim.UI/Views/SummaryView.axaml`
  - Add a third DataGrid "Current Market Prices" below the existing two, showing the above rows.

- `IndustrySim.UI/ViewModels/MarketOfferViewModel.cs`
  - Add `bool IsBelowFairPrice` property (offer price < PriceIndex for that resource).
  - Optionally used for additional color-coding (distinct from the existing green/red fulfillability tint — could be a small ↓ icon or italic text).

**Note:** full multi-turn price history chart is out of scope for this feature. The trend arrow (comparing to last turn only) gives the player directional signal without requiring chart infrastructure.

---

### Step 4 — AI Posts Offers Based on Surplus/Deficit

**Goal:** AIs actively shape the market by posting their own Sell/Buy offers after running their industries, which in turn changes the offer imbalance and feeds back into the price index.

**Files to change:**
- `IndustrySim.Core/AiCompanies/AiCompany.cs`
  - Add method `PostMarketOffers(Market market, IReadOnlyDictionary<string, decimal> priceIndex)`.
  - Logic: after industry production, compute surplus per resource (inventory − expected consumption by industries next turn).
  - If `surplus[r] > SurplusThreshold` and `priceIndex[r] >= basePrice[r] × 0.90` → post a Sell offer for a portion of the surplus (not all of it, to avoid dumping).
  - If `deficit[r] > DeficitThreshold` and `priceIndex[r] <= basePrice[r] × 1.20` → post a Buy offer.
  - Pre-commitment: deduct resources (Sell) or money (Buy) from AI balance/inventory immediately, same as player posting (`TryPostOffer`).
  - Offers posted this way have `Source = ai.Name` so the expiry refund path already handles them correctly.

- `IndustrySim.Core/Game/GameLoop.cs`
  - After AI industry runs and before price adjustment, call `ai.PostMarketOffers(State.Market, State.Market.PriceIndex)` for each AI.

**Constants (tunable per AI or global):**
- `SurplusThreshold` — minimum surplus before posting (e.g. 20 units)
- `DeficitThreshold` — minimum deficit before posting (e.g. 10 units)
- `OfferFraction = 0.5` — fraction of surplus to offer (so AIs keep a buffer)
- `OfferDuration = 3` turns

**Test coverage:** verify that an AI with a large coal surplus posts a Sell offer; verify that the posted offer reduces the AI's inventory immediately; verify that expiry refunds it correctly if unaccepted.

---

### Step 5 — AI Spawns Industries Based on Shortage

**Goal:** when a resource price is persistently high (shortage signal), AIs occasionally build the relevant production industry, increasing long-term supply.

**Files to change:**
- `IndustrySim.Core/AiCompanies/AiCompany.cs`
  - Add method `ConsiderBuildingIndustry(IReadOnlyDictionary<string, decimal> priceIndex, IReadOnlyList<Func<IIndustry>> catalog, Random rng)`.
  - For each resource where `priceIndex[r] >= basePrice[r] × ShortageThreshold`:
    - Find candidate industries in the catalog that produce `r`.
    - Roll `rng.NextDouble() < SpawnProbability`.
    - If roll succeeds and `Balance >= candidate.BuildCost × SafetyFactor` → build the industry (deduct cost, add to `Industries`).

- `IndustrySim.Core/Game/GameLoop.cs`
  - Pass the industry catalog (already used by the UI's build screen) and `State.Market.PriceIndex` to each AI's `ConsiderBuildingIndustry` call at the end of `ProcessTurn`.

**Constants (tunable):**
- `ShortageThreshold = 1.40` — price must be 40% above base to trigger consideration
- `SpawnProbability = 0.10` — 10% chance per turn when threshold met (so on average one build every 10 turns of shortage)
- `SafetyFactor = 3.0` — AI only builds if it has 3× the build cost in balance (avoids bankruptcy)

**Design note:** mines have finite `Capacity`. The AI should prefer processing plants when the upstream resource is already available and cheap, and prefer mines when the raw resource price is high. A simple heuristic: check `priceIndex[inputResource] < basePrice[inputResource] × 1.1` before preferring a processing plant over a mine for the same output.

**Test coverage:** verify that an AI with sufficient balance and a resource price above threshold builds the correct industry with expected probability; verify that an AI with insufficient balance does not build; verify that `BuildCost` is deducted.

---

## File Change Summary

| File | Change |
|------|--------|
| `IndustrySim.Core/Market/Market.cs` | Add `PriceIndex`, `PreviousPriceIndex`, `AdjustPrices`; update `GenerateOffers` |
| `IndustrySim.Core/Game/GameLoop.cs` | Compute sell/buy pressure; call `AdjustPrices`; call AI posting and build methods |
| `IndustrySim.Core/AiCompanies/AiCompany.cs` | Update acceptance threshold; add `PostMarketOffers`; add `ConsiderBuildingIndustry` |
| `IndustrySim.UI/ViewModels/SummaryViewModel.cs` | Add price index rows |
| `IndustrySim.UI/Views/SummaryView.axaml` | Add price index DataGrid |
| `IndustrySim.UI/ViewModels/MarketOfferViewModel.cs` | Add `IsBelowFairPrice` |

---

## Tuning Notes

All threshold constants should be extracted into a single static `MarketConfig` class (or simple constants file) so they can be adjusted without hunting through multiple files. Getting the drift rate and spawn probability to feel right will require playtesting — the code structure should make it trivial to tweak these values.
