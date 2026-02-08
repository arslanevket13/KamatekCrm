# KamatekCRM - Teknisyen Web Uygulaması Geliştirme Rehberi

## 📋 GENEL BİLGİLER

**Hedef:** Sahada çalışan teknisyenlerin mobil cihazlardan görevlerini görebilmesi, güncelleyebilmesi ve raporlayabilmesi için modern, responsive bir web uygulaması geliştirmek.

**Teknoloji Stack:**
- **Backend:** ASP.NET Core 9.0 Web API (Mevcut - Port 5050)
- **Frontend:** Blazor Server / Blazor WebAssembly
- **Database:** SQLite (Shared with WPF)
- **Authentication:** JWT Token (Mevcut API ile)
- **UI Framework:** MudBlazor / Bootstrap 5
- **Real-time:** SignalR (Opsiyonel - Bildirimler için)

**Mimari Yaklaşım:**
- Clean Architecture
- Repository Pattern (Mevcut)
- CQRS with MediatR (Önerilir)
- Response Caching
- Progressive Web App (PWA) desteği

---

## 🎯 İŞLEVSEL GEREKSİNİMLER

### Teknisyen Özellikleri

**1. Dashboard (Ana Sayfa)**
- Bugünkü görevler özeti
- Bekleyen görevler sayısı
- Tamamlanan görevler (bugün)
- Acil görevler uyarısı
- Konum bazlı yakındaki görevler (opsiyonel)

**2. Görev Listesi**
- Tüm atanmış görevler
- Filtreleme (Durum, Öncelik, Tarih)
- Arama (Müşteri adı, İş no)
- Detay görüntüleme
- Durum güncelleme

**3. Görev Detayı**
- Müşteri bilgileri
- Adres ve harita (WebView/Google Maps)
- İş tanımı ve notlar
- Malzeme listesi
- Fotoğraf ekleme/görüntüleme
- Süre takibi (başlat/bitir)
- İmza alma (müşteri onayı)

**4. Raporlama**
- Yapılan işlem kaydı
- Kullanılan malzemeler
- Harcanan süre
- Fotoğraf yükleme
- Müşteri imzası

**5. Profil**
- Kişisel bilgiler
- İstatistikler (tamamlanan iş, ortalama süre)
- Bildirim ayarları
- Çıkış

### Admin/Yönetici Özellikleri (WPF'ten)

**1. Teknisyen Yönetimi**
- Yeni teknisyen ekleme
- Teknisyen listesi ve düzenleme
- Aktif/pasif durumu
- İzin/tatil yönetimi

**2. Görev Atama**
- Manuel görev oluşturma
- Teknisyen seçme ve atama
- Toplu atama
- Yeniden atama

**3. Takip ve Raporlama**
- Teknisyen lokasyon takibi (opsiyonel)
- Performans raporları
- Tamamlanma oranları
- Müşteri memnuniyeti

---

## 🏗️ PROJE YAPISI

