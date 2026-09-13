using Contapop.Bookkeeping.Service.Infrastructure.Persistence;

namespace Contapop.Bookkeeping.Service.Application.Queries;

public static class PlanVsActualCalculator
{
    public static PlanVsActualData Calculate(IEnumerable<PlannedExpense> plannedExpenses, IEnumerable<PlannedRevenue> plannedRevenues, IEnumerable<Expense> actualExpenses, IEnumerable<Revenue> actualRevenues)
    {
        var expensePlan = plannedExpenses.Cast<PlannedLine>().ToArray();
        var revenuePlan = plannedRevenues.Cast<PlannedLine>().ToArray();
        var expenseActual = actualExpenses.Cast<FinancialRecord>().ToArray();
        var revenueActual = actualRevenues.Cast<FinancialRecord>().ToArray();
        var categories = ByCategory("expense", expensePlan, expenseActual)
            .Concat(ByCategory("revenue", revenuePlan, revenueActual))
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Category)
            .ToArray();
        var expenses = new PlanVsActualTotals(expensePlan.Sum(item => item.AmountMinor), expenseActual.Sum(item => item.AmountMinor));
        var revenues = new PlanVsActualTotals(revenuePlan.Sum(item => item.AmountMinor), revenueActual.Sum(item => item.AmountMinor));
        return new PlanVsActualData(expenses, revenues, categories);
    }

    private static IEnumerable<PlanVsActualCategory> ByCategory(string type, IEnumerable<PlannedLine> planned, IEnumerable<FinancialRecord> actual) => planned.Select(item => item.Category).Union(actual.Select(item => item.Category)).Select(category => new PlanVsActualCategory(type, category, planned.Where(item => item.Category == category).Sum(item => item.AmountMinor), actual.Where(item => item.Category == category).Sum(item => item.AmountMinor)));
}

public sealed record PlanVsActualTotals(long PlannedMinor, long ActualMinor)
{
    public long VarianceMinor => ActualMinor - PlannedMinor;
}

public sealed record PlanVsActualCategory(string Type, string Category, long PlannedMinor, long ActualMinor);
public sealed record PlanVsActualData(PlanVsActualTotals Expenses, PlanVsActualTotals Revenues, IReadOnlyList<PlanVsActualCategory> ByCategory);
