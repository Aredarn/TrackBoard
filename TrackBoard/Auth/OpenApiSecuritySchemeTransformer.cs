using Microsoft.AspNetCore.OpenApi;
// Microsoft.OpenApi 2.x moved these types out of the .Models namespace into the root.
using Microsoft.OpenApi;

namespace TrackBoard.Auth;

/// <summary>
/// Declares the bearer scheme on the OpenAPI document so the docs UI can attach a token.
/// Without this the generated document has no security definition at all.
/// </summary>
public sealed class OpenApiSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the accessToken returned by /api/auth/login.",
        };

        return Task.CompletedTask;
    }
}
