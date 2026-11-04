using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.Ngo.SponsorshipAssignments;

public sealed class CreateSponsorshipAssignmentRequestDto
{
    [Range(1, long.MaxValue, ErrorMessage = "A valid student category is required.")]
    public long StudentCategoryId { get; set; }

    [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public sealed class AssignDonorSponsorshipRequestDto
{
    public long ApplicationId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "A valid sponsorship category is required.")]
    public long SponsorshipCategoryId { get; set; }

    public long StudentCategoryId
    {
        get => SponsorshipCategoryId;
        set { if (value > 0) SponsorshipCategoryId = value; }
    }

    [Required(ErrorMessage = "Donor name is required.")]
    [StringLength(250, ErrorMessage = "Donor name cannot exceed 250 characters.")]
    public string DonorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Donor code / reference is required.")]
    [StringLength(100, ErrorMessage = "Donor code / reference cannot exceed 100 characters.")]
    public string DonorCode { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public sealed class SponsorshipAssignmentOptionDto
{
    public long MappingId { get; set; }
    public long StudentCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long SponsorshipTypeId { get; set; }
    public string SponsorshipName { get; set; } = string.Empty;
    public byte FrequencyType { get; set; }
    public int? DisplayOrder { get; set; }
}

public sealed class SponsorshipAssignmentDto
{
    public long AssignmentId { get; set; }
    public long ApplicationId { get; set; }
    public long SponsorshipCategoryMappingId { get; set; }
    public long SponsorshipTypeId { get; set; }
    public string SponsorshipName { get; set; } = string.Empty;
    public long StudentCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public long AssignedBy { get; set; }
    public DateTime AssignedDate { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? DonorName { get; set; }
    public string? DonorCode { get; set; }
    public string? Remarks { get; set; }
    public List<SponsorshipAssignmentOptionDto> PermittedSponsorshipTypes { get; set; } = new();
}
