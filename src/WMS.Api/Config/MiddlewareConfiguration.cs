namespace WMS.Api.Config;

using Microsoft.AspNetCore.Http.Features;

public static class MiddlewareConfiguration
{
    public static IServiceCollection AddMiddlewareConfiguration(
        this IServiceCollection services)
    {
        return services;
    }
}