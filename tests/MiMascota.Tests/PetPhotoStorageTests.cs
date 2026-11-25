using Microsoft.AspNetCore.Http;
using MiMascota.Web.Services;

namespace MiMascota.Tests;

public sealed class PetPhotoStorageTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"mimascota-photos-{Guid.NewGuid():N}");
    private readonly PetPhotoStorage _storage;

    public PetPhotoStorageTests()
    {
        _storage = new PetPhotoStorage(_webRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }

    // First bytes of each accepted format, followed by some filler.
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49];
    public static readonly byte[] WebP = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    public static IFormFile File(byte[] content, string fileName = "foto.jpg", string contentType = "image/jpeg")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "Photo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public static TheoryData<byte[], string> AcceptedImages => new()
    {
        { Jpeg, ".jpg" },
        { Png, ".png" },
        { WebP, ".webp" }
    };

    [Theory]
    [MemberData(nameof(AcceptedImages))]
    public void Accepts_jpeg_png_and_webp_by_their_magic_bytes(byte[] content, string expectedExtension)
    {
        // The client name and content type are irrelevant: only the bytes count.
        var result = _storage.Validate(File(content, "lo-que-sea.txt", "text/plain"));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedExtension, result.Value);
    }

    [Fact]
    public void Rejects_a_file_that_only_pretends_to_be_an_image()
    {
        var fake = "<script>alert(1)</script>"u8.ToArray();

        var result = _storage.Validate(File(fake, "gato.jpg", "image/jpeg"));

        Assert.True(result.IsFailure);
        Assert.Contains("no es una imagen válida", result.Error);
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 })] // GIF
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 0x00, 0x00, 0x00 })] // PDF
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 })] // RIFF but WAVE
    [InlineData(new byte[] { 0xFF, 0xD8 })] // truncated JPEG
    public void Rejects_other_file_types(byte[] content)
    {
        Assert.True(_storage.Validate(File(content)).IsFailure);
    }

    [Fact]
    public void Rejects_photos_bigger_than_5_MB()
    {
        var big = new byte[PetPhotoStorage.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);

        var result = _storage.Validate(File(big));

        Assert.True(result.IsFailure);
        Assert.Contains("5 MB", result.Error);
    }

    [Fact]
    public void Accepts_a_photo_of_exactly_5_MB()
    {
        var limit = new byte[PetPhotoStorage.MaxBytes];
        Png.CopyTo(limit, 0);

        Assert.True(_storage.Validate(File(limit)).IsSuccess);
    }

    [Fact]
    public void Rejects_an_empty_file()
    {
        Assert.True(_storage.Validate(File([])).IsFailure);
    }

    [Fact]
    public async Task Saves_with_a_random_name_and_the_detected_extension()
    {
        var result = await _storage.SaveAsync(File(Png, "../../evil name.jpg"));

        Assert.True(result.IsSuccess);
        Assert.StartsWith(PetPhotoStorage.WebFolder, result.Value);
        Assert.EndsWith(".png", result.Value);
        Assert.DoesNotContain("evil", result.Value);

        var physical = Path.Combine(_webRoot, "uploads", "mascotas", Path.GetFileName(result.Value!));
        Assert.Equal(Png, await System.IO.File.ReadAllBytesAsync(physical));
    }

    [Fact]
    public async Task Does_not_save_an_invalid_file()
    {
        var result = await _storage.SaveAsync(File("hola"u8.ToArray()));

        Assert.True(result.IsFailure);
        Assert.False(Directory.Exists(Path.Combine(_webRoot, "uploads", "mascotas"))
                     && Directory.EnumerateFiles(Path.Combine(_webRoot, "uploads", "mascotas")).Any());
    }

    [Fact]
    public async Task Deletes_a_saved_photo()
    {
        var saved = (await _storage.SaveAsync(File(Jpeg))).Value!;
        var physical = Path.Combine(_webRoot, "uploads", "mascotas", Path.GetFileName(saved));
        Assert.True(System.IO.File.Exists(physical));

        _storage.Delete(saved);

        Assert.False(System.IO.File.Exists(physical));
    }

    [Fact]
    public void Delete_ignores_paths_outside_the_uploads_folder()
    {
        var outside = Path.Combine(_webRoot, "keep.txt");
        Directory.CreateDirectory(_webRoot);
        System.IO.File.WriteAllText(outside, "no borrar");

        _storage.Delete("/uploads/mascotas/../../keep.txt");
        _storage.Delete("/img/mascotas/avatar/perico.webp");
        _storage.Delete(null);

        Assert.True(System.IO.File.Exists(outside));
    }
}
