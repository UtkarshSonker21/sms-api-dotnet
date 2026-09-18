using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.Ngo.Administration.PanelUsers;
using ScholarshipManagementAPI.DTOs.Ngo.Administration.UniversityCoordinators;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Common;
using ScholarshipManagementAPI.Services.Interface.Ngo;
using static ScholarshipManagementAPI.Helper.Utilities.Constant;

namespace ScholarshipManagementAPI.Services.Implementation.Ngo
{
    public class UniversityCoordinatorService : IUniversityCoordinatorService
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public UniversityCoordinatorService(AppDbContext context, INotificationService notificationService) 
        { 
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<long> CreateAsync(UniversityCoordinatorRequestDto dto, LoggedInUserDto currentUser)
        {
            // Validate Role
            var roleExists = await _context.KfUsersRoles
                .AnyAsync(x => 
                    x.RoleId == dto.RoleId &&
                    x.ModuleId == (long)StaffType.University &&
                    x.IsActive);

            if (!roleExists)
                throw new CustomException("Selected role does not exist.");

            // Validate Official Email
            if (await _context.KfStaffs
                .AnyAsync(x => x.OfficialEmail.Trim().ToLower() == dto.OfficialEmail.Trim().ToLower()))
            {
                throw new CustomException("Official email already exists.");
            }

            // Validate Recovery Email
            if (await _context.KfUsersLogins
                .AnyAsync(x => x.RecoveryEmail.Trim().ToLower() == dto.RecoveryEmail.Trim().ToLower()))
            {
                throw new CustomException("Recovery email already exists.");
            }

            // Validate University Selection
            if (dto.UniversityIds == null || !dto.UniversityIds.Any())
            {
                throw new CustomException("Please select at least one university.");
            }

            // Validate that all selected universities exist and are active
            var validUniversityCount = await _context.KfUniversities
                .CountAsync(x => dto.UniversityIds.Contains(x.UniversityId) && x.IsActive);

            if (validUniversityCount != dto.UniversityIds.Distinct().Count())
            {
                throw new CustomException("One or more selected universities are invalid.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ---------------- Create Staff ----------------
                var staff = new KfStaff
                {
                    StaffType = (long)StaffType.University,

                    StaffSalutation = dto.StaffSalutation,
                    StaffFirstName = dto.StaffFirstName,
                    StaffLastName = dto.StaffLastName,
                    Gender = dto.Gender,

                    OfficialEmail = dto.OfficialEmail.Trim().ToLower(),
                    PersonalEmail = string.IsNullOrWhiteSpace(dto.PersonalEmail)
                    ? null : dto.PersonalEmail.Trim().ToLower(),

                    MobileNumber = dto.MobileNumber,

                    Remarks = dto.Remarks,
                    IsActive = true,
                    IsDisabled = dto.IsDisabled,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUser.LoginId
                };

                _context.KfStaffs.Add(staff);

                // Need StaffId for username generation
                await _context.SaveChangesAsync();

                // ---------------- Generate Credentials ----------------
                var generatedPassword = HelperMethods.GeneratePassword();

                var loginName = HelperMethods.GenerateUsername(
                    (long)StaffType.University,
                    staff.StaffId);

                // ---------------- Create Login ----------------
                var login = new KfUsersLogin
                {
                    StaffId = staff.StaffId,
                    LoginName = loginName,

                    RecoveryEmail = dto.RecoveryEmail.Trim().ToLower(),

                    TempPassword = null,
                    TempPassDateTime = null,

                    IsActive = true,
                    IsDisabled = dto.IsDisabled,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUser.LoginId
                };

                // Hash password
                login.Password = HelperMethods.HashPassword(login, generatedPassword);

                _context.KfUsersLogins.Add(login);

                await _context.SaveChangesAsync();

                // ---------------- Assign Role ----------------
                var loginRole = new KfUsersRoleAssignment
                {
                    LoginId = login.LoginId,
                    RoleId = dto.RoleId,

                    IsDefault = true,

                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = currentUser.LoginId
                };

                _context.KfUsersRoleAssignments.Add(loginRole);

                // ---------------- University Mapping ----------------
                var universityMappings = dto.UniversityIds
                    .Distinct()
                    .Select(universityId => new KfStaffUniversityCoordinatorMapping
                    {
                        StaffId = staff.StaffId,
                        UniversityId = universityId,

                        IsActive = true,

                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = currentUser.LoginId
                    })
                    .ToList();

                _context.KfStaffUniversityCoordinatorMappings.AddRange(universityMappings);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Send notification email to the new user with their credentials
                await SendNewUserAccountNotificationAsync(staff, login, generatedPassword);

                return staff.StaffId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<bool> UpdateAsync(UniversityCoordinatorRequestDto dto, LoggedInUserDto currentUser)
        {
            // Validate Staff
            var staff = await _context.KfStaffs
                .FirstOrDefaultAsync(x => x.StaffId == dto.StaffId);

            if (staff == null)
                throw new CustomException("University coordinator not found.");

            // Validate Role
            var roleExists = await _context.KfUsersRoles
                .AnyAsync(x =>
                    x.RoleId == dto.RoleId &&
                    x.ModuleId == (long)StaffType.University &&
                    x.IsActive);

            if (!roleExists)
                throw new CustomException("Selected role does not exist.");

            // Validate Official Email
            if (await _context.KfStaffs.AnyAsync(x =>
                    x.StaffId != dto.StaffId &&
                    x.OfficialEmail.Trim().ToLower() == dto.OfficialEmail.Trim().ToLower()))
            {
                throw new CustomException("Official email already exists.");
            }

            // Validate University Selection
            if (dto.UniversityIds == null || !dto.UniversityIds.Any())
            {
                throw new CustomException("Please select at least one university.");
            }

            // Validate selected universities
            var validUniversityCount = await _context.KfUniversities
                .CountAsync(x => dto.UniversityIds.Contains(x.UniversityId) && x.IsActive);

            if (validUniversityCount != dto.UniversityIds.Distinct().Count())
            {
                throw new CustomException("One or more selected universities are invalid.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ---------------- Update Staff ----------------

                staff.StaffType = (long)StaffType.University;
                staff.StaffSalutation = dto.StaffSalutation;
                staff.StaffFirstName = dto.StaffFirstName;
                staff.StaffLastName = dto.StaffLastName;
                staff.Gender = dto.Gender;

                staff.OfficialEmail = dto.OfficialEmail.Trim().ToLower();
                staff.PersonalEmail = string.IsNullOrWhiteSpace(dto.PersonalEmail)
                    ? null : dto.PersonalEmail.Trim().ToLower();

                staff.MobileNumber = dto.MobileNumber;

                staff.Remarks = dto.Remarks;
                staff.IsDisabled = dto.IsDisabled;

                // not updating IsActive here,
                // staff.IsActive = dto.IsActive;

                staff.UpdatedDate = DateTime.UtcNow;
                staff.UpdatedBy = currentUser.LoginId;

                // ---------------- Update Login ----------------

                var login = await _context.KfUsersLogins
                    .FirstOrDefaultAsync(x => x.StaffId == staff.StaffId);

                if (login == null)
                    throw new CustomException("Login details not found.");

                if (await _context.KfUsersLogins.AnyAsync(x =>
                    x.LoginId != login.LoginId && x.RecoveryEmail.Trim().ToLower() == dto.RecoveryEmail.Trim().ToLower()))
                {
                    throw new CustomException("Recovery email already exists.");
                }

                login.RecoveryEmail = dto.RecoveryEmail.Trim().ToLower();

                // not updating IsActive here,
                // login.IsActive = dto.IsActive;

                login.IsDisabled = dto.IsDisabled;
                login.UpdatedDate = DateTime.UtcNow;
                login.UpdatedBy = currentUser.LoginId;

                // ---------------- Update Role ----------------
                var loginRole = await _context.KfUsersRoleAssignments
                    .FirstOrDefaultAsync(x =>
                        x.LoginId == login.LoginId &&
                        x.IsDefault );

                if (loginRole == null)
                    throw new CustomException("Role mapping not found.");

                loginRole.RoleId = dto.RoleId;

                // not updating IsActive here,
                // loginRole.IsActive = dto.IsActive;


                // ---------------- Update University Mapping ----------------

                // ---------------- Deactivate Existing University Mappings ----------------
                var existingMappings = await _context.KfStaffUniversityCoordinatorMappings
                    .Where(x => x.StaffId == staff.StaffId && x.IsActive)
                    .ToListAsync();

                foreach (var mapping in existingMappings)
                {
                    mapping.IsActive = false;
                    mapping.UpdatedDate = DateTime.UtcNow;
                    mapping.UpdatedBy = currentUser.LoginId;
                }

                // ---------------- Add New University Mappings ----------------
                var newMappings = dto.UniversityIds
                    .Distinct()
                    .Select(universityId => new KfStaffUniversityCoordinatorMapping
                    {
                        StaffId = staff.StaffId,
                        UniversityId = universityId,

                        IsActive = true,

                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = currentUser.LoginId
                    })
                    .ToList();

                _context.KfStaffUniversityCoordinatorMappings.AddRange(newMappings);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<bool> DeleteAsync(long staffId, LoggedInUserDto currentUser)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Staff
                var staff = await _context.KfStaffs
                    .FirstOrDefaultAsync(x => x.StaffId == staffId);

                if (staff == null || !staff.IsActive)
                    return false;

                staff.IsActive = false;
                staff.IsDisabled = true;
                staff.UpdatedBy = currentUser.LoginId;
                staff.UpdatedDate = DateTime.UtcNow;

                // Login
                var login = await _context.KfUsersLogins
                    .FirstOrDefaultAsync(x => x.StaffId == staffId);

                if (login != null)
                {
                    login.IsActive = false;
                    login.IsDisabled = true;
                    login.UpdatedBy = currentUser.LoginId;
                    login.UpdatedDate = DateTime.UtcNow;

                    // Login Role
                    var loginRoles = await _context.KfUsersRoleAssignments
                        .Where(x => x.LoginId == login.LoginId)
                        .ToListAsync();

                    foreach (var role in loginRoles)
                    {
                        //role.IsActive = false;
                        //role.UpdatedBy = currentUser.LoginId;
                        //role.UpdatedDate = DateTime.UtcNow;

                        _context.KfUsersRoleAssignments.Remove(role);
                    }
                }

                // ---------------- University Mappings ----------------
                var universityMappings = await _context.KfStaffUniversityCoordinatorMappings
                    .Where(x => x.StaffId == staffId && x.IsActive)
                    .ToListAsync();

                foreach (var mapping in universityMappings)
                {
                    mapping.IsActive = false;
                    mapping.UpdatedBy = currentUser.LoginId;
                    mapping.UpdatedDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        public async Task<UniversityCoordinatorRequestDto> GetByIdAsync(long staffId)
        {
            var universityCoordinator = await _context.KfUsersRoleAssignments
               .AsNoTracking()
               .Where(x =>
                   x.Login.IsActive &&
                   x.Login.Staff.IsActive &&
                   x.Login.Staff.StaffType == (long)StaffType.University &&
                   x.Login.StaffId == staffId)
               .Include(x => x.Login)
                   .ThenInclude(x => x.Staff)
               .Include(x => x.Role)
               .Select(x => new UniversityCoordinatorRequestDto
               {
                   // Ids
                   StaffId = x.Login.Staff.StaffId,
                   LoginId = x.Login.LoginId,

                   // Staff
                   StaffType = x.Login.Staff.StaffType,
                   StaffSalutation = x.Login.Staff.StaffSalutation,
                   StaffFirstName = x.Login.Staff.StaffFirstName,
                   StaffLastName = x.Login.Staff.StaffLastName,

                   Gender = x.Login.Staff.Gender,

                   OfficialEmail = x.Login.Staff.OfficialEmail,
                   PersonalEmail = x.Login.Staff.PersonalEmail,
                   MobileNumber = x.Login.Staff.MobileNumber,

                   Remarks = x.Login.Staff.Remarks,

                   // Login
                   LoginName = x.Login.LoginName,
                   RecoveryEmail = x.Login.RecoveryEmail,

                   // Role
                   RoleId = x.RoleId,
                   RoleName = x.Role.RoleName,

                   // Status
                   IsDefaultRole = x.IsDefault,
                   IsActive = x.Login.IsActive,
                   IsDisabled = x.Login.Staff.IsDisabled,

                   // Universities
                   UniversityIds = x.Login.Staff.KfStaffUniversityCoordinatorMappings
                       .Where(m => m.IsActive)
                       .Select(m => m.UniversityId)
                       .ToList(),

                   UniversityNames = x.Login.Staff.KfStaffUniversityCoordinatorMappings
                       .Where(m => m.IsActive)
                       .Select(m => m.University.UniversityName)
                       .ToList(),

                   // Audit
                   CreatedDate = x.Login.Staff.CreatedDate,
                   CreatedBy = x.Login.Staff.CreatedBy,
                   UpdatedDate = x.Login.Staff.UpdatedDate,
                   UpdatedBy = x.Login.Staff.UpdatedBy
               })
               .FirstOrDefaultAsync();

            if (universityCoordinator == null)
                throw new CustomException("University coordinator not found.");

            universityCoordinator.FullName = UserDisplayHelper.GetFullName(
                universityCoordinator.StaffSalutation,
                universityCoordinator.StaffFirstName,
                universityCoordinator.StaffLastName);

            return universityCoordinator;
        }



        public async Task<PagedResultDto<UniversityCoordinatorRequestDto>> GetByFilterAsync(UniversityCoordinatorFilterDto filter)
        {
            var query = _context.KfUsersRoleAssignments
                .AsNoTracking()
                .Where(x =>
                    x.IsDefault &&
                    x.Login.IsActive &&
                    x.Login.Staff.IsActive &&
                    x.Login.Staff.StaffType == (long)StaffType.University)
                .Include(x => x.Login)
                    .ThenInclude(x => x.Staff)
                .Include(x => x.Role)
                .AsQueryable();

            // University
            if (filter.UniversityId.HasValue)
            {
                query = query.Where(x =>
                    x.Login.Staff.KfStaffUniversityCoordinatorMappings.Any(m =>
                        m.UniversityId == filter.UniversityId.Value &&
                        m.IsActive));
            }

            // Role
            if (filter.RoleId.HasValue)
            {
                query = query.Where(x => x.RoleId == filter.RoleId.Value);
            }

            // Disabled Status
            if (filter.IsDisabled.HasValue)
            {
                query = query.Where(x => x.Login.Staff.IsDisabled == filter.IsDisabled.Value);
            }

            // Global Search
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();

                query = query.Where(x =>
                    x.Login.Staff.StaffFirstName.ToLower().Contains(search) ||
                    x.Login.Staff.StaffLastName.ToLower().Contains(search) ||
                    x.Login.Staff.OfficialEmail.ToLower().Contains(search) ||
                    (x.Login.Staff.PersonalEmail != null &&
                     x.Login.Staff.PersonalEmail.ToLower().Contains(search))||
                    (x.Login.Staff.MobileNumber != null &&
                     x.Login.Staff.MobileNumber.ToLower().Contains(search)) ||
                    x.Login.LoginName.ToLower().Contains(search));
            }

            // Total Count
            var totalCount = await query.CountAsync();

            // Ordering
            query = query.OrderByDescending(x => x.Login.Staff.StaffId);

            // Pagination
            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new UniversityCoordinatorRequestDto
                {
                    // Ids
                    StaffId = x.Login.Staff.StaffId,
                    LoginId = x.Login.LoginId,

                    // Staff
                    StaffType = x.Login.Staff.StaffType,
                    StaffSalutation = x.Login.Staff.StaffSalutation,
                    StaffFirstName = x.Login.Staff.StaffFirstName,
                    StaffLastName = x.Login.Staff.StaffLastName,
                    Gender = x.Login.Staff.Gender,

                    OfficialEmail = x.Login.Staff.OfficialEmail,
                    PersonalEmail = x.Login.Staff.PersonalEmail,
                    MobileNumber = x.Login.Staff.MobileNumber,
                    Remarks = x.Login.Staff.Remarks,

                    // Login
                    LoginName = x.Login.LoginName,
                    RecoveryEmail = x.Login.RecoveryEmail,

                    // Role
                    RoleId = x.RoleId,
                    RoleName = x.Role == null ? null : x.Role.RoleName,

                    // Status
                    IsDefaultRole = x.IsDefault,
                    IsActive = x.Login.IsActive,
                    IsDisabled = x.Login.Staff.IsDisabled,

                    // Audit
                    CreatedDate = x.Login.Staff.CreatedDate,
                    CreatedBy = x.Login.Staff.CreatedBy,
                    UpdatedDate = x.Login.Staff.UpdatedDate,
                    UpdatedBy = x.Login.Staff.UpdatedBy
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.FullName = UserDisplayHelper.GetFullName(
                    item.StaffSalutation,
                    item.StaffFirstName,
                    item.StaffLastName);
            }

            return new PagedResultDto<UniversityCoordinatorRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }





        private async Task SendNewUserAccountNotificationAsync(KfStaff staff, KfUsersLogin login, string generatedPassword)
        {
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

            string fullName =
                $"{staff.StaffSalutation} {staff.StaffFirstName} {staff.StaffLastName}"
                .Trim();

            await _notificationService.SendNewUserAccountAsync(
                login.RecoveryEmail,
                login.LoginName,
                fullName,
                organizationName,
                generatedPassword
            );

        }



    }
}
