using System;

namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicResultsSaveSummaryDto
    {
        public long AcademicRegistrationId { get; set; }

        public int CourseResultsCreated { get; set; }

        public int CourseResultsAlreadyPresent { get; set; }

        public bool SemesterResultCreated { get; set; }

        public string SubmissionStatus { get; set; } = "Draft";

        public DateTime? ExpectedUpdatedOn { get; set; }
    }
}
