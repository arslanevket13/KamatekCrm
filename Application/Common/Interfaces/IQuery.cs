using MediatR;

namespace KamatekCrm.Application.Common.Interfaces
{
    public interface IQuery<out TResponse> : IRequest<TResponse>
    {
    }
}
