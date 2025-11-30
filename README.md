# Subscription Reconciler

## Overview

The Subscription Reconciler is a service designed to calculate invoice amounts for customers based on active subscriptions and usage. It ensures that customers are billed accurately according to their subscription plans throughout a month.

## How to run locally
1. Git clone the repository
1. Open the solution in Visual Studio 2022, running on **.NET 9**
1. From Test Explorer, run all tests in the `SubscriptionReconciler.Tests` project.
1. Set the `SubscriptionReconciler` project as the startup project.
1. Press F5 to run the application locally and see a sample invoice in the console.

Design decisions
- Separation of Concerns: The design separates **models**, **application** (business logic), and **presentation** to enhance maintainability and testability.
- The SubscriptionReconcilerService calss has a default constructor to facilitate easy instantiation and allow dependency injection for testing purposes.
 <br>(in real scenario, the user initial plan is being loaded from database)
- The reconcile method breaks down the reconciliation process into clear steps:
	1.     Cleanup events (remove invalid events, for example if two events have the same plan)
	1.     Sort events chronologically
	1.     Detect indecisive clause (upgrade + downgrade within 24h)
	1.     Build intervals (most important step)
	1.     Convert intervals to invoice lines
	1.     Generate invoice lines based on intervals

- The important part of the logic is building intervals based on events.
    1. At first, I separated the logic into three states based on number of events.
 When events.Count is 0, 1 or more than 1. In each state, I checked the intial plan and generated intervals accordingly.
    1. Then I added a few tests to cover those scenarios. While adding and running tests, I found that the code block for events.Count == 1 and events.Count > 1 had a lot of duplication.
	1. Then I refactored the code and removed the code block when Count is 1. merged into the Count > 1 block to reduce code duplication.
	1. By adding more tests and covering edge cases, I fixed some bugs in the interval building logic.
	1. Finally, I refactored the code to improve readability and maintainability using AI.
Here is the old version of the interval building logic for reference:
```
private static List<Interval> BuildIntervals(List<SubscriptionEvent> events, DateTime start, DateTime end, PlanType? initialPlan = null)
{
	List<Interval> intervals = [];

	if (events.Count == 0)
	{
		if (initialPlan is null)
		{
			// No initial plan and no event then no intervals
			return intervals;
		}

		intervals.Add(new Interval(start, end, initialPlan.Value));
		return intervals;
	}

	PlanType? currentPlan;
	DateTime startDate;

	if (initialPlan is not null)
	{
		startDate = start;
		currentPlan = initialPlan;
	}
	else
	{
		startDate = events[0].EventDate;
		currentPlan = events[0].Plan?.PlanType;
		events = [.. events.Skip(1)];
	}

	foreach (var e in events)
	{
		DateTime eventDate;
		PlanType? planType;

		switch (e.EventType)
		{
			case EventType.Downgrade:
				// RULE: Downgrades Apply the following day
				eventDate = e.EventDate;
				planType = e.Plan!.PlanType;
				break;
			case EventType.Upgrade:
				// RULE: Upgrades Apply immediately
				eventDate = e.EventDate.AddDays(-1);
				planType = e.Plan!.PlanType;
				break;
			case EventType.Cancel:
			default:
				// RULE: Cancels Apply immediately?
				eventDate = e.EventDate;
				planType = null;
				break;
		}

		if (planType == currentPlan)
		{
			// No change in plan, skip
			continue;
		}

		if (currentPlan.HasValue)
		{
			intervals.Add(new Interval(startDate, eventDate, currentPlan.Value));
		}

		startDate = eventDate.AddDays(1);
		currentPlan = planType;
	}

	// Handle last interval
	if (currentPlan.HasValue)
	{
		intervals.Add(new Interval(startDate, end, currentPlan.Value));
	}

	return intervals;
}
```


## Further Improvements
- Accidental downgrade can be detected and ignored like indecisive clause
- EventType could be determined from plan price (e.g., Upgrade if higher price than current)
- In real scenario we would fetch user's current subscription plan from DB or null if none
