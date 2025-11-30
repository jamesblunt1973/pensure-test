namespace SubscriptionReconciler.Models;

public sealed class ReconciliationResult
{
	public decimal TotalExact { get; init; }
	public decimal TotalRounded { get; init; }
	public IReadOnlyList<InvoiceLine> InvoiceLines { get; init; } = [];
	public int Year { get; set; }
	public int Month { get; set; }
}
