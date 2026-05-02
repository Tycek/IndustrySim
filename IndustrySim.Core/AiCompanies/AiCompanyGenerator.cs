using IndustrySim.Core.Game;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Models;

namespace IndustrySim.Core.AiCompanies;

/// <summary>
/// Generates AI companies by scoring every industry against the current market landscape
/// and selecting via weighted-random. Three signals drive the score:
///   1. Shortage in outputs  — existing consumers lack a supplier (fill the gap).
///   2. Surplus in inputs    — raw materials are available, processing is viable.
///   3. Deficit in inputs    — raw materials are scarce; penalises industries that can't run.
/// A floor ensures every industry retains a tiny probability even in hostile conditions.
/// Only industries whose inputs are currently covered contribute output to the balance
/// (a CokeOven with no coal does not create a phantom CoalCoke surplus).
/// </summary>
public static class AiCompanyGenerator
{
    private static readonly string[] Names =
    [
        "Ironstone Co.",      "Blackrock Industries", "Ashford Mining",
        "Redstone Partners",  "Northern Ore Ltd.",    "Coppergate Foundry",
        "Silverton Works",    "Durham Resources",     "Midland Metals",
        "Eastwick Smelting",  "Greenvale Mining",     "Crown Foundry",
        "Highfield Resources","Westgate Materials",   "Kingsbridge Metals",
    ];

    // All buildable industry types. Adding a new IIndustry subclass here is enough
    // to include it in the generator — no other changes needed.
    internal static readonly Func<IIndustry>[] AllFactories =
    [
        () => new CoalMine(),
        () => new IronOreMine(),
        () => new CokeOven(),
        () => new IronOreSmelter(),
    ];

    private const double BaseWeight  = 1.0;
    private const double ScoreFloor  = 0.1; // minimum so no industry is completely excluded

    /// <summary>Generates <paramref name="count"/> complementary companies for game start.</summary>
    public static List<AiCompany> GenerateInitial(int count, Random rng, Player player)
    {
        var companies = new List<AiCompany>();
        var usedNames = new HashSet<string> { player.Name };

        for (var i = 0; i < count; i++)
        {
            var company = TryCreate(companies, player, rng, usedNames);
            if (company != null)
            {
                usedNames.Add(company.Name);
                companies.Add(company);
            }
        }

        return companies;
    }

    /// <summary>Attempts to spawn one new company mid-game. Returns null if no names remain.</summary>
    public static AiCompany? TryGenerateDynamic(IReadOnlyList<AiCompany> existing, Player player, Random rng)
    {
        var usedNames = new HashSet<string>(existing.Select(c => c.Name)) { player.Name };
        return TryCreate([.. existing], player, rng, usedNames);
    }

    private static AiCompany? TryCreate(
        List<AiCompany> existing, Player player, Random rng, HashSet<string> usedNames)
    {
        var name = PickUnusedName(rng, usedNames);
        if (name is null) return null;

        // Net balance across all existing companies and the player, counting only
        // industries that can actually run given available supply.
        var netBalance = ComputeNetBalance(existing, player);

        // Score every factory, then pick 1–2 industries using weighted-random selection
        // without replacement. After each pick, update the balance (if the chosen
        // industry is viable) so the next pick is scored against a realistic picture.
        var remaining     = AllFactories.Select(f => (Factory: f, Score: Score(f, netBalance))).ToList();
        var industryCount = rng.Next(1, 3); // 1 or 2
        var industries    = new List<IIndustry>();

        for (var i = 0; i < industryCount && remaining.Count > 0; i++)
        {
            var chosen = WeightedPick(remaining, rng);
            remaining.Remove(chosen);

            var built = chosen.Factory();
            industries.Add(built);

            // Only reflect this industry's contribution if it can actually run right now.
            // A CokeOven with no coal in the balance produces no CoalCoke and should not
            // make IronOreSmelter look attractive for the next pick.
            if (IsViable(built, netBalance))
            {
                foreach (var input in built.InputsRequired)
                    netBalance[input.Name] = netBalance.GetValueOrDefault(input.Name) - input.Quantity;
                foreach (var output in built.OutputsProduced)
                    netBalance[output.Name] = netBalance.GetValueOrDefault(output.Name) + output.Quantity;
            }

            // Rescore what's left with the updated balance.
            remaining = remaining
                .Select(r => (r.Factory, Score: Score(r.Factory, netBalance)))
                .ToList();
        }

        var runningCostPerTurn = industries.Sum(i => i.RunningCost);
        var startingBalance    = runningCostPerTurn * 30 + rng.Next(1_000, 5_000);

        return new AiCompany { Name = name, Balance = startingBalance, Industries = industries };
    }

