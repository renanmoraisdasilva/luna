using Luna.Payments.Domain;

namespace Luna.Payments.Application.Authorization;

public interface IPaymentWriteRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}

public interface IPaymentUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
