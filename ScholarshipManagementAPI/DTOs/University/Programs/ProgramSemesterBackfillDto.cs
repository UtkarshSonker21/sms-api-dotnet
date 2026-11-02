namespace ScholarshipManagementAPI.DTOs.University.Programs
{
    public class ProgramSemesterBackfillDto
    {
        public long ProgramId { get; set; }
        public int ConfiguredSemesterCount { get; set; }
        public List<int> ExistingSemesterNumbers { get; set; } = new();
        public List<int> MissingSemesterNumbers { get; set; } = new();
        public List<int> InvalidSemesterNumbers { get; set; } = new();
        public List<int> DuplicateSemesterNumbers { get; set; } = new();
        public List<long> InvalidSemesterIds { get; set; } = new();

        public List<long> DuplicateSemesterIds { get; set; } = new();
        public bool IsEligibleForRecovery { get; set; }
        public List<string> ValidationIssues { get; set; } = new();
        public int ExpectedNewSemesterCount { get; set; }
        public bool Applied { get; set; }
        public List<int> CreatedSemesterNumbers { get; set; } = new();
    }
}
