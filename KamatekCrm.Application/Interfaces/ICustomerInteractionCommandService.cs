using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;

namespace KamatekCrm.ApplicationCore.Interfaces
{
    public interface ICustomerInteractionCommandService
    {
        Task<Result<CustomerInteractionDto>> CreateAsync(
            CreateCustomerInteractionDto dto,
            CancellationToken cancellationToken = default);

        Task<Result> UpdateStatusAsync(
            UpdateCustomerInteractionStatusDto dto,
            CancellationToken cancellationToken = default);

        Task<Result> AssignUserAsync(
            int interactionId,
            int userId,
            string username,
            CancellationToken cancellationToken = default);

        Task<Result> ConvertToQuoteAsync(
            int interactionId,
            int quoteId,
            string quoteNumber,
            CancellationToken cancellationToken = default);

        Task<Result> ConvertToServiceJobAsync(
            int interactionId,
            int serviceJobId,
            string jobNo,
            CancellationToken cancellationToken = default);

        Task<Result> SaveDraftAsync(
            string draftJson,
            CancellationToken cancellationToken = default);

        Task<Result<string>> GetDraftAsync(
            CancellationToken cancellationToken = default);

        Task<Result> ClearDraftAsync(
            CancellationToken cancellationToken = default);
    }
}
