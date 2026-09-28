using MediatR;

namespace evalflow_backend_api.Features.Profile.Avatar;

/// <summary>New profile picture as base64 (bare payload or a data URL).</summary>
public record UploadAvatarRecord(string Data) : IRequest<IResult>;

public record DeleteAvatarRecord : IRequest<IResult>;

public record GetAvatarRecord : IRequest<IResult>;
