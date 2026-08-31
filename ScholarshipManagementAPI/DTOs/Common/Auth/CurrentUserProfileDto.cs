using ScholarshipManagementAPI.DTOs.Common.Settings;
using ScholarshipManagementAPI.Helper.Enums;

namespace ScholarshipManagementAPI.DTOs.Common.Auth
{
    public class CurrentUserProfileDto
    {
        public long LoginId { get; set; }
        public string LoginName { get; set; } = string.Empty;

        public List<AvailableRolesDto> AvailableRoles { get; set; } = new();

        public long CurrentRoleId { get; set; }
        public string CurrentRoleName { get; set; } = string.Empty;

        public long ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;


        public StaffType StaffType { get; set; }

        public List<long> UniversityIds { get; set; } = new();
        public List<long> SchoolIds { get; set; } = new();

        public string OrganizationName { get; set; } = string.Empty;



        // ADD THESE (for profile page)

        public string? ProfilePhoto { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;
        public string Salutation { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public string? PersonalEmail { get; set; }

        public string OfficialEmail { get; set; } = string.Empty;
        public bool Status { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        //public string? State { get; set; }
        public string? Country { get; set; }
        public long? CountryId { get; set; }
        public string? Zip { get; set; }

        //public DateTime? LastLogin { get; set; }


        // default currency info (for school & university) and for rest send base currency info
        public string DefaultCurrencyCode { get; set; } = "";
        public string DefaultCurrencyName { get; set; } = "";
        public string DefaultCurrencySymbol { get; set; } = "";

    }
}
