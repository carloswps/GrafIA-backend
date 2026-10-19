using SmartIa.Infrastructure.Files;
using Xunit;

public class DatasetAnalyzerTests
{
    private static List<Dictionary<string, string>> Records(params (string Day, string Total)[] rows) =>
        rows.Select(r => new Dictionary<string, string>
        {
            ["Day"] = r.Day,
            ["Total"] = r.Total
        }).ToList();

    // ---------- Batch B: date != number ----------

    [Fact]
    public void ExtractMetadata_DateColumn_IsNotClassifiedAsNumeric()
    {
        var records = new List<Dictionary<string, string>>
        {
            new() { ["IssueDate"] = "05/01/2026", ["Total"] = "10" },
            new() { ["IssueDate"] = "06/01/2026", ["Total"] = "20" }
        };

        var meta = DatasetAnalyzer.ExtractMetadata(records);

        Assert.Equal("date", meta.Columns.First(c => c.Name == "IssueDate").Type);
        Assert.Equal("numeric", meta.Columns.First(c => c.Name == "Total").Type);
    }

    // ---------- Batch B: chronological aggregation ----------

    [Fact]
    public void GroupTopValues_DateDimension_SortsChronologically()
    {
        var records = Records(("03/01/2026", "100"), ("01/01/2026", "50"), ("02/01/2026", "10"));

        var (labels, values, _, totalGroups) = DatasetAnalyzer.GroupTopValues(records, "Day", "Total");

        Assert.Equal(new[] { "01/01/2026", "02/01/2026", "03/01/2026" }, labels);
        Assert.Equal(new decimal[] { 50, 10, 100 }, values);
        Assert.Equal(3, totalGroups);
    }

    [Fact]
    public void BuildSummary_ChronologicalAxis_UsesMaximumForCr1()
    {
        // values[0] = 50 (oldest), but the leader value is 100.
        var summary = DatasetAnalyzer.BuildSummary(new List<decimal> { 50, 10, 100 }, 3, "Day", "Total", groupsShown: 3);

        Assert.Equal(62.5, summary.ShareTop1, 2);    // 100/160 — NOT 50/160 = 31.25
        Assert.Equal(100.0, summary.ShareTop3, 2);   // with 3 groups, Top3 sums everything
    }

    [Fact]
    public void BuildSummary_Cr3_SumsTopThreeRegardlessOfOrder()
    {
        // chronological order: 50, 10, 100, 5 -> total 165; top 3 largest = 100+50+10 = 160
        var summary = DatasetAnalyzer.BuildSummary(new List<decimal> { 50, 10, 100, 5 }, 4, "Day", "Total", groupsShown: 4);

        Assert.Equal(96.97, summary.ShareTop3, 2);
    }

    // ---------- Batch B: IsTruncated ----------

    [Fact]
    public void BuildSummary_MoreThan10Groups_MarksIsTruncatedTrue()
    {
        var values = Enumerable.Range(1, 27).Select(i => (decimal)i).ToList();

        var summary = DatasetAnalyzer.BuildSummary(values, 27, "State", "Total", groupsShown: 10);

        Assert.True(summary.IsTruncated);
        Assert.Equal(10, summary.GroupsShown);
        Assert.Equal(27, summary.TotalGroups);
    }

    [Fact]
    public void BuildSummary_UpTo10Groups_MarksIsTruncatedFalse()
    {
        var summary = DatasetAnalyzer.BuildSummary(new List<decimal> { 1, 2, 3 }, 3, "State", "Total", groupsShown: 3);

        Assert.False(summary.IsTruncated);
    }

    // ---------- Batch B: dimension selection ----------

    [Fact]
    public void PickBestDimension_DoesNotSelectNumericColumn()
    {
        var records = new List<Dictionary<string, string>>
        {
            new() { ["Company"] = "ILIKIA", ["ReturnDate"] = "05/01/2026", ["InvoiceNumber"] = "1", ["Amount"] = "10" },
            new() { ["Company"] = "ILIKIA", ["ReturnDate"] = "06/01/2026", ["InvoiceNumber"] = "2", ["Amount"] = "20" }
        };

        var result = DatasetAnalyzer.PickBestDimension(records, "Amount");

        // "Company" is constant; the date column has a cardinality of 2 and is non-numeric
        Assert.Equal("ReturnDate", result);
    }

    // ---------- Batch A: spelling normalization ----------

    [Fact]
    public void GroupTopValues_ColumnWithNormalizedSpelling_MatchesMetric()
    {
        // "Total Bruto" (file 2) must match the priority "TotalBruto"
        var records = new List<Dictionary<string, string>>
        {
            new() { ["State"] = "SP", ["Total Bruto"] = "100" },
            new() { ["State"] = "MG", ["Total Bruto"] = "50" }
        };

        var (columnX, columnY) = DatasetAnalyzer.ResolveColumns(
            records, new SmartIa.Application.Dtos.ParametersFilterDto(null, null, null, null), "State", "Total Bruto");

        Assert.Equal("State", columnX);
        Assert.Equal("Total Bruto", columnY);
    }
}