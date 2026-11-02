using System.Text.Json.Serialization;

namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class FinalizeAcademicResultsRequestDto
    {
        // Must be present in JSON; null is valid for a Draft that has not yet been updated.
        [JsonRequired]
        public DateTime? ExpectedUpdatedOn { get; set; }
    }
}
