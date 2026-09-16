using ScholarshipManagementAPI.DTOs.Common.Filter;

namespace ScholarshipManagementAPI.DTOs.Ngo.Administration.SchoolCoordinators
{
    public class SchoolCoordinatorFilterDto : BaseFilterDto
    {
        public long? SchoolId { get; set; }

        public long? RoleId { get; set; }

        public bool? IsActive { get; set; }
        public bool? IsDisabled { get; set; }
    }
}
