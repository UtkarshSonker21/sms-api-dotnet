namespace ScholarshipManagementAPI.DTOs.Common.GlobalSearch
{
    public class GlobalSearchSectionDto
    {
        public string SectionName { get; set; } = string.Empty;

        public List<GlobalSearchItemDto> Items { get; set; } = new();
    }
}
