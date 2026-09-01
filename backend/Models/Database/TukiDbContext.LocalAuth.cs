using Microsoft.EntityFrameworkCore;

namespace backend.Models.Database;

public partial class TukiDbContext
{
    public virtual DbSet<LocalUserCredential> LocalUserCredentials { get; set; }
    public virtual DbSet<TricyclePointSubmission> TricyclePointSubmissions { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransportRoute>(entity =>
        {
            entity.Property(e => e.ArchivedAt).HasColumnType("datetime2(7)");
            entity.HasQueryFilter(e => e.ArchivedAt == null);
        });

        modelBuilder.Entity<LocalUserCredential>(entity =>
        {
            entity.ToTable("LocalUserCredentials", "dbo");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<LocalUserCredential>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_LocalUserCredentials_UserProfiles");
        });

        modelBuilder.Entity<TricyclePointSubmission>(entity =>
        {
            entity.ToTable("TricyclePointSubmissions", "dbo");
            entity.HasKey(e => e.TricyclePointSubmissionId);

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_TricyclePointSubmissions_StatusCreatedAt");
            entity.HasIndex(e => new { e.SubmittedByUserId, e.CreatedAt }, "IX_TricyclePointSubmissions_SubmitterCreatedAt");
            entity.HasIndex(e => new { e.Latitude, e.Longitude }, "IX_TricyclePointSubmissions_Coordinates");

            entity.Property(e => e.ProofImageUrl).HasMaxLength(1000);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.AdminLatitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.AdminLongitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.AccuracyMeters).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.LocationCapturedAt).HasColumnType("datetimeoffset(7)");
            entity.Property(e => e.SuggestedTodaName).HasMaxLength(200);
            entity.Property(e => e.SuggestedLandmark).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Pending");
            entity.Property(e => e.AdminPointName).HasMaxLength(200);
            entity.Property(e => e.AdminOperatorName).HasMaxLength(200);
            entity.Property(e => e.AdminAddress).HasMaxLength(500);
            entity.Property(e => e.AdminLandmark).HasMaxLength(300);
            entity.Property(e => e.AdminDescription).HasMaxLength(500);
            entity.Property(e => e.AdminNotes).HasMaxLength(1000);
            entity.Property(e => e.ReviewedAt).HasColumnType("datetimeoffset(7)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetimeoffset(7)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetimeoffset(7)");

            entity.HasOne<UserProfile>()
                .WithMany()
                .HasForeignKey(e => e.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TricyclePointSubmissions_SubmittedByUser");

            entity.HasOne<UserProfile>()
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TricyclePointSubmissions_ReviewedByUser");

            entity.HasOne<TricyclePoint>()
                .WithMany()
                .HasForeignKey(e => e.PublishedTricyclePointId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TricyclePointSubmissions_PublishedTricyclePoint");
        });
    }
}
