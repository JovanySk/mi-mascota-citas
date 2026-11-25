using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;

namespace MiMascota.Web.ViewComponents;

/// <summary>Number of unread contact messages, shown as a badge next to "Mensajes" in the staff nav.</summary>
public class UnreadMessagesViewComponent(AppDbContext db, ILogger<UnreadMessagesViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        int unread;
        try
        {
            unread = await db.ContactMessages.CountAsync(m => !m.IsRead);
        }
        catch (Exception e)
        {
            // The nav must never break a page (e.g. the error page when the database is down).
            logger.LogWarning(e, "Could not count the unread messages.");
            unread = 0;
        }
        return View(unread);
    }
}
