namespace SubscriptionReconciler.Models;

public sealed class InvoiceLine
{
	public PlanType PlanType { get; init; }
	public DateTime Start { get; set; }
	public DateTime End { get; set; }
	public int Days { get; init; }
	public decimal AmountExact { get; init; }
	public decimal AmountRounded { get; init; }
}
