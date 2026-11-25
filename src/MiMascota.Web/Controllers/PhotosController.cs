using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MiMascota.Web.Security;
using MiMascota.Web.Services;

namespace MiMascota.Web.Controllers;

/// <summary>
/// Serves the pets' photos to logged-in staff (global fallback policy). The files live outside
/// wwwroot, so they are never reachable as static files.
/// </summary>
[Authorize(Roles = Roles.AnyStaff)]
public class PhotosController(IPetPhotoStorage photos) : Controller
{
    // GET /fotos/mascotas/0f8c…e1.webp
    [HttpGet("fotos/mascotas/{fileName}")]
    public IActionResult Pet(string fileName)
    {
        var path = photos.Find(fileName); // only "{guid}.webp" names, never sub-folders or ".."
        if (path is null)
        {
            return NotFound();
        }

        // "private": proxies must not store it. "no-cache": the browser may keep a copy but must ask
        // again each time (a cheap 304 thanks to ETag/Last-Modified), so the login is checked on every
        // view and a photo is not shown from the cache after logging out.
        Response.Headers.CacheControl = "private, no-cache";
        // A replaced photo always gets a new random name, so the name works as the ETag.
        var lastModified = new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        return PhysicalFile(path, "image/webp", lastModified, new EntityTagHeaderValue($"\"{fileName[..32]}\""));
    }
}
