namespace MiMascota.Web.Services;

/// <summary>Stores the pets' photos on disk, under wwwroot/uploads/mascotas.</summary>
public interface IPetPhotoStorage
{
    /// <summary>Checks size and real image type (by magic bytes). Returns the file extension to use.</summary>
    Result<string> Validate(IFormFile file);

    /// <summary>Validates, re-encodes (WebP, no metadata, max 1000 px) and saves the photo with a random name. Returns its web path ("/uploads/mascotas/….webp").</summary>
    Task<Result<string>> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>Deletes a photo saved by this storage. Ignores null and paths outside the uploads folder.</summary>
    void Delete(string? photoPath);
}

public class PetPhotoStorage(string webRootPath) : IPetPhotoStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string WebFolder = "/uploads/mascotas/";

    private const int HeaderLength = 12;

    private string PhysicalFolder => Path.Combine(webRootPath, "uploads", "mascotas");

    public Result<string> Validate(IFormFile file)
    {
        if (file.Length == 0)
        {
            return Result<string>.Failure("La foto está vacía. Elige otra imagen.");
        }

        if (file.Length > MaxBytes)
        {
            return Result<string>.Failure("La foto pesa más de 5 MB. Elige una imagen más ligera.");
        }

        var header = new byte[HeaderLength];
        using (var stream = file.OpenReadStream())
        {
            var read = 0;
            while (read < HeaderLength)
            {
                var n = stream.Read(header, read, HeaderLength - read);
                if (n == 0) break;
                read += n;
            }
            if (read < HeaderLength) Array.Resize(ref header, read);
        }

        var extension = DetectExtension(header);
        return extension is null
            ? Result<string>.Failure("El archivo no es una imagen válida. Usa una foto JPG, PNG o WebP.")
            : Result<string>.Success(extension);
    }

    public async Task<Result<string>> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var validation = Validate(file);
        if (validation.IsFailure)
        {
            return validation;
        }

        // The magic bytes only say what the file claims to be: decode it completely and re-encode it.
        // This rejects corrupt or truncated images and drops EXIF metadata such as the GPS location.
        byte[] original;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            original = buffer.ToArray();
        }

        var webp = PhotoNormalizer.ToWebp(original);
        if (webp is null)
        {
            return Result<string>.Failure("La foto está dañada o incompleta. Elige otra imagen.");
        }

        Directory.CreateDirectory(PhysicalFolder);
        // Never trust the client's file name: a random name, always saved as WebP.
        var fileName = $"{Guid.NewGuid():N}.webp";
        await File.WriteAllBytesAsync(Path.Combine(PhysicalFolder, fileName), webp, cancellationToken);

        return Result<string>.Success(WebFolder + fileName);
    }

    public void Delete(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath) || !photoPath.StartsWith(WebFolder, StringComparison.Ordinal))
        {
            return;
        }

        var fileName = photoPath[WebFolder.Length..];
        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName))
        {
            return; // no sub-folders or "..": only files this storage created
        }

        try
        {
            File.Delete(Path.Combine(PhysicalFolder, fileName));
        }
        catch (IOException)
        {
            // A missing or locked file must not break saving the pet.
        }
    }

    /// <summary>".jpg", ".png" or ".webp" from the first bytes of the file; null for anything else.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (header.StartsWith(png))
        {
            return ".png";
        }

        // WebP: "RIFF" + 4 bytes of size + "WEBP"
        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }
}
