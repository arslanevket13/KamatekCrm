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
            // Application Services (Transient — masaüstü ve web ortamlarında captive dependency riskini önler)
            services.AddTransient<ICustomerAppService, CustomerAppService>();
            services.AddTransient<IUserAppService, UserAppService>();
            services.AddTransient<IServiceJobAppService, ServiceJobAppService>();
            services.AddSingleton<IServiceJobStatusPolicy, ServiceJobStatusPolicy>();
            services.AddSingleton<IWorkOrderNextActionResolver, WorkOrderNextActionResolver>();
            services.AddSingleton<IApplicationAuthorizationService, ApplicationAuthorizationService>();
            services.AddSingleton<IPersonalDataProtectionService, PersonalDataProtectionService>();

            return services;
        }
    }
}
