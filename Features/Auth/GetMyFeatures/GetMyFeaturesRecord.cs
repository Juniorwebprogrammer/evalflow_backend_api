using MediatR;

namespace evalflow_backend_api.Features.Auth.GetMyFeatures;

public record GetMyFeaturesRecord(string RoleName) : IRequest<IResult>;

public record GetMyFeatureResponse(string Code, string Description);