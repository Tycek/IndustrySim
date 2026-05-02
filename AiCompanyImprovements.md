# AI Company Improvements

Gaps and missing behaviors identified by analysis of `AiCompany.cs`, `AiCompanyGenerator.cs`, and `GameLoop.cs`.

---

## Correctness issues (cause observable bugs)

### 3. No graceful contract exit
When a mine depletes and delivery contracts become unfulfillable, the AI passively accumulates strikes one by one until cancellation with a penalty. There is no attempt to:
- Stop accepting new Buy contracts when already overcommitted
- Source a spot-buy to cover a turn and avoid a strike
- Intentionally exit a contract before the third strike if the deficit is clearly unrecoverable

---

## Strategic decision-making gaps

### 4. No mid-game industry building
Companies are generated with 1–2 industries and can never expand. The generator's scoring logic (`Score`, `ComputeNetBalance`, `AllFactories`) is pure and would work identically mid-game. A company that starts as a Coal Mine will always only be a Coal Mine, even if the market clearly signals that a CokeOven would be profitable.

**Architecture note (from prior analysis):** `Score`, `IsViable`, `ComputeNetBalance`, and `AllFactories` need to be promoted from `private` to `internal static` in `AiCompanyGenerator`. A new `ConsiderBuildingIndustry(netBalance, rng)` method is added to `AiCompany`. `GameLoop.ProcessTurn` computes netBalance once per turn and calls it. Gates: score threshold > 2.0, balance safety (3× build cost + runway), probability roll (~10–15%).

---

### 5. Stockpile completely ignored in market decisions
Surplus calculation is purely rate-based (production minus consumption per turn). Existing inventory is invisible to it. An AI with 500 Iron Ore stockpiled and a small per-turn deficit still buys more, and an AI sitting on a large surplus won't take a contract it could comfortably cover from inventory. Stock should adjust aggressiveness: high stockpile → more willing to sell at lower prices or absorb delivery contracts; near-zero stockpile → more cautious about committing.

---

### 6. No low-balance defensive behavior
When an AI is running low on cash but not yet bankrupt, behavior does not change. No aggressive liquidation of stockpile, no pause on posting new offers, no preference for high-margin contracts. The AI acts normally until it crosses the insolvency threshold and dissolves.

---

## Market participation quality

### 7. No offer retraction or repricing
If an AI posted a Sell offer at $9 that nobody accepted for several turns, it never lowers the price. If competitors flood the market with cheap offers, the AI doesn't pull its own overpriced listings. Offers sit until they expire with no adaptation.

---

### 8. Static price tolerance
The AI always accepts offers within ±15% of base price and posts at 90–110% of base. This does not adjust for:
- **Urgency**: desperately short of an input → should accept up to 130% of base
- **Abundance**: sitting on a large stockpile → willing to sell at 80% to move stock
- **Competition**: many similar offers on the market → need to price competitively

Ties directly into the DynamicPricing.md plan — once a `PriceIndex` exists per resource, the tolerance bands should reference it instead of hardcoded percentages.

---

### 9. Offer size doesn't scale with surplus magnitude
`PostToMarket` caps posted offer quantities at `min(net, 50)`. A company with a surplus of 300 still only posts 50 units. Large production gluts never create large sell-offs, so the market doesn't flood even when it economically should.

---

### 10. Spawn rate ignores market conditions
New companies spawn on a fixed schedule (every 10 turns, 30% chance). There is no signal saying "Coal is critically short — higher spawn probability for a Coal Mine." The generator picks intelligently based on net balance once triggered, but the decision of *when* to spawn is blind to market pressure. Could check if any resource has a strongly negative net balance as an additional trigger.

---

## Priority order

| Priority | Item | Reason |
|----------|------|---------|
| High | #4 Mid-game industry building | Biggest strategic gap; architecture is ready |
| Medium | #3 Graceful contract exit | Reduces passive bankruptcy spiral |
| Medium | #5 Stockpile in market decisions | Makes trading feel rational |
| Medium | #8 Dynamic price tolerance | Depends on DynamicPricing.md being implemented first |
| Low | #6 Low-balance defense | Nice polish, low complexity |
| Low | #7 Offer retraction | Reduces market noise |
| Low | #9 Offer size scaling | Economic realism |
| Low | #10 Market-condition spawn rate | Small improvement to entry timing |
