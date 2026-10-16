using evalflow_backend_api.Infrastructure.SignalR;
using evalflow_backend_api.Features.AiAnalysis.GetCycleAiAnalyses;
using evalflow_backend_api.Features.AiAnalysis.RequestAiAnalysis;
using evalflow_backend_api.Features.AdminApi;
using evalflow_backend_api.Features.Auth.GetMyFeatures;
using evalflow_backend_api.Features.Auth.Vexxen;
using evalflow_backend_api.Features.Team.GetInvitation;
using evalflow_backend_api.Features.Clarifications.CreateClarification;
using evalflow_backend_api.Features.Clarifications.GetCycleClarifications;
using evalflow_backend_api.Features.Clarifications.GetMyClarifications;
using evalflow_backend_api.Features.Clarifications.RespondClarification;
using evalflow_backend_api.Features.Companies.DeleteCompanies;
using evalflow_backend_api.Features.Plans;
using evalflow_backend_api.Features.Companies.GetByIdentificationId;
using evalflow_backend_api.Features.Companies.GetByName;
using evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;
using evalflow_backend_api.Features.Dashboard.GetDashboardStats;
using evalflow_backend_api.Features.Departments.CreateDepartment;
using evalflow_backend_api.Features.Departments.GetAllDepartments;
using evalflow_backend_api.Features.Departments.GetDepartment;
using evalflow_backend_api.Features.EvaluationComparisons.AcceptDiscrepancies;
using evalflow_backend_api.Features.EvaluationComparisons.GetCycleComparisons;
using evalflow_backend_api.Features.EvaluationCycles.CompleteEvaluationCycle;
using evalflow_backend_api.Features.EvaluationResults.DownloadEvaluationResultPdf;
using evalflow_backend_api.Features.EvaluationResults.GetCycleEvaluationResults;
using evalflow_backend_api.Features.EvaluationResults.GetMyEvaluationResults;
using evalflow_backend_api.Features.EvaluationCycles.CreateEvaluationCycle;
using evalflow_backend_api.Features.EvaluationCycles.DeleteEvaluationCycle;
using evalflow_backend_api.Features.EvaluationCycles.GetAllEvaluationCycles;
using evalflow_backend_api.Features.EvaluationCycles.ToggleTemplateInCycle;
using evalflow_backend_api.Features.EvaluationCycles.UpdateEvaluationCycle;
using evalflow_backend_api.Features.EvaluationSubmissions.DeleteSubmission;
using evalflow_backend_api.Features.EvaluationSubmissions.GenerateSubmissions;
using evalflow_backend_api.Features.EvaluationSubmissions.GetCycleSubmissions;
using evalflow_backend_api.Features.EvaluationSubmissions.GetMyCompletedSubmissions;
using evalflow_backend_api.Features.EvaluationSubmissions.GetMyPendingSubmissions;
using evalflow_backend_api.Features.EvaluationSubmissions.GetSubmissionById;
using evalflow_backend_api.Features.EvaluationSubmissions.SaveSubmissionAnswers;
using evalflow_backend_api.Features.FavoriteLists.CreateFavoriteList;
using evalflow_backend_api.Features.FavoriteLists.DeleteFavoriteList;
using evalflow_backend_api.Features.FavoriteLists.GetMyFavoriteLists;
using evalflow_backend_api.Features.FavoriteLists.ToggleTemplateInList;
using evalflow_backend_api.Features.FavoriteLists.UpdateFavoriteList;
using evalflow_backend_api.Features.JobPositions.CreateJobPosition;
using evalflow_backend_api.Features.JobPositions.GetJobPositions;
using evalflow_backend_api.Features.JobPositions.ManageJobPositions;
using evalflow_backend_api.Features.Onboarding.RegisterOwner;
using evalflow_backend_api.Features.Profile.Avatar;
using evalflow_backend_api.Features.Profile.DeleteAccount;
using evalflow_backend_api.Features.Profile.GetProfileInformation;
using evalflow_backend_api.Features.Questions.CreateQuestion;
using evalflow_backend_api.Features.Questions.DeleteQuestion;
using evalflow_backend_api.Features.Questions.GetQuestionsByTemplate;
using evalflow_backend_api.Features.Questions.UpdateQuestion;
using evalflow_backend_api.Features.Team.AssignDepartment;
using evalflow_backend_api.Features.Team.AssignJobPosition;
using evalflow_backend_api.Features.Team.AssignSuperior;
using evalflow_backend_api.Features.Team.GetEmployees;
using evalflow_backend_api.Features.Team.GetSubordinates;
using evalflow_backend_api.Features.Team.InviteEmployee;
using evalflow_backend_api.Features.Team.ToggleUserStatus;
using evalflow_backend_api.Features.Templates.CreateTemplate;
using evalflow_backend_api.Features.Templates.DeleteTemplate;
using evalflow_backend_api.Features.Templates.GetAllTemplates;
using evalflow_backend_api.Features.Templates.GetTemplateById;
using evalflow_backend_api.Features.Templates.UpdateTemplate;
using Microsoft.AspNetCore.SignalR;

