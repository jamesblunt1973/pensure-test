using SubscriptionReconciler.Application;
using SubscriptionReconciler.Models;
using SubscriptionReconciler.Presentation;

SubscriptionReconcilerService service = new(PlanType.Basic);
List<SubscriptionEvent> events = [
	new(new DateTime(2024, 4, 5), EventType.Upgrade, Plan.Premium),
	new(new DateTime(2024, 4, 15), EventType.Downgrade, Plan.Basic),
	new(new DateTime(2024, 4, 25), EventType.Cancel),
];

var reconcileResult = service.Reconcile(events, 2024, 4);
Console.WriteLine(InvoiceFormatter.Format(reconcileResult));
