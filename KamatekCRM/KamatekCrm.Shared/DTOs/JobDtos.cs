using System;
using System.Collections.Generic;

namespace KamatekCrm.Shared.DTOs
{
    public class TechnicianJobDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class ServiceJobHistoryDto
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
    }

    public class TechnicianJobDetailDto : TechnicianJobDto
    {
        public List<ServiceJobHistoryDto> History { get; set; } = new List<ServiceJobHistoryDto>();
    }

    public class UpdateStatusRequest
    {
        public int JobId { get; set; }
        public int NewStatus { get; set; }
        public string TechnicianNote { get; set; } = string.Empty;
        public double? CurrentLatitude { get; set; }
        public double? CurrentLongitude { get; set; }
    }
}
