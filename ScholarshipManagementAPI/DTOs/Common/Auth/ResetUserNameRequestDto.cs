namespace ScholarshipManagementAPI.DTOs.Common.Auth
{
    public class UpdatePasswordRequestDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string UpdatedPassword { get; set; } = string.Empty;
    }
}
