using BulkImportPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BulkImportPlatform.Infrastructure.Data;

public class BulkImportDbContext : DbContext
{
    public BulkImportDbContext(DbContextOptions<BulkImportDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();
    public DbSet<ValidationResult> ValidationResults => Set<ValidationResult>();
    public DbSet<MappingProfile> MappingProfiles => Set<MappingProfile>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ClientAccount> ClientAccounts => Set<ClientAccount>();
    public DbSet<RpaAttempt> RpaAttempts => Set<RpaAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(e =>
        {
            // Unique per client, not globally - two different clients may legitimately use the
            // same vehicle number. A bare single-column unique index would reject the second
            // client's upload with a raw DB constraint violation.
            e.HasIndex(v => new { v.ClientAccountId, v.VehicleNumber }).IsUnique();
            e.Property(v => v.VehicleNumber).IsRequired().HasMaxLength(64);
            e.Property(v => v.TankSize).HasColumnType("decimal(18,2)");
            e.Property(v => v.Odometer).HasColumnType("decimal(18,2)");

            e.HasOne(v => v.ClientAccount)
                .WithMany()
                .HasForeignKey(v => v.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.CustomerName).IsRequired().HasMaxLength(256);

            e.HasOne(c => c.ClientAccount)
                .WithMany()
                .HasForeignKey(c => c.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportJob>(e =>
        {
            e.HasMany(j => j.Records)
                .WithOne(r => r.ImportJob)
                .HasForeignKey(r => r.ImportJobId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(j => j.AuditLogs)
                .WithOne(a => a.ImportJob)
                .HasForeignKey(a => a.ImportJobId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(j => j.MappingProfile)
                .WithMany()
                .HasForeignKey(j => j.MappingProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            // Restrict (not cascade/SetNull-on-delete-of-account): a ClientAccount referenced by
            // job history must be retired via IsActive, never deleted out from under that history.
            e.HasOne(j => j.ClientAccount)
                .WithMany()
                .HasForeignKey(j => j.ClientAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportRecord>(e =>
        {
            e.HasMany(r => r.ValidationResults)
                .WithOne(v => v.ImportRecord)
                .HasForeignKey(v => v.ImportRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(r => r.RpaAttempts)
                .WithOne(a => a.ImportRecord)
                .HasForeignKey(a => a.ImportRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientAccount>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.Property(c => c.IrentosoftUsername).IsRequired().HasMaxLength(256);
            e.Property(c => c.EncryptedPassword).IsRequired();
        });
    }
}
