using SubscriptionReconciler.Models;
using System.Globalization;
using System.Text;

namespace SubscriptionReconciler.Presentation;

public class InvoiceFormatter
{
	public static string Format(ReconciliationResult reconcileRes)
	{
		var sb = new StringBuilder();

		string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(reconcileRes.Month);
		
		sb.AppendLine("Invoice Breakdown");
		sb.AppendLine($"For: {monthName}, {reconcileRes.Year}");
		sb.AppendLine();

		foreach (var line in reconcileRes.InvoiceLines)
		{
			sb.AppendLine($"{line.PlanType}: {line.Start:MMM dd} -> {line.End:MMM dd} #{line.Days} day(s) = ${line.AmountRounded}");
		}

		sb.AppendLine();
		sb.AppendLine($"Total: ${reconcileRes.TotalRounded:F2}");
		
		return sb.ToString();
	}
}