```
KamatekCRM/
├── KamatekCrm.Web/                    # Blazor Server Web Application
│   ├── Pages/
│   │   ├── Index.razor                # Dashboard
│   │   ├── Login.razor                # Giriş ekranı
│   │   ├── Tasks/
│   │   │   ├── TaskList.razor         # Görev listesi
│   │   │   ├── TaskDetail.razor       # Görev detayı
│   │   │   └── TaskReport.razor       # Görev raporu
│   │   ├── Profile/
│   │   │   ├── Profile.razor          # Profil
│   │   │   └── Statistics.razor       # İstatistikler
│   │   └── Admin/
│   │       ├── TechnicianManagement.razor
│   │       └── TaskAssignment.razor
│   │
│   ├── Components/
│   │   ├── TaskCard.razor             # Görev kartı component
│   │   ├── StatusBadge.razor          # Durum badge'i
│   │   ├── PhotoUpload.razor          # Fotoğraf yükleme
│   │   ├── SignaturePad.razor         # İmza paneli
│   │   └── LoadingSpinner.razor       # Yükleme göstergesi
│   │
│   ├── Services/
│   │   ├── ApiClient.cs               # HTTP Client wrapper
│   │   ├── AuthService.cs             # Authentication
│   │   ├── TaskService.cs             # Görev işlemleri
│   │   ├── TechnicianService.cs       # Teknisyen işlemleri
│   │   ├── PhotoService.cs            # Fotoğraf yönetimi
│   │   ├── LocationService.cs         # Lokasyon servisi
│   │   └── CacheService.cs            # Client-side cache
│   │
│   ├── Models/
│   │   ├── DTOs/
│   │   │   ├── TaskDto.cs
│   │   │   ├── TechnicianDto.cs
│   │   │   ├── TaskReportDto.cs
│   │   │   └── LoginRequestDto.cs
│   │   └── ViewModels/
│   │       ├── DashboardViewModel.cs
│   │       └── TaskListViewModel.cs
│   │
│   ├── wwwroot/
│   │   ├── css/                       # Custom styles
│   │   ├── js/                        # JavaScript interop
│   │   ├── images/                    # Assets
│   │   └── manifest.json              # PWA manifest
│   │
│   └── Program.cs                     # Blazor configuration
│
├── KamatekCrm.Api/                    # Existing API (Enhanced)
│   ├── Controllers/
│   │   ├── AuthController.cs          # ✅ Mevcut
│   │   ├── TechnicianController.cs    # ✅ Mevcut (Enhanced)
│   │   ├── TaskController.cs          # 🆕 Yeni
│   │   ├── PhotoController.cs         # 🆕 Yeni
│   │   └── ReportController.cs        # 🆕 Yeni
│   │
│   ├── Application/                   # CQRS Commands & Queries
│   │   ├── Commands/
│   │   │   ├── Tasks/
│   │   │   │   ├── CreateTaskCommand.cs
│   │   │   │   ├── UpdateTaskStatusCommand.cs
│   │   │   │   └── AssignTaskCommand.cs
│   │   │   └── Reports/
│   │   │       └── SubmitTaskReportCommand.cs
│   │   │
│   │   └── Queries/
│   │       ├── Tasks/
│   │       │   ├── GetTechnicianTasksQuery.cs
│   │       │   └── GetTaskDetailQuery.cs
│   │       └── Technicians/
│   │           └── GetTechnicianStatisticsQuery.cs
│   │
│   └── Infrastructure/
│       ├── Services/
│       │   ├── PhotoStorageService.cs
│       │   └── SignatureService.cs
│       └── Hubs/
│           └── NotificationHub.cs     # SignalR hub
│
└── KamatekCrm.Shared/                 # Shared Models
    ├── DTOs/                          # Data Transfer Objects
    └── Enums/                         # Shared Enums
```

---

## 🚀 GELİŞTİRME ADIMLARI

# AŞAMA 1: BACKEND API GELİŞTİRME

## ADIM 1.1: YENİ API CONTROLLER'LARI OLUŞTURMA

### 1.1.1 TaskController - Görev İşlemleri

**Konum:** `KamatekCrm.Api/Controllers/TaskController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using KamatekCrm.Api.Application.Commands.Tasks;
using KamatekCrm.Api.Application.Queries.Tasks;

namespace KamatekCrm.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TaskController> _logger;

        public TaskController(IMediator mediator, ILogger<TaskController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Teknisyene atanmış tüm görevleri getirir
        /// </summary>
        [HttpGet("technician/tasks")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<List<TaskDto>>>> GetTechnicianTasks(
            [FromQuery] TaskStatus? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = GetCurrentUserId();
            
            var query = new GetTechnicianTasksQuery
            {
                TechnicianId = userId,
                Status = status,
                StartDate = startDate,
                EndDate = endDate
            };

            var result = await _mediator.Send(query);

            return Ok(new ApiResponse<List<TaskDto>>
            {
                Success = true,
                Data = result,
                Message = $"{result.Count} görev bulundu"
            });
        }

        /// <summary>
        /// Görev detayını getirir
        /// </summary>
        [HttpGet("{taskId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TaskDetailDto>>> GetTaskDetail(int taskId)
        {
            var query = new GetTaskDetailQuery { TaskId = taskId };
            var result = await _mediator.Send(query);

            if (result == null)
            {
                return NotFound(new ApiResponse<TaskDetailDto>
                {
                    Success = false,
                    Message = "Görev bulunamadı"
                });
            }

            return Ok(new ApiResponse<TaskDetailDto>
            {
                Success = true,
                Data = result
            });
        }

        /// <summary>
        /// Görev durumunu günceller
        /// </summary>
        [HttpPut("{taskId}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateTaskStatus(
            int taskId,
            [FromBody] UpdateTaskStatusRequest request)
        {
            var command = new UpdateTaskStatusCommand
            {
                TaskId = taskId,
                NewStatus = request.Status,
                Notes = request.Notes,
                UpdatedBy = GetCurrentUserId()
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                return Ok(new ApiResponse<bool>
                {
                    Success = true,
                    Data = true,
                    Message = "Görev durumu güncellendi"
                });
            }

            return BadRequest(new ApiResponse<bool>
            {
                Success = false,
                Message = result.ErrorMessage
            });
        }

        /// <summary>
        /// Görev için süre kaydı başlatır
        /// </summary>
        [HttpPost("{taskId}/start")]
        public async Task<ActionResult<ApiResponse<bool>>> StartTask(int taskId)
        {
            var command = new StartTaskCommand
            {
                TaskId = taskId,
                TechnicianId = GetCurrentUserId(),
                StartTime = DateTime.Now
            };

            var result = await _mediator.Send(command);

            return Ok(new ApiResponse<bool>
            {
                Success = result.IsSuccess,
                Data = result.IsSuccess,
                Message = result.IsSuccess ? "Görev başlatıldı" : result.ErrorMessage
            });
        }

        /// <summary>
        /// Görev için süre kaydını bitirir
        /// </summary>
        [HttpPost("{taskId}/complete")]
        public async Task<ActionResult<ApiResponse<bool>>> CompleteTask(
            int taskId,
            [FromBody] CompleteTaskRequest request)
        {
            var command = new CompleteTaskCommand
            {
                TaskId = taskId,
                TechnicianId = GetCurrentUserId(),
                EndTime = DateTime.Now,
                CompletionNotes = request.Notes,
                UsedMaterials = request.UsedMaterials
            };

            var result = await _mediator.Send(command);

            return Ok(new ApiResponse<bool>
            {
                Success = result.IsSuccess,
                Data = result.IsSuccess,
                Message = result.IsSuccess ? "Görev tamamlandı" : result.ErrorMessage
            });
        }

        /// <summary>
        /// Dashboard istatistikleri
        /// </summary>
        [HttpGet("dashboard/stats")]
        public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetDashboardStats()
        {
            var query = new GetDashboardStatsQuery
            {
                TechnicianId = GetCurrentUserId()
            };

            var result = await _mediator.Send(query);

            return Ok(new ApiResponse<DashboardStatsDto>
            {
                Success = true,
                Data = result
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return int.Parse(userIdClaim?.Value ?? "0");
        }
    }

    // Request DTOs
    public class UpdateTaskStatusRequest
    {
        public TaskStatus Status { get; set; }
        public string? Notes { get; set; }
    }

    public class CompleteTaskRequest
    {
        public string? Notes { get; set; }
        public List<UsedMaterialDto>? UsedMaterials { get; set; }
    }

    // Response wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
    }
}
```

