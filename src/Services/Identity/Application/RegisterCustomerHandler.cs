using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Luna.Identity.Application;

public sealed record RegisterCustomerCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public sealed record RegisterCustomerResult(
    bool Succeeded,
    string? UserId,
    IEnumerable<IdentityError> Errors)
{
    public static RegisterCustomerResult Failure(IEnumerable<IdentityError> errors) =>
        new(false, null, errors);

    public static RegisterCustomerResult Success(string userId) =>
        new(true, userId, []);
}

public sealed class RegisterCustomerHandler(
    UserManager<LunaUser> userManager,
    LunaIdentityDbContext dbContext)
{
    public async Task<RegisterCustomerResult> HandleAsync(
        RegisterCustomerCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var user = new LunaUser
        {
            UserName = command.Email,
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
        };

        var userResult = await userManager.CreateAsync(user, command.Password);
        if (!userResult.Succeeded)
        {
            await RollbackAsync(transaction, cancellationToken);
            return RegisterCustomerResult.Failure(userResult.Errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, IdentityRoles.Customer);
        if (!roleResult.Succeeded)
        {
            await RollbackAsync(transaction, cancellationToken);
            return RegisterCustomerResult.Failure(roleResult.Errors);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return RegisterCustomerResult.Success(user.Id);
    }

    private static Task RollbackAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null
            ? Task.CompletedTask
            : transaction.RollbackAsync(cancellationToken);
}