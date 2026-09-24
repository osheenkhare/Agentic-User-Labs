namespace WorkIqLab;

internal sealed class WorkIqSettings
{
    internal WorkIqSettings(IConfiguration configuration)
    {
        TenantId = configuration["WorkIQ:TenantId"] ?? "";
        if (!Guid.TryParse(TenantId, out _))
        {
            throw new InvalidOperationException("WorkIQ:TenantId must be a tenant GUID.");
        }
    }

    internal string TenantId { get; }
}
