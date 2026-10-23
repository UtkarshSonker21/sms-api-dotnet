using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using System.Data;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.Faculties;
using ScholarshipManagementAPI.DTOs.University.Programs;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Services.Implementation.University
{
    public class ProgramsService : IProgramsService
    {
        private readonly AppDbContext _context;

        public ProgramsService(AppDbContext context)
        {
            _context = context;
        }


        // ---------------- CREATE ----------------
        public async Task<long> CreateAsync(ProgramRequestDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (!await _context.KfUniversities
                    .AnyAsync(x => x.UniversityId == dto.UniversityId && x.IsActive))
                {
                    throw new CustomException("Selected university does not exist.");
                }

                if (!await _context.KfFaculties.AnyAsync(x =>
                     x.FacultyId == dto.FacultyId && x.UniversityId == dto.UniversityId &&
                     x.IsActive))
                {
                    throw new CustomException("The selected faculty is not available for the selected university.");
                }

                if (dto.Courses?.Any() == true)
                {
                    var courseIds = dto.Courses
                        .Select(x => x.CourseId)
                        .Distinct()
                        .ToList();

                    // 1. Check courses belong to the selected university
                    var validCourseIds = await _context.KfCourses
                        .Where(x =>
                            x.UniversityId == dto.UniversityId &&
                            x.IsActive &&
                            courseIds.Contains(x.CourseId))
                        .Select(x => x.CourseId)
                        .ToListAsync();

                    if (validCourseIds.Count != courseIds.Count)
                    {
                        throw new CustomException("One or more selected courses are not available for the selected university.");
                    }


                    // 2. Check courses are mapped to the selected faculty
                    var validFacultyCourseIds = await _context.KfCourseFaculties
                        .Where(x =>
                            x.FacultyId == dto.FacultyId &&
                            x.IsActive &&
                            courseIds.Contains(x.CourseId))
                        .Select(x => x.CourseId)
                        .Distinct()
                        .ToListAsync();

                    if (validFacultyCourseIds.Count != courseIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected courses are not mapped to the selected faculty."
                        );
                    }

                }


                if (await _context.KfPrograms.AnyAsync(x =>
                        x.ProgramCode.ToLower() == dto.ProgramCode.ToLower()
                        && x.UniversityId == dto.UniversityId))
                {
                    throw new CustomException("Program with same code already exists");
                }

                if (dto.NumberOfSemesters <= 0)
                {
                    throw new CustomException("Number of semesters must be greater than 0.");
                }

                if (dto.Documents?.Any() == true)
                {
                    var documentTypeIds = dto.Documents
                        .Select(x => x.DocumentTypeId)
                        .Distinct()
                        .ToList();

                    var validDocumentTypeIds = await _context.KfDocumentTypes
                        .Where(x =>
                            x.IsActive &&
                            documentTypeIds.Contains(x.DocumentTypeId))
                        .Select(x => x.DocumentTypeId)
                        .ToListAsync();

                    if (validDocumentTypeIds.Count != documentTypeIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected document types are no longer available."
                        );
                    }
                }

                if(dto.Costs?.Any() == true)
                {
                    var sponsorshipTypeIds = dto.Costs
                        .Select(x => x.SponsorshipTypeId)
                        .Distinct()
                        .ToList();

                    var validSponsorshipTypeIds = await _context.KfSponsorshipTypes
                        .Where(x =>
                            x.IsActive &&
                            sponsorshipTypeIds.Contains(x.SponsorshipTypeId))
                        .Select(x => x.SponsorshipTypeId)
                        .ToListAsync();

                    if (validSponsorshipTypeIds.Count != sponsorshipTypeIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected sponsorship types are no longer available."
                        );
                    }
                }

                ValidateProgramRequest(dto);

                var entity = new KfProgram
                {
                    UniversityId = dto.UniversityId,
                    FacultyId = dto.FacultyId,
                    ProgramName = dto.ProgramName,
                    ProgramCode = dto.ProgramCode,
                    Degree = dto.Degree,
                    NumberOfSemesters = dto.NumberOfSemesters,
                    CreditsRequired = dto.CreditsRequired,
                    AllowedStudentSeats = dto.AllowedStudentSeats,
                    MinAcceptanceRate = dto.MinAcceptanceRate,
                    AllowedHighSchoolDivisions = dto.AllowedHighSchoolDivisions,
                    AllowedTanzanianCombinations = dto.AllowedTanzanianCombinations,

                    IsDraft = dto.IsDraft,
                    
                    IsActive = true,
                    CreatedBy = dto.CreatedBy,
                    CreatedDate = DateTime.UtcNow
                };

                // Draft / Submit Logic
                if (dto.IsDraft)
                {
                    entity.AccreditationStatus = 0;
                    entity.SubmittedDate = null;
                    entity.CommitteeComment = null;
                }
                else
                {
                    entity.AccreditationStatus = (byte)AccreditationStatusEnum.Pending;
                    entity.SubmittedDate = DateTime.UtcNow;
                    entity.CommitteeComment = null;
                }


                _context.KfPrograms.Add(entity);

                await _context.SaveChangesAsync();

                var programSemesters = Enumerable.Range(1, dto.NumberOfSemesters)
                    .Select(semesterNo => CreateProgramSemester(
                        entity.ProgramId,
                        semesterNo,
                        dto.CreatedBy))
                    .ToList();

                _context.KfProgramSemesters.AddRange(programSemesters);
                await _context.SaveChangesAsync();

                var semesterIdsByNumber = programSemesters
                    .ToDictionary(x => x.SemesterNo, x => x.ProgramSemesterId);

                // Program Documents ---------------------------------------

                if (dto.Documents?.Any() == true)
                {
                    var documents = dto.Documents
                        .Select(x => new KfProgramDocument
                        {
                            ProgramId = entity.ProgramId,
                            DocumentTypeId = x.DocumentTypeId,
                            IsRequired = x.IsRequired,
                            DisplayOrder = x.DisplayOrder
                        })
                        .ToList();

                    _context.KfProgramDocuments.AddRange(documents);
                }

                // Program Costs ---------------------------------------

                if (dto.Costs?.Any() == true)
                {
                    var costs = dto.Costs
                        .Select(x => new KfProgramCost
                        {
                            ProgramId = entity.ProgramId,
                            SponsorshipTypeId = x.SponsorshipTypeId,
                            Amount = x.Amount
                        })
                        .ToList();

                    _context.KfProgramCosts.AddRange(costs);
                }

                // Program Courses ---------------------------------------

                if (dto.Courses?.Any() == true)
                {
                    var courses = dto.Courses
                        .Select(x => new KfProgramCourse
                        {
                            ProgramId = entity.ProgramId,
                            CourseId = x.CourseId,
                            CourseType = x.CourseType,
                            Credits = x.Credits,
                            DisplayOrder = x.DisplayOrder,
                            SemesterNo = x.SemesterNo,
                            ProgramSemesterId = semesterIdsByNumber[x.SemesterNo]
                        })
                        .ToList();

                    _context.KfProgramCourses.AddRange(courses);
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return entity.ProgramId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------- UPDATE ----------------
        public async Task<bool> UpdateAsync(ProgramRequestDto dto)
        {
            if (dto.ProgramId == null || dto.ProgramId == 0)
                return false;

            // Serialize edits to one program's semester schedule so concurrent updates
            // cannot assign the same academic term to different semester numbers.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var entity = await _context.KfPrograms
                    .FirstOrDefaultAsync(x => x.ProgramId == dto.ProgramId);

                if (entity == null)
                    return false;

                var universityId = entity.UniversityId;

                if (!await _context.KfUniversities
                    .AnyAsync(x => x.UniversityId == universityId && x.IsActive))
                {
                    throw new CustomException("The university assigned to this faculty is no longer available.");
                }


                if (!await _context.KfFaculties.AnyAsync(x =>
                     x.FacultyId == dto.FacultyId && 
                     x.UniversityId == universityId &&
                     x.IsActive))
                {
                    throw new CustomException("The selected faculty is not available for the selected university.");
                }

                if (dto.Courses?.Any() == true)
                {
                    var courseIds = dto.Courses
                       .Select(x => x.CourseId)
                       .Distinct()
                       .ToList();

                    // 1. Courses must belong to selected university and must be active

                    var validCourseIds = await _context.KfCourses
                        .Where(x =>
                            x.UniversityId == universityId &&
                            x.IsActive &&
                            courseIds.Contains(x.CourseId))
                        .Select(x => x.CourseId)
                        .ToListAsync();

                    if (validCourseIds.Count != courseIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected courses are not available for the selected university."
                        );
                    }

                    // 2. Courses must be mapped to selected faculty

                    var validFacultyCourseIds = await _context.KfCourseFaculties
                        .Where(x =>
                            x.FacultyId == dto.FacultyId &&
                            x.IsActive &&
                            courseIds.Contains(x.CourseId))
                        .Select(x => x.CourseId)
                        .Distinct()
                        .ToListAsync();

                    if (validFacultyCourseIds.Count != courseIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected courses are not mapped to the selected faculty."
                        );
                    }
                }

                if (await _context.KfPrograms.AnyAsync(x =>
                        x.ProgramCode.ToLower() == dto.ProgramCode.ToLower()
                        && x.UniversityId == universityId
                        && x.ProgramId != dto.ProgramId))
                {
                    throw new CustomException("Program with same code already exists");
                }

                if (dto.Documents?.Any() == true)
                {
                    var documentTypeIds = dto.Documents
                        .Select(x => x.DocumentTypeId)
                        .Distinct()
                        .ToList();

                    var validDocumentTypeIds = await _context.KfDocumentTypes
                        .Where(x =>
                            x.IsActive &&
                            documentTypeIds.Contains(x.DocumentTypeId))
                        .Select(x => x.DocumentTypeId)
                        .ToListAsync();

                    if (validDocumentTypeIds.Count != documentTypeIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected document types are no longer available."
                        );
                    }
                }

                if (dto.Costs?.Any() == true)
                {
                    var sponsorshipTypeIds = dto.Costs
                        .Select(x => x.SponsorshipTypeId)
                        .Distinct()
                        .ToList();

                    var validSponsorshipTypeIds = await _context.KfSponsorshipTypes
                        .Where(x =>
                            x.IsActive &&
                            sponsorshipTypeIds.Contains(x.SponsorshipTypeId))
                        .Select(x => x.SponsorshipTypeId)
                        .ToListAsync();

                    if (validSponsorshipTypeIds.Count != sponsorshipTypeIds.Count)
                    {
                        throw new CustomException(
                            "One or more selected sponsorship types are no longer available."
                        );
                    }
                }

                ValidateProgramRequest(dto);

                if (entity.AccreditationStatus == (byte)AccreditationStatusEnum.Pending)
                {
                    throw new CustomException("Program is under accreditation review");
                }

                if (entity.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited)
                {
                    throw new CustomException("Accredited program cannot be modified");
                }

                var semesterIdsByNumber = await EnsureProgramSemestersAsync(
                    entity,
                    dto.NumberOfSemesters,
                    dto.UpdatedBy ?? entity.CreatedBy);

                var oldStatus = entity.AccreditationStatus;
                var wasDraft = entity.IsDraft;

                // UniversityId should not be updated
                // entity.UniversityId = dto.UniversityId;

                entity.FacultyId = dto.FacultyId;
                entity.ProgramName = dto.ProgramName;
                entity.ProgramCode = dto.ProgramCode;
                entity.Degree = dto.Degree;
                entity.NumberOfSemesters = dto.NumberOfSemesters;
                entity.CreditsRequired = dto.CreditsRequired;
                entity.AllowedStudentSeats = dto.AllowedStudentSeats;
                entity.MinAcceptanceRate = dto.MinAcceptanceRate;
                entity.AllowedHighSchoolDivisions = dto.AllowedHighSchoolDivisions;
                entity.AllowedTanzanianCombinations = dto.AllowedTanzanianCombinations;

                entity.IsDraft = dto.IsDraft;

                // Workflow Logic

                // Draft -> Submit
                if (wasDraft && !dto.IsDraft)
                {
                    entity.AccreditationStatus = (byte)AccreditationStatusEnum.Pending;
                    entity.SubmittedDate = DateTime.UtcNow;
                    entity.CommitteeComment = null;
                }

                // Rejected -> Resubmit
                else if (oldStatus == (byte)AccreditationStatusEnum.Rejected && !dto.IsDraft)
                {
                    entity.AccreditationStatus = (byte)AccreditationStatusEnum.Pending;
                    entity.SubmittedDate = DateTime.UtcNow;
                    entity.CommitteeComment = null;
                }

                entity.UpdatedBy = dto.UpdatedBy; 
                entity.UpdatedDate = DateTime.UtcNow;

                // Documents
                var oldDocuments = _context.KfProgramDocuments.Where(x => x.ProgramId == entity.ProgramId);

                _context.KfProgramDocuments.RemoveRange(oldDocuments);

                if (dto.Documents?.Any() == true)
                {
                    var documents = dto.Documents
                        .Select(x => new KfProgramDocument
                        {
                            ProgramId = entity.ProgramId,
                            DocumentTypeId = x.DocumentTypeId,
                            IsRequired = x.IsRequired,
                            DisplayOrder = x.DisplayOrder
                        })
                        .ToList();

                    _context.KfProgramDocuments.AddRange(documents);
                }

                // Costs
                var oldCosts = _context.KfProgramCosts.Where(x => x.ProgramId == entity.ProgramId);

                _context.KfProgramCosts.RemoveRange(oldCosts);

                if (dto.Costs?.Any() == true)
                {
                    var costs = dto.Costs
                        .Select(x => new KfProgramCost
                        {
                            ProgramId = entity.ProgramId,
                            SponsorshipTypeId = x.SponsorshipTypeId,
                            Amount = x.Amount
                        })
                        .ToList();

                    _context.KfProgramCosts.AddRange(costs);
                }

                // Courses
                var oldCourses = _context.KfProgramCourses
                    .Where(x => x.ProgramId == entity.ProgramId);

                _context.KfProgramCourses.RemoveRange(oldCourses);

                if (dto.Courses?.Any() == true)
                {
                    var courses = dto.Courses
                        .Select(x => new KfProgramCourse
                        {
                            ProgramId = entity.ProgramId,
                            CourseId = x.CourseId,
                            CourseType = x.CourseType,
                            Credits = x.Credits,
                            DisplayOrder = x.DisplayOrder,
                            SemesterNo = x.SemesterNo,
                            ProgramSemesterId = semesterIdsByNumber[x.SemesterNo]
                        })
                        .ToList();

                    _context.KfProgramCourses.AddRange(courses);
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------- DELETE ----------------
        public async Task<bool> DeleteAsync(long id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var entity = await _context.KfPrograms
                    .FirstOrDefaultAsync(x => x.ProgramId == id);

                if (entity == null)
                    return false;

                if (!entity.IsActive)
                    return false;

                if (entity.AccreditationStatus == (byte)AccreditationStatusEnum.Pending)
                {
                    throw new CustomException(
                        "A program under accreditation review cannot be deleted."
                    );
                }

                if (entity.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited)
                {
                    throw new CustomException(
                        "An accredited program cannot be deleted."
                    );
                }

                var hasAcademicHistory = await _context.KfStudentAcademicRegistrations
                    .AnyAsync(x => x.ProgramId == id);
                var hasCourseEnrollments = await _context.KfProgramCourses
                    .AnyAsync(x => x.ProgramId == id && x.KfStudentCourseEnrollments.Any());
                var hasStudentDocumentReferences = await _context.KfProgramDocuments
                    .AnyAsync(x => x.ProgramId == id && x.KfStudentProgramDocuments.Any());

                // Keep the semester schedule permanently. Retain its supporting
                // curriculum records too when academic or document history depends on them.
                if (!hasAcademicHistory && !hasCourseEnrollments && !hasStudentDocumentReferences)
                {
                    var documents = await _context.KfProgramDocuments
                        .Where(x => x.ProgramId == id)
                        .ToListAsync();
                    _context.KfProgramDocuments.RemoveRange(documents);

                    var costs = await _context.KfProgramCosts
                        .Where(x => x.ProgramId == id)
                        .ToListAsync();
                    _context.KfProgramCosts.RemoveRange(costs);

                    var courses = await _context.KfProgramCourses
                        .Where(x => x.ProgramId == id)
                        .ToListAsync();
                    _context.KfProgramCourses.RemoveRange(courses);
                }

                // Soft delete program
                entity.IsActive = false;
                entity.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------- GET BY ID ----------------
        public async Task<ProgramRequestDto?> GetByIdAsync(long id,LoggedInUserDto currentUser)
        {
            var query = _context.KfPrograms
                .AsNoTracking()
                .Where(x => x.ProgramId == id && x.IsActive);

            // Role-based visibility
            if (currentUser.StaffType == StaffType.University)
            {
                // University users can see all active programs
                // but only for their assigned universities
                query = query.Where(x => currentUser.UniversityIds.Contains(x.UniversityId));
            }
            else if (currentUser.StaffType == StaffType.Ngo)
            {
                // NGO can see all active submitted programs
                // Pending + Accredited + Rejected
                query = query.Where(x => !x.IsDraft);
            }
            else
            {
                // Other users can see only active + accredited programs
                query = query.Where(x => x.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited);
            }

            var program = await query
                .Select(x => new ProgramRequestDto
                {
                    ProgramId = x.ProgramId,

                    UniversityId = x.UniversityId,
                    UniversityName = x.University.UniversityName,

                    FacultyId = x.FacultyId,
                    FacultyName = x.Faculty.FacultyName,
                    FacultyCode = x.Faculty.FacultyCode,

                    ProgramName = x.ProgramName,
                    ProgramCode = x.ProgramCode,
                    Degree = x.Degree,

                    NumberOfSemesters = x.NumberOfSemesters,
                    CreditsRequired = x.CreditsRequired,
                    AllowedStudentSeats = x.AllowedStudentSeats,
                    MinAcceptanceRate = x.MinAcceptanceRate,
                    AllowedHighSchoolDivisions = x.AllowedHighSchoolDivisions,
                    AllowedTanzanianCombinations = x.AllowedTanzanianCombinations,

                    IsDraft = x.IsDraft,
                    AccreditationStatus = (byte)x.AccreditationStatus,
                    CommitteeComment = x.CommitteeComment,
                    SubmittedDate = x.SubmittedDate,

                    IsActive = x.IsActive,

                    CreatedDate = x.CreatedDate,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByNavigation.LoginName,

                    UpdatedDate = x.UpdatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedByName = x.UpdatedByNavigation != null
                        ? x.UpdatedByNavigation.LoginName
                        : null,

                    Documents = x.KfProgramDocuments
                        .OrderBy(d => d.DisplayOrder)
                        .Select(d => new ProgramDocumentDto
                        {
                            ProgramDocumentId = d.ProgramDocumentId,
                            DocumentTypeId = d.DocumentTypeId,
                            DocumentTypeName = d.DocumentType.DocumentName,
                            IsRequired = d.IsRequired,
                            DisplayOrder = d.DisplayOrder
                        })
                        .ToList(),

                    Costs = x.KfProgramCosts
                        .Select(c => new ProgramCostDto
                        {
                            ProgramCostId = c.ProgramCostId,
                            SponsorshipTypeId = c.SponsorshipTypeId,
                            SponsorshipTypeName = c.SponsorshipType.SponsorshipName,
                            FrequencyTypeId = c.SponsorshipType.FrequencyType,
                            Amount = c.Amount
                        })
                        .ToList(),

                    Courses = x.KfProgramCourses
                        .OrderBy(c => c.SemesterNo)
                        .ThenBy(c => c.DisplayOrder)
                        .Select(c => new ProgramCourseDto
                        {
                            ProgramCourseId = c.ProgramCourseId,
                            CourseId = c.CourseId,
                            CourseCode = c.Course.CourseCode,
                            CourseNameEn = c.Course.CourseNameEn,
                            CourseNameAr = c.Course.CourseNameAr,

                            CourseType = c.CourseType,
                            Credits = c.Credits,
                            DisplayOrder = c.DisplayOrder,
                            SemesterNo = c.SemesterNo,
                            SemesterName = c.ProgramSemester != null && c.ProgramSemester.SemesterName != null
                                ? c.ProgramSemester.SemesterName
                                : $"Semester {c.SemesterNo}"
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (program != null)
            {
                var semesters = await _context.KfProgramSemesters
                    .AsNoTracking()
                    .Where(x => x.ProgramId == id)
                    .Include(x => x.AcademicTerm)
                        .ThenInclude(x => x!.AcademicCalendar)
                    .OrderBy(x => x.SemesterNo)
                    .ToListAsync();

                program.Semesters = semesters.Select(MapProgramSemester).ToList();
            }

            return program;
        }


        // ---------------- GET ALL FILTER ----------------
        public async Task<PagedResultDto<ProgramRequestDto>> GetByFilterAsync(ProgramFilterDto filter, LoggedInUserDto currentUser)
        {
            var query = _context.KfPrograms
                .AsNoTracking()
                .Where(x => x.IsActive)
                .AsQueryable();

            if (currentUser.StaffType == StaffType.University)
            {
                // All active programs
                // Draft + Pending + Accredited + Rejected

                // Only programs belonging to currentUser.UniversityIds,
                query = query.Where(x => currentUser.UniversityIds.Contains(x.UniversityId));

            }
            else if (currentUser.StaffType == StaffType.Ngo)
            {
                // Active + submitted
                // Pending + Accredited + Rejected
                query = query.Where(x => !x.IsDraft);

            }
            else
            {
                // Only active + accredited
                query = query.Where(x => x.AccreditationStatus ==  (byte)AccreditationStatusEnum.Accredited);
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
            {
                query = query.Where(x => x.UniversityId == filter.UniversityId.Value);
            }

            if (filter.FacultyId.HasValue)
            {
                query = query.Where(x => x.FacultyId == filter.FacultyId.Value);
            }


            /* Global Search */
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();
                query = query.Where(x =>
                    x.ProgramName.ToLower().Contains(search) ||
                    x.ProgramCode.ToLower().Contains(search)
                );
            }


            // ---------- Total Count (before pagination) ----------
            var totalCount = await query.CountAsync();

            // ---------- Ordering ----------
            query = query.OrderByDescending(x => x.ProgramId);

            // ---------- Pagination rule ----------
            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new ProgramRequestDto
                {
                    ProgramId = x.ProgramId,

                    UniversityId = x.UniversityId,
                    UniversityName = x.University.UniversityName,

                    FacultyId = x.FacultyId,
                    FacultyName = x.Faculty.FacultyName,
                    FacultyCode = x.Faculty.FacultyCode,

                    ProgramName = x.ProgramName,
                    ProgramCode = x.ProgramCode,
                    Degree = x.Degree,

                    NumberOfSemesters = x.NumberOfSemesters,
                    CreditsRequired = x.CreditsRequired,
                    AllowedStudentSeats = x.AllowedStudentSeats,

                    IsDraft = x.IsDraft,
                    AccreditationStatus = x.AccreditationStatus,
                    SubmittedDate = x.SubmittedDate,

                    IsActive = x.IsActive,

                    CreatedDate = x.CreatedDate,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByNavigation.LoginName,

                    UpdatedDate = x.UpdatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedByName = x.UpdatedByNavigation != null ? x.UpdatedByNavigation.LoginName : null
                })
                .ToListAsync();

            if (items.Count > 0)
            {
                var programIds = items.Select(x => x.ProgramId!.Value).ToList();
                var programSemesters = await _context.KfProgramSemesters
                    .AsNoTracking()
                    .Where(x => programIds.Contains(x.ProgramId))
                    .Include(x => x.AcademicTerm)
                        .ThenInclude(x => x!.AcademicCalendar)
                    .OrderBy(x => x.SemesterNo)
                    .ToListAsync();

                var semestersByProgram = programSemesters
                    .GroupBy(x => x.ProgramId)
                    .ToDictionary(x => x.Key, x => x.Select(MapProgramSemester).ToList());

                foreach (var item in items)
                {
                    if (item.ProgramId.HasValue && semestersByProgram.TryGetValue(item.ProgramId.Value, out var semesters))
                        item.Semesters = semesters;
                    else
                        item.Semesters = new List<ProgramSemesterDto>();
                }
            }

            return new PagedResultDto<ProgramRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        // ---------------- GET All Semesters ----------------
        public async Task<List<ProgramSemesterDto>> GetSemestersAsync(long programId, LoggedInUserDto currentUser)
        {
            var query = _context.KfPrograms
                .AsNoTracking()
                .Where(x => x.ProgramId == programId && x.IsActive);

            // Role-based visibility
            if (currentUser.StaffType == StaffType.University)
            {
                // University users can access programs
                // belonging to their assigned universities
                query = query.Where(x => currentUser.UniversityIds.Contains(x.UniversityId));
            }
            else if (currentUser.StaffType == StaffType.Ngo)
            {
                // NGO can access submitted programs
                // Pending + Accredited + Rejected
                query = query.Where(x => !x.IsDraft);
            }
            else
            {
                // Other users can access only accredited programs
                query = query.Where(x => x.AccreditationStatus ==  (byte)AccreditationStatusEnum.Accredited);
            }

            var program = await query
                .Select(x => new { x.ProgramId, x.UniversityId, x.NumberOfSemesters })
                .FirstOrDefaultAsync();

            if (program == null)
            {
                throw new CustomException("Program not found");
            }

            var semesters = await _context.KfProgramSemesters
                .AsNoTracking()
                .Where(x => x.ProgramId == program.ProgramId)
                .Include(x => x.AcademicTerm)
                    .ThenInclude(x => x!.AcademicCalendar)
                .OrderBy(x => x.SemesterNo)
                .ToListAsync();

            return semesters.Select(MapProgramSemester).ToList();
        }

        public async Task<List<ProgramSemesterDto>> GetSemesterDefaultsAsync(
            long programId,
            long academicCalendarId,
            LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University || !currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("University access is required to generate program semester dates.");

            var program = await _context.KfPrograms
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProgramId == programId && x.IsActive &&
                    currentUser.UniversityIds.Contains(x.UniversityId));

            if (program == null)
                throw new CustomException("Program not found.");

            return await GenerateProgramSemesterDefaultsAsync(
                program.ProgramId,
                program.UniversityId,
                program.NumberOfSemesters,
                academicCalendarId);
        }


        public async Task<bool> SaveSemestersAsync(
            long programId,
            List<ProgramSemesterDto> requestedSemesters,
            LoggedInUserDto currentUser,
            long? academicCalendarId = null)
        {
            if (currentUser.StaffType != StaffType.University || !currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("University access is required.");

            if (requestedSemesters == null)
                throw new CustomException("Program semester details are required.");

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var program = await _context.KfPrograms.FirstOrDefaultAsync(x =>
                    x.ProgramId == programId && currentUser.UniversityIds.Contains(x.UniversityId));

                if (program == null)
                    throw new CustomException("Program not found.");

                if (program.AccreditationStatus == (byte)AccreditationStatusEnum.Pending)
                    throw new CustomException("Program is under accreditation review.");

                if (program.AccreditationStatus == (byte)AccreditationStatusEnum.Accredited)
                    throw new CustomException("Accredited program cannot be modified.");

                if (program.NumberOfSemesters <= 0 || requestedSemesters.Count != program.NumberOfSemesters)
                    throw new CustomException("The schedule must include every semester configured for this program.");

                var generatedDefaults = academicCalendarId.HasValue
                    ? await GenerateProgramSemesterDefaultsAsync(
                        program.ProgramId,
                        program.UniversityId,
                        program.NumberOfSemesters,
                        academicCalendarId.Value)
                    : new List<ProgramSemesterDto>();
                var defaultsByNumber = generatedDefaults.ToDictionary(x => x.SemesterNo);

                var duplicateSemesterNumbers = requestedSemesters
                    .GroupBy(x => x.SemesterNo)
                    .Any(x => x.Count() > 1);
                if (duplicateSemesterNumbers || requestedSemesters.Any(x => x.SemesterNo < 1 || x.SemesterNo > program.NumberOfSemesters))
                    throw new CustomException("Each configured semester must be included exactly once.");

                var semesterIdsByNumber = await EnsureProgramSemestersAsync(
                    program,
                    program.NumberOfSemesters,
                    currentUser.LoginId);

                if (semesterIdsByNumber.Count < program.NumberOfSemesters)
                    throw new CustomException("The program semester schedule could not be prepared.");

                var persistedSemesters = await _context.KfProgramSemesters
                    .Where(x => x.ProgramId == programId)
                    .ToListAsync();
                var persistedByNumber = persistedSemesters.ToDictionary(x => x.SemesterNo);

                foreach (var request in requestedSemesters)
                {
                    if (!persistedByNumber.TryGetValue(request.SemesterNo, out var semester))
                        throw new CustomException($"Program semester {request.SemesterNo} was not found.");

                    var expectedId = semesterIdsByNumber[request.SemesterNo];
                    if (request.ProgramSemesterId > 0 && request.ProgramSemesterId != expectedId)
                        throw new CustomException("A submitted program semester does not match this program.");

                    if (!request.StartDate.HasValue || !request.EndDate.HasValue ||
                        !request.RegistrationStartDate.HasValue || !request.RegistrationEndDate.HasValue)
                        throw new CustomException($"All four dates are required for program semester {request.SemesterNo}.");

                    if (request.StartDate.Value == DateTime.MinValue || request.EndDate.Value == DateTime.MinValue ||
                        request.RegistrationStartDate.Value == DateTime.MinValue ||
                        request.RegistrationEndDate.Value == DateTime.MinValue)
                        throw new CustomException($"All four dates must be valid for program semester {request.SemesterNo}.");

                    if (request.StartDate > request.EndDate)
                        throw new CustomException($"Program semester {request.SemesterNo} start date must be on or before its end date.");

                    if (request.RegistrationStartDate > request.RegistrationEndDate)
                        throw new CustomException($"Program semester {request.SemesterNo} registration start date must be on or before its end date.");

                    semester.StartDate = request.StartDate.Value;
                    semester.EndDate = request.EndDate.Value;
                    semester.RegistrationStartDate = request.RegistrationStartDate.Value;
                    semester.RegistrationEndDate = request.RegistrationEndDate.Value;
                    if (defaultsByNumber.TryGetValue(request.SemesterNo, out var defaults))
                    {
                        semester.SemesterName = defaults.SemesterName;
                        if (!semester.AcademicTermId.HasValue && defaults.AcademicTermId.HasValue)
                            semester.AcademicTermId = defaults.AcademicTermId;
                    }
                    semester.UpdatedBy = currentUser.LoginId;
                    semester.UpdatedOn = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }



        // ---------------- PRIVATE get university academic terms ----------------
        private async Task<List<KfUniversityAcademicTerm>> GetUniversityAcademicTermsAsync(long universityId)
        {
            return await _context.KfUniversityAcademicTerms
                .AsNoTracking()
                .Where(x => x.AcademicCalendar.UniversityId == universityId)
                .OrderBy(x => x.AcademicCalendar.PeriodStartDate)
                .ThenBy(x => x.AcademicCalendar.PeriodEndDate)
                .ThenBy(x => x.TermSequence)
                .ThenBy(x => x.AcademicTermId)
                .ToListAsync();
        }


        // ---------------- PRIVATE ensure program semesters in update ----------------
        private async Task<Dictionary<int, long>> EnsureProgramSemestersAsync(
            KfProgram program,
            int numberOfSemesters,
            long actorId)
        {
            if (numberOfSemesters <= 0)
                throw new CustomException("Number of semesters must be greater than 0.");

            var terms = await GetUniversityAcademicTermsAsync(program.UniversityId);

            var termOrder = terms
                .Select((term, index) => new { term.AcademicTermId, Index = index })
                .ToDictionary(x => x.AcademicTermId, x => x.Index);
            var semesters = await _context.KfProgramSemesters
                .Where(x => x.ProgramId == program.ProgramId)
                .OrderBy(x => x.SemesterNo)
                .ToListAsync();

            if (semesters.GroupBy(x => x.SemesterNo).Any(x => x.Count() > 1))
                throw new CustomException("Duplicate program semester records were found.");

            if (semesters.Any(x => x.SemesterNo < 1))
                throw new CustomException("A program semester has an invalid semester number.");

            var usedTerms = new HashSet<long>();
            var priorMappedTermIndex = -1;
            foreach (var semester in semesters.Where(x => x.AcademicTermId.HasValue))
            {
                var academicTermId = semester.AcademicTermId!.Value;
                if (!termOrder.TryGetValue(academicTermId, out var index))
                {
                    throw new CustomException(
                        $"Program semester {semester.SemesterNo} is mapped to an academic term that does not belong to this program's university.");
                }

                if (!usedTerms.Add(academicTermId))
                    throw new CustomException("An academic term is mapped to more than one program semester.");

                if (index <= priorMappedTermIndex)
                {
                    throw new CustomException(
                        "Existing program semester mappings are not in chronological order and cannot be changed automatically.");
                }

                priorMappedTermIndex = index;
            }

            var semestersByNumber = semesters.ToDictionary(x => x.SemesterNo);
            var newSemesters = new List<KfProgramSemester>();

            for (var semesterNo = 1; semesterNo <= numberOfSemesters; semesterNo++)
            {
                if (!semestersByNumber.TryGetValue(semesterNo, out var semester))
                {
                    semester = new KfProgramSemester
                    {
                        ProgramId = program.ProgramId,
                        SemesterNo = semesterNo,
                        SemesterName = $"Semester {semesterNo}",
                        CreatedBy = actorId,
                        CreatedOn = DateTime.UtcNow
                    };
                    newSemesters.Add(semester);
                    semestersByNumber[semesterNo] = semester;
                }
            }

            if (newSemesters.Count > 0)
                _context.KfProgramSemesters.AddRange(newSemesters);

            await _context.SaveChangesAsync();

            return semestersByNumber.ToDictionary(x => x.Key, x => x.Value.ProgramSemesterId);
        }

        private async Task<List<ProgramSemesterDto>> GenerateProgramSemesterDefaultsAsync(
            long programId,
            long universityId,
            int numberOfSemesters,
            long academicCalendarId)
        {
            if (numberOfSemesters <= 0)
                throw new CustomException("Number of semesters must be greater than 0.");

            var calendar = await _context.KfUniversityAcademicCalendars
                .AsNoTracking()
                .Include(x => x.KfUniversityAcademicTerms)
                .FirstOrDefaultAsync(x => x.AcademicCalendarId == academicCalendarId && x.UniversityId == universityId);

            if (calendar == null)
                throw new CustomException("The selected academic calendar was not found for this university.");
            if (calendar.TermsPerYear is not (2 or 3 or 4))
                throw new CustomException("The selected academic calendar must have 2, 3, or 4 terms per year.");
            if (calendar.PeriodStartDate == DateOnly.MinValue || calendar.PeriodEndDate == DateOnly.MinValue ||
                calendar.PeriodStartDate > calendar.PeriodEndDate)
                throw new CustomException("The selected academic calendar has an invalid academic period.");
            if (calendar.AcademicYearStartMonth is < 1 or > 12 || calendar.AcademicYearEndMonth is < 1 or > 12 ||
                calendar.PeriodStartDate.Month != calendar.AcademicYearStartMonth ||
                calendar.PeriodEndDate.Month != calendar.AcademicYearEndMonth)
                throw new CustomException("The selected academic calendar period dates must match its configured start and end months.");

            var configuredTerms = calendar.KfUniversityAcademicTerms.OrderBy(x => x.TermSequence).ToList();
            if (configuredTerms.Count != calendar.TermsPerYear ||
                configuredTerms.Select(x => x.TermSequence).Distinct().Count() != calendar.TermsPerYear ||
                configuredTerms.Where((term, index) => term.TermSequence != index + 1).Any() ||
                configuredTerms.Any(x => string.IsNullOrWhiteSpace(x.TermName)))
                throw new CustomException("The selected academic calendar does not have a complete ordered term configuration.");

            var existingSemesterRows = await _context.KfProgramSemesters
                .AsNoTracking()
                .Include(x => x.AcademicTerm)
                    .ThenInclude(x => x!.AcademicCalendar)
                .Where(x => x.ProgramId == programId)
                .ToListAsync();
            if (existingSemesterRows.GroupBy(x => x.SemesterNo).Any(x => x.Count() > 1))
                throw new CustomException("Duplicate program semester records were found.");
            var existingSemesters = existingSemesterRows.ToDictionary(x => x.SemesterNo);
            var universityTerms = await GetUniversityAcademicTermsAsync(universityId);
            var termOrder = universityTerms
                .Select((term, index) => new { term.AcademicTermId, Index = index })
                .ToDictionary(x => x.AcademicTermId, x => x.Index);
            var usedTermIds = new HashSet<long>();
            var lastMappedTermIndex = -1;

            foreach (var existing in existingSemesterRows.Where(x => x.AcademicTermId.HasValue).OrderBy(x => x.SemesterNo))
            {
                if (!termOrder.TryGetValue(existing.AcademicTermId!.Value, out var index))
                    throw new CustomException($"Program semester {existing.SemesterNo} is linked to a calendar outside this university.");
                if (!usedTermIds.Add(existing.AcademicTermId.Value))
                    throw new CustomException("An academic term is mapped to more than one program semester.");
                if (index <= lastMappedTermIndex)
                    throw new CustomException("Existing program semester mappings are not in chronological order.");
                lastMappedTermIndex = index;
            }
            lastMappedTermIndex = -1;

            var otherCalendars = await _context.KfUniversityAcademicCalendars
                .AsNoTracking()
                .Include(x => x.KfUniversityAcademicTerms)
                .Where(x => x.UniversityId == universityId && x.AcademicCalendarId != academicCalendarId)
                .OrderBy(x => x.PeriodStartDate)
                .ThenBy(x => x.AcademicCalendarId)
                .ToListAsync();

            var result = new List<ProgramSemesterDto>(numberOfSemesters);
            var termsPerYear = calendar.TermsPerYear;

            for (var semesterNo = 1; semesterNo <= numberOfSemesters; semesterNo++)
            {
                var cycle = (semesterNo - 1) / termsPerYear;
                var termSequence = (byte)(((semesterNo - 1) % termsPerYear) + 1);
                DateOnly periodStart;
                DateOnly periodEnd;
                DateOnly startDate;
                DateOnly endDate;
                DateTime registrationStart;
                DateTime registrationEnd;
                try
                {
                    periodStart = calendar.PeriodStartDate.AddYears(cycle);
                    periodEnd = calendar.PeriodEndDate.AddYears(cycle);
                    if (cycle > 0 && periodStart <= calendar.PeriodEndDate.AddYears(cycle - 1))
                        throw new CustomException("The selected academic calendar periods would overlap when repeated across program years.");
                    var periodLength = periodEnd.DayNumber - periodStart.DayNumber + 1;
                    if (periodLength < termsPerYear)
                        throw new CustomException("The academic calendar period is too short to create non-empty terms.");

                    var termStartOffset = (termSequence - 1) * periodLength / termsPerYear;
                    var termEndOffsetExclusive = termSequence * periodLength / termsPerYear;
                    startDate = periodStart.AddDays(termStartOffset);
                    endDate = periodStart.AddDays(termEndOffsetExclusive - 1);
                    registrationStart = startDate.ToDateTime(TimeOnly.MinValue).AddMonths(-1);
                    registrationEnd = startDate.ToDateTime(TimeOnly.MinValue).AddDays(-1);
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new CustomException("The selected academic calendar cannot cover the configured program duration.");
                }

                var sourceTerm = configuredTerms[termSequence - 1];

                KfUniversityAcademicCalendar? occurrenceCalendar = null;
                if (cycle == 0)
                {
                    occurrenceCalendar = calendar;
                }
                else
                {
                    occurrenceCalendar = otherCalendars.FirstOrDefault(x =>
                        x.PeriodStartDate == periodStart &&
                        x.PeriodEndDate == periodEnd &&
                        x.TermsPerYear == termsPerYear);
                }

                var occurrenceTerm = occurrenceCalendar?.KfUniversityAcademicTerms
                    .FirstOrDefault(x => x.TermSequence == termSequence &&
                        string.Equals(x.TermName.Trim(), sourceTerm.TermName.Trim(), StringComparison.OrdinalIgnoreCase));
                existingSemesters.TryGetValue(semesterNo, out var existing);

                if (existing?.AcademicTerm != null && existing.AcademicTerm.AcademicCalendar.UniversityId != universityId)
                    throw new CustomException($"Program semester {semesterNo} is linked to a calendar outside this university.");

                long? generatedTermId = existing?.AcademicTermId;
                if (generatedTermId.HasValue)
                    lastMappedTermIndex = termOrder[generatedTermId.Value];
                if (!generatedTermId.HasValue && occurrenceTerm != null &&
                    termOrder.TryGetValue(occurrenceTerm.AcademicTermId, out var candidateTermIndex) &&
                    candidateTermIndex > lastMappedTermIndex && !usedTermIds.Contains(occurrenceTerm.AcademicTermId))
                {
                    var nextMappedTermIndex = existingSemesterRows
                        .Where(x => x.SemesterNo > semesterNo && x.AcademicTermId.HasValue)
                        .Select(x => termOrder[x.AcademicTermId!.Value])
                        .DefaultIfEmpty(int.MaxValue)
                        .Min();

                    if (candidateTermIndex < nextMappedTermIndex)
                    {
                        generatedTermId = occurrenceTerm.AcademicTermId;
                        usedTermIds.Add(occurrenceTerm.AcademicTermId);
                        lastMappedTermIndex = candidateTermIndex;
                    }
                }

                var defaultName = string.Equals(calendar.NamingStyle, "Custom", StringComparison.OrdinalIgnoreCase)
                    ? $"{sourceTerm.TermName.Trim()} {startDate.Year}"
                    : $"Semester {semesterNo}";

                result.Add(new ProgramSemesterDto
                {
                    ProgramSemesterId = existing?.ProgramSemesterId ?? 0,
                    SemesterNo = semesterNo,
                    SemesterName = defaultName,
                    AcademicTermId = existing?.AcademicTermId ?? generatedTermId,
                    AcademicCalendarId = existing?.AcademicTerm?.AcademicCalendarId ?? (generatedTermId.HasValue ? occurrenceCalendar?.AcademicCalendarId : null),
                    AcademicYearName = existing?.AcademicTerm?.AcademicCalendar.AcademicYearName ??
                        occurrenceCalendar?.AcademicYearName ?? $"{periodStart.Year}-{periodEnd.Year}",
                    TermSequence = existing?.AcademicTerm?.TermSequence ?? occurrenceTerm?.TermSequence ?? termSequence,
                    TermName = existing?.AcademicTerm?.TermName ?? sourceTerm.TermName,
                    StartDate = existing?.StartDate ?? startDate.ToDateTime(TimeOnly.MinValue),
                    EndDate = existing?.EndDate ?? endDate.ToDateTime(TimeOnly.MinValue),
                    RegistrationStartDate = existing?.RegistrationStartDate ?? registrationStart,
                    RegistrationEndDate = existing?.RegistrationEndDate ?? registrationEnd
                });
            }

            return result;
        }


        // ---------------- PRIVATE create program semester in create ----------------
        private static KfProgramSemester CreateProgramSemester(
            long programId,
            int semesterNo,
            long actorId)
        {
            var semester = new KfProgramSemester
            {
                ProgramId = programId,
                SemesterNo = semesterNo,
                SemesterName = $"Semester {semesterNo}",
                CreatedBy = actorId,
                CreatedOn = DateTime.UtcNow
            };

            return semester;
        }


        // ---------------- PRIVATE map program semester ----------------
        private static ProgramSemesterDto MapProgramSemester(KfProgramSemester semester)
        {
            var term = semester.AcademicTerm;
            return new ProgramSemesterDto
            {
                ProgramSemesterId = semester.ProgramSemesterId,
                SemesterNo = semester.SemesterNo,
                SemesterName = semester.SemesterName ?? $"Semester {semester.SemesterNo}",
                AcademicTermId = semester.AcademicTermId,
                AcademicCalendarId = term?.AcademicCalendarId,
                AcademicYearName = term?.AcademicCalendar.AcademicYearName,
                TermSequence = term?.TermSequence,
                TermName = term?.TermName,
                StartDate = semester.StartDate,
                EndDate = semester.EndDate,
                RegistrationStartDate = semester.RegistrationStartDate,
                RegistrationEndDate = semester.RegistrationEndDate
            };
        }


        // ---------------- PRIVATE validate program request ----------------
        private void ValidateProgramRequest(ProgramRequestDto dto)
        {
            // ---------------- Course Validations ----------------

            if (dto.Courses?.Any() == true)
            {
                // Semester range validation
                var invalidSemester = dto.Courses.Any(x =>
                    x.SemesterNo < 1 ||
                    x.SemesterNo > dto.NumberOfSemesters);

                if (invalidSemester)
                {
                    throw new CustomException(
                        $"Semester number must be between 1 and {dto.NumberOfSemesters}");
                }

                // Duplicate course validation
                var duplicateCourse = dto.Courses
                    .GroupBy(x => x.CourseId)
                    .Any(g => g.Count() > 1);

                if (duplicateCourse)
                {
                    throw new CustomException(
                        "Duplicate courses are not allowed");
                }

                // Duplicate display order validation
                var duplicateDisplayOrder = dto.Courses
                    .GroupBy(x => new
                    {
                        x.SemesterNo,
                        x.DisplayOrder
                    })
                    .Any(g => g.Count() > 1);

                if (duplicateDisplayOrder)
                {
                    throw new CustomException(
                        "Duplicate display order found within a semester");
                }
            }

            // ---------------- Submit Validation ----------------

            if (!dto.IsDraft)
            {
                if (dto.Courses == null || !dto.Courses.Any())
                {
                    throw new CustomException(
                        "At least one course is required before submission");
                }

                var totalCredits = dto.Courses.Sum(x => x.Credits);

                if (totalCredits != dto.CreditsRequired)
                {
                    throw new CustomException(
                        $"Total course credits ({totalCredits}) must equal Credits Required ({dto.CreditsRequired})");
                }

                for (int semester = 1;
                     semester <= dto.NumberOfSemesters;
                     semester++)
                {
                    if (!dto.Courses.Any(x => x.SemesterNo == semester))
                    {
                        throw new CustomException(
                            $"Semester {semester} has no courses assigned");
                    }
                }
            }
        }



    }
}
