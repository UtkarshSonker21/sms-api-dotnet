using ScholarshipManagementAPI.DTOs.Common;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Common;
using System.Text.Json;

namespace ScholarshipManagementAPI.Helper.Middlewares
{
    public class ExceptionMiddleware : IMiddleware
    {
        private readonly INotificationService _notificationService;
        private readonly IWebHostEnvironment _env;

        public ExceptionMiddleware(
            INotificationService notificationService,
            IWebHostEnvironment env)
        {
            _notificationService = notificationService;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new ApiResponseDto
                {
                    Success = false,
                    Message = ex.Message,  
                    Result = null
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
            }
            catch (CustomException ex)
            {
                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json";

                var response = new ApiResponseDto
                {
                    Success = false,
                    Message = ex.Message,
                    Result = null
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
            }
            catch (InvalidOperationException ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var endpoint = $"{context.Request.Method} {context.Request.Path}";
                var requestId = context.Items["RequestId"]?.ToString() ?? "N/A";

                FileLogger.LogError(requestId, ex, endpoint);

                // Send exception email only in Production
                if (_env.IsStaging() || _env.IsProduction())
                {
                    await _notificationService.SendExceptionAlertAsync(
                        ex,
                        $"{context.Request.Method} {context.Request.Path}");
                }

                var response = new ApiResponseDto
                {
                    Success = false,
                    Message = "Unable to process request at this time.",
                    Result = null
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var endpoint = $"{context.Request.Method} {context.Request.Path}";
                var requestId = context.Items["RequestId"]?.ToString() ?? "N/A";

                FileLogger.LogError(requestId, ex, endpoint);

                // Send exception email only in Production
                if (_env.IsStaging() || _env.IsProduction())
                {
                    await _notificationService.SendExceptionAlertAsync(
                        ex,
                        $"{context.Request.Method} {context.Request.Path}");
                }

                var response = new ApiResponseDto
                {
                    Success = false,
                    Message = "Internal server error.",
                    Result = null
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response));
            }
        }
    }
}
