using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.School.MasterSchool;
using ScholarshipManagementAPI.DTOs.University;
using ScholarshipManagementAPI.DTOs.University.MasterUniversity;
using ScholarshipManagementAPI.Helper;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Services.Implementation.University
{
    public class UniversityService : IUniversityService
    {
        private readonly AppDbContext _context;

        public UniversityService(AppDbContext context)
        {
            _context = context;
        }


        // ---------------- CREATE ----------------
        public async Task<long> CreateAsync(UniversityRequestDto dto)
        {
            if (await _context.KfUniversities
                .AnyAsync(x => x.UniversityName.ToLower() == dto.UniversityName.ToLower()))
            {
                throw new CustomException("University with same name already exists");
            }

            var entity = new KfUniversity
            {
                UniversityName = dto.UniversityName,
                UniversityType = dto.UniversityTypeId,

                CharterAccreditation = dto.CharterAccreditation,
                EstablishedYear = dto.EstablishedYear,

                CountryId = dto.CountryId,
                City = dto.City,
                Address = dto.Address,
                Website = dto.Website,

                VcName = dto.VcName,
                VcEmail = dto.VcEmail,
                VcMobile = dto.VcMobile,

                CoordName = dto.CoordName,
                CoordPosition = dto.CoordPosition,
                CoordEmail = dto.CoordEmail,
                CoordPhone = dto.CoordPhone,

                FacultiesCount = dto.FacultiesCount,
                FacultyFulltimeCount = dto.FacultyFulltimeCount,
                AdminStaffCount = dto.AdminStaffCount,

                ProgDegreeCount = dto.ProgDegreeCount,
                ProgDiplomaCount = dto.ProgDiplomaCount,
                ProgCertificateCount = dto.ProgCertificateCount,
                ProgPostgradCount = dto.ProgPostgradCount,

                StudentsTotal = dto.StudentsTotal,
                StudentsEnrolled = dto.StudentsEnrolled,
                IntlStudentsPct = dto.IntlStudentsPct,
                StudentsGenderTypeId = dto.StudentsGenderTypeId,

                StudDegreeCount = dto.StudDegreeCount,
                StudDiplomaCount = dto.StudDiplomaCount,
                StudCertificateCount = dto.StudCertificateCount,
                StudPostgradCount = dto.StudPostgradCount,

                GraduatesTotal = dto.GraduatesTotal,
                AlumniCount = dto.AlumniCount,

                OpSustainabilityPct = dto.OpSustainabilityPct,
                EmployabilityPct = dto.EmployabilityPct,
                PhdStaffPct = dto.PhdStaffPct,
                FteRatio = dto.FteRatio,
                TeachingLoadHours = dto.TeachingLoadHours,

                AnnualPublications = dto.AnnualPublications,
                OnlineProgramsCount = dto.OnlineProgramsCount,
                IntlAccreditedProgramsCount = dto.IntlAccreditedProgramsCount,

                ExternalGrants = dto.ExternalGrants,
                Notes = dto.Notes,

                IsDraft = dto.IsDraft,
                IsActive = dto.IsActive,
                CreatedBy = dto.CreatedBy,
                CreatedDate = DateTime.UtcNow,
                UpdatedBy = null,
                UpdatedDate = null,

                // Draft / Submit Logic
                AccreditationStatus = dto.IsDraft ? (byte)0 : (byte)AccreditationStatusEnum.Pending,

                CommitteeComment = null,
                AccreditationBy = null,
                AccreditationDate = null,
            };

            _context.KfUniversities.Add(entity);
            await _context.SaveChangesAsync();

            return entity.UniversityId;
        }



        // ---------------- UPDATE ----------------
        public async Task<bool> UpdateAsync(UniversityRequestDto dto)
        {
            if (dto.UniversityId == null || dto.UniversityId == 0)
                return false;

            if (await _context.KfUniversities.AnyAsync(x =>
                      x.UniversityName.ToLower() == dto.UniversityName.ToLower()
                      && x.UniversityId != dto.UniversityId))
            {
                throw new CustomException("University with same name already exists");
            }

            var entity = await _context.KfUniversities
                .FirstOrDefaultAsync(x => x.UniversityId == dto.UniversityId);

            if (entity == null)
                return false;


            // Accreditation / Workflow Logic

            var oldStatus = entity.AccreditationStatus;
            var wasDraft = entity.IsDraft;

            // Do not allow modification while under review
            if (oldStatus == (byte)AccreditationStatusEnum.Pending)
            {
                throw new CustomException("University is under accreditation review.");
            }

            // Do not allow modification after accreditation
            if (oldStatus == (byte)AccreditationStatusEnum.Accredited)
            {
                throw new CustomException("Accredited university cannot be modified.");
            }

            entity.UniversityName = dto.UniversityName;
            entity.UniversityType = dto.UniversityTypeId;

            entity.CharterAccreditation = dto.CharterAccreditation;
            entity.EstablishedYear = dto.EstablishedYear;

            entity.CountryId = dto.CountryId;
            entity.City = dto.City;
            entity.Address = dto.Address;
            entity.Website = dto.Website;

            entity.VcName = dto.VcName;
            entity.VcEmail = dto.VcEmail;
            entity.VcMobile = dto.VcMobile;

            entity.CoordName = dto.CoordName;
            entity.CoordPosition = dto.CoordPosition;
            entity.CoordEmail = dto.CoordEmail;
            entity.CoordPhone = dto.CoordPhone;

            entity.FacultiesCount = dto.FacultiesCount;
            entity.FacultyFulltimeCount = dto.FacultyFulltimeCount;
            entity.AdminStaffCount = dto.AdminStaffCount;

            entity.ProgDegreeCount = dto.ProgDegreeCount;
            entity.ProgDiplomaCount = dto.ProgDiplomaCount;
            entity.ProgCertificateCount = dto.ProgCertificateCount;
            entity.ProgPostgradCount = dto.ProgPostgradCount;

            entity.StudentsTotal = dto.StudentsTotal;
            entity.StudentsEnrolled = dto.StudentsEnrolled;
            entity.IntlStudentsPct = dto.IntlStudentsPct;
            entity.StudentsGenderTypeId = dto.StudentsGenderTypeId;

            entity.StudDegreeCount = dto.StudDegreeCount;
            entity.StudDiplomaCount = dto.StudDiplomaCount;
            entity.StudCertificateCount = dto.StudCertificateCount;
            entity.StudPostgradCount = dto.StudPostgradCount;

            entity.GraduatesTotal = dto.GraduatesTotal;
            entity.AlumniCount = dto.AlumniCount;

            entity.OpSustainabilityPct = dto.OpSustainabilityPct;
            entity.EmployabilityPct = dto.EmployabilityPct;
            entity.PhdStaffPct = dto.PhdStaffPct;
            entity.FteRatio = dto.FteRatio;
            entity.TeachingLoadHours = dto.TeachingLoadHours;

            entity.AnnualPublications = dto.AnnualPublications;
            entity.OnlineProgramsCount = dto.OnlineProgramsCount;
            entity.IntlAccreditedProgramsCount = dto.IntlAccreditedProgramsCount;

            entity.ExternalGrants = dto.ExternalGrants;
            entity.Notes = dto.Notes;


            // Draft -> Submit
            if (wasDraft && !dto.IsDraft && dto.AccreditationStatus == (byte)AccreditationStatusEnum.Pending)
            {
                entity.IsDraft = false;
                entity.AccreditationStatus = (byte)AccreditationStatusEnum.Pending;
                entity.AccreditationDate = null;
                entity.AccreditationBy = null;
                entity.CommitteeComment = null;
            }

            // entity.IsActive = dto.IsActive;

            entity.UpdatedBy = dto.UpdatedBy;
            entity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }



        // ---------------- DELETE ----------------
        public async Task<bool> DeleteAsync(long id)
        {
            var entity = await _context.KfUniversities
                .FirstOrDefaultAsync(x => x.UniversityId == id);

            if (entity == null)
                return false;

            // University is already soft deleted
            if (!entity.IsActive)
            {
                throw new CustomException("University is already deleted.");
            }

            // Do not allow deletion while under accreditation review
            if (entity.AccreditationStatus == (byte)AccreditationStatusEnum.Pending)
            {
                throw new CustomException(
                    "University under accreditation review cannot be deleted."
                );
            }

            // Do not allow deletion after accreditation
            if (entity.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited)
            {
                throw new CustomException(
                    "Accredited university cannot be deleted."
                );
            }

            // Check whether university has active faculties
            var hasFaculties = await _context.KfFaculties
                .AnyAsync(x =>
                    x.UniversityId == id &&
                    x.IsActive);

            if (hasFaculties)
            {
                throw new CustomException(
                    "This university cannot be deleted because it has active faculties."
                );
            }

            // Check whether university has active programs
            var hasPrograms = await _context.KfPrograms
                .AnyAsync(x =>
                    x.UniversityId == id &&
                    x.IsActive);

            if (hasPrograms)
            {
                throw new CustomException(
                    "This university cannot be deleted because it has active programs."
                );
            }

            // Check whether university has active courses
            var hasCourses = await _context.KfCourses
                .AnyAsync(x =>
                    x.UniversityId == id &&
                    x.IsActive);

            if (hasCourses)
            {
                throw new CustomException(
                    "This university cannot be deleted because it has active courses."
                );
            }

            // Soft delete
            entity.IsActive = false;
            await _context.SaveChangesAsync();

            return true;
        }



        // ---------------- GET BY ID ----------------
        public async Task<UniversityRequestDto?> GetByIdAsync(long id, LoggedInUserDto currentUser)
        {
            var query = _context.KfUniversities
                .AsNoTracking()
                .Where(x => x.UniversityId == id && x.IsActive);

            // Role-based visibility
            if (currentUser.StaffType == StaffType.University)
            {
                // University users can see all active universities
            }
            else if (currentUser.StaffType == StaffType.Ngo)
            {
                // NGO can see submitted/non-draft active universities
                query = query.Where(x => !x.IsDraft);
            }
            else
            {
                // Other users can see only accredited active universities
                query = query.Where(x => x.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited);
            }

            return await query
                .Select(x => new UniversityRequestDto
                {
                    UniversityId = x.UniversityId,

                    UniversityName = x.UniversityName,
                    UniversityTypeId = x.UniversityType,

                    CharterAccreditation = x.CharterAccreditation,
                    EstablishedYear = x.EstablishedYear,

                    CountryId = x.CountryId,
                    City = x.City,
                    Address = x.Address,
                    Website = x.Website,

                    VcName = x.VcName,
                    VcEmail = x.VcEmail,
                    VcMobile = x.VcMobile,

                    CoordName = x.CoordName,
                    CoordPosition = x.CoordPosition,
                    CoordEmail = x.CoordEmail,
                    CoordPhone = x.CoordPhone,

                    FacultiesCount = x.FacultiesCount,
                    FacultyFulltimeCount = x.FacultyFulltimeCount,
                    AdminStaffCount = x.AdminStaffCount,

                    ProgDegreeCount = x.ProgDegreeCount,
                    ProgDiplomaCount = x.ProgDiplomaCount,
                    ProgCertificateCount = x.ProgCertificateCount,
                    ProgPostgradCount = x.ProgPostgradCount,

                    StudentsTotal = x.StudentsTotal,
                    StudentsEnrolled = x.StudentsEnrolled,
                    IntlStudentsPct = x.IntlStudentsPct,

                    StudentsGenderTypeId = x.StudentsGenderTypeId,

                    StudDegreeCount = x.StudDegreeCount,
                    StudDiplomaCount = x.StudDiplomaCount,
                    StudCertificateCount = x.StudCertificateCount,
                    StudPostgradCount = x.StudPostgradCount,

                    GraduatesTotal = x.GraduatesTotal,
                    AlumniCount = x.AlumniCount,

                    OpSustainabilityPct = x.OpSustainabilityPct,
                    EmployabilityPct = x.EmployabilityPct,
                    PhdStaffPct = x.PhdStaffPct,
                    FteRatio = x.FteRatio,
                    TeachingLoadHours = x.TeachingLoadHours,

                    AnnualPublications = x.AnnualPublications,
                    OnlineProgramsCount = x.OnlineProgramsCount,
                    IntlAccreditedProgramsCount = x.IntlAccreditedProgramsCount,

                    ExternalGrants = x.ExternalGrants,
                    Notes = x.Notes,

                    AccreditationStatus = x.AccreditationStatus,
                    AccreditationBy = x.AccreditationBy,
                    AccreditationDate = x.AccreditationDate,
                    CommitteeComment = x.CommitteeComment,

                    IsDraft = x.IsDraft,
                    IsActive = x.IsActive,

                    CreatedBy = x.CreatedBy,
                    CreatedDate = x.CreatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedDate = x.UpdatedDate,

                    // Navigation Properties
                    CountryName = x.Country != null
                        ? x.Country.CountryName
                        : null,

                    UniversityTypeName = x.UniversityTypeNavigation != null
                        ? x.UniversityTypeNavigation.DisplayText
                        : null,

                    StudentsGenderTypeName = x.StudentsGenderType != null
                        ? x.StudentsGenderType.DisplayText
                        : null,

                    AccreditationByName = x.AccreditationByNavigation != null
                        ? x.AccreditationByNavigation.LoginName
                        : null,

                    CreatedByName = x.CreatedByNavigation != null
                        ? x.CreatedByNavigation.LoginName
                        : null,

                    UpdatedByName = x.UpdatedByNavigation != null
                        ? x.UpdatedByNavigation.LoginName
                        : null,

                    FormattedCreatedDate = x.CreatedDate.ToString("dd MMM yyyy"),
                    FormattedUpdatedDate = x.UpdatedDate.HasValue
                        ? x.UpdatedDate.Value.ToString("dd MMM yyyy")
                        : null
                })
                .FirstOrDefaultAsync();
        }


        // ---------------- GET ALL FILTER ----------------
        public async Task<PagedResultDto<UniversityRequestDto>> GetByFilterAsync(UniversityFilterDto filter, LoggedInUserDto currentUser)
        {
            var query = _context.KfUniversities
                .AsNoTracking()
                .Where(x => x.IsActive)
                .AsQueryable();


            // Role-based visibility
            if (currentUser.StaffType == StaffType.University)
            {
                // All active universities
                // Draft + Pending + Accredited + Rejected

                // Only universities belonging to currentUser.UniversityIds,
                query = query.Where(x => currentUser.UniversityIds.Contains(x.UniversityId));
            }
            else if (currentUser.StaffType == StaffType.Ngo)
            {
                // NGO can see submitted/non-draft active universities
                query = query.Where(x => !x.IsDraft);
            }
            else
            {
                // Other users can see only accredited active universities
                query = query.Where(x => x.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited);
            }

            // Accreditation filter is meaningful only for
            // University and NGO users.
            if (filter.AccreditationStatus.HasValue &&
                (currentUser.StaffType == StaffType.University ||
                 currentUser.StaffType == StaffType.Ngo))
            {
                query = query.Where(x =>
                    x.AccreditationStatus == filter.AccreditationStatus.Value);
            }

            if (filter.UniversityId.HasValue)
                query = query.Where(x => x.UniversityId == filter.UniversityId);

            if (filter.CountryId.HasValue)
                query = query.Where(x => x.CountryId == filter.CountryId);

            if (filter.UniversityTypeId.HasValue)
                query = query.Where(x => x.UniversityType == filter.UniversityTypeId);

            if (filter.StudentsGenderTypeId.HasValue)
                query = query.Where(x => x.StudentsGenderTypeId == filter.StudentsGenderTypeId);


            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();

                query = query.Where(x =>
                    x.UniversityName.ToLower().Contains(search) ||
                    x.City.ToLower().Contains(search) ||
                    x.CoordName.ToLower().Contains(search));
            }

            var totalCount = await query.CountAsync();

            query = query.OrderByDescending(x => x.UniversityId);

            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new UniversityRequestDto
                {
                    UniversityId = x.UniversityId,

                    UniversityName = x.UniversityName,

                    UniversityTypeId = x.UniversityType,
                    UniversityTypeName = x.UniversityTypeNavigation != null
                        ? x.UniversityTypeNavigation.DisplayText
                        : null,

                    CharterAccreditation = x.CharterAccreditation,

                    EstablishedYear = x.EstablishedYear,

                    CountryId = x.CountryId,
                    CountryName = x.Country.CountryName,

                    City = x.City,
                    Address = x.Address,
                    Website = x.Website,

                    CoordName = x.CoordName,
                    CoordEmail = x.CoordEmail,
                    CoordPhone = x.CoordPhone,

                    StudentsGenderTypeId = x.StudentsGenderTypeId,
                    StudentsGenderTypeName = x.StudentsGenderType != null
                        ? x.StudentsGenderType.DisplayText
                        : null,

                    StudentsTotal = x.StudentsTotal,
                    StudentsEnrolled = x.StudentsEnrolled,

                    AccreditationStatus = x.AccreditationStatus,

                    AccreditationBy = x.AccreditationBy,
                    AccreditationByName = x.AccreditationByNavigation != null
                        ? x.AccreditationByNavigation.LoginName
                        : null,

                    IsDraft = x.IsDraft,
                    IsActive = x.IsActive,

                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByNavigation != null
                        ? x.CreatedByNavigation.LoginName
                        : null,

                    CreatedDate = x.CreatedDate,
                    FormattedCreatedDate = x.CreatedDate.ToString("dd MMM yyyy"),

                    FacultiesCount = x.FacultiesCount,

                    // Programs belonging to this university only
                    ProgramsCount = _context.KfPrograms
                    .Count(p => p.UniversityId == x.UniversityId && !p.IsDraft && p.IsActive)
                })
                .ToListAsync();

            return new PagedResultDto<UniversityRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }


    }
}