    /// <summary>
    /// Scores a candidate industry against the current net balance.
    /// Higher score = stronger market signal to build this industry right now.
    /// Input deficit is allowed to drive the score negative (clamped to ScoreFloor).
    /// </summary>
    internal static double Score(Func<IIndustry> factory, Dictionary<string, double> netBalance)
    {
        var industry = factory();
        var score    = BaseWeight;

        // Shortage in outputs: existing consumers need more of what we produce.
        foreach (var output in industry.OutputsProduced)
            score += Math.Max(0, -netBalance.GetValueOrDefault(output.Name)) / output.Quantity;

        // Input availability: surplus boosts the score; deficit penalises it.
        // If the key is absent entirely (no producer exists in the economy), treat it as a
        // full deficit (-input.Quantity) rather than neutral (0) — "no supply chain" is worse
        // than "supply chain exists but is fully consumed".
        foreach (var input in industry.InputsRequired)
        {
            var available = netBalance.TryGetValue(input.Name, out var v) ? v : -input.Quantity;
            score += available / input.Quantity;
        }

        return Math.Max(ScoreFloor, score);
    }

    /// <summary>
    /// Returns true when all of <paramref name="industry"/>'s inputs are currently
    /// covered by <paramref name="netBalance"/>. Extractive industries (no inputs)
    /// are always viable.
    /// </summary>
    internal static bool IsViable(IIndustry industry, Dictionary<string, double> netBalance) =>
        industry.InputsRequired.All(input =>
            netBalance.GetValueOrDefault(input.Name) >= input.Quantity);

    /// <summary>
    /// Selects one entry from <paramref name="candidates"/> using weighted-random
    /// sampling proportional to each entry's Score.
    /// </summary>
    internal static (Func<IIndustry> Factory, double Score) WeightedPick(
        List<(Func<IIndustry> Factory, double Score)> candidates, Random rng)
    {
        var total      = candidates.Sum(c => c.Score);
        var roll       = rng.NextDouble() * total;
        var cumulative = 0.0;

        foreach (var candidate in candidates)
        {
            cumulative += candidate.Score;
            if (roll <= cumulative) return candidate;
        }

        return candidates[^1];
    }

    private static string? PickUnusedName(Random rng, HashSet<string> used)
    {
        var available = Names.Where(n => !used.Contains(n)).ToList();
        return available.Count == 0 ? null : available[rng.Next(available.Count)];
    }

    /// <summary>
    /// Net units per turn per resource across all existing companies and the player,
    /// counting only industries that can actually run given available supply.
    /// Extractive industries (no inputs) are processed first; processing industries are
    /// added iteratively until no more can run — this handles dependency chains of any
    /// depth without relying on declaration order.
    /// </summary>
    internal static Dictionary<string, double> ComputeNetBalance(
        IEnumerable<AiCompany> companies, Player player)
    {
        var allIndustries = player.Industries
            .Concat(companies.SelectMany(c => c.Industries))
            .Where(i => i is not MineBase mine || mine.IsOpen)
            .ToList();

        var net = new Dictionary<string, double>();

        // Extractive industries have no inputs and are always viable.
        foreach (var industry in allIndustries.Where(i => !i.InputsRequired.Any()))
            foreach (var output in industry.OutputsProduced)
                net[output.Name] = net.GetValueOrDefault(output.Name) + output.Quantity;

        // Processing industries: keep trying until no more can become viable.
        // Each pass may unlock industries that depend on outputs from the previous pass.
        var pending = allIndustries.Where(i => i.InputsRequired.Any()).ToList();
        bool progress;
        do
        {
            progress = false;
            foreach (var industry in pending.ToList())
            {
                if (!IsViable(industry, net)) continue;

                foreach (var input in industry.InputsRequired)
                    net[input.Name] = net.GetValueOrDefault(input.Name) - input.Quantity;
                foreach (var output in industry.OutputsProduced)
                    net[output.Name] = net.GetValueOrDefault(output.Name) + output.Quantity;

                pending.Remove(industry);
                progress = true;
            }
        }
        while (progress);

        return net;
    }
}
