using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.Courses;
using ScholarshipManagementAPI.DTOs.University.Faculties;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Services.Implementation.University
{
    public class CoursesService : ICoursesService
    {
        private readonly AppDbContext _context;

        public CoursesService(AppDbContext context)
        {
            _context = context;
        }


        // ---------------- CREATE ----------------
        public async Task<long> CreateAsync(CourseRequestDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (await _context.KfCourses
                    .AnyAsync(x => x.CourseNameEn.ToLower() == dto.CourseNameEn.ToLower() 
                    && x.UniversityId == dto.UniversityId))
                {
                    throw new CustomException("Course with same name already exists");
                }

                // Validate faculties belong to selected university
                if (dto.FacultyIds != null && dto.FacultyIds.Any())
                {
                    var validFacultyIds = await _context.KfFaculties
                        .Where(x =>
                            x.UniversityId == dto.UniversityId &&
                            x.IsActive &&
                            dto.FacultyIds.Contains(x.FacultyId))
                        .Select(x => x.FacultyId)
                        .ToListAsync();

                    if (validFacultyIds.Count != dto.FacultyIds.Distinct().Count())
                    {
                        throw new CustomException("One or more selected faculties are not available for this university.");
                    }
                }

                var entity = new KfCourse
                {
                    UniversityId = dto.UniversityId,
                    CourseCode = dto.CourseCode,
                    CourseNameEn = dto.CourseNameEn,
                    CourseNameAr = dto.CourseNameAr,
                    IsActive = true,

                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = dto.CreatedBy,

                    UpdatedBy = null,
                    UpdatedDate = null
                };

                _context.KfCourses.Add(entity);

                await _context.SaveChangesAsync();

                var targetFacultyIds = dto.FacultyIds?.Distinct().ToList() ?? new List<long>();
                if (targetFacultyIds.Count == 0 && dto.Faculties?.Any() == true)
                {
                    targetFacultyIds = dto.Faculties.Select(f => f.FacultyId).Distinct().ToList();
                }

                var facultyOverrides = dto.Faculties?
                    .Where(f => f.IsActive.HasValue)
                    .ToDictionary(f => f.FacultyId, f => f.IsActive!.Value)
                    ?? new Dictionary<long, bool>();

                if (targetFacultyIds.Any())
                {
                    var courseFaculties = targetFacultyIds
                        .Select(facultyId => new KfCourseFaculty
                        {
                            CourseId = entity.CourseId,
                            FacultyId = facultyId,
                            IsActive = facultyOverrides.TryGetValue(facultyId, out var explicitActive) ? explicitActive : true,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = entity.CreatedBy,
                            UpdatedBy = null,
                            UpdatedDate = null
                        })
                        .ToList();

                    _context.KfCourseFaculties.AddRange(courseFaculties);

                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return entity.CourseId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        // ---------------- UPDATE ----------------
        public async Task<bool> UpdateAsync(CourseRequestDto dto)
        {
            if (dto.CourseId == null || dto.CourseId == 0)
                return false;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var entity = await _context.KfCourses
                    .FirstOrDefaultAsync(x => x.CourseId == dto.CourseId);

                if (entity == null)
                    return false;

                var universityId = entity.UniversityId; // Ensure UniversityId is not changed

                if (await _context.KfCourses.AnyAsync(x =>
                    x.CourseNameEn.ToLower() == dto.CourseNameEn.ToLower()
                    && x.CourseId != dto.CourseId
                    && x.UniversityId == universityId))
                {
                    throw new CustomException("Course with same name already exists");
                }

                // Validate faculties belong to selected university
                if (dto.FacultyIds != null && dto.FacultyIds.Any())
                {
                    var validFacultyIds = await _context.KfFaculties
                        .Where(x =>
                            x.UniversityId == universityId && 
                            x.IsActive &&
                            dto.FacultyIds.Contains(x.FacultyId))
                        .Select(x => x.FacultyId)
                        .ToListAsync();

                    if (validFacultyIds.Count != dto.FacultyIds.Distinct().Count())
                    {
                        throw new CustomException("One or more selected faculties are not available for this university.");
                    }
                }


                // note: UniversityId is not updated here to maintain data integrity.
                // entity.UniversityId = dto.UniversityId;

                entity.CourseCode = dto.CourseCode;
                entity.CourseNameEn = dto.CourseNameEn;
                entity.CourseNameAr = dto.CourseNameAr;

                entity.UpdatedDate = DateTime.UtcNow;
                entity.UpdatedBy = dto.UpdatedBy;

                await _context.SaveChangesAsync();

                // Sync course-faculty mappings preserving active states
                var existingMappings = await _context.KfCourseFaculties
                    .Where(x => x.CourseId == entity.CourseId)
                    .ToListAsync();

                var targetFacultyIds = dto.FacultyIds?.Distinct().ToList() ?? new List<long>();
                if (targetFacultyIds.Count == 0 && dto.Faculties?.Any() == true)
                {
                    targetFacultyIds = dto.Faculties.Select(f => f.FacultyId).Distinct().ToList();
                }

                var facultyOverrides = dto.Faculties?
                    .Where(f => f.IsActive.HasValue)
                    .ToDictionary(f => f.FacultyId, f => f.IsActive!.Value)
                    ?? new Dictionary<long, bool>();

                var existingFacultyIds = new HashSet<long>();
                foreach (var existing in existingMappings)
                {
                    if (targetFacultyIds.Contains(existing.FacultyId))
                    {
                        existingFacultyIds.Add(existing.FacultyId);
                        if (facultyOverrides.TryGetValue(existing.FacultyId, out var explicitActive))
                        {
                            existing.IsActive = explicitActive;
                        }
                        // Otherwise, preserve existing.IsActive as-is!
                        existing.UpdatedBy = dto.UpdatedBy;
                        existing.UpdatedDate = DateTime.UtcNow;
                    }
                    else
                    {
                        _context.KfCourseFaculties.Remove(existing);
                    }
                }

                var newFacultyIds = targetFacultyIds.Where(id => !existingFacultyIds.Contains(id)).ToList();
                if (newFacultyIds.Any())
                {
                    var newMappings = newFacultyIds
                        .Select(facultyId => new KfCourseFaculty
                        {
                            CourseId = entity.CourseId,
                            FacultyId = facultyId,
                            IsActive = facultyOverrides.TryGetValue(facultyId, out var explicitActive) ? explicitActive : true,
                            CreatedDate = DateTime.UtcNow,
                            CreatedBy = dto.UpdatedBy ?? entity.CreatedBy,
                            UpdatedBy = null,
                            UpdatedDate = null
                        })
                        .ToList();

                    _context.KfCourseFaculties.AddRange(newMappings);
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
                var entity = await _context.KfCourses
                    .FirstOrDefaultAsync(x => x.CourseId == id);

                if (entity == null)
                    return false;

                // Check if the program is already inactive/deleted
                if (!entity.IsActive)
                    return false;

                // Soft delete course
                entity.IsActive = false;

                // Remove faculty mappings
                var existingMappings = await _context.KfCourseFaculties
                    .Where(x => x.CourseId == id)
                    .ToListAsync();

                if (existingMappings.Any())
                {
                    _context.KfCourseFaculties.RemoveRange(existingMappings);
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


        // ---------------- GET BY ID ----------------
        public async Task<CourseRequestDto?> GetByIdAsync(long id, LoggedInUserDto currentUser)
        {
            var query =  _context.KfCourses
                .AsNoTracking()
                .Where(x => x.CourseId == id && x.IsActive);

            // Only course belonging to currentUser.UniversityIds,
            if (currentUser.StaffType == StaffType.University)
            {
                query = query.Where(x =>
                    currentUser.UniversityIds.Contains(x.UniversityId));
            }

            var result = await query
                .Select(x => new CourseRequestDto
                {
                    UniversityId = x.UniversityId,
                    UniversityName = x.University.UniversityName,

                    CourseId = x.CourseId,
                    CourseCode = x.CourseCode,
                    CourseNameEn = x.CourseNameEn,
                    CourseNameAr = x.CourseNameAr,

                    Faculties = x.KfCourseFaculties
                    .Select(cf => new CourseFacultyDto
                    {
                        FacultyId = cf.FacultyId,
                        FacultyName = cf.Faculty.FacultyName,
                        IsActive = cf.IsActive
                    })
                    .ToList(),

                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByNavigation.LoginName,
                    UpdatedDate = x.UpdatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedByName = x.UpdatedByNavigation == null ? null : x.UpdatedByNavigation.LoginName
                })
                .FirstOrDefaultAsync();

            return result;
        }


        // ---------------- GET ALL FILTER ----------------
        public async Task<PagedResultDto<CourseRequestDto>> GetByFilterAsync(CourseFilterDto filter, LoggedInUserDto currentUser)
        {
            var query = _context.KfCourses
                .AsNoTracking()
                .Where(x => x.IsActive)
                .AsQueryable();

            // Only courses belonging to currentUser.UniversityIds,
            if (currentUser.StaffType == StaffType.University)
            {
                query = query.Where(x => currentUser.UniversityIds.Contains(x.UniversityId));
            }

            if (filter.UniversityId.HasValue)
            {
                query = query.Where(x => x.UniversityId == filter.UniversityId.Value);
            }

            if (filter.FacultyId.HasValue)
            {
                query = query.Where(x => x.KfCourseFaculties.Any(cf => cf.FacultyId == filter.FacultyId.Value));
            }

            /* Global Search */
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim().ToLower();
                query = query.Where(x =>
                    x.CourseNameEn.ToLower().Contains(search) ||
                    (x.CourseNameAr != null && x.CourseNameAr.ToLower().Contains(search)) ||
                    x.CourseCode.ToLower().Contains(search)
                );
            }


            // ---------- Total Count (before pagination) ----------
            var totalCount = await query.CountAsync();

            // ---------- Ordering ----------
            query = query.OrderByDescending(x => x.CourseId);

            // ---------- Pagination rule ----------
            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new CourseRequestDto
                {
                    UniversityId = x.UniversityId,
                    UniversityName = x.University.UniversityName,

                    CourseId = x.CourseId,
                    CourseCode = x.CourseCode,
                    CourseNameEn = x.CourseNameEn,
                    CourseNameAr = x.CourseNameAr,

                    Faculties = x.KfCourseFaculties
                    .Select(cf => new CourseFacultyDto
                    {
                        FacultyId = cf.FacultyId,
                        FacultyName = cf.Faculty.FacultyName,
                        IsActive = cf.IsActive
                    })
                    .ToList(),

                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate,
                    CreatedBy = x.CreatedBy,
                    CreatedByName = x.CreatedByNavigation.LoginName,
                    UpdatedDate = x.UpdatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedByName = x.UpdatedByNavigation == null ? null : x.UpdatedByNavigation.LoginName
                })
                .ToListAsync();

            return new PagedResultDto<CourseRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }



    }
}
