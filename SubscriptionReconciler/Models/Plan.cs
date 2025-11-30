namespace SubscriptionReconciler.Models;

public sealed class Plan(PlanType type, decimal monthlyPrice)
{
	public PlanType PlanType { get; } = type;
	public decimal Price { get; } = monthlyPrice;

	public static readonly Plan Basic = new(PlanType.Basic, 10m);
	public static readonly Plan Premium = new(PlanType.Premium, 30m);

	public static readonly Dictionary<PlanType, Plan> ActivePlans = new()
		{
			{ Basic.PlanType, Basic },
			{ Premium.PlanType, Premium }
		};
}