### 1.1.2 PhotoController - Fotoğraf Yönetimi

**Konum:** `KamatekCrm.Api/Controllers/PhotoController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KamatekCrm.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoStorageService _photoService;
        private readonly ILogger<PhotoController> _logger;
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        public PhotoController(
            IPhotoStorageService photoService,
            ILogger<PhotoController> logger)
        {
            _photoService = photoService;
            _logger = logger;
        }

        /// <summary>
        /// Görev için fotoğraf yükler
        /// </summary>
        [HttpPost("upload/{taskId}")]
        [RequestSizeLimit(MaxFileSize)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<PhotoUploadResult>>> UploadPhoto(
            int taskId,
            [FromForm] IFormFile photo,
            [FromForm] string? description)
        {
            // Validasyon
            if (photo == null || photo.Length == 0)
            {
                return BadRequest(new ApiResponse<PhotoUploadResult>
                {
                    Success = false,
                    Message = "Fotoğraf seçilmedi"
                });
            }

            // Dosya tipi kontrolü
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".heic" };
            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new ApiResponse<PhotoUploadResult>
                {
                    Success = false,
                    Message = "Geçersiz dosya formatı. Sadece JPG, PNG, HEIC kabul edilir."
                });
            }

            // Boyut kontrolü
            if (photo.Length > MaxFileSize)
            {
                return BadRequest(new ApiResponse<PhotoUploadResult>
                {
                    Success = false,
                    Message = "Dosya boyutu 10MB'dan büyük olamaz"
                });
            }

            try
            {
                var userId = GetCurrentUserId();

                var result = await _photoService.SavePhotoAsync(
                    taskId,
                    photo,
                    description,
                    userId);

                _logger.LogInformation(
                    "Photo uploaded for task {TaskId} by user {UserId}: {PhotoId}",
                    taskId, userId, result.PhotoId);

                return Ok(new ApiResponse<PhotoUploadResult>
                {
                    Success = true,
                    Data = result,
                    Message = "Fotoğraf başarıyla yüklendi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading photo for task {TaskId}", taskId);
                
                return StatusCode(500, new ApiResponse<PhotoUploadResult>
                {
                    Success = false,
                    Message = "Fotoğraf yüklenirken bir hata oluştu"
                });
            }
        }

        /// <summary>
        /// Göreve ait fotoğrafları listeler
        /// </summary>
        [HttpGet("task/{taskId}")]
        public async Task<ActionResult<ApiResponse<List<PhotoDto>>>> GetTaskPhotos(int taskId)
        {
            var photos = await _photoService.GetTaskPhotosAsync(taskId);

            return Ok(new ApiResponse<List<PhotoDto>>
            {
                Success = true,
                Data = photos,
                Message = $"{photos.Count} fotoğraf bulundu"
            });
        }

        /// <summary>
        /// Fotoğraf dosyasını indirir
        /// </summary>
        [HttpGet("{photoId}/download")]
        [AllowAnonymous] // Veya token ile korumalı
        public async Task<IActionResult> DownloadPhoto(int photoId)
        {
            var photo = await _photoService.GetPhotoAsync(photoId);

            if (photo == null)
            {
                return NotFound();
            }

            var filePath = photo.FilePath;
            
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Photo file not found: {FilePath}", filePath);
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = "image/jpeg"; // veya photo.MimeType

            return File(fileBytes, contentType, photo.FileName);
        }

        /// <summary>
        /// Fotoğrafı siler
        /// </summary>
        [HttpDelete("{photoId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeletePhoto(int photoId)
        {
            var result = await _photoService.DeletePhotoAsync(photoId);

            return Ok(new ApiResponse<bool>
            {
                Success = result,
                Data = result,
                Message = result ? "Fotoğraf silindi" : "Fotoğraf silinemedi"
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return int.Parse(userIdClaim?.Value ?? "0");
        }
    }

    public class PhotoUploadResult
    {
        public int PhotoId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
    }
}
```

