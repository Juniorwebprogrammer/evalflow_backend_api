namespace evalflow_backend_api.Domain.Entities;

public class RoleFeature
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string FeatureCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}