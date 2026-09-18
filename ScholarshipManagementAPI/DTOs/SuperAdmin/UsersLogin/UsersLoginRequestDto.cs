using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.SuperAdmin.UsersLogin
{
    public class UsersLoginRequestDto
    {
        public long? LoginId { get; set; }   // null / 0 = Create, >0 = Update

        [Required(ErrorMessage = "Staff Id is required")]
        public long StaffId { get; set; }

        [Required(ErrorMessage = "Login name is required")]
        [StringLength(200)]
        public string LoginName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(200)]
        public string Password { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(200)]
        public string RecoveryEmail { get; set; } = string.Empty;

        [Required]
        public bool IsActive { get; set; }
        public bool IsDisabled { get; set; }



        [StringLength(50)]
        public string? TempPassword { get; set; }

        public DateTime? TempPassDateTime { get; set; }

        public DateTime? CreatedDate { get; set; }
        public long CreatedBy { get; set; }
        public string? CreatedByName { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public long? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }


    }
}
