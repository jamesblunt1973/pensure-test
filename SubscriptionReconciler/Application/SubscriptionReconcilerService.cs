using SubscriptionReconciler.Models;

namespace SubscriptionReconciler.Application;

public class SubscriptionReconcilerService(PlanType? initialPlan)
{
	public ReconciliationResult Reconcile(List<SubscriptionEvent> events, int year, int month)
	{
		var daysInMonth = DateTime.DaysInMonth(year, month);
		var monthStart = new DateTime(year, month, 1);
		var monthEnd = new DateTime(year, month, daysInMonth);

		List<InvoiceLine> invoiceLines = [];

		// Cleanup events
		CleanupEvents(events, monthStart, monthEnd);

		// Sort events
		var sortedEvents = events.OrderBy(e => e.EventDate).ToList();

		// Detect indecisive clause (upgrade + downgrade within 24h)
		ApplyIndecisiveClause(sortedEvents);

		// Build change intervals
		var intervals = BuildIntervals(sortedEvents, monthStart, monthEnd, initialPlan);

		decimal totalRounded = 0m, totalExact = 0m;
		// Convert intervals to invoice lines
		foreach (var interval in intervals)
		{
			var days = (int)(interval.End - interval.Start).TotalDays + 1;
			var dailyRate = Plan.ActivePlans[interval.PlanType].Price / daysInMonth;

			var amountExact = dailyRate * days;
			totalExact += amountExact;
			var amountRounded = Math.Round(amountExact, 2, MidpointRounding.AwayFromZero);
			totalRounded += amountRounded;

			invoiceLines.Add(new InvoiceLine
			{
				Start = interval.Start,
				End = interval.End,
				PlanType = interval.PlanType,
				Days = days,
				AmountExact = amountExact,
				AmountRounded = amountRounded
			});
		}

		return new ReconciliationResult
		{
			TotalExact = totalExact,
			TotalRounded = totalRounded,
			InvoiceLines = invoiceLines,
			Month = month,
			Year = year
		};
	}

	private static void CleanupEvents(List<SubscriptionEvent> events, DateTime start, DateTime end)
	{
		// Remove events outside of the month
		events.RemoveAll(e => e.EventDate < start || e.EventDate > end);
	}

	private static void ApplyIndecisiveClause(List<SubscriptionEvent> events)
	{
		for (int i = 0; i < events.Count - 1; i++)
		{
			var e1 = events[i];
			var e2 = events[i + 1];

			if (e1.EventType == EventType.Upgrade &&
				e2.EventType == EventType.Downgrade &&
				(e2.EventDate - e1.EventDate).TotalHours < 24)
			{
				// Remove both: ignore the upgrade
				events.RemoveAt(i + 1);
				events.RemoveAt(i);

				// Restart evaluation from beginning
				ApplyIndecisiveClause(events);
				return;
			}
		}
	}

	private static List<Interval> BuildIntervals(List<SubscriptionEvent> events, DateTime start, DateTime end, PlanType? initialPlan = null)
	{
		var intervals = new List<Interval>();

		if (events.Count == 0)
		{
			if (initialPlan is null)
				return intervals;

			intervals.Add(new Interval(start, end, initialPlan.Value));
			return intervals;
		}

		// Helper: given an event, return the date at which it ends previous plan + the new plan.
		static (DateTime boundaryDate, PlanType? newPlan) ResolveEvent(SubscriptionEvent e)
		{
			var plan = e.Plan?.PlanType;

			return e.EventType switch
			{
				EventType.Downgrade => (e.EventDate, plan),				// applies next day
				EventType.Upgrade => (e.EventDate.AddDays(-1), plan),	// applies immediately
				EventType.Cancel => (e.EventDate, null),				// end immediately; no plan afterwards
				_ => (e.EventDate, plan)
			};
		}

		// Establish initial plan
		PlanType? currentPlan;
		DateTime currentStart;

		int index = 0;

		if (initialPlan is not null)
		{
			currentPlan = initialPlan.Value;
			currentStart = start;
		}
		else
		{
			var firstEvent = events[0];
			(var firstBoundary, var firstPlan) = ResolveEvent(firstEvent);

			currentPlan = firstPlan;
			currentStart = firstBoundary + TimeSpan.FromDays(1);

			index = 1; // skip first event
		}

		// Process remaining events
		for (; index < events.Count; index++)
		{
			var e = events[index];

			// decode event
			var (boundaryDate, nextPlan) = ResolveEvent(e);

			// if no change, skip
			if (nextPlan == currentPlan)
				continue;

			// close current interval if plan was active
			if (currentPlan.HasValue)
			{
				intervals.Add(new Interval(currentStart, boundaryDate, currentPlan.Value));
			}

			// next plan starts the following day after boundary
			currentStart = boundaryDate.AddDays(1);
			currentPlan = nextPlan;
		}

		// Add final interval
		if (currentPlan.HasValue)
		{
			intervals.Add(new Interval(currentStart, end, currentPlan.Value));
		}

		return intervals;
	}
}
