using IndustrySim.Core.Game;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Models;

namespace IndustrySim.Core.AiCompanies;

/// <summary>
/// Generates AI companies using two-phase dependency-aware logic:
/// Phase 1 assigns extractive industries (mines); Phase 2 adds a processing industry
/// only when the required input resources are already covered by the combined supply
/// of the player and all existing AI companies (including this company's own mines).
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

    private static readonly Func<IIndustry>[] ExtractiveFactories =
    [
        () => new CoalMine(),
        () => new IronOreMine(),
    ];

    // Each entry: factory + the resource names its inputs require.
    private static readonly (Func<IIndustry> Factory, string[] RequiredInputs)[] ProcessingFactories =
    [
        (() => new CokeOven(),       [ResourceNames.Coal]),
        (() => new IronOreSmelter(), [ResourceNames.CoalCoke, ResourceNames.IronOre]),
    ];

    /// <summary>Generates <paramref name="count"/> complementary companies for the game start.</summary>/usage
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

    /// <summary>
    /// Attempts to spawn one new company mid-game. Returns null if no names remain.
    /// </summary>
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

        // Phase 1: pick 1–2 extractive industries at random.
        var industries = new List<IIndustry>();
        var extractiveCount = rng.Next(1, 3);
        foreach (var factory in ExtractiveFactories.OrderBy(_ => rng.Next()).Take(extractiveCount))
            industries.Add(factory());

        // Build the supply picture that this company will contribute to.
        var supply = ComputeTotalSupply(existing, player);
        foreach (var ind in industries)
            foreach (var output in ind.OutputsProduced)
                supply[output.Name] = supply.GetValueOrDefault(output.Name) + output.Quantity;

        // Phase 2: optionally add one processing industry if inputs are covered.
        if (rng.NextDouble() < 0.5)
        {
            foreach (var (factory, required) in ProcessingFactories.OrderBy(_ => rng.Next()))
            {
                if (required.All(r => supply.GetValueOrDefault(r) > 0))
                {
                    industries.Add(factory());
                    break;
                }
            }
        }

        var runningCostPerTurn = industries.Sum(i => i.RunningCost);
        var startingBalance    = runningCostPerTurn * 30 + rng.Next(1_000, 5_000);

        return new AiCompany { Name = name, Balance = startingBalance, Industries = industries };
    }

    private static string? PickUnusedName(Random rng, HashSet<string> used)
    {
        var available = Names.Where(n => !used.Contains(n)).ToList();
        return available.Count == 0 ? null : available[rng.Next(available.Count)];
    }

    private static Dictionary<string, double> ComputeTotalSupply(
        IEnumerable<AiCompany> companies, Player player)
    {
        var supply = new Dictionary<string, double>();

        foreach (var industry in player.Industries)
        {
            if (industry is MineBase mine && !mine.IsOpen) continue;
            foreach (var output in industry.OutputsProduced)
                supply[output.Name] = supply.GetValueOrDefault(output.Name) + output.Quantity;
        }

        foreach (var company in companies)
        {
            foreach (var industry in company.Industries)
            {
                if (industry is MineBase mine && !mine.IsOpen) continue;
                foreach (var output in industry.OutputsProduced)
                    supply[output.Name] = supply.GetValueOrDefault(output.Name) + output.Quantity;
            }
        }

        return supply;
    }
}
