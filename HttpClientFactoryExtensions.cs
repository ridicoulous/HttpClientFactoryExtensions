using HttpClientFactoryExtensions.Loggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using System;

namespace HttpClientFactoryExtensions
{
    public static class HttpClientFactoryExtensions
    {
        public static void LogTimingAndRequestId(this IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, CustomLoggingFilter>());
        }
    }
}
