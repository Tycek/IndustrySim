using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class SteelBarRoller : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.SteelIngot, 1)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.SteelBar, 1)];

    public override string Name => "Steel Bar Roller";
    public override decimal BuildCost => 500m;
    public override decimal RunningCost => 50m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
