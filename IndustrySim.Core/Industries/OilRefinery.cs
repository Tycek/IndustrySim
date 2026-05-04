using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class OilRefinery : IndustryBase
{
    private static readonly IReadOnlyList<Resource> _inputs =
        [new Resource(ResourceNames.CrudeOil, 3)];

    private static readonly IReadOnlyList<Resource> _outputs =
    [
        new Resource(ResourceNames.Gas,          1),
        new Resource(ResourceNames.Diesel,       1),
        new Resource(ResourceNames.Kerosene,     1),
        new Resource(ResourceNames.Chemicals,    1),
        new Resource(ResourceNames.PlasticResin, 1),
    ];

    public override string Name => "Oil Refinery";
    public override decimal BuildCost => 1500m;
    public override decimal RunningCost => 150m;
    public override IReadOnlyList<Resource> InputsRequired => _inputs;
    public override IReadOnlyList<Resource> OutputsProduced => _outputs;
}