### 1.1.3 PhotoStorageService Implementation

**Konum:** `KamatekCrm.Api/Infrastructure/Services/PhotoStorageService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace KamatekCrm.Api.Infrastructure.Services
{
    public interface IPhotoStorageService
    {
        Task<PhotoUploadResult> SavePhotoAsync(int taskId, IFormFile photo, string? description, int userId);
        Task<List<PhotoDto>> GetTaskPhotosAsync(int taskId);
        Task<PhotoDto?> GetPhotoAsync(int photoId);
        Task<bool> DeletePhotoAsync(int photoId);
    }

    public class PhotoStorageService : IPhotoStorageService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PhotoStorageService> _logger;
        private const int ThumbnailSize = 300;

        public PhotoStorageService(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<PhotoStorageService> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<PhotoUploadResult> SavePhotoAsync(
            int taskId, 
            IFormFile photo, 
            string? description, 
            int userId)
        {
            // Uploads klasörünü oluştur
            var uploadsPath = Path.Combine(_environment.ContentRootPath, "Uploads", "TaskPhotos");
            Directory.CreateDirectory(uploadsPath);

            // Unique dosya adı oluştur
            var fileExtension = Path.GetExtension(photo.FileName);
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Thumbnail için klasör
            var thumbnailsPath = Path.Combine(uploadsPath, "thumbnails");
            Directory.CreateDirectory(thumbnailsPath);
            var thumbnailFileName = $"thumb_{fileName}";
            var thumbnailPath = Path.Combine(thumbnailsPath, thumbnailFileName);

            // Orijinal fotoğrafı kaydet
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            // Thumbnail oluştur
            await CreateThumbnailAsync(filePath, thumbnailPath, ThumbnailSize);

            // Veritabanına kaydet
            var photoEntity = new TaskPhoto
            {
                TaskId = taskId,
                FileName = photo.FileName,
                FilePath = filePath,
                ThumbnailPath = thumbnailPath,
                FileSize = photo.Length,
                MimeType = photo.ContentType,
                Description = description,
                UploadedBy = userId,
                UploadedAt = DateTime.Now
            };

            _context.TaskPhotos.Add(photoEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Photo saved: {PhotoId} for task {TaskId}", photoEntity.Id, taskId);

            return new PhotoUploadResult
            {
                PhotoId = photoEntity.Id,
                FileName = fileName,
                Url = $"/api/photo/{photoEntity.Id}/download",
                ThumbnailUrl = $"/api/photo/{photoEntity.Id}/thumbnail"
            };
        }

        public async Task<List<PhotoDto>> GetTaskPhotosAsync(int taskId)
        {
            var photos = await _context.TaskPhotos
                .Where(p => p.TaskId == taskId && !p.IsDeleted)
                .OrderByDescending(p => p.UploadedAt)
                .Select(p => new PhotoDto
                {
                    Id = p.Id,
                    FileName = p.FileName,
                    Description = p.Description,
                    UploadedAt = p.UploadedAt,
                    Url = $"/api/photo/{p.Id}/download",
                    ThumbnailUrl = $"/api/photo/{p.Id}/thumbnail"
                })
                .ToListAsync();

            return photos;
        }

        public async Task<PhotoDto?> GetPhotoAsync(int photoId)
        {
            var photo = await _context.TaskPhotos
                .Where(p => p.Id == photoId && !p.IsDeleted)
                .Select(p => new PhotoDto
                {
                    Id = p.Id,
                    FileName = p.FileName,
                    FilePath = p.FilePath,
                    Description = p.Description,
                    UploadedAt = p.UploadedAt
                })
                .FirstOrDefaultAsync();

            return photo;
        }

        public async Task<bool> DeletePhotoAsync(int photoId)
        {
            var photo = await _context.TaskPhotos.FindAsync(photoId);
            
            if (photo == null) return false;

            // Soft delete
            photo.IsDeleted = true;
            photo.DeletedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Photo deleted: {PhotoId}", photoId);

            return true;
        }

        private async Task CreateThumbnailAsync(string sourcePath, string targetPath, int size)
        {
            try
            {
                using var image = await Image.LoadAsync(sourcePath);
                
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max
                }));

                await image.SaveAsync(targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating thumbnail for {SourcePath}", sourcePath);
                throw;
            }
        }
    }

    // DTO
    public class PhotoDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
    }
}
```

