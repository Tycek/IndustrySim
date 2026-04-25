using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

/// <summary>
/// A single production unit that consumes input resources and produces output resources each tick.
/// </summary>
public interface IIndustry
{
    string Name { get; }

    /// <summary>One-time cost to build this industry.</summary>
    decimal BuildCost { get; }

    /// <summary>Money deducted from the player each turn this industry operates.</summary>
    decimal RunningCost { get; }

    /// <summary>Resources consumed per tick at full operation.</summary>
    IReadOnlyList<Resource> InputsRequired { get; }

    /// <summary>Resources produced per tick at full operation.</summary>
    IReadOnlyList<Resource> OutputsProduced { get; }

    /// <summary>
    /// Runs one simulation tick. Returns the resources produced, or an empty list if
    /// inputs are insufficient.
    /// </summary>
    IReadOnlyList<Resource> Process(IReadOnlyDictionary<string, double> availableResources);
}
