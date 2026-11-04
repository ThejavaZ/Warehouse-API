using Microsoft.AspNetCore.Http.Features;

namespace WMS.Api.Config;

public static class UploadConfiguration
{
    public static IServiceCollection AddUploadConfiguration(
        this IServiceCollection services)
    {
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 104857600;
        });

        return services;
    }
}