### 1.1.4 TaskPhoto Entity Modeli

**Konum:** `KamatekCrm.Shared/Models/TaskPhoto.cs`

```csharp
namespace KamatekCrm.Shared.Models
{
    public class TaskPhoto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation
        public ServiceJob? Task { get; set; }
        public User? UploadedByUser { get; set; }
    }
}
```

### 1.1.5 NuGet Paketleri (API Projesi)

```bash
# API projesinde
cd KamatekCrm.Api

# Image processing için
dotnet add package SixLabors.ImageSharp
dotnet add package SixLabors.ImageSharp.Web

# MediatR (eğer yoksa)
dotnet add package MediatR
dotnet add package MediatR.Extensions.Microsoft.DependencyInjection

# SignalR (real-time bildirimler için)
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

---

## ADIM 1.2: CQRS COMMANDS & QUERIES

### 1.2.1 GetTechnicianTasksQuery

**Konum:** `KamatekCrm.Api/Application/Queries/Tasks/GetTechnicianTasksQuery.cs`

```csharp
using MediatR;

namespace KamatekCrm.Api.Application.Queries.Tasks
{
    public class GetTechnicianTasksQuery : IRequest<List<TaskDto>>
    {
        public int TechnicianId { get; set; }
        public TaskStatus? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GetTechnicianTasksQueryHandler 
        : IRequestHandler<GetTechnicianTasksQuery, List<TaskDto>>
    {
        private readonly AppDbContext _context;

        public GetTechnicianTasksQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<TaskDto>> Handle(
            GetTechnicianTasksQuery request,
            CancellationToken cancellationToken)
        {
            var query = _context.ServiceJobs
                .Include(j => j.Customer)
                .Include(j => j.AssignedTechnician)
                .Where(j => j.AssignedTechnicianId == request.TechnicianId && !j.IsDeleted);

            // Durum filtresi
            if (request.Status.HasValue)
            {
                query = query.Where(j => j.Status == request.Status.Value);
            }

            // Tarih filtresi
            if (request.StartDate.HasValue)
            {
                query = query.Where(j => j.ScheduledDate >= request.StartDate.Value);
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(j => j.ScheduledDate <= request.EndDate.Value);
            }

            var tasks = await query
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.ScheduledDate)
                .Select(j => new TaskDto
                {
                    Id = j.Id,
                    Title = j.Title ?? $"İş #{j.Id}",
                    Description = j.Description,
                    Status = j.Status,
                    Priority = j.Priority,
                    ScheduledDate = j.ScheduledDate,
                    EstimatedDuration = j.EstimatedDuration,
                    Customer = new CustomerDto
                    {
                        Id = j.Customer.Id,
                        Name = j.Customer.Name,
                        Phone = j.Customer.Phone,
                        Address = j.Customer.Address,
                        City = j.Customer.City,
                        District = j.Customer.District
                    },
                    Category = j.JobCategory.ToString(),
                    CreatedAt = j.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return tasks;
        }
    }

    // DTO
    public class TaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaskStatus Status { get; set; }
        public JobPriority Priority { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public int? EstimatedDuration { get; set; }
        public CustomerDto Customer { get; set; } = null!;
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}
```

### 1.2.2 UpdateTaskStatusCommand

**Konum:** `KamatekCrm.Api/Application/Commands/Tasks/UpdateTaskStatusCommand.cs`

```csharp
using MediatR;

namespace KamatekCrm.Api.Application.Commands.Tasks
{
    public class UpdateTaskStatusCommand : IRequest<Result>
    {
        public int TaskId { get; set; }
        public TaskStatus NewStatus { get; set; }
        public string? Notes { get; set; }
        public int UpdatedBy { get; set; }
    }

    public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Result>
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UpdateTaskStatusCommandHandler> _logger;

        public UpdateTaskStatusCommandHandler(
            AppDbContext context,
            ILogger<UpdateTaskStatusCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result> Handle(
            UpdateTaskStatusCommand request,
            CancellationToken cancellationToken)
        {
            var task = await _context.ServiceJobs.FindAsync(request.TaskId);

            if (task == null)
            {
                return Result.Failure("Görev bulunamadı");
            }

            var oldStatus = task.Status;
            task.Status = request.NewStatus;
            task.ModifiedAt = DateTime.Now;
            task.ModifiedBy = request.UpdatedBy.ToString();

            // History kaydı oluştur
            var history = new ServiceJobHistory
            {
                ServiceJobId = task.Id,
                Action = $"Durum değişti: {oldStatus} → {request.NewStatus}",
                Notes = request.Notes,
                PerformedBy = request.UpdatedBy,
                PerformedAt = DateTime.Now
            };

            _context.ServiceJobHistories.Add(history);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Task {TaskId} status updated from {OldStatus} to {NewStatus} by user {UserId}",
                request.TaskId, oldStatus, request.NewStatus, request.UpdatedBy);

            return Result.Success();
        }
    }

    // Result class
    public class Result
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }

