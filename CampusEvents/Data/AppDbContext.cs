using CampusEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusEvents.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CampusEvent> Events => Set<CampusEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampusEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.Organizer).HasMaxLength(120);
            e.HasMany(x => x.Registrations)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Registration>(r =>
        {
            r.HasKey(x => x.Id);
            r.Property(x => x.AttendeeName).HasMaxLength(100);
            r.Property(x => x.AttendeeEmail).HasMaxLength(200);
        });
    }
}
