using Amazon;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.Common.Settings;
using ScholarshipManagementAPI.Helper.Middlewares;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Implementation.Common;
using ScholarshipManagementAPI.Services.Implementation.Ngo;
using ScholarshipManagementAPI.Services.Implementation.School;
using ScholarshipManagementAPI.Services.Implementation.SuperAdmin;
using ScholarshipManagementAPI.Services.Implementation.University;
using ScholarshipManagementAPI.Services.Interface.Common;
using ScholarshipManagementAPI.Services.Interface.Ngo;
using ScholarshipManagementAPI.Services.Interface.School;
using ScholarshipManagementAPI.Services.Interface.SuperAdmin;
using ScholarshipManagementAPI.Services.Interface.University;
using System.Text;
using System.Text.Json;
using UAParser;

var builder = WebApplication.CreateBuilder(args);

//var allowedOrigins =
//    builder.Configuration
//           .GetSection("Cors:AllowedOrigins")
//           .Get<string[]>() ?? Array.Empty<string>();

// Enable CORS so the Blazor client (another port) can call this API
var policyName = "AllowLocalhost";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: policyName, policy =>
         {
             policy.WithOrigins(
                 "https://localhost:7064",                    // Local dev client (Blazor WASM)
                 "https://localhost:7197",                    // Local dev client (Blazor WASM)
                 "https://sms-ui-v1.runasp.net",             // staging dev client (Blazor WASM)
                 "https://smsui.runasp.net" ,
                 "http://localhost:4200",                // staging dev client (Blazor WASM)
                 "https://sms-ui-angular.vercel.app" ,
                 "https://kafat.ifnoss.us"//Angular Ui
             )
             .AllowAnyHeader()
             .AllowAnyMethod();

             //        policy.WithOrigins(allowedOrigins)
             //              .AllowAnyHeader()
             //              .AllowAnyMethod();
         });
});




builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();

// db context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// model validation response customization
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        return new BadRequestObjectResult(new ApiResponseDto
        {
            Success = false,
            Message = "Validation failed",
            Result = errors
        });
    };
});


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),

            ClockSkew = TimeSpan.Zero
        };

        // THIS PART IS IMPORTANT
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse(); // stop default behavior

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new ApiResponseDto
                {
                    Success = false,
                    Message = "Unauthorized. Please login.",
                    Result = null
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response)
                );
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = new ApiResponseDto
                {
                    Success = false,
                    Message = "Forbidden. You do not have permission to access this resource.",
                    Result = null
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response)
                );
            }
        };

    });


// Caching : In-memory caching for frequently accessed data
// (e.g., dropdown lists, country/currency lists)
builder.Services.AddMemoryCache();

builder.Services.AddAuthorization();
builder.Services.AddScoped<IJwtService, JwtService>();

// HttpContextAccessor -- to access HttpContext in services
builder.Services.AddHttpContextAccessor();

// UAParser
builder.Services.AddSingleton<Parser>(sp =>
{
    return Parser.GetDefault();
});

builder.Services.AddTransient<ExceptionMiddleware>();

// email service
builder.Services.Configure<SmtpSettingsDto>(
    builder.Configuration.GetSection("SmtpSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();


// local file service
builder.Services.AddScoped<ILocalFileService, LocalFileService>();

// aws service
builder.Services.Configure<AwsS3OptionsDto>(
    builder.Configuration.GetSection("AWS"));

builder.Services.AddScoped<IAwsBucketService, AwsBucketService>();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = builder.Configuration.GetSection("AWS").Get<AwsS3OptionsDto>();

    if (config == null)
        throw new Exception("AWS configuration is missing");

    return new AmazonS3Client(
        config.AccessKey,
        config.SecretKey,
        RegionEndpoint.GetBySystemName(config.Region)
    );
});

// services
builder.Services.AddScoped<ICommonService, CommonService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<CurrentUserContextService>();

builder.Services.AddScoped<IGeneralSettingsService, GeneralSettingsService>();
builder.Services.AddScoped<IMasterDropDownService, MasterDropDownService>();
builder.Services.AddScoped<IMasterCountryService, MasterCountryService>();
builder.Services.AddScoped<IMasterCurrencyService, MasterCurrrencyService>();
builder.Services.AddHttpClient<ICurrencyConversionService, CurrencyConversionService>();
builder.Services.AddScoped<IUsersMenuService, UsersMenuService>();
builder.Services.AddScoped<IUsersRoleService, UsersRoleService>();
builder.Services.AddScoped<IUsersRolePermissionService, UsersRolePermissionService>();
builder.Services.AddScoped<IUsersLoginService, UsersLoginService>();
builder.Services.AddScoped<IUsersLoginLogService, UsersLoginLogService>();
builder.Services.AddScoped<IUsersRoleAssignmentService, UsersRoleAssignmentService>();
builder.Services.AddScoped<IAdminEmailTemplateService, AdminEmailTemplateService>();

builder.Services.AddScoped<IMasterSchoolService, MasterSchoolService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IStudentProgramApplicationService, StudentProgramApplicationService>();

builder.Services.AddScoped<IUniversityService, UniversityService>();
builder.Services.AddScoped<IStaffService, StaffService>();

builder.Services.AddScoped<IAccreditationService, AccreditationService>();


// new service 
builder.Services.AddScoped<IFacultiesService, FacultiesService>();
builder.Services.AddScoped<ICoursesService, CoursesService>();
builder.Services.AddScoped<IDocumentTypesService, DocumentTypesService>();
builder.Services.AddScoped<ISponsorshipMatrixService, SponsorshipMatrixService>();
builder.Services.AddScoped<ISponsorshipTypesService, SponsorshipTypesService>();
builder.Services.AddScoped<IStudentCategoryService, StudentCategoryService>();
builder.Services.AddScoped<IProgramsService, ProgramsService>();
builder.Services.AddScoped<IMarketingAdministrativeFeeService, MarketingAdministrativeFeeService>();
builder.Services.AddScoped<IProgramRegistrationWindowService, ProgramRegistrationWindowService>();
builder.Services.AddScoped<IUniversityRegistrationWindowService, UniversityRegistrationWindowService>();
builder.Services.AddScoped<IUniversityAcademicCalendarService, UniversityAcademicCalendarService>();
builder.Services.AddScoped<IStudentAcademicRegistrationService, StudentAcademicRegistrationService>();
builder.Services.AddScoped<IPanelUsersService, PanelUsersService>();
builder.Services.AddScoped<IUniversityCoordinatorService, UniversityCoordinatorService>();
builder.Services.AddScoped<ISchoolCoordinatorService, SchoolCoordinatorService>();
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<ILabelService, LabelService>();
builder.Services.AddScoped<ILanguageTranslationService, LanguageTranslationService>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();


// Swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Scholarship Management API",
        Version = "v1"
    });

    // JWT Bearer definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token as: Bearer {your_token}"
    });

    // Apply JWT globally
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});




var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scholarship Management API v1");
    c.RoutePrefix = "swagger"; // optional: serve at /swagger
});


app.UseHttpsRedirection();


app.UseCors(policyName);

app.UseStaticFiles();

// Global exception handling (EARLY)
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseMiddleware<RequestLoggingMiddleware>();


// Authentication first
app.UseAuthentication();

// Authorization after authentication
app.UseAuthorization();

// Controllers last
app.MapControllers();

app.Run();



