using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.Ngo.SponsorshipAssignments;

public sealed class CreateSponsorshipAssignmentRequestDto
{
    [Range(1, long.MaxValue)]
    public long StudentCategoryId { get; set; }

    [StringLength(500)]
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
    // AssignmentId identifies the persisted category assignment row. Since the
    // existing schema stores a matrix mapping FK, SponsorshipCategoryMappingId
    // is an anchor mapping for its category; permitted types are derived live.
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
    public string? Remarks { get; set; }
    public List<SponsorshipAssignmentOptionDto> PermittedSponsorshipTypes { get; set; } = new();
}
