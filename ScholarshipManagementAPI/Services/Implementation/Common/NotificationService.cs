using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.DTOs.SuperAdmin.AdminEmailTemplate;
using ScholarshipManagementAPI.Helper;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Common;
using ScholarshipManagementAPI.Services.Interface.SuperAdmin;
using System.Buffers.Text;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using static System.Net.Mime.MediaTypeNames;

namespace ScholarshipManagementAPI.Services.Implementation.Common
{
    public class NotificationService : INotificationService
    {
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly IAdminEmailTemplateService _adminEmailTemplateService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationService(
            IEmailService emailService,
            IConfiguration config,
            IAdminEmailTemplateService adminEmailTemplateService,
            IHttpContextAccessor httpContextAccessor)
        {
            _emailService = emailService;
            _config = config;
            _adminEmailTemplateService = adminEmailTemplateService;
            _httpContextAccessor = httpContextAccessor;
        }


        /// <summary>
        /// Sends an email notification with account credentials to a newly created user using the database template.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The username for logging in.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organizationName">The campus or organization name.</param>
        /// <param name="tempPassword">The temporary password assigned to the account.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        public async Task SendNewUserAccountAsync(
            string toEmail, string loginName, string fullName,
            string organizationName, string tempPassword)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new CustomException("Email address is required.");

            var result = await _adminEmailTemplateService.GetByFilterAsync(
                new AdminEmailTemplateFilterDto
                {
                    SearchText = "New User Account",
                    IsActive = true
                });

            var template = result?.Items?.FirstOrDefault();

            if (template == null)
                throw new InvalidOperationException("Email template 'New User Account' not found.");


            string subject = template.Subject ?? "Your Account Has Been Created";
            string body = template.Template ?? string.Empty;

            // Read config safely
            string baseUrl = _config["AlertSettings:BaseUrl"]
                ?? throw new InvalidOperationException("AlertSettings:BaseUrl is not configured.");

            string logoPath = _config["AlertSettings:LogoPath"]
                ?? throw new InvalidOperationException("AlertSettings:LogoPath is not configured.");

            // Ensure proper URL formatting
            baseUrl = baseUrl.TrimEnd('/') + "/";
            logoPath = logoPath.TrimStart('/');

            string logoUrl = baseUrl + logoPath;

            body = body
                .Replace("###Name###", fullName ?? "")
                .Replace("###username###", loginName ?? "")
                .Replace("###tempPassword###", tempPassword ?? "")
                .Replace("###campusname###", organizationName ?? "")
                .Replace("###siteurl###", baseUrl)
                .Replace("###logoUrl###", logoUrl);

