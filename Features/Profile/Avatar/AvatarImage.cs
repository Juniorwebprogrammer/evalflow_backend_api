namespace evalflow_backend_api.Features.Profile.Avatar;

/// <summary>
/// Validates an uploaded profile picture: base64 payload, size limit and a
/// real JPEG / PNG / WebP signature (the declared content type alone is not
/// trusted — the stored type is the one detected from the bytes).
/// </summary>
public static class AvatarImage
{
    /// <summary>The frontend resizes to 256×256 before uploading, so this is generous.</summary>
    public const int MaxBytes = 512 * 1024;

    public static (byte[] Data, string ContentType)? TryDecode(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;

        // Accept a full data URL ("data:image/png;base64,...") as well as the bare payload.
        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0
            ? base64[(comma + 1)..]
            : base64;

        byte[] data;
        try
        {
            data = Convert.FromBase64String(payload.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        if (data.Length == 0 || data.Length > MaxBytes) return null;

        var contentType = DetectContentType(data);
        return contentType is null ? null : (data, contentType);
    }

    private static string? DetectContentType(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return "image/png";

        if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
            return "image/webp";

        return null;
    }
}
