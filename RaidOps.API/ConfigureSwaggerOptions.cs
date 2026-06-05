using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics.CodeAnalysis;

namespace RaidOps.API;

[ExcludeFromCodeCoverage]
public class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var groupName in provider.ApiVersionDescriptions.Select(d => d.GroupName))
            options.SwaggerDoc(groupName, new OpenApiInfo { 
                Title = "RaidOps API", 
                Version = groupName 
            });
    }
}