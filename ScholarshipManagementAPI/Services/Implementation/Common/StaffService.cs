using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Staff;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Common;
using static ScholarshipManagementAPI.Helper.Utilities.Constant;

namespace ScholarshipManagementAPI.Services.Implementation.Common
{
    public class StaffService : IStaffService
    {

        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ICommonService _commonService;

        public StaffService(AppDbContext context,
            INotificationService notificationService,
            ICommonService commonService)
        {
            _context = context;
            _notificationService = notificationService;
            _commonService = commonService;
        }


        // ---------------- CREATE ----------------
        public async Task<long> CreateAsync(StaffRequestDto dto, LoggedInUserDto currentUser)
        {
            // ---------- 1. Permission & business validation ----------

            if (currentUser.StaffType != StaffType.SuperAdmin) 
            { 
                throw new CustomException("Only Super Admin can create staff accounts");
            }
            
            ValidateOrganisation(dto);

            // ---------- 2. Duplicate email check ----------
            if (await _context.KfUsersLogins
                .AnyAsync(x => x.RecoveryEmail == dto.OfficialEmail))
            {
                throw new CustomException("User with same email already exists");
            }

            // ---------- 3. Begin transaction ----------
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ---------- 4. Create HrStaffMaster ----------
                var staff = new KfStaff
                {
                    StaffType = dto.StaffType,

                    StaffSalutation = dto.StaffSalutation,
                    StaffFirstName = dto.StaffFirstName,
                    StaffLastName = dto.StaffLastName,
                    Gender = dto.Gender,

                    PermAddress = dto.PermAddress,
                    PermCity = dto.PermCity,
                    PermZipCode = dto.PermZipCode,
                    PermState = dto.PermState,
                    PermCountryId = dto.PermCountryId,

                    OfficialEmail = dto.OfficialEmail,
                    PersonalEmail = dto.PersonalEmail,
                    MobileNumber = dto.MobileNumber,

                    Remarks = dto.Remarks,
                    IsActive = true,
                    IsDisabled = dto.IsDisabled,
                    CreatedBy = dto.CreatedBy,
                    CreatedDate = DateTime.UtcNow,
                };

                _context.KfStaffs.Add(staff);
                await _context.SaveChangesAsync();

                var generatedPassword = HelperMethods.GeneratePassword();
                var loginName = HelperMethods.GenerateUsername(dto.StaffType, staff.StaffId);

                // ---------- 5. Create UsersLogin ----------
                var usersLogin = new KfUsersLogin
                {
                    StaffId = staff.StaffId,
                    LoginName = loginName,

                    // Password & OTP are NULL initially
                    TempPassword = null,
                    TempPassDateTime = null,

                    RecoveryEmail = dto.OfficialEmail,
                    IsActive = true,
                    IsDisabled = dto.IsDisabled,

                    CreatedBy = dto.CreatedBy,
                    CreatedDate = DateTime.UtcNow,
                };

                // hash AFTER object creation
                usersLogin.Password = HelperMethods.HashPassword(usersLogin, generatedPassword);

                _context.KfUsersLogins.Add(usersLogin);
                await _context.SaveChangesAsync();

                // ---------- 6. Commit ----------
                await transaction.CommitAsync();

                string organizationName = staff.StaffType switch
                {
                    (long)StaffType.University => "University Coordinator",

                    (long)StaffType.School => "School Coordinator",

                    (long)StaffType.Ngo => "NGO Administration",
                    (long)StaffType.SuperAdmin => "System Administration",
                    (long)StaffType.Marketing => "Marketing",
                    (long)StaffType.Finance => "Finance",
                    _ => string.Empty
                };

                string fullName = $"{staff.StaffSalutation} {staff.StaffFirstName} {staff.StaffLastName}"
                                 .Trim();

                await _notificationService.SendNewUserAccountAsync(
                    usersLogin.RecoveryEmail,
                    usersLogin.LoginName,
                    fullName,
                    organizationName,
                    generatedPassword
                );

                return staff.StaffId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------- UPDATE ----------------
        public async Task<bool> UpdateAsync(StaffRequestDto dto, LoggedInUserDto currentUser)
        {
            // ---------- 1. Business validation ----------

            if (currentUser.StaffType != StaffType.SuperAdmin)
            {
                throw new CustomException("Only Super Admin can update staff accounts");
            }

            ValidateOrganisation(dto);


            // ---------- 2. Fetch existing staff ----------
            var staff = await _context.KfStaffs
                .FirstOrDefaultAsync(x => x.StaffId == dto.StaffId);

            if (staff == null)
                throw new CustomException("Staff not found");

            // ---------- 3. Duplicate login check (exclude self) ----------
            if (!string.IsNullOrWhiteSpace(dto.LoginName))
            {
                var existingLogin = await _context.KfUsersLogins
                    .FirstOrDefaultAsync(x => x.StaffId == dto.StaffId);

                if (existingLogin != null &&
                    await _context.KfUsersLogins.AnyAsync(x =>
                        x.LoginName == dto.LoginName &&
                        x.StaffId != dto.StaffId))
                {
                    throw new CustomException("User with same login name already exists");
                }
            }

            // ---------- 4. Duplicate email check ----------
            if (await _context.KfUsersLogins
                .AnyAsync(x => x.RecoveryEmail == dto.OfficialEmail && x.StaffId != dto.StaffId))
            {
                throw new CustomException("User with same email already exists");
            }

            // ---------- 5. Begin transaction ----------
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ---------- 6. Update HrStaffMaster ----------
                staff.StaffType = dto.StaffType;

                staff.StaffSalutation = dto.StaffSalutation;
                staff.StaffFirstName = dto.StaffFirstName;
                staff.StaffLastName = dto.StaffLastName;
                staff.Gender = dto.Gender;

                staff.PermAddress = dto.PermAddress;
                staff.PermCity = dto.PermCity;
                staff.PermZipCode = dto.PermZipCode;
                staff.PermState = dto.PermState;
                staff.PermCountryId = dto.PermCountryId;

                staff.OfficialEmail = dto.OfficialEmail;
                staff.PersonalEmail = dto.PersonalEmail;
                staff.MobileNumber = dto.MobileNumber;

                staff.Remarks = dto.Remarks;
                staff.IsActive = true;
                staff.IsDisabled = dto.IsDisabled;

                staff.UpdatedBy = dto.UpdatedBy;
                staff.UpdatedDate = DateTime.UtcNow;

                _context.KfStaffs.Update(staff);
                await _context.SaveChangesAsync();

                // ---------- 7. Update UsersLogin ----------
                var usersLogin = await _context.KfUsersLogins
                    .FirstOrDefaultAsync(x => x.StaffId == dto.StaffId);

                if (usersLogin == null)
                    throw new CustomException("User login not found");

                // update from login name api
                //usersLogin.LoginName = dto.LoginName;
                usersLogin.RecoveryEmail = dto.OfficialEmail;
                usersLogin.IsActive = true;
                usersLogin.IsDisabled = dto.IsDisabled;
                usersLogin.UpdatedBy = dto.UpdatedBy;
                usersLogin.UpdatedDate = DateTime.UtcNow;

                _context.KfUsersLogins.Update(usersLogin);
                await _context.SaveChangesAsync();

                // ---------- 8. Commit ----------
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------- DELETE (Soft) ----------------
        public async Task<bool> DeleteAsync(long staffId, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.SuperAdmin)
            {
                throw new CustomException("Only Super Admin can delete staff accounts");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var staff = await _context.KfStaffs
                    .FirstOrDefaultAsync(x => x.StaffId == staffId);

                if (staff == null)
                    throw new CustomException("Staff not found");

                if(staff.StaffType == (long)StaffType.SuperAdmin)
                    throw new CustomException("Super Admin staff cannot be deleted");

                var usersLogin = await _context.KfUsersLogins
                    .FirstOrDefaultAsync(x => x.StaffId == staffId);

                if (usersLogin == null)
                    throw new CustomException("User login not found");

                // Soft delete
                staff.IsActive = false;
                staff.Remarks += "[Deleted]";
                staff.IsDisabled = true;
                usersLogin.IsActive = false;
                usersLogin.IsDisabled = true;

                _context.KfStaffs.Update(staff);
                _context.KfUsersLogins.Update(usersLogin);

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


        // ---------------- GET BY ID ----------------
        public async Task<StaffRequestDto?> GetByIdAsync(long id)
        {
            var staff = await _context.KfStaffs
                .AsNoTracking()
                .Include(x => x.KfUsersLogins)
                .Where(x => x.StaffId == id && x.IsActive)
                .Select(x => new StaffRequestDto
                {
                    StaffId = x.StaffId,

                    StaffType = x.StaffType,
                    StaffTypeName = x.StaffTypeNavigation == null
                                    ? null : x.StaffTypeNavigation.ModuleName,


                    StaffSalutation = x.StaffSalutation,
                    StaffFirstName = x.StaffFirstName,
                    StaffLastName = x.StaffLastName,
                    Gender = x.Gender,

                    PermAddress = x.PermAddress,
                    PermCity = x.PermCity,
                    PermZipCode = x.PermZipCode,
                    PermState = x.PermState,
                    PermCountryId = x.PermCountryId,
                    Photo = _commonService.GetProfileImageUrl(x.Photo),

                    OfficialEmail = x.OfficialEmail,
                    PersonalEmail = x.PersonalEmail,
                    MobileNumber = x.MobileNumber,

                    Remarks = x.Remarks,
                    IsActive = x.IsActive,
                    IsDisabled = x.IsDisabled,

                    LoginName = x.KfUsersLogins.Select(u => u.LoginName).FirstOrDefault(),


                    CreatedBy = x.CreatedBy,
                    CreatedDate = x.CreatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedDate = x.UpdatedDate
                })
                .FirstOrDefaultAsync();

            return staff;
        }


        // ---------------- GET ALL FILTER ----------------
        public async Task<PagedResultDto<StaffRequestDto>> GetByFilterAsync(StaffFilterDto filter, LoggedInUserDto currentUser)
        {
            var query = _context.KfStaffs
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Include(x => x.KfUsersLogins)
                .AsQueryable();

            // Staff filter
            if (filter.StaffType.HasValue)
            {
                query = query.Where(x => x.StaffType == filter.StaffType.Value);
            }

            // Country filter
            if (filter.CountryId.HasValue)
            {
                query = query.Where(x => x.PermCountryId == filter.CountryId.Value);
            }

            // active filter
            if (filter.IsActive.HasValue)
            {
                query = query.Where(x => x.IsActive == filter.IsActive.Value);
            }

            // disabled filter
            if (filter.IsDisabled.HasValue)
            {
                query = query.Where(x => x.IsDisabled == filter.IsDisabled.Value);
            }

            /* Global Search */
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();

                query = query.Where(x =>
                    x.StaffFirstName.ToLower().Contains(search) ||
                    x.StaffLastName.ToLower().Contains(search) ||
                    x.OfficialEmail.ToLower().Contains(search) ||
                    (x.MobileNumber != null && x.MobileNumber.ToLower().Contains(search)) ||
                    x.KfUsersLogins.Any(u => u.LoginName.ToLower().Contains(search))
                );
            }


            // ---------- Total Count (before pagination) ----------
            var totalCount = await query.CountAsync();

            // ---------- Ordering ----------
            query = query.OrderByDescending(x => x.StaffId);

            // ---------- Pagination rule ----------
            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new StaffRequestDto
                {
                    StaffId = x.StaffId,

                    StaffType = x.StaffType,
                    StaffTypeName = x.StaffTypeNavigation == null
                                    ? null : x.StaffTypeNavigation.ModuleName,

                    StaffSalutation = x.StaffSalutation,
                    StaffFirstName = x.StaffFirstName,
                    StaffLastName = x.StaffLastName,
                    Gender = x.Gender,

                    PermAddress = x.PermAddress,
                    PermCity = x.PermCity,
                    PermZipCode = x.PermZipCode,
                    PermState = x.PermState,
                    PermCountryId = x.PermCountryId,
                    PermCountryName = x.PermCountry == null ? null : x.PermCountry.CountryName,
                    Photo = _commonService.GetProfileImageUrl(x.Photo),
           
                    OfficialEmail = x.OfficialEmail,
                    PersonalEmail = x.PersonalEmail,
                    MobileNumber = x.MobileNumber,

                    Remarks = x.Remarks,
                    IsActive = x.IsActive,
                    IsDisabled = x.IsDisabled,
                    LoginName = x.KfUsersLogins.Select(u => u.LoginName).FirstOrDefault(),


                    CreatedBy = x.CreatedBy,
                    CreatedDate = x.CreatedDate,

                    UpdatedBy = x.UpdatedBy,
                    UpdatedDate = x.UpdatedDate
                })
                .ToListAsync();

            return new PagedResultDto<StaffRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }




        private static void ValidateOrganisation(StaffRequestDto dto)
        {
            var hasUniversity = dto.UniversityIds != null && dto.UniversityIds.Count > 0;
            var hasSchool = dto.SchoolIds != null && dto.SchoolIds.Count > 0;

            // Staff cannot be assigned to both organisations
            if (hasUniversity && hasSchool)
                throw new CustomException(
                    "Staff cannot be assigned to both universities and schools.");

            // Super Admin cannot belong to any organisation
            if (dto.StaffType == (long)StaffType.SuperAdmin &&
                (hasUniversity || hasSchool))
            {
                throw new CustomException(
                    "Super Admin cannot be assigned to any organisation.");
            }

        }



    }
}
