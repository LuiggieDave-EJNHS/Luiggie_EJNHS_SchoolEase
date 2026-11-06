using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Helpers;

namespace SchoolEase.Web.Models.ViewModels;

/// <summary>A call-to-action button rendered in a page header or card header.</summary>
public class PageAction
{
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Icon { get; set; } = "bi-plus-lg";
    public string Css { get; set; } = "btn btn-primary";
    public string? Confirm { get; set; }
    public string? Target { get; set; }

    /// <summary>Rendered as a submit button inside the enclosing form instead of a link.</summary>
    public bool IsSubmit { get; set; }
}

public class PageHeader
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
    public List<PageAction> Actions { get; set; } = new();
}

public class StatCard
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "0";
    public string Icon { get; set; } = "bi-graph-up";
    public string Tone { get; set; } = "primary";
    public string? Meta { get; set; }
    public string? Url { get; set; }
}

public class ChartWidget
{
    public ChartData Data { get; set; } = new();
    public string Type { get; set; } = "bar";
    public string Height { get; set; } = "se-chart--md";
    public string? DatasetLabel { get; set; }
    public string? Footer { get; set; }

    public string ElementId => $"chart{Math.Abs(Data.Title.GetHashCode())}";
}

public class StatusBadge
{
    public string Text { get; set; } = string.Empty;
    public string Tone { get; set; } = "primary";
    public string? Icon { get; set; }
}

public class EmptyState
{
    public string Icon { get; set; } = "bi-inbox";
    public string Title { get; set; } = "Nothing here yet";
    public string Message { get; set; } = "Records will appear here once they are added.";
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
    public string ActionIcon { get; set; } = "bi-plus-lg";
}

public class PaginationInfo
{
    public int PageIndex { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 10;
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public Dictionary<string, string?> RouteValues { get; set; } = new();
    public string NoResultsText { get; set; } = "No records found.";

    public bool HasPrevious => PageIndex > 1;
    public bool HasNext => PageIndex < TotalPages;
    public int FirstItemOnPage => TotalCount == 0 ? 0 : ((PageIndex - 1) * PageSize) + 1;
    public int LastItemOnPage => Math.Min(PageIndex * PageSize, TotalCount);

    /// <summary>Route values for a given page, preserving the active filters.</summary>
    public Dictionary<string, string?> ForPage(int page)
    {
        var values = new Dictionary<string, string?>(RouteValues) { ["page"] = page.ToString() };
        return values;
    }
}

/// <summary>Drop-down option sets reused across modules.</summary>
public static class SelectLists
{
    public static List<SelectListItem> EnumOptions<TEnum>(TEnum? selected = null) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(v => new SelectListItem(
                Helpers.DisplayExtensions.DisplayName(v),
                Convert.ToInt32(v).ToString(),
                selected.HasValue && selected.Value.Equals(v)))
            .ToList();

    public static List<SelectListItem> Days() =>
        Enum.GetValues<DayOfWeek>()
            .Select(d => new SelectListItem(
                d == DayOfWeek.Monday ? "Monday" : d.ToString(), ((int)d).ToString()))
            .ToList();

    public static List<SelectListItem> Terms() =>
        new[] { 1, 2, 3 }
            .Select(t => new SelectListItem($"Term {t}", t.ToString()))
            .ToList();

    public static List<SelectListItem> YesNo(bool selected) =>
        new()
        {
            new SelectListItem("Yes", "true", selected),
            new SelectListItem("No", "false", !selected)
        };
}

/// <summary>
/// A subject choice that knows which grade level it belongs to. Used to drive
/// cascading Grade Level → Subject dropdowns on the client.
/// </summary>
public class SubjectOption
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;

    /// <summary>Owning grade level, or null when the subject applies to any level.</summary>
    public int? GradeLevelId { get; set; }
}

/// <summary>Small payload used by the dashboard "quick actions" grid.</summary>
public class QuickAction
{
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = "#";
    public string Icon { get; set; } = "bi-lightning";
    public string Tone { get; set; } = "primary";
}
