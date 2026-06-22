namespace FinApp.Domain.Periods;

public enum PeriodStatus
{
    /// <summary>The active period — expenses and contributions can be recorded.</summary>
    Open = 0,

    /// <summary>Reconciled and closed; retained for history and carry-over validation.</summary>
    Closed = 1,
}