namespace evalflow_backend_api.Core.Extensions;

public static class EndpointExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        // Endpoint base
        app.MapGet("/", () => "¡API de EvalFlow iniciada con Vertical Slices, Postgres y MediatR! 🚀")
            .WithName("Home");
        
        // Onboarding
        app.MapRegisterOwner();

        // Auth (con Vexxen; los flujos antiguos de contraseña y 2FA responden 410)
        app.MapVexxenAuth();
        app.MapRetiredAuth();
        app.MapGetMyFeatures();

        // Team
        app.MapInviteEmployee();
        app.MapGetInvitation();
        app.MapGetEmployees();
        app.MapAssignDepartment();
        app.MapAssignSuperior();
        app.MapGetSubordinates();
        app.MapToggleUserStatus();
        app.MapAssignJobPosition();

        // Profile
        app.MapGetProfile();
        app.MapProfileAvatar();
        app.MapDeleteAccount();

        // Company
        app.MapGetCompanyByName();
        app.MapGetCompanyByIdentificationId();
        app.MapUpdateCompany();
        app.MapDeleteCompany();

        // Plans
        app.MapPlans();

        // Departments
        app.MapCreateDepartment();
        app.MapGetDepartment();
        app.MapGetAllDepartments();

        // Job Positions
        app.MapCreateJobPosition();
        app.MapGetJobPositions();
        app.MapUpdateJobPosition();
        app.MapDeleteJobPosition();

        // Templates
        app.MapCreateTemplate();
        app.MapGetTemplateById();
        app.MapGetAllTemplates();
        app.MapUpdateTemplate();
        app.MapDeleteTemplate();

        // Favorite Lists
        app.MapCreateFavoriteList();
        app.MapGetMyFavoriteLists();
        app.MapUpdateFavoriteList();
        app.MapDeleteFavoriteList();
        app.MapToggleTemplateInList();
        
        // Evaluation Cycle
        app.MapCreateEvaluationCycle();
        app.MapGetAllEvaluationCycles();
        app.MapUpdateEvaluationCycle();
        app.MapDeleteEvaluationCycle();
        app.MapToggleTemplateInCycle();
        
        // Questions
        app.MapCreateQuestion();
        app.MapGetQuestionsByTemplate();
        app.MapUpdateQuestion();
        app.MapDeleteQuestion();
        
        // GenerateSubmissions
        app.MapGenerateSubmissions();
        app.MapGetMyPendingSubmissions();
        app.MapGetMyCompletedSubmissions();
        app.MapGetSubmissionById();
        app.MapSaveSubmissionAnswers();
        app.MapDeleteSubmission();
        app.MapGetCycleSubmissions();
        app.MapGetCycleComparisons();
        app.MapAcceptDiscrepancies();
        app.MapCompleteEvaluationCycle();

        // Evaluation Results
        app.MapGetMyEvaluationResults();
        app.MapGetCycleEvaluationResults();
        app.MapDownloadEvaluationResultPdf();

        // Clarifications
        app.MapCreateClarification();
        app.MapGetCycleClarifications();
        app.MapGetMyClarifications();
        app.MapRespondClarification();

        // AI Analysis
        app.MapRequestAiAnalysis();
        app.MapGetCycleAiAnalyses();

        // Dashboard
        app.MapGetDashboardStats();

        // Admin API para Vexxen Administrator (solo con el esquema VexxenAdmin)
        app.MapAdminApi();

        return app;
    }

    public static void MapHubExtensions(this IEndpointRouteBuilder app)
    {
        app.MapHub<DashboardHub>("/hubs/dashboard").RequireCors("SignalR");
    }
}