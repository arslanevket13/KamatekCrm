using KamatekCrm.Shared.DTOs;
using System.Net.Http.Json;

namespace KamatekCrm.Web.Services
{
    public interface ITaskService
    {
        Task<List<TaskDto>> GetTasks(int technicianId);
        Task<TaskDetailDto?> GetTaskDetail(int taskId);
        Task<bool> UpdateStatus(int taskId, int status, string notes);
        Task<DashboardStatsDto?> GetDashboardStats();
        Task<bool> UploadPhoto(int taskId, MultipartFormDataContent content);
    }

    public class TaskService : ITaskService
    {
        private readonly HttpClient _httpClient;

        public TaskService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<TaskDto>> GetTasks(int technicianId)
        {
            // API expects technicianId via query or route? 
            // TaskController.GetTechnicianTasks takes query params.
            // And assumes Authorized user context.
            // So default call might be just /api/task/technician/tasks
            // But wait, TaskController uses GetCurrentUserId(). So no ID passed in query usually.
            
            var result = await _httpClient.GetFromJsonAsync<ApiResponse<List<TaskDto>>>("api/task/technician/tasks");
            return result?.Data ?? new List<TaskDto>();
        }

        public async Task<TaskDetailDto?> GetTaskDetail(int taskId)
        {
            var result = await _httpClient.GetFromJsonAsync<ApiResponse<TaskDetailDto>>($"api/task/{taskId}");
            return result?.Data;
        }

        public async Task<bool> UpdateStatus(int taskId, int status, string notes)
        {
             // TODO: Define UpdateTaskStatusRequest in Shared DTOs if not exists
             // Guide said UpdateTaskStatusRequest is inside Controller file. BAD practice.
             // I need to use anonymous object or create DTO.
             
             var request = new { Status = status, Notes = notes };
             var response = await _httpClient.PutAsJsonAsync($"api/task/{taskId}/status", request);
             return response.IsSuccessStatusCode;
        }

        public async Task<DashboardStatsDto?> GetDashboardStats()
        {
            var result = await _httpClient.GetFromJsonAsync<ApiResponse<DashboardStatsDto>>("api/task/dashboard/stats");
            return result?.Data;
        }

        public async Task<bool> UploadPhoto(int taskId, MultipartFormDataContent content)
        {
            // Note: PhotoController route is api/photo/upload/{taskId}
            var response = await _httpClient.PostAsync($"api/photo/upload/{taskId}", content);
            return response.IsSuccessStatusCode;
        }
    }

    // Helper classes until Shared DTOs are fully sorted
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
    }
}
