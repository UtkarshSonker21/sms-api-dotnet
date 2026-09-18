using ScholarshipManagementAPI.DTOs.Common.Filter;

namespace ScholarshipManagementAPI.DTOs.SuperAdmin.UsersLogin
{
    public class UsersLoginFilterDto :BaseFilterDto
    {
        public bool? IsActive { get; set; }
        public bool? IsDisabled { get; set; }

    }
}
