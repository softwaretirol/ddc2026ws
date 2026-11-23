using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConferenceApp.Api.Data;

public class ConferenceDbContext(DbContextOptions<ConferenceDbContext> options) : DbContext(options)
{
    public DbSet<Conference> Conferences => Set<Conference>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Speaker> Speakers => Set<Speaker>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Conference>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.Property(x => x.Location).HasMaxLength(200);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Track>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Color).HasMaxLength(7);
            e.HasOne(x => x.Conference).WithMany(c => c.Tracks).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Room>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Conference).WithMany(c => c.Rooms).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Speaker>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Company).HasMaxLength(200);
            e.Property(x => x.PhotoUrl).HasMaxLength(500);
        });

        b.Entity<Session>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasOne(x => x.Conference).WithMany(c => c.Sessions).OnDelete(DeleteBehavior.Cascade);
            // Raum mit Sessions darf nicht geloescht werden (API liefert 409); NoAction erlaubt das Cascade ueber die Konferenz.
            e.HasOne(x => x.Room).WithMany().OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Track).WithMany().OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Speakers).WithMany(s => s.Sessions).UsingEntity("SessionSpeaker");
            e.HasIndex(x => new { x.RoomId, x.Day, x.StartTime });
        });
    }
}

/// <summary>Nur fuer "dotnet ef migrations" – zur Laufzeit kommt der Connection-String von Aspire.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<ConferenceDbContext>
{
    public ConferenceDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ConferenceDbContext>()
            .UseNpgsql("Host=localhost;Database=conferencedb;Username=postgres;Password=postgres")
            .Options);
}
