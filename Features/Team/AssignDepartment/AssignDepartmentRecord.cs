using MediatR;
using Microsoft.AspNetCore.Http;

namespace evalflow_backend_api.Features.Team.AssignDepartment;

public record AssignDepartmentRecord(int UserId, int? DepartmentId) : IRequest<IResult>;

public record DepartmentAssignBody(int? DepartmentId);