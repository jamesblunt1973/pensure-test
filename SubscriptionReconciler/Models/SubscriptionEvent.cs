namespace SubscriptionReconciler.Models;

public sealed class SubscriptionEvent(DateTime dateTime, EventType type, Plan? plan = null)
{
	public DateTime EventDate { get; } = dateTime;
	public EventType EventType { get; } = type;
	public Plan? Plan { get; } = plan;
}
