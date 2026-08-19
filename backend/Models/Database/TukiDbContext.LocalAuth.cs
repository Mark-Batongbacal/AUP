using Microsoft.EntityFrameworkCore;

namespace backend.Models.Database;

public partial class TukiDbContext
{
    public virtual DbSet<LocalUserCredential> LocalUserCredentials { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
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
    }
}
