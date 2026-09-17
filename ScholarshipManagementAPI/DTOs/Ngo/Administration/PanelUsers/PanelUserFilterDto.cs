using ScholarshipManagementAPI.DTOs.Common.Filter;

namespace ScholarshipManagementAPI.DTOs.Ngo.Administration.PanelUsers
{
    public class PanelUserFilterDto : BaseFilterDto
    { 
        public long? RoleId { get; set; }
        public long? StaffType { get; set; }

        public bool? IsActive { get; set; }
        public bool? IsDisabled { get; set; }
    }
}
