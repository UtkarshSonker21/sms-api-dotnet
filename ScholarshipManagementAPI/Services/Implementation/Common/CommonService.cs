using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.GlobalSearch;
using ScholarshipManagementAPI.DTOs.Common.HrStaff;
using ScholarshipManagementAPI.DTOs.Common.Menu;
using ScholarshipManagementAPI.DTOs.Common.Settings;
using ScholarshipManagementAPI.DTOs.SuperADmin.ZzMasterDropdown;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Services.Interface.Common;
using ScholarshipManagementAPI.Services.Interface.SuperAdmin;

namespace ScholarshipManagementAPI.Services.Implementation.Common
{

    public class CommonService : ICommonService
    {
        private readonly AppDbContext _context;
        private readonly IAwsBucketService _awsBucketService;

        public CommonService(AppDbContext context, IAwsBucketService awsBucketService)
        {
            _context = context;
            _awsBucketService = awsBucketService;
        }


        public async Task<List<UsersModuleDto>> GetAllUsersModule()
        {
            return await _context.KfUsersModules
                .AsNoTracking()
                .OrderBy(x => x.ModuleId)
                .Select(x => new UsersModuleDto
                {
                    ModuleId = x.ModuleId,
                    ModuleName = x.ModuleName,
                    IsActive = x.IsActive
                })
                .ToListAsync();
        }


        public async Task<List<LoadMenuDto>> LoadMenusByRoleAsync(long roleId)
        {
            // Fetch role-page permissions + menu
            var roleMenus = await _context.KfUsersRolePermissions
                .AsNoTracking()
                .Include(rp => rp.MenuLink)
                .Where(rp => rp.RoleId == roleId && rp.ViewPer)
                .OrderBy(x => x.MenuLink.ParentId ?? x.MenuLink.MenuLinkId)
                .ThenBy(x => x.MenuLink.LevelNo)
                .ThenBy(x => x.MenuLink.SequenceNo)
                .Select(rp => new
                {
                    rp.MenuLink.MenuLinkId,
                    rp.MenuLink.ParentId,
                    rp.MenuLink.ModuleId,
                    rp.MenuLink.PageHeading,
                    rp.MenuLink.PagePath,
                    rp.MenuLink.ActualName,
                    rp.MenuLink.SequenceNo,
                    rp.MenuLink.Icon,
                    Permissions = new MenuPermissionDto
                    {
                        CanView = rp.ViewPer,
                        CanInsert = rp.InsertPer,
                        CanUpdate = rp.UpdatePer,
                        CanDelete = rp.DeletePer
                    }
                })
                .ToListAsync();

            // Convert to dictionary
            var menuDict = roleMenus.ToDictionary(
                x => x.MenuLinkId,
                x => new LoadMenuDto
                {
                    MenuLinkId = x.MenuLinkId,
                    ModuleId = x.ModuleId,
                    PageHeading = x.PageHeading,
                    PagePath = x.PagePath,
                    ActualName = x.ActualName,
                    SequenceNo = x.SequenceNo,
                    Icon = x.Icon,
                    Permissions = x.Permissions
                }
            );

            // Build hierarchy
            List<LoadMenuDto> rootMenus = new();

            foreach (var item in roleMenus)
            {
                if (item.ParentId != null && menuDict.ContainsKey(item.ParentId.Value))
                {
                    menuDict[item.ParentId.Value]
                        .SubMenus.Add(menuDict[item.MenuLinkId]);
                }
                else
                {
                    rootMenus.Add(menuDict[item.MenuLinkId]);
                }
            }

            // Sort recursively
            SortMenus(rootMenus);

            return rootMenus;
        }