        private Result(bool isSuccess, string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static Result Success() => new(true);
        public static Result Failure(string error) => new(false, error);
    }
}
```

### 1.2.3 GetDashboardStatsQuery

**Konum:** `KamatekCrm.Api/Application/Queries/Tasks/GetDashboardStatsQuery.cs`

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.Api.Application.Queries.Tasks
{
    public class GetDashboardStatsQuery : IRequest<DashboardStatsDto>
    {
        public int TechnicianId { get; set; }
    }

    public class GetDashboardStatsQueryHandler 
        : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
    {
        private readonly AppDbContext _context;

        public GetDashboardStatsQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatsDto> Handle(
            GetDashboardStatsQuery request,
            CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            var tasks = await _context.ServiceJobs
                .Where(j => j.AssignedTechnicianId == request.TechnicianId && !j.IsDeleted)
                .ToListAsync(cancellationToken);

            var stats = new DashboardStatsDto
            {
                TotalTasks = tasks.Count,
                PendingTasks = tasks.Count(t => t.Status == TaskStatus.Pending),
                InProgressTasks = tasks.Count(t => t.Status == TaskStatus.InProgress),
                CompletedTasks = tasks.Count(t => t.Status == TaskStatus.Completed),
                TodayTasks = tasks.Count(t => t.ScheduledDate?.Date == today),
                ThisWeekTasks = tasks.Count(t => t.ScheduledDate >= startOfWeek),
                UrgentTasks = tasks.Count(t => 
                    t.Priority == JobPriority.Urgent || t.Priority == JobPriority.Critical),
                CompletedToday = tasks.Count(t => 
                    t.Status == TaskStatus.Completed && 
                    t.ModifiedAt?.Date == today),
                AverageCompletionTime = CalculateAverageCompletionTime(tasks)
            };

            return stats;
        }

        private double CalculateAverageCompletionTime(List<ServiceJob> tasks)
        {
            var completedTasks = tasks
                .Where(t => t.Status == TaskStatus.Completed && 
                           t.CreatedAt != null && 
                           t.ModifiedAt != null)
                .ToList();

            if (!completedTasks.Any())
                return 0;

            var totalHours = completedTasks
                .Average(t => (t.ModifiedAt!.Value - t.CreatedAt).TotalHours);

            return Math.Round(totalHours, 1);
        }
    }

    public class DashboardStatsDto
    {
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int TodayTasks { get; set; }
        public int ThisWeekTasks { get; set; }
        public int UrgentTasks { get; set; }
        public int CompletedToday { get; set; }
        public double AverageCompletionTime { get; set; }
    }
}
```

---

## ADIM 1.3: DATABASE MİGRATİON

### 1.3.1 Migration Oluştur

```bash
# API projesinde
cd KamatekCrm.Api

dotnet ef migrations add AddTaskPhotosAndHistory
dotnet ef database update
```

### 1.3.2 AppDbContext Güncellemesi

**Konum:** `KamatekCrm/Data/AppDbContext.cs` (veya API'de paylaşılan)

```csharp
public class AppDbContext : DbContext
{
    // Mevcut DbSets...
    public DbSet<ServiceJob> ServiceJobs { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<User> Users { get; set; }
    
    // YENİ
    public DbSet<TaskPhoto> TaskPhotos { get; set; }
    public DbSet<ServiceJobHistory> ServiceJobHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TaskPhoto configuration
        modelBuilder.Entity<TaskPhoto>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UploadedByUser)
                .WithMany()
                .HasForeignKey(e => e.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => new { e.TaskId, e.IsDeleted });
        });

        // ServiceJobHistory configuration
        modelBuilder.Entity<ServiceJobHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.ServiceJob)
                .WithMany()
                .HasForeignKey(e => e.ServiceJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ServiceJobId);
            entity.HasIndex(e => e.PerformedAt);
        });
    }
}
```

### 1.3.3 ServiceJobHistory Entity

**Konum:** `KamatekCrm.Shared/Models/ServiceJobHistory.cs`

```csharp
namespace KamatekCrm.Shared.Models
{
    public class ServiceJobHistory
    {
        public int Id { get; set; }
        public int ServiceJobId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; }

        // Navigation
        public ServiceJob? ServiceJob { get; set; }
    }
}
```

---

# AŞAMA 2: BLAZOR WEB UYGULAMASI OLUŞTURMA

## ADIM 2.1: BLAZOR PROJESİ OLUŞTURMA

### 2.1.1 Yeni Blazor Server Projesi

```bash
# Solution root'ta
dotnet new blazorserver -n KamatekCrm.Web -o KamatekCrm.Web

# Solution'a ekle
dotnet sln add KamatekCrm.Web/KamatekCrm.Web.csproj

# Shared projesine referans
cd KamatekCrm.Web
dotnet add reference ../KamatekCrm.Shared/KamatekCrm.Shared.csproj
```

### 2.1.2 NuGet Paketleri (Web Projesi)

```bash
# UI Framework
dotnet add package MudBlazor

# HTTP Client
dotnet add package Microsoft.Extensions.Http

# Authentication
dotnet add package Microsoft.AspNetCore.Components.Authorization
dotnet add package Blazored.LocalStorage

# PWA Support
dotnet add package Blazor.PWA

# Image resizing (client-side)
dotnet add package Blazor.FileReader

# SignalR (real-time)
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

### 2.1.3 Program.cs Yapılandırması

**Konum:** `KamatekCrm.Web/Program.cs`

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using Blazored.LocalStorage;
using KamatekCrm.Web.Services;
using KamatekCrm.Web.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// MudBlazor
builder.Services.AddMudServices();

// Local Storage
builder.Services.AddBlazoredLocalStorage();

// HttpClient (API bağlantısı)
builder.Services.AddHttpClient("API", client =>
{
    client.BaseAddress = new Uri("http://localhost:5050");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Application Services
builder.Services.AddScoped<IApiClient, ApiClient>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ITechnicianService, TechnicianService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();

// Authentication
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore();

// SignalR (opsiyonel)
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

---

## ADIM 2.2: AUTHENTICATION SERVİSLERİ

### 2.2.1 IAuthService Interface

**Konum:** `KamatekCrm.Web/Services/IAuthService.cs`

```csharp
namespace KamatekCrm.Web.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(string username, string password);
        Task LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<string?> GetTokenAsync();
        Task<UserInfo?> GetCurrentUserAsync();
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Message { get; set; }
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
```

### 2.2.2 AuthService Implementation

**Konum:** `KamatekCrm.Web/Services/AuthService.cs`

```csharp
using Blazored.LocalStorage;
using System.Net.Http.Json;

namespace KamatekCrm.Web.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private const string TokenKey = "authToken";
        private const string UserKey = "currentUser";

        public AuthService(
            IHttpClientFactory httpClientFactory,
            ILocalStorageService localStorage)
        {
            _httpClient = httpClientFactory.CreateClient("API");
            _localStorage = localStorage;
        }

        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new
                {
                    username,
                    password
                });

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

