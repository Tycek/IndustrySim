using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

/// <summary>
/// Base class for industries. Handles input sufficiency checks; subclasses declare
/// their inputs/outputs and can override <see cref="Process"/> for custom behaviour.
/// </summary>
public abstract class IndustryBase : IIndustry
{
    public abstract string Name { get; }
    public abstract decimal BuildCost { get; }
    public abstract decimal RunningCost { get; }
    public abstract IReadOnlyList<Resource> InputsRequired { get; }
    public abstract IReadOnlyList<Resource> OutputsProduced { get; }

    public bool IsSuspended { get; private set; }

    public void Suspend() => IsSuspended = true;
    public void Resume()  => IsSuspended = false;

    public virtual IReadOnlyList<Resource> Process(IReadOnlyDictionary<string, double> availableResources)
    {
        if (IsSuspended) return [];

        foreach (var input in InputsRequired)
        {
            if (!availableResources.TryGetValue(input.Name, out var qty) || qty < input.Quantity)
                return [];
        }

        return OutputsProduced;
    }
}
