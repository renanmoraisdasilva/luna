using Luna.Payments.Application.Authorization;
using Luna.Payments.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Luna.Payments.Infrastructure.Repositories;

public sealed class PaymentWriteRepository(PaymentDbContext db) : IPaymentWriteRepository
{
    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Payments
            .Include(payment => payment.Attempts)
            .SingleOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        await db.Payments.AddAsync(payment, cancellationToken);
    }

}

public sealed class PaymentUnitOfWork(PaymentDbContext db) : IPaymentUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateOrderConstraint(exception))
        {
            throw new InvalidOperationException(
                "A payment already exists for this order.",
                exception);
        }
    }

    private static bool IsDuplicateOrderConstraint(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && (sqlException.Number is 2601 or 2627)
        && sqlException.Message.Contains("OrderId", StringComparison.OrdinalIgnoreCase);
}