                    if (loginResponse != null && loginResponse.Success)
                    {
                        // Token'ı sakla
                        await _localStorage.SetItemAsync(TokenKey, loginResponse.Token);

                        // Kullanıcı bilgilerini sakla
                        await _localStorage.SetItemAsync(UserKey, new UserInfo
                        {
                            Id = loginResponse.UserId,
                            FullName = loginResponse.FullName ?? string.Empty,
                            Role = loginResponse.Role ?? string.Empty
                        });

                        return loginResponse;
                    }
                }

                return new LoginResponse
                {
                    Success = false,
                    Message = "Geçersiz kullanıcı adı veya şifre"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return new LoginResponse
                {
                    Success = false,
                    Message = "Sunucuya bağlanılamadı"
                };
            }
        }

        public async Task LogoutAsync()
        {
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(UserKey);
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        public async Task<string?> GetTokenAsync()
        {
            return await _localStorage.GetItemAsync<string>(TokenKey);
        }

        public async Task<UserInfo?> GetCurrentUserAsync()
        {
            return await _localStorage.GetItemAsync<UserInfo>(UserKey);
        }
    }
}
```

### 2.2.3 Custom Authentication State Provider

**Konum:** `KamatekCrm.Web/Authentication/CustomAuthenticationStateProvider.cs`

```csharp
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using KamatekCrm.Web.Services;

