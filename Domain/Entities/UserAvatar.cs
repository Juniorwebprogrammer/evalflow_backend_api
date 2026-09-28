using System.ComponentModel.DataAnnotations;

namespace evalflow_backend_api.Domain.Entities;

/// <summary>
/// A user's profile picture. Kept in its own table (1:1 with <see cref="User"/>)
/// so loading users never drags the image bytes along.
/// </summary>
public class UserAvatar
{
    public int UserId { get; set; }

    public required byte[] Data { get; set; }

    [MaxLength(32)]
    public required string ContentType { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
