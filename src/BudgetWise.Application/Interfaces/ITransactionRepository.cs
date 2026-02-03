using BudgetWise.Domain.Entities;
using BudgetWise.Domain.ValueObjects;

namespace BudgetWise.Application.Interfaces;

/// <summary>
/// Repository for Transaction entities with specialized queries.
/// </summary>
public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByEnvelopeAsync(Guid envelopeId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByDateRangeAsync(DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByAccountAndDateRangeAsync(Guid accountId, DateRange range, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetUnclearedAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetUnassignedAsync(CancellationToken ct = default);
    Task<Money> GetAccountBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<Money> GetAccountClearedBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<Money> GetEnvelopeSpentAsync(Guid envelopeId, DateRange range, CancellationToken ct = default);
}
