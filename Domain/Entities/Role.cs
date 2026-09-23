namespace evalflow_backend_api.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<RoleFeature> Features { get; set; } = new List<RoleFeature>();
}