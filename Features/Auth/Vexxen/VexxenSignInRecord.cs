using MediatR;

namespace evalflow_backend_api.Features.Auth.Vexxen;

/// <summary>POST /Auth/vexxen: the access token the frontend got from the Vexxen popup.</summary>
public record VexxenSignInRecord(string AccessToken) : IRequest<IResult>;

/// <summary>The person has a Vexxen account but no EvalFlow company yet: they go to onboarding.</summary>
public record NeedsOnboardingResponse(
    string Status,
    string Email,
    string FirstName,
    string LastName,
    /// <summary>They already have an EvalFlow plan (or trial) in Vexxen: they can create their company now.</summary>
    bool HasPlan,
    /// <summary>Where to pick a plan and start the free trial (the Vexxen catalog).</summary>
    string CatalogUrl);

/// <summary>POST /Auth/refresh: the Vexxen refresh token (the frontend keeps it in the ef_refresh cookie).</summary>
public record RefreshSessionRecord(string RefreshToken) : IRequest<IResult>;

/// <summary>POST /Auth/logout: revokes the Vexxen refresh token.</summary>
public record LogoutRecord(string RefreshToken) : IRequest<IResult>;
