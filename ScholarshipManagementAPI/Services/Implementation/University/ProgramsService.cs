using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
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
                            SemesterNo = x.SemesterNo
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

            await using var transaction = await _context.Database.BeginTransactionAsync();

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
                            SemesterNo = x.SemesterNo
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

                // Delete program documents
                var documents = await _context.KfProgramDocuments
                    .Where(x => x.ProgramId == id)
                    .ToListAsync();

                _context.KfProgramDocuments.RemoveRange(documents);

                // Delete program costs
                var costs = await _context.KfProgramCosts
                    .Where(x => x.ProgramId == id)
                    .ToListAsync();

                _context.KfProgramCosts.RemoveRange(costs);

                // Delete program courses
                var courses = await _context.KfProgramCourses
                    .Where(x => x.ProgramId == id)
                    .ToListAsync();

                _context.KfProgramCourses.RemoveRange(courses);

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

            return await query
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
                            SemesterName = $"Semester {c.SemesterNo}"
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();
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

            return new PagedResultDto<ProgramRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }


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
                .Select(x => new
                {
                    x.NumberOfSemesters
                })
                .FirstOrDefaultAsync();

            if (program == null)
            {
                throw new CustomException("Program not found");
            }

            var semesters = new List<ProgramSemesterDto>();

            for (int i = 1; i <= program.NumberOfSemesters; i++)
            {
                semesters.Add(new ProgramSemesterDto
                {
                    SemesterNo = i,
                    SemesterName = $"Semester {i}"
                });
            }

            return semesters;
        }



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
