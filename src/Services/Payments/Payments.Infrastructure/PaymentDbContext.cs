using Luna.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Payments.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.OrderId).IsRequired();
            entity.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(payment => payment.RowVersion).IsRowVersion();
            entity.HasIndex(payment => payment.OrderId).IsUnique();
            entity.Navigation(payment => payment.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(payment => payment.Attempts)
                .WithOne()
                .HasForeignKey(attempt => attempt.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(attempt => attempt.ProviderReference).HasMaxLength(200).IsRequired();
            entity.Property(attempt => attempt.FailureCode).HasMaxLength(100);
            entity.Property(attempt => attempt.Succeeded).IsRequired();
            entity.Property(attempt => attempt.AttemptedAt).IsRequired();
        });
    }
}
