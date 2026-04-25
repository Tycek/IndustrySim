using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class CokeOven : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.Coal, 2)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.CoalCoke, 1)];

    public override string Name => "Coke Oven";
    public override decimal BuildCost => 300m;
    public override decimal RunningCost => 30m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