            await _emailService.SendHtmlAsync(toEmail, subject, body);
        }




        /// <summary>
        /// Sends an email notification to the user with their registered username.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The user's registered username.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organizationName">The campus or organization name.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        public async Task SendForgotUsernameAsync(
            string toEmail, string loginName , 
            string fullName, string organizationName)
        {
            //var subject = "Your Username";

            //var body = BuildForgotUsernameBody(username);


            // Send email
            // await _emailService.SendAsync(toEmail, subject, body);

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new CustomException("Email address is required.");

            // Get template from DB
            var result = await _adminEmailTemplateService.GetByFilterAsync(
                new AdminEmailTemplateFilterDto
                {
                    SearchText = "Forgot Username",
                    IsActive = true
                }
            );

            var template = result?.Items?.FirstOrDefault();

            if (template == null)
                throw new InvalidOperationException("Email template 'Forgot Username' not found.");

            // Safe subject/body
            string subject = template.Subject ?? "Your Username";
            string body = template.Template ?? string.Empty;

            // Read config safely
            string baseUrl = _config["AlertSettings:BaseUrl"]
                ?? throw new InvalidOperationException("AlertSettings:BaseUrl is not configured.");

            string logoPath = _config["AlertSettings:LogoPath"]
                ?? throw new InvalidOperationException("AlertSettings:LogoPath is not configured.");

            // Ensure proper URL formatting
            baseUrl = baseUrl.TrimEnd('/') + "/";
            logoPath = logoPath.TrimStart('/');

            string logoUrl = baseUrl + logoPath;


            // Replace placeholders
            body = body
                .Replace("###Name###", fullName ?? string.Empty)
                .Replace("###username###", loginName ?? string.Empty)
                .Replace("###campusname###", organizationName ?? string.Empty)
                .Replace("###siteurl###", baseUrl ?? string.Empty)
                .Replace("###logoUrl###", logoUrl ?? string.Empty);

            // Send email
            await _emailService.SendHtmlAsync(toEmail, subject, body);
        }


        /// <summary>
        /// Sends an email notification to the user containing a secure password reset link.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The user's registered username.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organizationName">The campus or organization name.</param>
        /// <param name="resetLink">The secure, time-limited password reset URL.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        public async Task SendForgotPasswordAsync(
            string toEmail, string loginName,
            string fullName, string organizationName, string resetLink)
        {
            //var subject = "Reset Your Password";

            //var body = BuildForgotPasswordBody(resetLink);

            //await _emailService.SendAsync(toEmail, subject, body);


            if (string.IsNullOrWhiteSpace(toEmail))
                throw new CustomException("Email address is required.");

            // Get template from DB
            var result = await _adminEmailTemplateService.GetByFilterAsync(
                new AdminEmailTemplateFilterDto
                {
                    SearchText = "Forgot Password",
                    IsActive = true
                }
            );

            var template = result?.Items?.FirstOrDefault();

            if (template == null)
                throw new InvalidOperationException("Email template 'Forgot Password' not found.");

            // Safe subject/body
            string subject = template.Subject ?? "Forgot Password";
            string body = template.Template ?? string.Empty;

            // Read config safely
            string baseUrl = _config["AlertSettings:BaseUrl"]
                ?? throw new InvalidOperationException("AlertSettings:BaseUrl is not configured.");

            string logoPath = _config["AlertSettings:LogoPath"]
                ?? throw new InvalidOperationException("AlertSettings:LogoPath is not configured.");

            // Ensure proper URL formatting
            baseUrl = baseUrl.TrimEnd('/') + "/";
            logoPath = logoPath.TrimStart('/');

            string logoUrl = baseUrl + logoPath;


            // Replace placeholders
            body = body
                .Replace("###Name###", fullName ?? string.Empty)
                .Replace("###username###", loginName ?? string.Empty)
                .Replace("###resetlink###", resetLink ?? string.Empty)
                .Replace("###expirytime###", "10" ?? string.Empty)
                .Replace("###campusname###", organizationName ?? string.Empty)
                .Replace("###siteurl###", baseUrl ?? string.Empty)
                .Replace("###logoUrl###", logoUrl ?? string.Empty);

            // Send email
            await _emailService.SendHtmlAsync(toEmail, subject, body);

        }


        /// <summary>
        /// Sends an email notification to the user containing a two-factor / login verification code.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The user's registered username.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organizationName">The campus or organization name.</param>
        /// <param name="code">The verification code.</param>
        /// <param name="expiryMin">The code validity duration in minutes.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        public async Task SendLoginCodeAsync(
            string toEmail, string loginName,
            string fullName, string organizationName,
            string code , string expiryMin)
        {
            //var subject = "Your Login Verification Code";
            //var body = BuildLoginWithCodeBody(code);

            //await _emailService.SendAsync(toEmail, subject, body);

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new CustomException("Email address is required.");

            // Get template from DB
            var result = await _adminEmailTemplateService.GetByFilterAsync(
                new AdminEmailTemplateFilterDto
                {
                    SearchText = "Login With Code",
                    IsActive = true
                }
            );

            var template = result?.Items?.FirstOrDefault();

            if (template == null)
                throw new InvalidOperationException("Email template 'Login With Code' not found.");

            // Safe subject/body
            string subject = template.Subject ?? "Login With Code";
            string body = template.Template ?? string.Empty;

            // Read config safely
            string baseUrl = _config["AlertSettings:BaseUrl"]
                ?? throw new InvalidOperationException("AlertSettings:BaseUrl is not configured.");

            string logoPath = _config["AlertSettings:LogoPath"]
                ?? throw new InvalidOperationException("AlertSettings:LogoPath is not configured.");

            // Ensure proper URL formatting
            baseUrl = baseUrl.TrimEnd('/') + "/";
            logoPath = logoPath.TrimStart('/');

            string logoUrl = baseUrl + logoPath;


            // Replace placeholders
            body = body
                .Replace("###Name###", fullName ?? string.Empty)
                .Replace("###username###", loginName ?? string.Empty)
                .Replace("###loginCode###", code ?? string.Empty)
                .Replace("###expiryMinutes###", expiryMin ?? "10")
                .Replace("###campusname###", organizationName ?? string.Empty)
                .Replace("###siteurl###", baseUrl ?? string.Empty)
                .Replace("###logoUrl###", logoUrl ?? string.Empty);

            // Send email
            await _emailService.SendHtmlAsync(toEmail, subject, body);

        }




        /// <summary>
        /// Sends a detailed exception alert email to the configured administrator using the active
        /// database-configured Exception Alert template (EmailTempID = 20).
        /// </summary>
        /// <param name="ex">The exception captured by the application or middleware.</param>
        /// <param name="source">The API endpoint, controller action, or operation source where the exception occurred.</param>
        /// <param name="httpContext">The current HTTP context containing request, headers, claims, and form data (optional).</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        public async Task SendExceptionAlertAsync(
            Exception ex, string source, HttpContext? httpContext = null)
        {
            var adminEmail = _config["AlertSettings:AdminEmail"];

            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                // Fail fast – configuration error
                throw new InvalidOperationException(
                    "AlertSettings:AdminEmail is not configured.");
            }

            // 1. Fetch the active Exception Alert template using AdminEmailTemplateService
            var templateResult = await _adminEmailTemplateService.GetByIdAsync(20);
            if (templateResult == null || !templateResult.IsActive)
            {
                // Fallback by search if record 20 wasn't retrieved directly
                var filterResult = await _adminEmailTemplateService.GetByFilterAsync(
                    new AdminEmailTemplateFilterDto
                    {
                        SearchText = "Exception Alert",
                        IsActive = true
                    });
                templateResult = filterResult?.Items?.FirstOrDefault(t => t.EmailTempId == 20)
                                 ?? filterResult?.Items?.FirstOrDefault();
            }

            if (templateResult == null || string.IsNullOrWhiteSpace(templateResult.Template))
            {
                throw new InvalidOperationException("Email template 'Exception Alert' (EmailTempID = 20) not found or inactive in database.");
            }

            httpContext ??= _httpContextAccessor?.HttpContext;

            string baseUrl = _config["AlertSettings:BaseUrl"] ?? "https://ifnoss.com/";
            string logoPath = _config["AlertSettings:LogoPath"] ?? "assets/images/ifnoss-logo.png";

            baseUrl = baseUrl.TrimEnd('/') + "/";
            logoPath = logoPath.TrimStart('/');

            string logoUrl = baseUrl + logoPath;
            string occurredOnUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

            // 2. Format subject dynamically
            string rawSubject = templateResult.Subject ?? "System Exception Alert";
            string subject = rawSubject.Contains("###source###")
                ? rawSubject.Replace("###source###", source ?? string.Empty)
                : $"{rawSubject} ({source})";

            // 3. Populate dynamic placeholders in database HTML template
            string body = await PopulateExceptionAlertPlaceholdersAsync(
                templateResult.Template,
                ex,
                source,
                httpContext,
                logoUrl,
                occurredOnUtc);

            await _emailService.SendHtmlAsync(adminEmail, subject, body);
        }

        private static readonly string[] SensitiveKeySubstrings = new[]
        {
            "password", "pwd", "token", "secret", "apikey", "api_key",
            "otp", "code", "pin", "cvv", "creditcard", "cardnumber",
            "auth", "credential", "cookie", "ssn", "privatekey"
        };

        /// <summary>
        /// Determines whether a given key or claim name represents sensitive or secret data that should be redacted.
        /// </summary>
        /// <param name="key">The parameter or claim key name.</param>
        /// <returns>True if the key contains sensitive substrings; otherwise, false.</returns>
        private static bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            foreach (var sub in SensitiveKeySubstrings)
            {
                if (key.Contains(sub, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Sanitizes a raw JSON string by replacing values of sensitive properties (passwords, tokens, keys) with [REDACTED].
        /// </summary>
        /// <param name="json">The raw JSON request body.</param>
        /// <returns>A sanitized JSON string safe for email diagnostics.</returns>
        private static string SanitizeJsonBody(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try
            {
                var pattern = @"(""(?:[^""]*(?:password|pwd|secret|token|otp|code|pin|auth|credential|key)[^""]*)""\s*:\s*)(?:""[^""]*""|[^,}\]\s]+)";
                return Regex.Replace(json, pattern, "$1\"[REDACTED]\"", RegexOptions.IgnoreCase);
            }
            catch
            {
                return "[UNABLE TO SANITIZE BODY]";
            }
        }

        /// <summary>
        /// Collects and formats HTTP form values, JSON request body, or query string parameters
        /// into an HTML representation for the exception alert email while masking sensitive data.
        /// </summary>
        /// <param name="context">The HTTP context of the failing request.</param>
        /// <returns>A formatted HTML string (table or code block) or "N/A" if no form/query data is present.</returns>
        private async Task<string> CollectFormValuesHtmlAsync(HttpContext? context)
        {
            if (context == null) return "N/A";

            try
            {
                var request = context.Request;

                // 1. Multipart or url-encoded form values
                if (request.HasFormContentType && request.Form != null && request.Form.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width: 100%; border-collapse: collapse; font-size: 12px; margin-top: 4px;\">");
                    sb.Append("<thead><tr style=\"background-color: #f8fafc;\"><th style=\"width: 200px; text-align: left; padding: 6px 12px; border: 1px solid #e2e8f0; color: #1e40af; font-weight: 600;\">Parameter</th><th style=\"text-align: left; padding: 6px 12px; border: 1px solid #e2e8f0; color: #1e40af; font-weight: 600;\">Value</th></tr></thead><tbody>");

                    foreach (var key in request.Form.Keys)
                    {
                        var isSensitive = IsSensitiveKey(key);
                        var rawValue = isSensitive ? "[REDACTED]" : request.Form[key].ToString();
                        if (!isSensitive && rawValue.Length > 500)
                        {
                            rawValue = rawValue.Substring(0, 500) + "... [truncated]";
                        }

                        sb.Append("<tr>");
                        sb.Append($"<td style=\"padding: 6px 12px; border: 1px solid #e2e8f0; font-weight: 600; color: #334155; vertical-align: top;\">{WebUtility.HtmlEncode(key)}</td>");
                        sb.Append($"<td style=\"padding: 6px 12px; border: 1px solid #e2e8f0; color: #0f172a; vertical-align: top; word-break: break-all;\">{WebUtility.HtmlEncode(rawValue)}</td>");
                        sb.Append("</tr>");
                    }

                    sb.Append("</tbody></table>");
                    return sb.ToString();
                }

                // 2. Request body (e.g. application/json) if rewindable stream
                if (request.Body != null && request.Body.CanSeek && request.ContentLength is > 0 and < 100000)
                {
                    var originalPosition = request.Body.Position;
                    try
                    {
                        request.Body.Position = 0;
                        using var reader = new StreamReader(
                            request.Body,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: 1024,
                            leaveOpen: true);
                        var bodyText = await reader.ReadToEndAsync();
                        request.Body.Position = originalPosition;

                        if (!string.IsNullOrWhiteSpace(bodyText))
                        {
                            var sanitized = SanitizeJsonBody(bodyText);
                            if (sanitized.Length > 2000)
                            {
                                sanitized = sanitized.Substring(0, 2000) + "... [truncated]";
                            }
                            return $"<pre style=\"margin: 0; padding: 10px; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 4px; font-family: Consolas, monospace; font-size: 11.5px; white-space: pre-wrap; word-break: break-all;\">{WebUtility.HtmlEncode(sanitized)}</pre>";
                        }
                    }
                    catch
                    {
                        try { request.Body.Position = originalPosition; } catch { }
                    }
                }

                // 3. Query string parameters if present
                if (request.Query != null && request.Query.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width: 100%; border-collapse: collapse; font-size: 12px; margin-top: 4px;\">");
                    sb.Append("<thead><tr style=\"background-color: #f8fafc;\"><th style=\"width: 200px; text-align: left; padding: 6px 12px; border: 1px solid #e2e8f0; color: #1e40af; font-weight: 600;\">Query Parameter</th><th style=\"text-align: left; padding: 6px 12px; border: 1px solid #e2e8f0; color: #1e40af; font-weight: 600;\">Value</th></tr></thead><tbody>");

                    foreach (var key in request.Query.Keys)
                    {
                        var isSensitive = IsSensitiveKey(key);
                        var rawValue = isSensitive ? "[REDACTED]" : request.Query[key].ToString();
                        sb.Append("<tr>");
                        sb.Append($"<td style=\"padding: 6px 12px; border: 1px solid #e2e8f0; font-weight: 600; color: #334155; vertical-align: top;\">{WebUtility.HtmlEncode(key)}</td>");
                        sb.Append($"<td style=\"padding: 6px 12px; border: 1px solid #e2e8f0; color: #0f172a; vertical-align: top; word-break: break-all;\">{WebUtility.HtmlEncode(rawValue)}</td>");
                        sb.Append("</tr>");
                    }

                    sb.Append("</tbody></table>");
                    return sb.ToString();
                }
            }
            catch
            {
                // Safety fallback
            }

            return "N/A";
        }

        /// <summary>
        /// Collects authenticated user claims and session keys, formatting each claim on a separate line
        /// within an email-safe list for the Current Sessions section of the exception alert email.
        /// </summary>
        /// <param name="context">The HTTP context of the failing request.</param>
        /// <returns>Formatted HTML containing Claims and Session values, or "N/A" if context is null.</returns>
        private static string CollectCurrentSessionsHtml(HttpContext? context)
        {
            if (context == null) return "N/A";

            try
            {
                var items = new List<string>();

                // User Claims
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var relevantClaims = context.User.Claims
                        .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                        .Select(c =>
                        {
                            var typeShort = c.Type.Contains('/') ? c.Type.Substring(c.Type.LastIndexOf('/') + 1) : c.Type;
                            var val = IsSensitiveKey(typeShort) ? "[REDACTED]" : c.Value;
                            return $"<li style=\"margin: 3px 0; padding: 0;\"><strong>{WebUtility.HtmlEncode(typeShort)}:</strong> {WebUtility.HtmlEncode(val)}</li>";
                        })
                        .ToList();

                    if (relevantClaims.Count > 0)
                    {
                        var claimsHtml = new StringBuilder();
                        claimsHtml.Append("<div style=\"font-weight: 700; color: #0f172a; margin-bottom: 4px;\">Claims:</div>");
                        claimsHtml.Append("<ul style=\"margin: 0; padding-left: 18px; list-style-type: disc; color: #334155;\">");
                        foreach (var claimItem in relevantClaims)
                        {
                            claimsHtml.Append(claimItem);
                        }
                        claimsHtml.Append("</ul>");
                        items.Add(claimsHtml.ToString());
                    }
                }

                // ASP.NET Core Session if active
                if (context.Features.Get<ISessionFeature>()?.Session != null && context.Session.IsAvailable)
                {
                    var sessionKeys = context.Session.Keys.ToList();
                    if (sessionKeys.Count > 0)
                    {
                        var sessionList = sessionKeys.Select(k =>
                        {
                            var val = IsSensitiveKey(k) ? "[REDACTED]" : context.Session.GetString(k) ?? "null";
                            return $"<li style=\"margin: 3px 0; padding: 0;\"><strong>{WebUtility.HtmlEncode(k)}:</strong> {WebUtility.HtmlEncode(val)}</li>";
                        });
                        var sessionHtml = new StringBuilder();
                        sessionHtml.Append("<div style=\"font-weight: 700; color: #0f172a; margin-top: 8px; margin-bottom: 4px;\">Session Values:</div>");
                        sessionHtml.Append("<ul style=\"margin: 0; padding-left: 18px; list-style-type: disc; color: #334155;\">");
                        foreach (var sItem in sessionList)
                        {
                            sessionHtml.Append(sItem);
                        }
                        sessionHtml.Append("</ul>");
                        items.Add(sessionHtml.ToString());
                    }
                }

                if (items.Count > 0)
                {
                    return string.Join("", items);
                }
            }
            catch
            {
                // Safety fallback
            }

            return "N/A";
        }

        /// <summary>
        /// Replaces all dynamic placeholders in the database HTML template with
        /// extracted request, execution, user, form, and exception details.
        /// </summary>
        /// <param name="templateHtml">The raw HTML template fetched from the database.</param>
        /// <param name="ex">The captured exception.</param>
        /// <param name="source">The operation or endpoint source.</param>
        /// <param name="context">The HTTP context of the failing request.</param>
        /// <param name="logoUrl">The resolved logo URL.</param>
        /// <param name="occurredOnUtc">The UTC timestamp string.</param>
        /// <returns>A completed HTML email string ready for transmission.</returns>
        private async Task<string> PopulateExceptionAlertPlaceholdersAsync(
            string templateHtml,
            Exception ex,
            string source,
            HttpContext? context,
            string logoUrl,
            string occurredOnUtc)
        {
            var req = context?.Request;

            string rawAppName = "Kafaat Application";
            string applicationName = WebUtility.HtmlEncode(rawAppName);

            // 1. Error Details
            string exceptionType = WebUtility.HtmlEncode(ex.GetType().FullName ?? ex.GetType().Name);
            string exceptionMessage = WebUtility.HtmlEncode(ex.Message ?? "N/A");
            string innerErrorRow = "";
            if (ex.InnerException != null)
            {
                innerErrorRow = $@"<tr>
                  <td style=""width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 700; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;""><strong>Inner Details</strong></td>
                  <td style=""padding: 10px 20px; font-size: 13px; color: #b91c1c; border-bottom: 1px solid #f1f5f9; vertical-align: top; font-weight: 700;""><strong>{WebUtility.HtmlEncode(ex.InnerException.GetType().FullName ?? ex.InnerException.GetType().Name)}: {WebUtility.HtmlEncode(ex.InnerException.Message)}</strong></td>
                </tr>";
            }

            // 2. Execution Information
            string method = WebUtility.HtmlEncode(req?.Method ?? "N/A");
            string sourceEnc = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(source) ? "N/A" : source);
            string exSource = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ex.Source) ? "N/A" : ex.Source);

            // 3. Request Information
            string requestUrl = "N/A";
            string requestHost = "N/A";
            string userAgent = "N/A";
            string urlReferer = "N/A";
            string requestIp = "N/A";

            if (req != null)
            {
                requestUrl = $"{req.Scheme}://{req.Host}{req.PathBase}{req.Path}{req.QueryString}";
                requestHost = req.Host.Value;

                if (req.Headers.TryGetValue("User-Agent", out var ua) && !string.IsNullOrWhiteSpace(ua))
                    userAgent = ua.ToString();

                if (req.Headers.TryGetValue("Referer", out var referer) && !string.IsNullOrWhiteSpace(referer))
                    urlReferer = referer.ToString();

                var ip = context?.Connection?.RemoteIpAddress?.ToString();
                if (req.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
                {
                    requestIp = string.IsNullOrWhiteSpace(ip) ? forwardedFor.ToString() : $"{ip} (Forwarded: {forwardedFor})";
                }
                else if (!string.IsNullOrWhiteSpace(ip))
                {
                    requestIp = ip;
                }
            }

            string hostName;
            try
            {
                hostName = System.Net.Dns.GetHostName();
            }
            catch
            {
                hostName = Environment.MachineName ?? "N/A";
            }

            string urlHtml = requestUrl != "N/A"
                ? $"<a href=\"{WebUtility.HtmlEncode(requestUrl)}\" style=\"color: #2563eb; text-decoration: underline;\">{WebUtility.HtmlEncode(requestUrl)}</a>"
                : "N/A";

            // 4. User Information
            string userName = "N/A";
            string userId = "N/A";
            string roleId = "N/A";

            if (context?.User?.Identity?.IsAuthenticated == true)
            {
                userName = context.User.FindFirst(JwtClaimTypes.UserName)?.Value
                           ?? context.User.Identity.Name
                           ?? "Authenticated User";
                userId = context.User.FindFirst(JwtClaimTypes.LoginId)?.Value
                         ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? "N/A";
                roleId = context.User.FindFirst(JwtClaimTypes.RoleId)?.Value ?? "N/A";
            }

            string currentSessions = CollectCurrentSessionsHtml(context);

            // 5. Form Values
            string formValuesHtml = await CollectFormValuesHtmlAsync(context);

            // 6. Stack Trace
            string stackTrace = WebUtility.HtmlEncode(ex.ToString());
            // Replace all dynamic placeholders
            return templateHtml
                .Replace("###applicationName###", applicationName)
                .Replace("###logoUrl###", WebUtility.HtmlEncode(logoUrl))
                .Replace("###occurredOn###", WebUtility.HtmlEncode(occurredOnUtc))
                .Replace("###exceptionType###", exceptionType)
                .Replace("###message###", exceptionMessage)
                .Replace("###innerException###", innerErrorRow)
                .Replace("###method###", method)
                .Replace("###source###", sourceEnc)
                .Replace("###exceptionSource###", exSource)
                .Replace("###url###", urlHtml)
                .Replace("###requestHost###", WebUtility.HtmlEncode(requestHost))
                .Replace("###hostName###", WebUtility.HtmlEncode(hostName))
                .Replace("###requestIp###", WebUtility.HtmlEncode(requestIp))
                .Replace("###userAgent###", WebUtility.HtmlEncode(userAgent))
                .Replace("###urlReferer###", WebUtility.HtmlEncode(urlReferer))
                .Replace("###userName###", WebUtility.HtmlEncode(userName))
                .Replace("###userId###", WebUtility.HtmlEncode(userId))
                .Replace("###roleId###", WebUtility.HtmlEncode(roleId))
                .Replace("###currentSessions###", currentSessions)
                .Replace("###formValues###", formValuesHtml)
                .Replace("###stackTrace###", stackTrace);
        }





    }
}
