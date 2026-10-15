namespace evalflow_backend_api.Domain.Constants;

/// <summary>
/// The state of a company's plan, copied from Vexxen (getAccess) for the billing user. Trialing, active,
/// canceling (canceled, access until the end of the period) and past_due (a payment failed, access during the
/// grace period) give access; none leaves the company read-only.
/// </summary>
public static class PlanStatuses
{
    public const string Trialing = "trialing";
    public const string Active = "active";
    public const string Canceling = "canceling";
    public const string PastDue = "past_due";
    public const string None = "none";

    public static bool HasAccess(string? status) => status is Trialing or Active or Canceling or PastDue;

    /// <summary>Vexxen's status, as EvalFlow keeps it. free/none (EvalFlow has no free plan) and anything unknown are none.</summary>
    public static string FromVexxen(string? status) => HasAccess(status) ? status! : None;
}
