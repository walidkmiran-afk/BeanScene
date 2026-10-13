using BeanScene.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Sitting> Sittings => Set<Sitting>();
    public DbSet<StoreHour> StoreHours => Set<StoreHour>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationTable> ReservationTables => Set<ReservationTable>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Area (one) -> Tables (many) ----
        builder.Entity<Area>(e =>
        {
            e.HasIndex(a => a.AreaName).IsUnique();
            e.HasMany(a => a.Tables).WithOne(t => t.Area).HasForeignKey(t => t.AreaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Table>(e =>
        {
            e.HasIndex(t => t.TableName).IsUnique();
        });

        // ---- Sitting (one) -> Reservations (many) ----
        builder.Entity<Sitting>(e =>
        {
            e.Property(s => s.SittingType).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.ClosedReason).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(s => new { s.SittingDate, s.SittingType });
            e.HasMany(s => s.Reservations).WithOne(r => r.Sitting).HasForeignKey(r => r.SittingId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        builder.Entity<StoreHour>(e =>
        {
            e.Property(h => h.SittingType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(h => new { h.DayOfWeek, h.SittingType }).IsUnique();
        });

        // ---- Customer (one) -> Reservations (many) ----
        builder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email);
            e.HasMany(c => c.Reservations).WithOne(r => r.Customer).HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Customer <-> ApplicationUser: one-to-one (optional) ----
            e.HasOne(c => c.User).WithOne(u => u.Customer).HasForeignKey<Customer>(c => c.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Reservation>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Source).HasConversion<string>().HasMaxLength(20);
        });

        // ---- Reservation <-> Table: many-to-many through the ReservationTable join entity ----
        builder.Entity<ReservationTable>(e =>
        {
            e.HasKey(rt => new { rt.ReservationId, rt.TableId });
            e.HasOne(rt => rt.Reservation).WithMany(r => r.ReservationTables)
                .HasForeignKey(rt => rt.ReservationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rt => rt.Table).WithMany(t => t.ReservationTables)
                .HasForeignKey(rt => rt.TableId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Sets CreatedAt / UpdatedAt and generates a new RowVersion. The ORIGINAL RowVersion value is
    /// what EF Core compares in the UPDATE ... WHERE clause, which detects concurrent edits.
    /// </summary>
    private void StampEntities()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
                entry.Entity.RowVersion = Guid.NewGuid();
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.RowVersion = Guid.NewGuid();
            }
        }
    }
}
