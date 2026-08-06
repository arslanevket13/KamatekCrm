using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KamatekCrm.ApplicationCore
{
    /// <summary>
    /// Application katmanı servislerini DI konteynerine kaydeden extension metot.
    /// Ana WPF projesindeki ServiceCollectionExtensions tarafından çağrılır.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationLayerServices(this IServiceCollection services)
        {
            // Application Services (Scoped — her UnitOfWork ömrüyle eşleşir)
            services.AddScoped<ICustomerAppService, CustomerAppService>();
            services.AddScoped<IUserAppService, UserAppService>();
            services.AddScoped<IServiceJobAppService, ServiceJobAppService>();
            services.AddSingleton<IServiceJobStatusPolicy, ServiceJobStatusPolicy>();
            services.AddSingleton<IWorkOrderNextActionResolver, WorkOrderNextActionResolver>();
            services.AddSingleton<IApplicationAuthorizationService, ApplicationAuthorizationService>();
            services.AddSingleton<IPersonalDataProtectionService, PersonalDataProtectionService>();

            return services;
        }
    }
}
