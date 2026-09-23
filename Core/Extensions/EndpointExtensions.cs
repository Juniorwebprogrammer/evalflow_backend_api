using evalflow_backend_api.Infrastructure.SignalR;
using evalflow_backend_api.Features.Auth.ForgotPassword;
using evalflow_backend_api.Features.Auth.GetMyFeatures;
using evalflow_backend_api.Features.Auth.Login;
using evalflow_backend_api.Features.Auth.Resend2FA;
using evalflow_backend_api.Features.Auth.ResendVerification;
using evalflow_backend_api.Features.Auth.ResetPassword;
using evalflow_backend_api.Features.Auth.Verify2FA;
using evalflow_backend_api.Features.Auth.VerifyEmail;
using evalflow_backend_api.Features.Companies.DeleteCompanies;
using evalflow_backend_api.Features.Companies.GetByIdentificationId;
using evalflow_backend_api.Features.Companies.GetByName;
using evalflow_backend_api.Features.Companies.UpdateCompaniesInformation;
using evalflow_backend_api.Features.Dashboard.GetDashboardStats;
using evalflow_backend_api.Features.Departments.CreateDepartment;
using evalflow_backend_api.Features.Departments.GetAllDepartments;
using evalflow_backend_api.Features.Departments.GetDepartment;
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
using evalflow_backend_api.Features.Profile.ChangePassword;
using evalflow_backend_api.Features.Profile.DeleteAccount;
using evalflow_backend_api.Features.Profile.GetProfileInformation;
using evalflow_backend_api.Features.Profile.UpdateProfileInformation;
using evalflow_backend_api.Features.Questions.CreateQuestion;
using evalflow_backend_api.Features.Questions.DeleteQuestion;
using evalflow_backend_api.Features.Questions.GetQuestionsByTemplate;
using evalflow_backend_api.Features.Questions.UpdateQuestion;
using evalflow_backend_api.Features.Settings.Toggle2FA;
using evalflow_backend_api.Features.Team.AcceptInvite;
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

        // Auth
        app.MapLogin();
        app.MapVerifyEmail();
        app.MapResendVerification();
        app.MapVerify2FA();
        app.MapResend2FA();
        app.MapGetMyFeatures();
        app.MapForgotPassword();
        app.MapResetPassword();

        // Settings
        app.MapToggle2FA();

        // Team
        app.MapInviteEmployee();
        app.MapAcceptInvite();
        app.MapGetEmployees();
        app.MapAssignDepartment();
        app.MapAssignSuperior();
        app.MapGetSubordinates();
        app.MapToggleUserStatus();
        app.MapAssignJobPosition();

        // Profile
        app.MapGetProfile();
        app.MapUpdateProfile();
        app.MapChangePassword();
        app.MapDeleteAccount();

        // Company
        app.MapGetCompanyByName();
        app.MapGetCompanyByIdentificationId();
        app.MapUpdateCompany();
        app.MapDeleteCompany();

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

        // Dashboard
        app.MapGetDashboardStats();

        return app;
    }

    public static void MapHubExtensions(this IEndpointRouteBuilder app)
    {
        app.MapHub<DashboardHub>("/hubs/dashboard").RequireCors("SignalR");
    }
}