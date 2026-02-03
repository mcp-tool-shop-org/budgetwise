using BudgetWise.Application.DTOs;
using BudgetWise.Application.Interfaces;
using BudgetWise.Domain.Entities;
using BudgetWise.Domain.ValueObjects;

namespace BudgetWise.Application.Services;

/// <summary>
/// Service for managing transactions.
/// </summary>
public sealed class TransactionService
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Create an outflow (expense) transaction.
    /// </summary>
    public async Task<Transaction> CreateOutflowAsync(
        CreateOutflowRequest request,
        CancellationToken ct = default)
    {
        // Validate account exists
        var account = await _unitOfWork.Accounts.GetByIdAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        // Validate envelope if provided
        if (request.EnvelopeId.HasValue)
        {
            var envelope = await _unitOfWork.Envelopes.GetByIdAsync(request.EnvelopeId.Value, ct)
                ?? throw new InvalidOperationException($"Envelope {request.EnvelopeId} not found.");
        }

        var transaction = Transaction.CreateOutflow(
            request.AccountId,
            request.Date,
            request.Amount,
            request.Payee,
            request.EnvelopeId,
            request.Memo
        );

        await _unitOfWork.Transactions.AddAsync(transaction, ct);

        // Update payee usage
        var payee = await _unitOfWork.Payees.GetOrCreateAsync(request.Payee, ct);
        payee.RecordUsage();
        await _unitOfWork.Payees.UpdateAsync(payee, ct);

        // Recalculate account balance
        await UpdateAccountBalanceAsync(request.AccountId, ct);

        return transaction;
    }

    /// <summary>
    /// Create an inflow (income) transaction.
    /// </summary>
    public async Task<Transaction> CreateInflowAsync(
        CreateInflowRequest request,
        CancellationToken ct = default)
    {
        // Validate account exists
        var account = await _unitOfWork.Accounts.GetByIdAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        var transaction = Transaction.CreateInflow(
            request.AccountId,
            request.Date,
            request.Amount,
            request.Payee,
            null, // Inflows don't get assigned to envelopes directly
            request.Memo
        );

        await _unitOfWork.Transactions.AddAsync(transaction, ct);

        // Update payee
        var payee = await _unitOfWork.Payees.GetOrCreateAsync(request.Payee, ct);
        payee.RecordUsage();
        await _unitOfWork.Payees.UpdateAsync(payee, ct);

        // Update account balance
        await UpdateAccountBalanceAsync(request.AccountId, ct);

        // Update budget period income
        await UpdatePeriodIncomeAsync(request.Date, ct);

        return transaction;
    }

    /// <summary>
    /// Create a transfer between two accounts.
    /// </summary>
    public async Task<(Transaction from, Transaction to)> CreateTransferAsync(
        CreateTransferRequest request,
        CancellationToken ct = default)
    {
        // Validate both accounts exist
        var fromAccount = await _unitOfWork.Accounts.GetByIdAsync(request.FromAccountId, ct)
            ?? throw new InvalidOperationException($"From account {request.FromAccountId} not found.");

        var toAccount = await _unitOfWork.Accounts.GetByIdAsync(request.ToAccountId, ct)
            ?? throw new InvalidOperationException($"To account {request.ToAccountId} not found.");

        var (fromTx, toTx) = Transaction.CreateTransfer(
            request.FromAccountId,
            request.ToAccountId,
            request.Date,
            request.Amount,
            request.Memo
        );

        await _unitOfWork.Transactions.AddAsync(fromTx, ct);
        await _unitOfWork.Transactions.AddAsync(toTx, ct);

        // Update both account balances
        await UpdateAccountBalanceAsync(request.FromAccountId, ct);
        await UpdateAccountBalanceAsync(request.ToAccountId, ct);

        return (fromTx, toTx);
    }

    /// <summary>
    /// Update an existing transaction.
    /// </summary>
    public async Task<Transaction> UpdateTransactionAsync(
        UpdateTransactionRequest request,
        CancellationToken ct = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(request.Id, ct)
            ?? throw new InvalidOperationException($"Transaction {request.Id} not found.");

        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot modify reconciled transaction.");

        if (request.Date.HasValue)
            transaction.SetDate(request.Date.Value);

        if (request.Amount.HasValue)
            transaction.SetAmount(request.Amount.Value);

        if (request.Payee is not null)
            transaction.SetPayee(request.Payee);

        if (request.EnvelopeId.HasValue || request.EnvelopeId is null)
            transaction.AssignToEnvelope(request.EnvelopeId);

        if (request.Memo is not null)
            transaction.SetMemo(request.Memo);

        await _unitOfWork.Transactions.UpdateAsync(transaction, ct);
        await UpdateAccountBalanceAsync(transaction.AccountId, ct);

        return transaction;
    }

    /// <summary>
    /// Delete a transaction.
    /// </summary>
    public async Task DeleteTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");

        if (transaction.IsReconciled)
            throw new InvalidOperationException("Cannot delete reconciled transaction.");

        var accountId = transaction.AccountId;

        // If transfer, delete linked transaction too
        if (transaction.IsTransfer && transaction.LinkedTransactionId.HasValue)
        {
            var linkedAccountId = transaction.TransferAccountId!.Value;
            await _unitOfWork.Transactions.DeleteAsync(transaction.LinkedTransactionId.Value, ct);
            await UpdateAccountBalanceAsync(linkedAccountId, ct);
        }

        await _unitOfWork.Transactions.DeleteAsync(transactionId, ct);
        await UpdateAccountBalanceAsync(accountId, ct);
    }

    /// <summary>
    /// Assign a transaction to an envelope.
    /// </summary>
    public async Task AssignToEnvelopeAsync(
        Guid transactionId,
        Guid envelopeId,
        CancellationToken ct = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");

        var envelope = await _unitOfWork.Envelopes.GetByIdAsync(envelopeId, ct)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found.");

        transaction.AssignToEnvelope(envelopeId);
        await _unitOfWork.Transactions.UpdateAsync(transaction, ct);

        // Update payee default envelope
        var payee = await _unitOfWork.Payees.GetByNameAsync(transaction.Payee, ct);
        if (payee is not null && !payee.DefaultEnvelopeId.HasValue)
        {
            payee.SetDefaultEnvelope(envelopeId);
            await _unitOfWork.Payees.UpdateAsync(payee, ct);
        }
    }

    /// <summary>
    /// Mark a transaction as cleared.
    /// </summary>
    public async Task MarkClearedAsync(Guid transactionId, CancellationToken ct = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");

        transaction.MarkCleared();
        await _unitOfWork.Transactions.UpdateAsync(transaction, ct);
        await UpdateAccountBalanceAsync(transaction.AccountId, ct);
    }

    /// <summary>
    /// Mark a transaction as uncleared.
    /// </summary>
    public async Task MarkUnclearedAsync(Guid transactionId, CancellationToken ct = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Transaction {transactionId} not found.");

        transaction.MarkUncleared();
        await _unitOfWork.Transactions.UpdateAsync(transaction, ct);
        await UpdateAccountBalanceAsync(transaction.AccountId, ct);
    }

    /// <summary>
    /// Get transactions for an account.
    /// </summary>
    public async Task<IReadOnlyList<TransactionDto>> GetAccountTransactionsAsync(
        Guid accountId,
        DateRange? dateRange = null,
        CancellationToken ct = default)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account {accountId} not found.");

        var transactions = dateRange.HasValue
            ? await _unitOfWork.Transactions.GetByAccountAndDateRangeAsync(accountId, dateRange.Value, ct)
            : await _unitOfWork.Transactions.GetByAccountAsync(accountId, ct);

        return await MapTransactionsToDtosAsync(transactions, ct);
    }

    /// <summary>
    /// Get unassigned transactions (outflows without an envelope).
    /// </summary>
    public async Task<IReadOnlyList<TransactionDto>> GetUnassignedTransactionsAsync(CancellationToken ct = default)
    {
        var transactions = await _unitOfWork.Transactions.GetUnassignedAsync(ct);
        return await MapTransactionsToDtosAsync(transactions, ct);
    }

    /// <summary>
    /// Search payees for autocomplete.
    /// </summary>
    public async Task<IReadOnlyList<Payee>> SearchPayeesAsync(
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        return await _unitOfWork.Payees.SearchAsync(query, limit, ct);
    }

    private async Task UpdateAccountBalanceAsync(Guid accountId, CancellationToken ct)
    {
        var account = await _unitOfWork.Accounts.GetByIdAsync(accountId, ct);
        if (account is null) return;

        var balance = await _unitOfWork.Transactions.GetAccountBalanceAsync(accountId, ct);
        var clearedBalance = await _unitOfWork.Transactions.GetAccountClearedBalanceAsync(accountId, ct);
        var unclearedBalance = balance - clearedBalance;

        account.UpdateBalance(clearedBalance, unclearedBalance);
        await _unitOfWork.Accounts.UpdateAsync(account, ct);
    }

    private async Task UpdatePeriodIncomeAsync(DateOnly date, CancellationToken ct)
    {
        var period = await _unitOfWork.BudgetPeriods.GetOrCreateAsync(date.Year, date.Month, ct);
        var dateRange = DateRange.ForMonth(date.Year, date.Month);

        // Sum all inflows for the period
        var transactions = await _unitOfWork.Transactions.GetByDateRangeAsync(dateRange, ct);
        var totalIncome = transactions
            .Where(t => t.IsInflow && !t.IsTransfer)
            .Aggregate(Money.Zero, (sum, t) => sum + t.Amount);

        period.UpdateIncome(totalIncome);
        await _unitOfWork.BudgetPeriods.UpdateAsync(period, ct);
    }

    private async Task<IReadOnlyList<TransactionDto>> MapTransactionsToDtosAsync(
        IEnumerable<Transaction> transactions,
        CancellationToken ct)
    {
        // Get accounts and envelopes for mapping
        var accounts = await _unitOfWork.Accounts.GetAllAsync(ct);
        var envelopes = await _unitOfWork.Envelopes.GetAllAsync(ct);

        var accountDict = accounts.ToDictionary(a => a.Id, a => a.Name);
        var envelopeDict = envelopes.ToDictionary(e => e.Id, e => e.Name);

        return transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            AccountId = t.AccountId,
            AccountName = accountDict.GetValueOrDefault(t.AccountId, "Unknown"),
            EnvelopeId = t.EnvelopeId,
            EnvelopeName = t.EnvelopeId.HasValue ? envelopeDict.GetValueOrDefault(t.EnvelopeId.Value) : null,
            Date = t.Date,
            Amount = t.Amount,
            Payee = t.Payee,
            Memo = t.Memo,
            Type = t.Type,
            IsCleared = t.IsCleared,
            IsReconciled = t.IsReconciled
        }).ToList();
    }
}
