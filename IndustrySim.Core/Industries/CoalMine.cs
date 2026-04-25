using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class CoalMine : MineBase
{
    public CoalMine() => Capacity = 1000;

    public override string Name => "Coal Mine";
    protected override Resource BaseOutput => new(ResourceNames.Coal, 10);
    protected override decimal BaseBuildCost => 500m;
    protected override decimal BaseCost => 50m;
}
