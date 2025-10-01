namespace Vanq.Application.Configuration;

public sealed class RbacOptions
{
    public const string SectionName = "Rbac";

    public bool FeatureEnabled { get; set; } = true;

    public string DefaultRole { get; set; } = "viewer";
}
