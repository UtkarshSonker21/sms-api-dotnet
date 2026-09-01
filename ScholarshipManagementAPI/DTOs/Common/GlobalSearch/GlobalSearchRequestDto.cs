namespace ScholarshipManagementAPI.DTOs.Common.GlobalSearch
{
    public class GlobalSearchRequestDto
    {
        public string SearchText { get; set; } = string.Empty;

        public int Limit { get; set; } = 5;
    }
}
