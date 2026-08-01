using KamatekCrm.Shared.Models;

namespace KamatekCrm.Shared.Services
{
    public interface IQuotePdfService
    {
        void GenerateProjectQuote(ServiceProject project, List<ScopeNode> rootNodes, string filePath);
        void GenerateStandardQuote(Quote quote, string filePath);
    }
}
