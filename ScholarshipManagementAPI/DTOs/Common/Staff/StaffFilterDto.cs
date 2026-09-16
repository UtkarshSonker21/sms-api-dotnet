using ScholarshipManagementAPI.DTOs.Common.Filter;

namespace ScholarshipManagementAPI.DTOs.Common.Staff
{
    public class StaffFilterDto : BaseFilterDto
    {
        public long? StaffType { get; set; }        // Filter by module
        public long? OrganisationId { get; set; }   // School / University / Ngo
        public long? CountryId { get; set; }   // School / University / Ngo
        public bool? IsActive { get; set; }
        public bool? IsDisabled { get; set; }
    }
}
