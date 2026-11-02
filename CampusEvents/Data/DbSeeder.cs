using CampusEvents.Models;

namespace CampusEvents.Data;

public static class DbSeeder
{
    public static void SeedIfEmpty(AppDbContext db)
    {
        if (db.Events.Any())
        {
            return;
        }

        DateTime now = DateTime.UtcNow;

        db.Events.AddRange(
        [
            new CampusEvent
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Intro to Azure for Students",
                Description = "Hands-on workshop: Azure Portal, CLI, and your first App Service deploy.",
                Location = "Campus Lab A201",
                StartsAtUtc = now.AddDays(7).Date.AddHours(14),
                Capacity = 30,
                CreatedAtUtc = now.AddDays(-3)
            },
            new CampusEvent
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "DevOps Coffee Hour",
                Description = "Informal meetup about CI/CD pipelines, GitHub Actions, and release habits.",
                Location = "Student Lounge",
                StartsAtUtc = now.AddDays(14).Date.AddHours(16),
                Capacity = 20,
                CreatedAtUtc = now.AddDays(-2),
                Registrations =
                [
                    new Registration
                    {
                        Id = Guid.NewGuid(),
                        AttendeeName = "Alex Example",
                        AttendeeEmail = "alex@example.com",
                        RegisteredAtUtc = now.AddDays(-1)
                    }
                ]
            },
            new CampusEvent
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Title = "Cloud Cost Clinic",
                Description = "Learn budgets, tags, and how to avoid surprise Azure bills.",
                Location = "Online / Teams",
                StartsAtUtc = now.AddDays(21).Date.AddHours(12),
                Capacity = 50,
                CreatedAtUtc = now.AddDays(-1)
            }
        ]);

        db.SaveChanges();
    }
}