namespace KamatekCrm.Web.Authentication
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthService _authService;

        public CustomAuthenticationStateProvider(IAuthService authService)
        {
            _authService = authService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var user = await _authService.GetCurrentUserAsync();

            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var identity = new ClaimsIdentity(claims, "apiauth");
                var principal = new ClaimsPrincipal(identity);

                return new AuthenticationState(principal);
            }

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public void NotifyUserAuthentication(UserInfo user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, "apiauth");
            var principal = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(principal)));
        }

        public void NotifyUserLogout()
        {
            var identity = new ClaimsIdentity();
            var principal = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(principal)));
        }
    }
}
```

---

## ADIM 2.3: API CLIENT SERVİSİ

### 2.3.1 ApiClient Base Class

**Konum:** `KamatekCrm.Web/Services/ApiClient.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace KamatekCrm.Web.Services
{
    public interface IApiClient
    {
        Task<T?> GetAsync<T>(string endpoint);
        Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<bool> DeleteAsync(string endpoint);
    }

    public class ApiClient : IApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public ApiClient(
            IHttpClientFactory httpClientFactory,
            IAuthService authService)
        {
            _httpClient = httpClientFactory.CreateClient("API");
            _authService = authService;
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            await SetAuthorizationHeaderAsync();

            var response = await _httpClient.GetAsync(endpoint);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }

            return default;
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            await SetAuthorizationHeaderAsync();

            var response = await _httpClient.PostAsJsonAsync(endpoint, data);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TResponse>();
            }

            return default;
        }

        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            await SetAuthorizationHeaderAsync();

            var response = await _httpClient.PutAsJsonAsync(endpoint, data);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TResponse>();
            }

            return default;
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            await SetAuthorizationHeaderAsync();

            var response = await _httpClient.DeleteAsync(endpoint);
            return response.IsSuccessStatusCode;
        }

        private async Task SetAuthorizationHeaderAsync()
        {
            var token = await _authService.GetTokenAsync();
            
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}
```

---

## ADIM 2.4: TASK SERVICE

### 2.4.1 ITaskService Interface

**Konum:** `KamatekCrm.Web/Services/ITaskService.cs`

```csharp
namespace KamatekCrm.Web.Services
{
    public interface ITaskService
    {
        Task<List<TaskDto>> GetMyTasksAsync(TaskStatus? status = null);
        Task<TaskDetailDto?> GetTaskDetailAsync(int taskId);
        Task<DashboardStatsDto?> GetDashboardStatsAsync();
        Task<bool> UpdateTaskStatusAsync(int taskId, TaskStatus newStatus, string? notes = null);
        Task<bool> StartTaskAsync(int taskId);
        Task<bool> CompleteTaskAsync(int taskId, string? notes, List<UsedMaterialDto>? materials);
    }
}
```

### 2.4.2 TaskService Implementation

**Konum:** `KamatekCrm.Web/Services/TaskService.cs`

```csharp
namespace KamatekCrm.Web.Services
{
    public class TaskService : ITaskService
    {
        private readonly IApiClient _apiClient;

        public TaskService(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<List<TaskDto>> GetMyTasksAsync(TaskStatus? status = null)
        {
            var endpoint = "/api/task/technician/tasks";
            
            if (status.HasValue)
            {
                endpoint += $"?status={status.Value}";
            }

            var response = await _apiClient.GetAsync<ApiResponse<List<TaskDto>>>(endpoint);
            return response?.Data ?? new List<TaskDto>();
        }

        public async Task<TaskDetailDto?> GetTaskDetailAsync(int taskId)
        {
            var response = await _apiClient.GetAsync<ApiResponse<TaskDetailDto>>(
                $"/api/task/{taskId}");
            
            return response?.Data;
        }

        public async Task<DashboardStatsDto?> GetDashboardStatsAsync()
        {
            var response = await _apiClient.GetAsync<ApiResponse<DashboardStatsDto>>(
                "/api/task/dashboard/stats");
            
            return response?.Data;
        }

        public async Task<bool> UpdateTaskStatusAsync(
            int taskId,
            TaskStatus newStatus,
            string? notes = null)
        {
            var request = new { Status = newStatus, Notes = notes };
            
            var response = await _apiClient.PutAsync<object, ApiResponse<bool>>(
                $"/api/task/{taskId}/status",
                request);

            return response?.Success ?? false;
        }

        public async Task<bool> StartTaskAsync(int taskId)
        {
            var response = await _apiClient.PostAsync<object, ApiResponse<bool>>(
                $"/api/task/{taskId}/start",
                new { });

            return response?.Success ?? false;
        }

        public async Task<bool> CompleteTaskAsync(
            int taskId,
            string? notes,
            List<UsedMaterialDto>? materials)
        {
            var request = new { Notes = notes, UsedMaterials = materials };
            
            var response = await _apiClient.PostAsync<object, ApiResponse<bool>>(
                $"/api/task/{taskId}/complete",
                request);

            return response?.Success ?? false;
        }
    }
}
```

---

DEVAM EDİYOR... (Bu rehber 15,000+ satır olacak)

Şimdi Blazor UI sayfalarını, componentleri, WPF entegrasyonunu ve deployment adımlarını ekleyeceğim. Devam etmemi ister misiniz?
