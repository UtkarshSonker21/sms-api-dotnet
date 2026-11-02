using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Ngo.SponsorshipMatrix;
using ScholarshipManagementAPI.DTOs.Ngo.SponsorshipTypes;
using ScholarshipManagementAPI.DTOs.Ngo.StudentCategory;
using ScholarshipManagementAPI.Services.Interface.Ngo;

namespace ScholarshipManagementAPI.Services.Implementation.Ngo
{
    public class SponsorshipMatrixService : ISponsorshipMatrixService
    {
        private readonly AppDbContext _context;

        public SponsorshipMatrixService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SponsorshipMatrixDto> GetMatrixAsync()
        {
            var sponsorshipTypes = await _context.KfSponsorshipTypes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new SponsorshipTypeRequestDto
                {
                    SponsorshipTypeId = x.SponsorshipTypeId,
                    SponsorshipName = x.SponsorshipName,
                    FrequencyType = x.FrequencyType,
                    DisplayOrder = x.DisplayOrder
                })
                .ToListAsync();

            var studentCategories = await _context.KfSponsorshipStudentCategories
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new StudentCategoryRequestDto
                {
                    StudentCategoryId = x.StudentCategoryId,
                    CategoryName = x.CategoryName,
                    DisplayOrder = x.DisplayOrder
                })
                .ToListAsync();

            var mappings = await _context.KfSponsorshipCategoryMappings
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => new SponsorshipCategoryMappingDto
                {
                    MappingId = x.MappingId,
                    SponsorshipTypeId = x.SponsorshipTypeId,
                    StudentCategoryId = x.StudentCategoryId,
                    IsActive = x.IsActive
                })
                .ToListAsync();

            return new SponsorshipMatrixDto
            {
                SponsorshipTypes = sponsorshipTypes,
                StudentCategories = studentCategories,
                Mappings = mappings
            };
        }

        public async Task<bool> ToggleAsync(SponsorshipMatrixToggleRequestDto dto, long loginId)
        {
            var entity = await _context.KfSponsorshipCategoryMappings
                .FirstOrDefaultAsync(x =>
                    x.SponsorshipTypeId == dto.SponsorshipTypeId &&
                    x.StudentCategoryId == dto.StudentCategoryId);

            if (entity == null)
            {
                entity = new KfSponsorshipCategoryMapping
                {
                    SponsorshipTypeId = dto.SponsorshipTypeId,
                    StudentCategoryId = dto.StudentCategoryId,
                    IsActive = true,
                    CreatedBy = loginId,
                    CreatedDate = DateTime.UtcNow
                };

                _context.KfSponsorshipCategoryMappings.Add(entity);
            }
            else
            {
                entity.IsActive = !entity.IsActive;
                entity.UpdatedBy = loginId;
                entity.UpdatedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;


        }
   

    }
}
