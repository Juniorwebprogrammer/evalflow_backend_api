using evalflow_backend_api.Domain.Entities;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace evalflow_backend_api.Features.FavoriteLists.ToggleTemplateInList;

public class ToggleTemplateInListHandler(AppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<ToggleTemplateInListRecord, IResult>
{
    public async Task<IResult> Handle(ToggleTemplateInListRecord request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = currentUser.GetIdentificationId();
        
        if (userId is null || string.IsNullOrWhiteSpace(tenantId)) return Results.Unauthorized();

        var list = await GetFavoriteListWithTemplatesAsync(request.ListId, userId.Value, cancellationToken);
        if (list is null) return Results.NotFound(new { Message = "Lista no encontrada." });

        var template = await GetTemplateAsync(request.TemplateId, tenantId, cancellationToken);
        if (template is null) return Results.BadRequest(new { Message = "El template no existe o no pertenece a tu empresa." });

        var actionMessage = await ToggleAndSaveAsync(list, template, cancellationToken);

        return Results.Ok(new { Message = actionMessage });
    }

    private int? GetUserId()
    {
        var userIdStr = currentUser.GetUserId();
        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<TemplateFavoriteList?> GetFavoriteListWithTemplatesAsync(int listId, int userId, CancellationToken cancellationToken)
    {
        return await dbContext.TemplateFavoriteLists
            .Include(l => l.Templates)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, cancellationToken);
    }

    private async Task<Template?> GetTemplateAsync(int templateId, string tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.Templates
            .Include(t => t.Empresa)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.Empresa!.IdentificationId == tenantId, cancellationToken);
    }

    private async Task<string> ToggleAndSaveAsync(TemplateFavoriteList list, Template template, CancellationToken cancellationToken)
    {
        var existsInList = list.Templates.Any(t => t.Id == template.Id);
        string actionMessage;

        if (existsInList)
        {
            list.Templates.Remove(template);
            actionMessage = "Template removido de la lista.";
        }
        else
        {
            list.Templates.Add(template);
            actionMessage = "Template añadido a la lista.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return actionMessage;
    }
}