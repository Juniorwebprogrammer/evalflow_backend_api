namespace evalflow_backend_api.Infrastructure.SignalR;

/// <summary>
/// Payload sent over <see cref="DashboardHub"/> as the "EvaluationCompleted" event
/// whenever a respondent (subordinate or evaluator) finishes and saves a submission.
/// Carries enough context (names, template, whether it's a self-evaluation) so the
/// dashboard can render an activity entry without a follow-up request.
/// </summary>
public record EvaluationCompletedNotification(
    int SubmissionId,
    int CycleId,
    int RespondentUserId,
    string RespondentUserName,
    int EvaluatedUserId,
    string EvaluatedUserName,
    string TemplateTitle,
    bool IsSelfEvaluation,
    DateTime Timestamp
);
