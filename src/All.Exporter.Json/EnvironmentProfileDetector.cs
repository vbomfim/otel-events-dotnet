namespace All.Exporter.Json;

/// <summary>
/// Detects the <see cref="AllEnvironmentProfile"/> from well-known
/// environment variables when not explicitly configured.
/// </summary>
/// <remarks>
/// Checks <c>ASPNETCORE_ENVIRONMENT</c> first (ASP.NET Core standard),
/// then <c>DOTNET_ENVIRONMENT</c> (.NET Generic Host standard).
/// Maps known values ("Development", "Staging", "Production") to the
/// corresponding enum value. Unknown or absent values default to
/// <see cref="AllEnvironmentProfile.Production"/> (most restrictive).
/// </remarks>
internal static class EnvironmentProfileDetector
{
    /// <summary>
    /// Detects the environment profile from host environment variables.
    /// </summary>
    /// <param name="getEnvironmentVariable">
    /// Optional function to read environment variables.
    /// Defaults to <see cref="Environment.GetEnvironmentVariable(string)"/>.
    /// Accepts a custom function for testability.
    /// </param>
    /// <returns>
    /// The detected <see cref="AllEnvironmentProfile"/>, or
    /// <see cref="AllEnvironmentProfile.Production"/> if no known value is found.
    /// </returns>
    internal static AllEnvironmentProfile Detect(
        Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        var environmentName =
            getEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? getEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (environmentName is null)
        {
            return AllEnvironmentProfile.Production;
        }

        if (environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return AllEnvironmentProfile.Development;
        }

        if (environmentName.Equals("Staging", StringComparison.OrdinalIgnoreCase))
        {
            return AllEnvironmentProfile.Staging;
        }

        return AllEnvironmentProfile.Production;
    }
}
