using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class PlasticPlant : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.PlasticResin, 1)];

    private static readonly IReadOnlyList<Resource> _outputs =
        [new Resource(ResourceNames.Plastics, 2)];

    public override string Name => "Plastic Plant";
    public override decimal BuildCost => 800m;
    public override decimal RunningCost => 80m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
