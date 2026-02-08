using MediatR;
using KamatekCrm.Application.Common.Models;

namespace KamatekCrm.Application.Common.Interfaces
{
    public interface ICommand : IRequest<Result>
    {
    }

    public interface ICommand<TResponse> : IRequest<TResponse>
    {
    }
}
