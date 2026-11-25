namespace MiMascota.Web.Services;

/// <summary>
/// Stores the pets' photos on disk in a private folder (App_Data/uploads/mascotas by default, outside wwwroot),
/// so they are not static files: PhotosController serves them to logged-in staff only.
/// </summary>
public interface IPetPhotoStorage
{
    /// <summary>Checks size and real image type (by magic bytes). Returns the file extension to use.</summary>
    Result<string> Validate(IFormFile file);

    /// <summary>Validates, re-encodes (WebP, no metadata, max 1000 px) and saves the photo with a random name. Returns its web path ("/fotos/mascotas/….webp").</summary>
    Task<Result<string>> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>Deletes a photo saved by this storage. Ignores null and paths outside the uploads folder.</summary>
    void Delete(string? photoPath);

    /// <summary>
    /// Physical path of a stored photo, or null when the name is not one this storage creates
    /// ("{32 hex chars}.webp") or the file does not exist.
    /// </summary>
    string? Find(string fileName);
}

/// <param name="folder">Physical folder for the files (absolute path).</param>
public partial class PetPhotoStorage(string folder) : IPetPhotoStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>URL prefix of the photos (see PhotosController).</summary>
    public const string WebFolder = "/fotos/mascotas/";

    /// <summary>Default folder, relative to the app's content root ("PetPhotos:Folder" changes it).</summary>
    public static readonly string DefaultFolder = Path.Combine("App_Data", "uploads", "mascotas");

    private const int HeaderLength = 12;

    public string PhysicalFolder { get; } = folder;

    /// <summary>The only file names this storage creates: a GUID without dashes plus ".webp".</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9a-f]{32}\.webp$")]
    private static partial System.Text.RegularExpressions.Regex StoredName();

    public static bool IsStoredName(string? fileName) => fileName is not null && StoredName().IsMatch(fileName);

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
        if (!IsStoredName(fileName))
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

    public string? Find(string fileName)
    {
        if (!IsStoredName(fileName))
        {
            return null;
        }

        var path = Path.Combine(PhysicalFolder, fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Moves photos saved by older versions in the public folder (wwwroot/uploads/mascotas) to the
    /// private one. Their PhotoPath values are rewritten by the database setup.
    /// </summary>
    public int MoveLegacyPhotos(string legacyFolder)
    {
        if (!Directory.Exists(legacyFolder))
        {
            return 0;
        }

        var moved = 0;
        foreach (var file in Directory.EnumerateFiles(legacyFolder))
        {
            var name = Path.GetFileName(file);
            if (!IsStoredName(name)) continue;

            Directory.CreateDirectory(PhysicalFolder);
            var target = Path.Combine(PhysicalFolder, name);
            try
            {
                if (File.Exists(target)) File.Delete(file);
                else File.Move(file, target);
                moved++;
            }
            catch (IOException)
            {
                // best effort: a locked file stays where it is
            }
        }
        return moved;
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
