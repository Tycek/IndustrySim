namespace IndustrySim.UI.ViewModels;

public class PriceIndexViewModel : ViewModelBase
{
    public string Resource { get; }
    public string Current  { get; }
    public string LastTurn { get; }
    public string Low10T   { get; }
    public string High10T  { get; }
    public string Trend    { get; }

    public decimal CurrentValue  { get; }
    public decimal LastTurnValue { get; }
    public decimal Low10TValue   { get; }
    public decimal High10TValue  { get; }

    public PriceIndexViewModel(string resource, decimal current, decimal prev, decimal low, decimal high)
    {
        Resource     = resource;
        CurrentValue  = current;
        LastTurnValue = prev;
        Low10TValue   = low;
        High10TValue  = high;
        Current  = $"${current:N2}";
        LastTurn = $"${prev:N2}";
        Low10T   = $"${low:N2}";
        High10T  = $"${high:N2}";
        Trend    = current > prev + 0.005m ? "↑" :
                   current < prev - 0.005m ? "↓" : "→";
    }
}
