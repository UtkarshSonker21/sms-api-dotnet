using Azure.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Settings;
using ScholarshipManagementAPI.DTOs.SuperAdmin.UsersLoginLog;
using ScholarshipManagementAPI.Helper;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Common;
using ScholarshipManagementAPI.Services.Interface.SuperAdmin;
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using UAParser;
using static ScholarshipManagementAPI.Helper.Utilities.Constant;
using static System.Net.WebRequestMethods;

namespace ScholarshipManagementAPI.Services.Implementation.Common
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwt;
        private readonly IUsersLoginLogService _loginLogService;
        private readonly IHttpContextAccessor _httpContext;
        private readonly IConfiguration _config;
        private readonly Parser _uaParser;
        private readonly INotificationService _notificationService;
        private readonly ICommonService _commonService;
        private readonly IGeneralSettingsService _generalSettingsService;

        public AuthService(
            AppDbContext context,
            IJwtService jwt,
            IUsersLoginLogService loginLogService,
            IHttpContextAccessor httpContext,
            IConfiguration config,
            Parser uaParser,
            INotificationService notificationService,
            ICommonService commonService,
            IGeneralSettingsService generalSettingsService
            )
        {
            _context = context;
            _jwt = jwt;
            _loginLogService = loginLogService;
            _httpContext = httpContext;
            _config = config;
            _uaParser = uaParser;
            _notificationService = notificationService;
            _commonService = commonService;
            _generalSettingsService = generalSettingsService;
        }


        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto) 
        {
            var user = await _context.KfUsersLogins
                .Where(x =>
                    x.LoginName == dto.LoginName &&
                    x.IsActive
                ).FirstOrDefaultAsync();

            if (user == null)
                throw new UnauthorizedAccessException("Invalid credentials");


            // Verify hashed password
            var isValid = HelperMethods.VerifyPassword(
                user,
                user.Password,
                dto.Password
            );

            if (!isValid)
                throw new UnauthorizedAccessException("Invalid credentials");

            return await BuildLoginResponseAsync(user);
        }

        public async Task LogoutAsync(long loginId)
        {
            // Get latest active login log
            var log = await _context.KfUsersLoginLogs
                .Where(x => x.LoginId == loginId && x.LogoutDateTime == null)
                .OrderByDescending(x => x.LoginLogId)
                .FirstOrDefaultAsync();

            if (log == null)
                return; // already logged out or no log found

            if (log.LogoutDateTime != null)
                return;

            log.LogoutDateTime = DateTime.UtcNow;
            // log.LogoutReason = "USER_LOGOUT";
            // TOKEN_EXPIRED FORCE_LOGOUT ROLE_SWITCH  SESSION_TIMEOUT

            await _context.SaveChangesAsync();
        }

        public async Task<LoginResponseDto> SwitchRoleAsync(long loginId, long roleId)
        {
            // Validate role belongs to this user
            var userRole = await _context.KfUsersRoleAssignments
                .Include(x => x.Login)
                .Include(x => x.Role)
                .ThenInclude(r => r.Module)
                .Where(x => x.LoginId == loginId && x.RoleId == roleId)
                .FirstOrDefaultAsync();

            if (userRole == null)
                throw new UnauthorizedAccessException("Role not assigned to user");

            // fetch all available roles 
            var roles = await _context.KfUsersRoleAssignments
                .Where(x => x.LoginId == loginId)
                .Include(x => x.Role)
                .ThenInclude(r => r.Module)
                .OrderByDescending(x => x.IsDefault)
                .Select(x => new AvailableRolesDto
                {
                    RoleId = x.Role.RoleId,
                    RoleName = x.Role.RoleName,
                    ModuleId = x.Role.ModuleId,
                    ModuleName = x.Role.Module.ModuleName,
                    IsDefault = x.IsDefault
                })
                .ToListAsync();

            if (!roles.Any())
                throw new UnauthorizedAccessException("No roles assigned to user");

            // Generate NEW token with switched role
            var token = _jwt.GenerateToken(new TokenDto
            {
                LoginId = loginId,
                LoginName = userRole.Login.LoginName,

                RoleId = userRole.RoleId,
                RoleName = userRole.Role.RoleName,

                ModuleId = userRole.Role.ModuleId
            });

            return new LoginResponseDto
            {
                Token = token,
                Expiry = DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(_config["Jwt:ExpiryMinutes"])
                ),

                LoginId = loginId,
                LoginName = userRole.Login.LoginName,

                ModuleId = userRole.Role.ModuleId,
                ModuleName = userRole.Role.Module.ModuleName,

                CurrentRoleId = userRole.RoleId,
                CurrentRoleName = userRole.Role.RoleName,

                AvailableRoles = roles,
                UniversityIds = await _context.KfStaffUniversityCoordinatorMappings
                    .Where(m => m.StaffId == userRole.Login.StaffId && m.IsActive)
                    .Select(m => m.UniversityId).ToListAsync(),
                SchoolIds = await _context.KfStaffSchoolCoordinatorMappings
                    .Where(m => m.StaffId == userRole.Login.StaffId && m.IsActive)
                    .Select(m => m.SchoolId).ToListAsync()
            };
        }




        #region without Auth Methods
     
        public async Task<bool> ForgotUserNameAsync(UserIdentifierDto request)
        {

            if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
                throw new CustomException("Email is required.");

            var user = await _context.KfUsersLogins
                .Where(x =>
                    x.RecoveryEmail == request.EmailOrUsername.Trim() &&
                    x.IsActive)
                .Select(x => new
                {
                    x.LoginName,
                    x.RecoveryEmail,
                    x.IsActive,
                    x.Staff.StaffSalutation,
                    x.Staff.StaffFirstName,
                    x.Staff.StaffLastName,
                    x.Staff.StaffType
                })
                .FirstOrDefaultAsync();


            if (user != null)
            {
                string organizationName = user.StaffType switch
                {
                    (long)StaffType.SuperAdmin => "System Administration",
                    (long)StaffType.Ngo => "NGO Administration",
                    (long)StaffType.Marketing => "Marketing",
                    (long)StaffType.Finance => "Finance",

                    (long)StaffType.University => "University Coordinator",
                    (long)StaffType.School => "Shool Coordinator",
                    _ => string.Empty
                };

                string fullName = $"{user.StaffSalutation} {user.StaffFirstName} {user.StaffLastName}"
                                 .Trim();


                await _notificationService.SendForgotUsernameAsync(
                    user.RecoveryEmail,
                    user.LoginName,
                    fullName,
                    organizationName
                );
            }

            // Always return success
            return true;
        }
      
        public async Task<bool> ForgotUserPasswordAsync(UserIdentifierDto request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
                throw new CustomException("Email is required.");


            var user = await _context.KfUsersLogins
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x =>
                    (x.RecoveryEmail.Trim() == request.EmailOrUsername.Trim() 
                    || x.LoginName.Trim() == request.EmailOrUsername.Trim())
                    && x.IsActive);

            // Avoid user enumeration
            if (user == null)
                return true;

            var baseUrl = _config["AppSettings:FrontendUrl"]?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("FrontendUrl not configured.");

            // Invalidate old token
            user.TempPassword = null;
            user.TempPassDateTime = null;

            // Generate new reset token
            var token = GenerateResetToken();

            // store new token & expiry time
            user.TempPassword = $"RESET:{token}";
            user.TempPassDateTime = DateTime.UtcNow.AddMinutes(10);

            await _context.SaveChangesAsync();

            // reset link
            var resetLink = $"{baseUrl}/reset-password?token={token}";

            // Determine organization name
            string organizationName = user.Staff.StaffType switch
            {
                (long)StaffType.SuperAdmin => "System Administration",
                (long)StaffType.Ngo => "NGO Administration",
                (long)StaffType.Marketing => "Marketing",
                (long)StaffType.Finance => "Finance",

                (long)StaffType.University => "University Coordinator",
                (long)StaffType.School => "Shool Coordinator",
                _ => string.Empty
            };

            string fullName =
                $"{user.Staff.StaffSalutation} {user.Staff.StaffFirstName} {user.Staff.StaffLastName}"
                .Trim();

            await _notificationService.SendForgotPasswordAsync(
                user.RecoveryEmail,
                user.LoginName,
                fullName,
                organizationName,
                resetLink
            );

            return true;
        }

        public async Task<bool> ResetUserPasswordAsync(ResetPasswordRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                throw new CustomException("Invalid or missing reset token.");

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new CustomException("New password is required.");

            // Load user by prefixed token
            var dbToken = $"RESET:{request.Token}";

            var user = await _context.KfUsersLogins
                .FirstOrDefaultAsync(x =>
                    x.TempPassword == dbToken &&
                    x.TempPassDateTime != null);

            if (user == null)
                throw new CustomException("Reset link is invalid or expired.");

            // Expiry validation
            if (!user.TempPassDateTime.HasValue || user.TempPassDateTime < DateTime.UtcNow)
            {
                user.TempPassword = null;
                user.TempPassDateTime = null;
                await _context.SaveChangesAsync();

                throw new CustomException("Reset link expired.");
            }
                

            var newPassword = request.NewPassword.Trim();

            // Check if new password same as old (use hasher)
            if (!string.IsNullOrEmpty(user.Password))
            {
                var isSame = HelperMethods.VerifyPassword(
                    user,
                    user.Password,
                    newPassword
                );

                if (isSame)
                    throw new CustomException("New password cannot be same as old password.");
            }

            // Hash new password
            user.Password = HelperMethods.HashPassword(user, newPassword);

            // Invalidate token
            user.TempPassword = null;
            user.TempPassDateTime = null;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> LoginWithCodeAsync(UserIdentifierDto request)
        {

            if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
                throw new CustomException("Email or Username is required.");

            var user = await _context.KfUsersLogins
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x =>
                    (x.LoginName == request.EmailOrUsername.Trim() ||
                    x.RecoveryEmail == request.EmailOrUsername.Trim()) &&
                    x.IsActive);

            if (user != null)
            {
                if (user.TempPassDateTime.HasValue &&
                    user.TempPassDateTime > DateTime.UtcNow.AddMinutes(-1))
                {
                    // Prevent resend within 1 minute
                    return true;
                }

                // Invalidate old value
                user.TempPassword = null;
                user.TempPassDateTime = null;

                var code = Generate6DigitCode();

                user.TempPassword = $"LOGIN:{code}";
                user.TempPassDateTime = DateTime.UtcNow.AddMinutes(10);

                await _context.SaveChangesAsync();


                // Determine organization name
                string organizationName = user.Staff.StaffType switch
                {
                    (long)StaffType.SuperAdmin => "System Administration",
                    (long)StaffType.Ngo => "NGO Administration",
                    (long)StaffType.Marketing => "Marketing",
                    (long)StaffType.Finance => "Finance",

                    (long)StaffType.University => "University Coordinator",
                    (long)StaffType.School => "Shool Coordinator",
                    _ => string.Empty
                };

                string fullName =
                    $"{user.Staff.StaffSalutation} {user.Staff.StaffFirstName} {user.Staff.StaffLastName}"
                    .Trim();

                await _notificationService.SendLoginCodeAsync(
                    user.RecoveryEmail,
                    user.LoginName,
                    fullName,
                    organizationName,
                    code,
                    "10" 
                );
            }

            // Always return success
            return true;
        }

        public async Task<LoginResponseDto?> VerifyLoginCodeAsync(VerifyOtpDto request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
                throw new CustomException("Email or Username is required.");

            if (string.IsNullOrWhiteSpace(request.Code))
                throw new CustomException("Verification code is required.");

            var user = await _context.KfUsersLogins
                .Where(x =>
                    (x.LoginName == request.EmailOrUsername ||
                     x.RecoveryEmail == request.EmailOrUsername) &&
                    x.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            if (string.IsNullOrWhiteSpace(user.TempPassword))
                return null;

            if (user.TempPassDateTime < DateTime.UtcNow)
                throw new CustomException("Code expired");

            if (!user.TempPassword?.StartsWith("LOGIN:") == true)
                throw new CustomException("Invalid code type");

            var actualCode = user.TempPassword?.Replace("LOGIN:", "");

            if (actualCode != request.Code)
                throw new CustomException("Invalid code");

            // Invalidate OTP
            user.TempPassword = null;
            user.TempPassDateTime = null;
            await _context.SaveChangesAsync();

            // Auto login
            return await BuildLoginResponseAsync(user);
        }

        #endregion



        #region Auth Methods

        public async Task<CurrentUserProfileDto?> GetMyProfileAsync(long loginId , long roleId)
        {
            var user = await _context.KfUsersLogins
                .Include(x => x.KfUsersRoleAssignmentLogins)
                .ThenInclude(x => x.Role)
                .ThenInclude(x => x.Module)
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x => x.LoginId == loginId && x.IsActive);


            if (user == null)
                throw new CustomException("User not found.");


            var roles = user.KfUsersRoleAssignmentLogins
                .OrderByDescending(x => x.IsDefault)
                .Select(x => new AvailableRolesDto
            {
                RoleId = x.RoleId,
                RoleName = x.Role.RoleName,
                ModuleId = x.Role.ModuleId,
                ModuleName = x.Role.Module.ModuleName,
                IsDefault = x.IsDefault
            }).ToList();

            var staff = user.Staff;

            if (staff == null)
                throw new CustomException("Staff record not found.");

            string organizationName = staff.StaffType switch
            {
                (long)StaffType.SuperAdmin => "System Administration",

                (long)StaffType.Ngo => "NGO Administration",
                (long)StaffType.Marketing => "Marketing",
                (long)StaffType.Finance => "Finance",

                (long)StaffType.University => "University Coordinator",
                (long)StaffType.School => "School Coordinator",

                _ => string.Empty
            };

            var currentRole = user.KfUsersRoleAssignmentLogins
                .FirstOrDefault(x => x.RoleId == roleId && x.LoginId == loginId);

            var currency = await GetDefaultCurrencyAsync(staff);

            return new CurrentUserProfileDto
            {
                LoginId = user.LoginId,
                LoginName = user.LoginName ?? string.Empty,

                AvailableRoles = roles?? new List<AvailableRolesDto>(),

                CurrentRoleName = currentRole?.Role.RoleName ?? string.Empty,
                CurrentRoleId = currentRole?.RoleId ?? 0,

                ModuleId = currentRole?.Role.ModuleId ?? 0,
                ModuleName = currentRole?.Role.Module.ModuleName ?? string.Empty,

                StaffType = (StaffType)staff.StaffType,
                UniversityIds = await _context.KfStaffUniversityCoordinatorMappings
                    .Where(m => m.StaffId == staff.StaffId && m.IsActive)
                    .Select(m => m.UniversityId).ToListAsync(),
                SchoolIds = await _context.KfStaffSchoolCoordinatorMappings
                    .Where(m => m.StaffId == staff.StaffId && m.IsActive)
                    .Select(m => m.SchoolId).ToListAsync(),
                OrganizationName = organizationName,

                ProfilePhoto = _commonService.GetProfileImageUrl(staff.Photo),
                FullName = $"{staff.StaffSalutation} {staff.StaffFirstName} {staff.StaffLastName}".Trim(),
                Salutation = staff.StaffSalutation,
                FirstName = staff.StaffFirstName ?? string.Empty,
                LastName = staff.StaffLastName ?? string.Empty,
                Mobile = staff.MobileNumber,
                PersonalEmail = staff.PersonalEmail,

                OfficialEmail = user.RecoveryEmail ?? string.Empty,
                Status = user.IsActive,

                Address = staff.PermAddress,
                City = staff.PermCity,
                Country = staff.PermCountry?.CountryName,
                CountryId = staff.PermCountryId,
                Zip = staff.PermZipCode,

                DefaultCurrencyCode = currency.code,
                DefaultCurrencyName = currency.name,
                DefaultCurrencySymbol = currency.symbol,

                //LastLogin = user.LastLoginDate
            };
        }

        public async Task<bool> UpdateMyProfileAsync(long loginId, UpdateMyProfileDto dto)
        {
            var user = await _context.KfUsersLogins
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x => x.LoginId == loginId && x.IsActive);

            if (user == null)
                throw new CustomException("User not found.");

            if (user.Staff == null)
                throw new CustomException("Staff record not found.");

            var staff = user.Staff;

            // Check duplicate login name
            var loginName = dto.UsernameOrLoginName?.Trim();

            if (string.IsNullOrWhiteSpace(loginName))
                throw new CustomException("Login name is required.");

            var exists = await _context.KfUsersLogins
                .AnyAsync(x =>
                    x.LoginId != loginId &&
                    x.LoginName == loginName);

            if (exists)
                throw new CustomException("Login name already exists.");


            // 🔹 Update only editable fields
            staff.StaffSalutation = dto.Saluatation;
            staff.StaffFirstName = dto.FirstName;
            staff.StaffLastName = dto.LastName;
            staff.MobileNumber = dto.Mobile;
            staff.PersonalEmail = dto.PersonalEmail;

            staff.PermAddress = dto.Address;
            staff.PermCity = dto.City;
            staff.PermCountryId = dto.CountryId;
            staff.PermZipCode = dto.Zip;

            // Update login name
            user.LoginName = loginName;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdatePasswordAsync(UpdatePasswordRequestDto request, long loginId)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                throw new CustomException("Current password is required.");

            if (string.IsNullOrWhiteSpace(request.UpdatedPassword))
                throw new CustomException("New password is required.");

            var user = await _context.KfUsersLogins
                .FirstOrDefaultAsync(x =>
                    x.LoginId == loginId &&
                    x.IsActive);

            if (user == null)
                throw new CustomException("User not found.");

            var currentPassword = request.CurrentPassword.Trim();
            var newPassword = request.UpdatedPassword.Trim();

            // Verify current password
            if (string.IsNullOrEmpty(user.Password) ||
                !HelperMethods.VerifyPassword(
                    user,
                    user.Password,
                    currentPassword))
            {
                throw new CustomException("Current password is incorrect.");
            }

            // Prevent using the same password
            if (HelperMethods.VerifyPassword(
                user,
                user.Password,
                newPassword))
            {
                throw new CustomException(
                    "New password cannot be same as old password.");
            }

            // Hash and update new password
            user.Password = HelperMethods.HashPassword(
                user,
                newPassword);

            // Clear temporary password/reset information
            user.TempPassword = null;
            user.TempPassDateTime = null;

            await _context.SaveChangesAsync();

            return true;
        }




        #endregion



        #region Private Methods

        // helper method to build login response
        private async Task<LoginResponseDto> BuildLoginResponseAsync(KfUsersLogin user)
        {
            var roles = await _context.KfUsersRoleAssignments
                .Where(x => x.LoginId == user.LoginId)
                .Include(x => x.Role)
                .ThenInclude(r => r.Module)
                .OrderByDescending(x => x.IsDefault)
                .Select(x => new AvailableRolesDto
                {
                    RoleId = x.Role.RoleId,
                    RoleName = x.Role.RoleName,
                    ModuleId = x.Role.ModuleId,
                    ModuleName = x.Role.Module.ModuleName,
                    IsDefault = x.IsDefault
                })
                .ToListAsync();

            if (!roles.Any())
                throw new UnauthorizedAccessException("No roles assigned to user");

            var defaultRole = await _context.KfUsersRoleAssignments
                .Include(x => x.Role)
                .ThenInclude(r => r.Module)
                .Where(x => x.LoginId == user.LoginId)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefaultAsync();

            if (defaultRole?.Role == null)
                throw new UnauthorizedAccessException("Default role configuration error");

            var token = _jwt.GenerateToken(new TokenDto
            {
                LoginId = user.LoginId,
                LoginName = user.LoginName,
                RoleId = defaultRole.RoleId,
                RoleName = defaultRole.Role.RoleName,
                ModuleId = defaultRole.Role.ModuleId
            });

            await LogLoginAsync(user);

            return new LoginResponseDto
            {
                Token = token,
                Expiry = DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(_config["Jwt:ExpiryMinutes"])
                ),
                LoginId = user.LoginId,
                LoginName = user.LoginName,
                ModuleId = defaultRole.Role.ModuleId,
                ModuleName = defaultRole.Role.Module.ModuleName,
                CurrentRoleId = defaultRole.RoleId,
                CurrentRoleName = defaultRole.Role.RoleName,
                AvailableRoles = roles,
                UniversityIds = await _context.KfStaffUniversityCoordinatorMappings
                    .Where(m => m.StaffId == user.StaffId && m.IsActive)
                    .Select(m => m.UniversityId).ToListAsync(),
                SchoolIds = await _context.KfStaffSchoolCoordinatorMappings
                    .Where(m => m.StaffId == user.StaffId && m.IsActive)
                    .Select(m => m.SchoolId).ToListAsync()
            };
        }



        // helper method to log login details
        private async Task LogLoginAsync(KfUsersLogin user)
        {
            var context = _httpContext.HttpContext;
            if (context == null) return;

            string userAgent = context.Request.Headers["User-Agent"].ToString();
            var info = _uaParser.Parse(userAgent);

            string browserName = $"{info.UA.Family} {info.UA.Major}";
            string osName = $"{info.OS.Family} {info.OS.Major}";
            string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var logDto = new UsersLoginLogRequestDto
            {
                LoginId = user.LoginId,
                UserName = user.LoginName,
                IpAddress = ipAddress,
                BrowserName = browserName,
                OperatingSystem = osName,
                ComputerName = "Desktop" // optional
            };

            await _loginLogService.CreateAsync(logDto);
        }


        // helper method to generate secure token for password reset
        private string GenerateResetToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return WebEncoders.Base64UrlEncode(bytes); // URL safe
        }


        // helper method to generate 6-digit code
        private static string Generate6DigitCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(4);
            var value = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return value.ToString("D6");
        }


        // helper method to get default currency based on staff's organization or fallback to base currency
        private async Task<(string code, string name, string symbol)> GetDefaultCurrencyAsync(KfStaff staff)
        {
            var config = await _generalSettingsService.GetGeneralConfigAsync();

            var baseCode = config.BaseCurrencyCode;
            var baseName = config.BaseCurrencyName;
            var baseSymbol = config.BaseCurrencySymbol;

            // University currency lookup removed because DefaultCurrencyId is deleted from UnUniversityRegistration

            //if (staff.StaffType == (long)StaffType.School && staff.School?.DefaultCurrencyId != null)
            //{
            //    var currency = await _context.ZzMasterCurrencies
            //        .Where(x => x.CurrencyId == staff.School.DefaultCurrencyId)
            //        .Select(x => new { x.CurrencyCode, x.CurrencyName, x.CurrencySymbol })
            //        .FirstOrDefaultAsync();

            //    if (currency != null)
            //        return (currency.CurrencyCode, currency.CurrencyName, currency.CurrencySymbol);
            //}

            // fallback → base currency
            return (baseCode, baseName, baseSymbol);
        }



        #endregion



    }
}
