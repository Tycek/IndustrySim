using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class SteelWireDrawer : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.SteelIngot, 1)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.SteelWire, 2)];

    public override string Name => "Steel Wire Drawer";
    public override decimal BuildCost => 600m;
    public override decimal RunningCost => 60m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
