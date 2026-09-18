using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.SuperAdmin.UsersLogin;

namespace ScholarshipManagementAPI.Services.Interface.SuperAdmin
{
    public interface IUsersLoginService
    {
        Task<UsersLoginRequestDto?> GetByIdAsync(long id);
        Task<PagedResultDto<UsersLoginRequestDto>> GetByFilterAsync(UsersLoginFilterDto filter);
    }
}
