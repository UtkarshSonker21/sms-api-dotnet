namespace ScholarshipManagementAPI.Services.Interface.Common
{
    public interface INotificationService
    {
        /// <summary>
        /// Sends an email notification with account credentials to a newly created user.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The username for logging in.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organisationName">The campus or organisation name.</param>
        /// <param name="tempPassword">The temporary password assigned to the account.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        Task SendNewUserAccountAsync(string toEmail, string loginName,
            string fullName, string organisationName, string tempPassword);

        /// <summary>
        /// Sends an email notification reminding the user of their username.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The user's registered username.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organisationName">The campus or organisation name.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        Task SendForgotUsernameAsync(string toEmail, string loginName,
            string fullName, string organisationName);
        
        /// <summary>
        /// Sends an email notification containing a secure password reset link.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The user's username.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organisationName">The campus or organisation name.</param>
        /// <param name="resetLink">The secure, time-limited password reset URL.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        Task SendForgotPasswordAsync(string toEmail, string loginName,
            string fullName, string organisationName, string resetLink);

        /// <summary>
        /// Sends a two-factor / login verification code via email.
        /// </summary>
        /// <param name="toEmail">The recipient's email address.</param>
        /// <param name="loginName">The user's username.</param>
        /// <param name="fullName">The full name of the user.</param>
        /// <param name="organisationName">The campus or organisation name.</param>
        /// <param name="code">The numeric or alphanumeric verification code.</param>
        /// <param name="expiryMin">The code validity duration in minutes.</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        Task SendLoginCodeAsync(string toEmail, string loginName,
            string fullName, string organisationName,
            string code, string expiryMin);

        /// <summary>
        /// Sends a detailed exception alert email to the configured administrator using the active
        /// database-configured Exception Alert template (EmailTempID = 20).
        /// </summary>
        /// <param name="ex">The captured exception.</param>
        /// <param name="source">The API endpoint, controller action, or operation source where the exception occurred.</param>
        /// <param name="httpContext">The current HTTP context containing request, headers, claims, and form data (optional).</param>
        /// <returns>A task representing the asynchronous email operation.</returns>
        Task SendExceptionAlertAsync(Exception ex, string source, Microsoft.AspNetCore.Http.HttpContext? httpContext = null);
    }
}
