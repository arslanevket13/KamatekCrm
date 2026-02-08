using System;
using KamatekCrm.Shared.Models.Common;

namespace KamatekCrm.Shared.Models
{
    public class TaskPhoto : BaseEntity
    {
        public int TaskId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        
        // Inherits IsDeleted, DeletedAt, DeletedBy from BaseEntity

        // Navigation
        public ServiceJob? Task { get; set; }
        public User? UploadedByUser { get; set; }
    }
}
