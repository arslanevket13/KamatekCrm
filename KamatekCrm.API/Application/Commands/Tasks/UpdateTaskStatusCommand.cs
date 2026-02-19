using MediatR;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.API.Application.Common;

namespace KamatekCrm.API.Application.Commands.Tasks
{
    public class UpdateTaskStatusCommand : IRequest<Result>
    {
        public int TaskId { get; set; }
        public JobStatus NewStatus { get; set; }
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
            task.ModifiedDate = DateTime.Now;
            task.ModifiedBy = request.UpdatedBy.ToString(); // Assuming UpdatedBy is int UserId

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
}
