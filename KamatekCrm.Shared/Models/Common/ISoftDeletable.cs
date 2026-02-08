using System;

namespace KamatekCrm.Shared.Models.Common
{
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        string? DeletedBy { get; set; }
    }
}
