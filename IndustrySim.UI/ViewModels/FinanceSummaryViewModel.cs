using System;

namespace IndustrySim.UI.ViewModels;

public class FinanceSummaryViewModel : ViewModelBase
{
    public string  Category { get; }
    public string  PerTurn  { get; }
    public bool    IsTotal  { get; }
    public decimal Value    { get; }

    public FinanceSummaryViewModel(string category, decimal value, bool isTotal = false)
    {
        Category = category;
        Value    = value;
        IsTotal  = isTotal;
        PerTurn  = value >= 0
            ? $"+${value:N0}"
            : $"-${Math.Abs(value):N0}";
    }
}
