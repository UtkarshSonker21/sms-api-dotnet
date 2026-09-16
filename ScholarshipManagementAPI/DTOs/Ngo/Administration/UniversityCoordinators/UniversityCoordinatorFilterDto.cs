using ScholarshipManagementAPI.DTOs.Common.Filter;

namespace ScholarshipManagementAPI.DTOs.Ngo.Administration.UniversityCoordinators
{
    public class UniversityCoordinatorFilterDto : BaseFilterDto
    {
        public long? UniversityId { get; set; }

        public long? RoleId { get; set; }

        public bool? IsActive { get; set; }
        public bool? IsDisabled { get; set; }
    }
}
