namespace ScholarshipManagementAPI.DTOs.Ngo.SponsorshipMatrix
{
    public class SponsorshipCategoryMappingDto
    {
        public long MappingId { get; set; }

        public long SponsorshipTypeId { get; set; }

        public long StudentCategoryId { get; set; }

        public bool IsActive { get; set; }
    }
}
