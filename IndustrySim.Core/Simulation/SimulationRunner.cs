using IndustrySim.Core.Industries;

namespace IndustrySim.Core.Simulation;

public record SimulationTurnResult(
    int     Turn,
    decimal Revenue,
    decimal RunningCosts,
    decimal Balance);

public class SimulationResult
{
    public required IReadOnlyList<SimulationTurnResult> Turns { get; init; }
    public decimal TotalRevenue      => Turns.Sum(t => t.Revenue);
    public decimal TotalRunningCosts => Turns.Sum(t => t.RunningCosts);
    public decimal NetProfit         => TotalRevenue - TotalRunningCosts;
}

public class SimulationRunner
{
    /// <summary>
    /// Runs a headless simulation. Each turn, industries produce, all remaining inventory
    /// is sold at <paramref name="fixedPrices"/>, and running costs are deducted.
    /// Balance starts at zero so the result reflects operational profit only.
    /// </summary>
    public SimulationResult Run(
        IEnumerable<Func<IIndustry>> factories,
        IReadOnlyDictionary<string, decimal> fixedPrices,
        int turnCount)
    {
        var inventory  = new Dictionary<string, double>();
        decimal balance = 0;
        var turns = new List<SimulationTurnResult>(turnCount);
        var industries = factories.Select(f => f()).ToList();

        for (int turn = 1; turn <= turnCount; turn++)
        {
            // Mines produce first
            foreach (var mine in industries.OfType<MineBase>().Where(m => m.IsOpen))
                foreach (var r in mine.Process(inventory))
                    inventory[r.Name] = inventory.GetValueOrDefault(r.Name) + r.Quantity;

            // Processing industries consume inputs then produce outputs
            foreach (var industry in industries.Where(i => i is not MineBase))
            {
                var produced = industry.Process(inventory);
                if (produced.Count == 0) continue;

                foreach (var input in industry.InputsRequired)
                    inventory[input.Name] = inventory.GetValueOrDefault(input.Name) - input.Quantity;

                foreach (var output in produced)
                    inventory[output.Name] = inventory.GetValueOrDefault(output.Name) + output.Quantity;
            }

            // Auto-sell all inventory at fixed prices
            decimal revenue = 0;
            foreach (var key in inventory.Keys.ToList())
            {
                if (fixedPrices.TryGetValue(key, out var price) && inventory[key] > 0)
                {
                    revenue += (decimal)inventory[key] * price;
                    inventory[key] = 0;
                }
            }
            balance += revenue;

            // Deduct running costs; closed (depleted) mines excluded
            decimal costs = 0;
            foreach (var industry in industries)
            {
                if (industry is MineBase mine && !mine.IsOpen) continue;
                costs += industry.RunningCost;
            }
            balance -= costs;

            turns.Add(new SimulationTurnResult(turn, revenue, costs, balance));
        }

        return new SimulationResult { Turns = turns };
    }
}
