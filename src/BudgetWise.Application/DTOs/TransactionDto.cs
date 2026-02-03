using BudgetWise.Domain.Enums;
using BudgetWise.Domain.ValueObjects;

namespace BudgetWise.Application.DTOs;

/// <summary>
/// Transaction for display with related names.
/// </summary>
public sealed record TransactionDto
{
    public required Guid Id { get; init; }
    public required Guid AccountId { get; init; }
    public required string AccountName { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? EnvelopeName { get; init; }
    public required DateOnly Date { get; init; }
    public required Money Amount { get; init; }
    public required string Payee { get; init; }
    public string? Memo { get; init; }
    public required TransactionType Type { get; init; }
    public required bool IsCleared { get; init; }
    public required bool IsReconciled { get; init; }
    public bool IsTransfer => Type == TransactionType.Transfer;
    public bool IsInflow => Amount.IsPositive;
    public bool IsOutflow => Amount.IsNegative;
    public Money AbsoluteAmount => Amount.Abs();
}

/// <summary>
/// Request to create an outflow (expense) transaction.
/// </summary>
public sealed record CreateOutflowRequest
{
    public required Guid AccountId { get; init; }
    public required DateOnly Date { get; init; }
    public required Money Amount { get; init; }
    public required string Payee { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? Memo { get; init; }
}

/// <summary>
/// Request to create an inflow (income) transaction.
/// </summary>
public sealed record CreateInflowRequest
{
    public required Guid AccountId { get; init; }
    public required DateOnly Date { get; init; }
    public required Money Amount { get; init; }
    public required string Payee { get; init; }
    public string? Memo { get; init; }
}

/// <summary>
/// Request to create a transfer between accounts.
/// </summary>
public sealed record CreateTransferRequest
{
    public required Guid FromAccountId { get; init; }
    public required Guid ToAccountId { get; init; }
    public required DateOnly Date { get; init; }
    public required Money Amount { get; init; }
    public string? Memo { get; init; }
}

/// <summary>
/// Request to update a transaction.
/// </summary>
public sealed record UpdateTransactionRequest
{
    public required Guid Id { get; init; }
    public DateOnly? Date { get; init; }
    public Money? Amount { get; init; }
    public string? Payee { get; init; }
    public Guid? EnvelopeId { get; init; }
    public string? Memo { get; init; }
}

/// <summary>
/// Filters for querying transactions.
/// </summary>
public sealed record TransactionFilterDto
{
    public Guid? AccountId { get; init; }
    public Guid? EnvelopeId { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? PayeeSearch { get; init; }
    public bool? IsCleared { get; init; }
    public bool? IsUnassigned { get; init; }
    public int PageSize { get; init; } = 50;
    public int PageNumber { get; init; } = 1;
}