        public async Task<GlobalSearchResponseDto> GlobalSearchAsync(GlobalSearchRequestDto request, LoggedInUserDto currentUser)
        {
            var response = new GlobalSearchResponseDto();

            if (string.IsNullOrWhiteSpace(request.SearchText))
                return response;

            var search = request.SearchText.Trim().ToLower();

            var maxResults = request.Limit > 0
               ? request.Limit
               : 5;

            switch (currentUser.StaffType)
            {
                case StaffType.SuperAdmin:
                    await SearchNgoAsync(response, search, currentUser, maxResults);
                    break;

                case StaffType.Ngo:
                    await SearchNgoAsync(response, search, currentUser, maxResults);
                    break;

                case StaffType.University:
                    await SearchUniversityAsync(response, search, currentUser, maxResults);
                    break;

                case StaffType.School:
                    await SearchSchoolAsync(response, search, currentUser, maxResults);
                    break;

                case StaffType.Marketing:
                    await SearchMarketingAsync(response, search, currentUser, maxResults);
                    break;

                case StaffType.Finance:
                    await SearchFinanceAsync(response, search, currentUser, maxResults);
                    break;
            }


            // Remove empty sections
            response.Sections = response.Sections
                .Where(x => x.Items.Count > 0)
                .ToList();

            return response;
        }



        #region File Upload Methods

        public async Task<string> UploadUserProfileImageAsync(int userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File is required");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };

            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                throw new Exception("Invalid image type");

            if (file.Length > 2 * 1024 * 1024)
                throw new Exception("Image must be less than 2MB");

            var fileKey = $"users/{userId}/profile/{Guid.NewGuid()}{extension}";

            using var stream = file.OpenReadStream();

            await _awsBucketService.UploadFileAsync(
                stream,
                fileKey,
                file.ContentType
            );


            // Save fileKey in database later

            return fileKey;
        }


        public string? GetProfileImageUrl(string? fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return null;

            return _awsBucketService.GeneratePreSignedUrl(fileKey);
        }



        public async Task<string> UploadUserDocumentAsync(int userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("File is required");

            var allowedExtensions = new[] { ".pdf" };

            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                throw new Exception("Only PDF documents are allowed");

            if (file.Length > 10 * 1024 * 1024)
                throw new Exception("Document size must be less than 10MB");

            var fileKey = $"users/{userId}/documents/{Guid.NewGuid()}{extension}";

            using var stream = file.OpenReadStream();

            await _awsBucketService.UploadFileAsync(
                stream,
                fileKey,
                file.ContentType
            );


            // Save fileKey in database later

            return fileKey;
        }


        public string? GetDocumentUrl(string? fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return null;

            return _awsBucketService.GeneratePreSignedUrl(fileKey);
        }


        #endregion





        #region Private Methods

        private void SortMenus(List<LoadMenuDto>? menus)
        {
            if (menus == null || menus.Count == 0)
                return;

            menus.Sort((a, b) => a.SequenceNo.CompareTo(b.SequenceNo));

            foreach (var menu in menus)
            {
                SortMenus(menu.SubMenus);
            }
        }


        private async Task SearchNgoAsync(
            GlobalSearchResponseDto response,
            string search,
            LoggedInUserDto currentUser,
            int maxResults)
        {
            // Universities
            var universities = await _context.KfUniversities
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDraft)
                .Where(x =>
                    x.UniversityName.ToLower().Contains(search) ||
                    x.City.ToLower().Contains(search))
                .OrderBy(x => x.UniversityName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.UniversityId,
                    Title = x.UniversityName,
                    Subtitle = x.City,
                    Route = $"/university-accreditation-detail/{x.UniversityId}"
                })
                .ToListAsync();

