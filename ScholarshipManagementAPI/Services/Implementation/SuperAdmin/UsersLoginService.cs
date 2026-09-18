using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.SuperAdmin.UsersLogin;
using ScholarshipManagementAPI.DTOs.SuperAdmin.UsersRole;
using ScholarshipManagementAPI.DTOs.SuperAdmin.UsersRolePermission;
using ScholarshipManagementAPI.Helper;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.SuperAdmin;

namespace ScholarshipManagementAPI.Services.Implementation.SuperAdmin
{
    public class UsersLoginService : IUsersLoginService
    {
        private readonly AppDbContext _context;

        public UsersLoginService(AppDbContext context)
        {
            _context = context;
        }



        // ---------------- GET BY ID ----------------
        public async Task<UsersLoginRequestDto?> GetByIdAsync(long id)
        {
            return await _context.KfUsersLogins
                .AsNoTracking()
                .Where(x => x.LoginId == id)
                .Include(x => x.Staff)
                .Select(x => new UsersLoginRequestDto
                {
                    LoginId = x.LoginId,
                    StaffId = x.StaffId,
                    LoginName = x.LoginName,
                    Password = x.Password,
                    RecoveryEmail = x.RecoveryEmail,
                    IsActive = x.IsActive,
                    TempPassword = x.TempPassword,
                    TempPassDateTime = x.TempPassDateTime,
                    CreatedDate = x.CreatedDate,
                    CreatedBy = x.CreatedBy
                })
                .FirstOrDefaultAsync();
        }


        // ---------------- GET ALL FILTER ----------------
        public async Task<PagedResultDto<UsersLoginRequestDto>> GetByFilterAsync(UsersLoginFilterDto filter)
        {
            var query = _context.KfUsersLogins
                .AsNoTracking()
                .Include(x => x.Staff)
                .AsQueryable();

            if (filter.IsActive.HasValue)
                query = query.Where(x => x.IsActive == filter.IsActive);

            //if (filter.LoginType.HasValue)
            //    query = query.Where(x => x.LoginType == filter.LoginType);

            /* Global Search */
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();
                query = query.Where(x =>
                    x.LoginName.ToLower().Contains(search) ||
                    (x.RecoveryEmail != null && x.RecoveryEmail.ToLower().Contains(search)) 
                );
            }


            // ---------- Total Count (before pagination) ----------
            var totalCount = await query.CountAsync();

            // ---------- Ordering ----------
            query = query.OrderBy(x => x.LoginId);

            // ---------- Pagination rule ----------
            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new UsersLoginRequestDto
                {
                    LoginId = x.LoginId,
                    StaffId = x.StaffId,
                    LoginName = x.LoginName,
                    Password = x.Password,
                    RecoveryEmail = x.RecoveryEmail,
                    IsActive = x.IsActive,
                    TempPassword = x.TempPassword,
                    TempPassDateTime = x.TempPassDateTime,
                    CreatedDate = x.CreatedDate,
                    CreatedBy = x.CreatedBy
                })
                .ToListAsync();

            return new PagedResultDto<UsersLoginRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }



    }
}
