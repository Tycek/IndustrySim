using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

/// <summary>
/// Base class for extractive industries. <see cref="Capacity"/> represents the total
/// remaining reserves. Each turn, production depletes it by the amount extracted.
/// A mine with <see cref="Capacity"/> of 0 is closed and produces nothing.
/// </summary>
public abstract class MineBase : IndustryBase
{
    /// <summary>Remaining extractable reserves. Decreases each turn by the amount produced.</summary>
    public double Capacity { get; set; }

    public bool IsOpen => Capacity > 0;

    protected abstract Resource BaseOutput { get; }
    protected abstract decimal BaseBuildCost { get; }
    protected abstract decimal BaseCost { get; }

    public override decimal BuildCost => BaseBuildCost;
    public override decimal RunningCost => BaseCost;
    public override IReadOnlyList<Resource> InputsRequired => [];

    /// <summary>Full output when reserves are sufficient. Used for display purposes.</summary>
    public override IReadOnlyList<Resource> OutputsProduced => [BaseOutput];

    public override IReadOnlyList<Resource> Process(IReadOnlyDictionary<string, double> availableResources)
    {
        if (IsSuspended || Capacity <= 0)
            return [];

        var baseOutput = BaseOutput;
        var produced = Math.Min(baseOutput.Quantity, Capacity);
        Capacity -= produced;
        return [baseOutput.WithQuantity(produced)];
    }
}
