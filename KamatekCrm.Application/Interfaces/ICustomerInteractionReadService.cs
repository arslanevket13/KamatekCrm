using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;

using KamatekCrm.Shared.Models.Common;

namespace KamatekCrm.ApplicationCore.Interfaces
{
    public interface ICustomerInteractionReadService
    {
        Task<Result<CustomerInteractionDto>> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<Result<PagedResult<CustomerInteractionDto>>> FilterAsync(
            CustomerInteractionFilterDto filter,
            CancellationToken cancellationToken = default);

        Task<Result<List<CustomerPhoneMatchResultDto>>> SearchByPhoneAsync(
            string phoneQuery,
            CancellationToken cancellationToken = default);

        Task<Result<List<CustomerInteractionDto>>> GetByCustomerIdAsync(
            int customerId,
            CancellationToken cancellationToken = default);

        Task<Result<CustomerInteractionSummaryDto>> GetSummaryMetricsAsync(
            CancellationToken cancellationToken = default);

        Task<Result<List<CustomerInteractionDto>>> GetManagerAgendaAsync(
            CancellationToken cancellationToken = default);

        Task<Result<List<CustomerInteractionDto>>> GetOverdueFollowUpsAsync(
            CancellationToken cancellationToken = default);
    }
}
