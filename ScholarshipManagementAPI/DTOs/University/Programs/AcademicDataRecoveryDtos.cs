namespace ScholarshipManagementAPI.DTOs.University.Programs
{
    public sealed class AcademicCourseMappingRecoveryDto
    {
        public long ProgramCourseId { get; set; }
        public long ProgramId { get; set; }
        public int SemesterNo { get; set; }
        public long? ProgramSemesterId { get; set; }
        public long CourseId { get; set; }
        public int Credits { get; set; }
        public bool IsActive { get; set; }
        public bool CanBeLinked { get; set; }
        public bool WillResolveAfterSemesterBackfill { get; set; }
        public string? LinkIssue { get; set; }
        public List<long> EnrollmentIds { get; set; } = new();
        public List<long> CourseResultIds { get; set; } = new();
        public List<long> SemesterResultIds { get; set; } = new();
        public List<AcademicRegistrationRecoveryDependencyDto> DependentRegistrations { get; set; } = new();
    }

    public sealed class AcademicRegistrationRecoveryDependencyDto
    {
        public long EnrollmentId { get; set; }
        public long? AcademicRegistrationId { get; set; }
        public long? RegistrationProgramId { get; set; }
        public int? RegistrationSemesterNo { get; set; }
        public long? RegistrationProgramSemesterId { get; set; }
        public long? RegistrationStudentId { get; set; }
        public long? ApplicationId { get; set; }
        public long? ApplicationProgramId { get; set; }
        public long? ApplicationStudentId { get; set; }
        public long? CourseResultId { get; set; }
        public long? SemesterResultId { get; set; }
        public string? Conflict { get; set; }
    }

    public sealed class AcademicProgramRecoveryPreviewDto
    {
        public long ProgramId { get; set; }
        public int ConfiguredSemesterCount { get; set; }
        public int PersistedSemesterCount { get; set; }
        public List<int> MissingSemesterNumbers { get; set; } = new();
        public List<AcademicCourseMappingRecoveryDto> Mappings { get; set; } = new();
        public bool IsEligibleForRecovery { get; set; }
        public List<string> ValidationIssues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? PreviewToken { get; set; }
    }

    public sealed class AcademicRegistrationRecoveryPreviewDto
    {
        public long AcademicRegistrationId { get; set; }
        public long ApplicationId { get; set; }
        public long ProgramId { get; set; }
        public long UniversityId { get; set; }
        public int SemesterNo { get; set; }
        public int ConfiguredSemesterCount { get; set; }
        public int PersistedSemesterCount { get; set; }
        public int MatchingProgramSemesterCount { get; set; }
        public long? ExistingProgramSemesterId { get; set; }
        public long? ResolvedProgramSemesterId { get; set; }
        public bool WillCreateProgramSemesters { get; set; }
        public List<int> MissingProgramSemesterNumbers { get; set; } = new();
        public bool WillLinkRegistration { get; set; }
        public List<AcademicCourseMappingRecoveryDto> ActiveCourseMappings { get; set; } = new();
        public List<long> ExistingEnrollmentIds { get; set; } = new();
        public List<long> MissingProgramCourseIds { get; set; } = new();
        public List<long> ExistingCourseResultIds { get; set; } = new();
        public long? ExistingSemesterResultId { get; set; }
        public bool IsEligibleForRecovery { get; set; }
        public List<string> ValidationIssues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? PreviewToken { get; set; }
    }

    public sealed class AcademicRegistrationRecoveryApplyRequestDto
    {
        public string PreviewToken { get; set; } = string.Empty;
        public bool ConfirmRecovery { get; set; }
        public bool AcknowledgeCreditWarnings { get; set; }
    }

    public sealed class AcademicDataRecoveryApplyResultDto
    {
        public long AcademicRegistrationId { get; set; }
        public long ProgramId { get; set; }
        public List<int> CreatedSemesterNumbers { get; set; } = new();
        public List<long> LinkedProgramCourseIds { get; set; } = new();
        public bool RegistrationSemesterLinked { get; set; }
        public List<long> CreatedEnrollmentIds { get; set; } = new();
        public List<long> ExistingEnrollmentIds { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool Applied { get; set; }
    }
}