            if (universities.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "UNIVERSITIES",
                    Items = universities
                });
            }


            // Programs
            var programs = await _context.KfPrograms
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x =>
                    x.ProgramName.ToLower().Contains(search) ||
                    x.ProgramCode.ToLower().Contains(search))
                .OrderBy(x => x.ProgramName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.ProgramId,
                    Title = x.ProgramName,
                    Subtitle = x.ProgramCode,
                    Route = $"/program-accreditation-detail/{x.ProgramId}"
                })
                .ToListAsync();

            if (programs.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "PROGRAMS",
                    Items = programs
                });
            }


            // Schools
            var schools = await _context.KfSchools
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDraft)
                .Where(x =>
                    x.SchoolName.ToLower().Contains(search) ||
                    x.ShortName.ToLower().Contains(search))
                .OrderBy(x => x.SchoolName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.SchoolId,
                    Title = x.SchoolName,
                    Subtitle = x.ShortName,
                    Route = $"/school-accreditation-detail/{x.SchoolId}"
                })
                .ToListAsync();

            if (schools.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "SCHOOLS",
                    Items = schools
                });
            }


            // Cases
            // Will be implemented later.
        }


        private async Task SearchUniversityAsync(
            GlobalSearchResponseDto response,
            string search,
            LoggedInUserDto currentUser,
            int maxResults)
        {
            // Students
            var students = await _context.KfStudentRegistrations
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x =>
                    x.FirstName.ToLower().Contains(search) ||
                    x.LastName.ToLower().Contains(search) ||
                    x.StudentCode.ToLower().Contains(search))
                .OrderBy(x => x.FirstName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.StudentId,
                    Title = x.FirstName + " " + x.LastName,
                    Subtitle = x.StudentCode,
                    Route = $"/university-student-details/{x.StudentId}"
                })
                .ToListAsync();

            if (students.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "STUDENTS",
                    Items = students
                });
            }


            // Programs
            var programs = await _context.KfPrograms
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x =>
                    x.ProgramName.ToLower().Contains(search) ||
                    x.ProgramCode.ToLower().Contains(search))
                .OrderBy(x => x.ProgramName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.ProgramId,
                    Title = x.ProgramName,
                    Subtitle = x.ProgramCode,
                    Route = $"/programs-detail/{x.ProgramId}"
                })
                .ToListAsync();

            if (programs.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "PROGRAMS",
                    Items = programs
                });
            }


            // Payments
            // Will be implemented later.
        }



        private async Task SearchSchoolAsync(
            GlobalSearchResponseDto response,
            string search,
            LoggedInUserDto currentUser,
            int maxResults)
        {
            // Nominees / Students
            var nominees = await _context.KfStudentRegistrations
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x =>
                    x.FirstName.ToLower().Contains(search) ||
                    x.LastName.ToLower().Contains(search) ||
                    x.StudentCode.ToLower().Contains(search))
                .OrderBy(x => x.FirstName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.StudentId,
                    Title = x.FirstName + " " + x.LastName,
                    Subtitle = x.StudentCode,
                    Route = $"/edit-student/{x.StudentId}"
                })
                .ToListAsync();

            if (nominees.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "NOMINEES",
                    Items = nominees
                });
            }
        }


        private async Task SearchMarketingAsync(
            GlobalSearchResponseDto response,
            string search,
            LoggedInUserDto currentUser,
            int maxResults)
        {
            // Students
            // TODO: Add marketing-specific search sections later.

            var students = await _context.KfStudentRegistrations
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x =>
                    x.FirstName.ToLower().Contains(search) ||
                    x.LastName.ToLower().Contains(search) ||
                    x.StudentCode.ToLower().Contains(search))
                .OrderBy(x => x.FirstName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.StudentId,
                    Title = x.FirstName + " " + x.LastName,
                    Subtitle = x.StudentCode,
                    Route = null
                })
                .ToListAsync();

            if (students.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "STUDENTS",
                    Items = students
                });
            }
        }



        private async Task SearchFinanceAsync(
            GlobalSearchResponseDto response,
            string search,
            LoggedInUserDto currentUser,
            int maxResults)
        {
            // Students
            // TODO: Add finance-specific search sections later.

            var students = await _context.KfStudentRegistrations
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x =>
                    x.FirstName.ToLower().Contains(search) ||
                    x.LastName.ToLower().Contains(search) ||
                    x.StudentCode.ToLower().Contains(search))
                .OrderBy(x => x.FirstName)
                .Take(maxResults)
                .Select(x => new GlobalSearchItemDto
                {
                    Id = x.StudentId,
                    Title = x.FirstName + " " + x.LastName,
                    Subtitle = x.StudentCode,
                    Route = null
                })
                .ToListAsync();

            if (students.Count > 0)
            {
                response.Sections.Add(new GlobalSearchSectionDto
                {
                    SectionName = "STUDENTS",
                    Items = students
                });
            }
        }




        #endregion






    }
}