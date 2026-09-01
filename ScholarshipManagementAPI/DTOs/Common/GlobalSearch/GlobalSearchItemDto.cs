namespace ScholarshipManagementAPI.DTOs.Common.GlobalSearch
{
    public class GlobalSearchItemDto
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public string? Route { get; set; }
    }
}
