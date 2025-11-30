using SubscriptionReconciler.Application;
using SubscriptionReconciler.Models;

namespace SubscriptionReconciler.Test;

public class ReconcilerTest
{
	[Fact]
	public void When_EventsOutsideMonth_AreRemoved_ByCleanup()
	{
		SubscriptionReconcilerService service = new(PlanType.Basic);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 5, 1), EventType.Cancel) // May 1 is outside April
		];

		var res = service.Reconcile(events, 2024, 4);

		// Cleanup should remove the event -> same as full-month initial plan
		Assert.Single(res.InvoiceLines);
		Assert.Equal(10m, res.TotalRounded);
	}

	[Fact]
	public void When_NoEvents_WithInitialPlan_ReturnsFullMonth()
	{
		SubscriptionReconcilerService service = new(PlanType.Basic);
		const int moth = 2;
		const int year = 2024;

		var res = service.Reconcile([], year, moth);

		Assert.Single(res.InvoiceLines);
		Assert.Equal(DateTime.DaysInMonth(year, moth), res.InvoiceLines[0].Days);
		Assert.Equal(Plan.Basic.Price, res.TotalRounded);
	}

	[Fact]
	public void When_NoEvents_NoInitialPlan_ReturnsEmptyResult()
	{
		SubscriptionReconcilerService service = new(null);
		const int moth = 2;
		const int year = 2024;

		var res = service.Reconcile([], year, moth);

		Assert.Empty(res.InvoiceLines);
		Assert.Equal(0m, res.TotalRounded);
		Assert.Equal(0m, res.TotalExact);
	}

	[Fact]
	public void When_SingleCancel_WithInitialPlan_BillsUntilCancelDateInclusive()
	{
		SubscriptionReconcilerService service = new(PlanType.Basic);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 10), EventType.Cancel)
		];

		var res = service.Reconcile(events, 2024, 4);

		// Interval should be Apr 1..Apr 10 inclusive => 10 days
		Assert.Single(res.InvoiceLines);
		var l = res.InvoiceLines[0];
		Assert.Equal(new DateTime(2024, 4, 1), l.Start);
		Assert.Equal(new DateTime(2024, 4, 10), l.End);
		Assert.Equal(10, l.Days);
		// Basic: 10 / 30 * 10 = 100/30 = 10/3 = 3.33333... rounds to 3.33
		Assert.Equal(3.33m, res.TotalRounded);
	}

	[Fact]
	public void When_SingleCancel_NoInitialPlan_ReturnsEmptyResult()
	{
		SubscriptionReconcilerService service = new(null);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 10), EventType.Cancel)
		];

		var res = service.Reconcile(events, 2024, 4);

		Assert.Empty(res.InvoiceLines);
		Assert.Equal(0m, res.TotalRounded);
		Assert.Equal(0m, res.TotalExact);
	}

	[Fact]
	public void When_SingleNonCancel_WithInitialPlan_NewPlanAfter()
	{
		SubscriptionReconcilerService service = new(PlanType.Basic);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 10), EventType.Upgrade, Plan.Premium)
		];

		var res = service.Reconcile(events, 2024, 4);

		// Expect two invoice lines: Basic from Apr 1..Apr 9 (9 days), Premium from Apr 10..Apr 30 (21 days)
		Assert.Equal(2, res.InvoiceLines.Count);

		var basicLine = res.InvoiceLines[0];
		Assert.Equal(new DateTime(2024, 4, 1), basicLine.Start);
		Assert.Equal(new DateTime(2024, 4, 9), basicLine.End);
		Assert.Equal(9, basicLine.Days);

		var premiumLine = res.InvoiceLines[1];
		Assert.Equal(new DateTime(2024, 4, 10), premiumLine.Start);
		Assert.Equal(new DateTime(2024, 4, 30), premiumLine.End);
		Assert.Equal(21, premiumLine.Days);

		// Amounts:
		// Basic: 10/30 * 9 =  = 3
		// Premium: 30/30 * 21 = 21
		// Total exact = 24 -> rounded = 24
		Assert.Equal(24m, res.TotalRounded);
	}

	[Fact]
	public void When_SingleNonCancel_NoInitialPlan_NewPlanAfter()
	{
		// when a single non-cancel event is present.
		SubscriptionReconcilerService service = new(null);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 5), EventType.Upgrade, Plan.Premium)
		];

		var res = service.Reconcile(events, 2024, 4);

		var premiumLine = res.InvoiceLines[0];

		Assert.Equal(new DateTime(2024, 4, 5), premiumLine.Start);
		Assert.Equal(new DateTime(2024, 4, 30), premiumLine.End);
		Assert.Equal(26, premiumLine.Days);
		Assert.Equal(26m, premiumLine.AmountRounded);
	}

	[Fact]
	public void When_IndecisiveClause_WithInitialPlan_UpgradeFollowedByDowngradeWithin24Hours()
	{
		// If there was an initial plan, removing indecisive events should leave the initial plan intact
		var serviceWithInitial = new SubscriptionReconcilerService(PlanType.Basic);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 5, 8, 0, 0), EventType.Upgrade, Plan.Premium),
			new(new DateTime(2024, 4, 5, 16, 0, 0), EventType.Downgrade, Plan.Basic)
		];

		var res = serviceWithInitial.Reconcile(events, 2024, 4);

		Assert.Single(res.InvoiceLines);
		Assert.Equal(10m, res.TotalRounded);
	}

	[Fact]
	public void When_IndecisiveClause_NoInitialPlan_UpgradeFollowedByDowngradeWithin24Hours()
	{
		// No initial plan: if both indecisive events are removed, result should be empty
		SubscriptionReconcilerService service = new(null);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 5, 8, 0, 0), EventType.Upgrade, Plan.Premium),
			new(new DateTime(2024, 4, 5, 16, 0, 0), EventType.Downgrade, Plan.Basic) // 8 hours later
		];

		var res = service.Reconcile(events, 2024, 4);

		Assert.Empty(res.InvoiceLines);
		Assert.Equal(0m, res.TotalExact);
		Assert.Equal(0m, res.TotalRounded);
	}

	[Fact]
	public void When_MultipleEvents_WithInitialPlan_CalculateCorrectly()
	{
		SubscriptionReconcilerService service = new(PlanType.Basic);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 5), EventType.Upgrade, Plan.Premium),
			new(new DateTime(2024, 4, 15), EventType.Downgrade, Plan.Basic),
			new(new DateTime(2024, 4, 25), EventType.Cancel),
		];

		var res = service.Reconcile(events, 2024, 4);

		Assert.Equal(3, res.InvoiceLines.Count);
		Assert.Equal(15.66m, res.TotalRounded);
	}

	[Fact]
	public void When_MultipleEvents_NoInitialPlan_CalculateCorrectly()
	{
		SubscriptionReconcilerService service = new(null);
		List<SubscriptionEvent> events = [
			new(new DateTime(2024, 4, 5), EventType.Upgrade, Plan.Premium),
			new(new DateTime(2024, 4, 15), EventType.Downgrade, Plan.Basic),
			new(new DateTime(2024, 4, 25), EventType.Cancel),
		];

		var res = service.Reconcile(events, 2024, 4);

		Assert.Equal(2, res.InvoiceLines.Count);
		Assert.Equal(14.33m, res.TotalRounded);
	}
}
