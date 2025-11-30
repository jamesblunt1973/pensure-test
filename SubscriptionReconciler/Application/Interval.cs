using SubscriptionReconciler.Models;

namespace SubscriptionReconciler.Application;

public record Interval(DateTime Start, DateTime End, PlanType PlanType);
