using IndustrySim.Core.Models;

namespace IndustrySim.Core.Industries;

public class OilWell : MineBase
{
    public OilWell() => Capacity = 1000;

    public override string Name => "Oil Well";
    protected override Resource BaseOutput => new(ResourceNames.CrudeOil, 10);
    protected override decimal BaseBuildCost => 700m;
    protected override decimal BaseCost => 70m;
}
