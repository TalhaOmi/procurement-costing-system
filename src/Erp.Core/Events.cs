namespace Erp.Core;

/// <summary>Marker for domain events. In ERP products this is usually called a business event.</summary>
public interface IBusinessEvent { }

public record BudgetThresholdCrossed(
    int ProjectId,
    string ProjectCode,
    decimal Budget,
    decimal Actual,
    int ThresholdPct,
    string? AlertEmail) : IBusinessEvent;

public static class BudgetAlerts
{
    /// <summary>Builds the notification for a threshold event, or null when the project has no alert address.</summary>
    public static OutboxEmail? Build(BudgetThresholdCrossed e)
    {
        if (string.IsNullOrWhiteSpace(e.AlertEmail)) return null;

        var pct = e.Budget == 0 ? 0 : e.Actual / e.Budget * 100m;
        return new OutboxEmail
        {
            ToAddress = e.AlertEmail!,
            Subject = $"[Budget alert] {e.ProjectCode} has used {pct:0.#}% of budget",
            Body = $"Project {e.ProjectCode} passed its {e.ThresholdPct}% alert threshold. " +
                   $"Actual cost {e.Actual:N2} against a budget of {e.Budget:N2}."
        };
    }
}
