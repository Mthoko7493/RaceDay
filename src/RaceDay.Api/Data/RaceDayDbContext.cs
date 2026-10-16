using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core model that mirrors the Part 1 ERD and SQL script
/// (same table names, column names, keys, constraints and delete behaviour).
/// </summary>
public class RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.RoleID);
            e.Property(x => x.RoleName).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.RoleName).IsUnique();
        });

        mb.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserID);
            e.Property(x => x.FullName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleID)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Users_Roles");
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(x => x.EventID);
            e.Property(x => x.Name).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000).IsUnicode(false);
            e.Property(x => x.EventDate).HasColumnType("datetime");
            e.Property(x => x.Location).HasMaxLength(150).IsUnicode(false).IsRequired();
            e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
            e.Property(x => x.EventType).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents).HasForeignKey(x => x.OrganiserID)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Events_Organiser");
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryID);
            e.Property(x => x.CategoryName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories).HasForeignKey(x => x.EventID)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Categories_Events");
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
            e.HasKey(x => x.EnrolmentID);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired().HasDefaultValue(EnrolmentStatus.Pending);
            e.HasIndex(x => new { x.ParticipantID, x.EventID }).IsUnique().HasDatabaseName("UQ_Enrolments_ParticipantEvent");
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments).HasForeignKey(x => x.ParticipantID)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Enrolments_Users");
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments).HasForeignKey(x => x.EventID)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Enrolments_Events");
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments).HasForeignKey(x => x.CategoryID)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Enrolments_Categories");
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Results");
            e.HasKey(x => x.ResultID);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.RecordedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.EnrolmentID).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result).HasForeignKey<Result>(x => x.EnrolmentID)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Results_Enrolments");
        });
    }
}
