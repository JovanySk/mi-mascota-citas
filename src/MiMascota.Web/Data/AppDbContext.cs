using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Models;

namespace MiMascota.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<VetService> VetServices => Set<VetService>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Owner>(entity =>
        {
            entity.Property(o => o.Name).HasMaxLength(100);
            entity.Property(o => o.Phone).HasMaxLength(20);
            entity.Property(o => o.Email).HasMaxLength(150);
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(60);
            entity.Property(p => p.Species).HasMaxLength(40);
            entity.Property(p => p.Breed).HasMaxLength(60);
            entity.Property(p => p.PhotoPath).HasMaxLength(200);

            // An owner with pets cannot be deleted by accident.
            entity.HasOne(p => p.Owner)
                  .WithMany(o => o.Pets)
                  .HasForeignKey(p => p.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VetService>(entity =>
        {
            entity.Property(s => s.Name).HasMaxLength(80);
            entity.Property(s => s.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(a => a.Notes).HasMaxLength(500);

            // Store the status as text ("Pending", "Confirmed"...) so the table is readable.
            entity.Property(a => a.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasOne(a => a.Pet)
                  .WithMany(p => p.Appointments)
                  .HasForeignKey(a => a.PetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.VetService)
                  .WithMany(s => s.Appointments)
                  .HasForeignKey(a => a.VetServiceId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Availability and overlap queries filter by start time.
            entity.HasIndex(a => a.Start);
        });

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.Property(m => m.Name).HasMaxLength(100);
            entity.Property(m => m.Contact).HasMaxLength(150);
            entity.Property(m => m.Message).HasMaxLength(1000);

            // The staff inbox lists the newest messages first.
            entity.HasIndex(m => m.CreatedAt);
        });
    }
}
