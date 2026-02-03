using BudgetWise.Domain.Entities;
using BudgetWise.Domain.Enums;

namespace BudgetWise.Application.Interfaces;

/// <summary>
/// Repository for Account entities with specialized queries.
/// </summary>
public interface IAccountRepository : IRepository<Account>
{
    Task<IReadOnlyList<Account>> GetActiveAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetOnBudgetAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetByTypeAsync(AccountType type, CancellationToken ct = default);
    Task<Account?> GetByNameAsync(string name, CancellationToken ct = default);
}
