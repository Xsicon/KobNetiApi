using System.Globalization;
using KobNeti.Api.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KobNeti.Api.Services;

public static class PayrollStatementPdf
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly Color Ink = Color.FromHex("0F172A");
    private static readonly Color Muted = Color.FromHex("64748B");
    private static readonly Color Indigo = Color.FromHex("4F46E5");
    private static readonly Color Line = Color.FromHex("E2E8F0");
    private static readonly Color Surface = Color.FromHex("F8FAFC");

    public static byte[] Build(PayrollStatementModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container => Compose(container, model)).GeneratePdf();
    }

    private static void Compose(IDocumentContainer container, PayrollStatementModel model)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.MarginTop(48);
            page.MarginBottom(40);
            page.MarginHorizontal(52);
            page.DefaultTextStyle(x => x.FontSize(10.5f).FontColor(Ink).LineHeight(1.35f));
            page.Header().Element(c => Header(c, model));
            page.Content().Element(c => Body(c, model));
            page.Footer().Element(c => Footer(c, model));
        });
    }

    private static void Header(IContainer container, PayrollStatementModel model)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(brand =>
                {
                    brand.Item().Text("KOBNETI").FontSize(11).Bold().LetterSpacing(1.4f).FontColor(Indigo);
                    brand.Item().PaddingTop(2).Text("People Operations").FontSize(9).FontColor(Muted);
                });
                row.ConstantItem(220).AlignRight().Column(meta =>
                {
                    meta.Item().Text("CONFIDENTIAL").FontSize(8).Bold().FontColor(Indigo).LetterSpacing(1.1f);
                    meta.Item().PaddingTop(2).Text(model.CompanyName).FontSize(9).SemiBold();
                    meta.Item().Text($"Issued {model.GeneratedAt:MMMM d, yyyy}").FontSize(8.5f).FontColor(Muted);
                });
            });
            col.Item().PaddingTop(14).Height(3).Background(Indigo);
            col.Item().PaddingTop(18).Text("Payroll Summary Statement").FontSize(20).Bold().FontColor(Ink);
            col.Item().PaddingTop(4).Text(model.PeriodLabel).FontSize(11).FontColor(Muted);
        });
    }

    private static void Body(IContainer container, PayrollStatementModel model)
    {
        var payDate = model.EndsOn.ToDateTime(TimeOnly.MinValue).AddDays(5);
        var hours = model.Lines.Sum(l => l.Hours);
        var gross = model.Lines.Sum(l => l.Amount);

        container.PaddingTop(18).Column(col =>
        {
            col.Spacing(14);

            col.Item().Background(Surface).Border(1).BorderColor(Line).Padding(14).Row(row =>
            {
                Fact(row, "Pay period", $"{model.StartsOn:MMM d} – {model.EndsOn:MMM d, yyyy}");
                Fact(row, "Pay date", payDate.ToString("MMM d, yyyy", Us));
                Fact(row, "Status", StatusLabel(model.Status));
                Fact(row, "Currency", model.Currency);
            });

            col.Item().Column(letter =>
            {
                letter.Item().Text($"Payroll & Finance — {model.CompanyName}").SemiBold();
                letter.Item().PaddingTop(10).Text(text =>
                {
                    text.Span("This letter certifies that the payroll run for ");
                    text.Span(model.CompanyName).SemiBold();
                    text.Span(" covering ");
                    text.Span($"{model.StartsOn:MMMM d} through {model.EndsOn:MMMM d, yyyy}").SemiBold();
                    text.Span(" has been calculated from approved timesheets, reviewed by management, and finalized for disbursement.");
                });
                letter.Item().PaddingTop(8).Text(
                    "Please process the amounts in the schedule below according to each person’s recorded hours and hourly rate. This statement is the official record of the run and may be retained for audit.");
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.0f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    HeaderCell(header, "Person");
                    HeaderCell(header, "Role");
                    HeaderCell(header, "Email");
                    HeaderCell(header, "Hours", right: true);
                    HeaderCell(header, "Rate", right: true);
                    HeaderCell(header, "Gross", right: true);
                });

                var i = 0;
                foreach (var line in model.Lines)
                {
                    var shade = i++ % 2 == 0;
                    BodyCell(table, line.Name, shade, bold: true);
                    BodyCell(table, line.Role, shade);
                    BodyCell(table, string.IsNullOrWhiteSpace(line.Email) ? "—" : line.Email, shade);
                    BodyCell(table, line.Hours.ToString("0.0", Us), shade, right: true);
                    BodyCell(table, line.Rate.ToString("C", Us), shade, right: true, mono: true);
                    BodyCell(table, line.Amount.ToString("C", Us), shade, right: true, mono: true, bold: true);
                }

                if (model.Lines.Count == 0)
                {
                    table.Cell().ColumnSpan(6).Padding(12).AlignCenter()
                        .Text("No employees in this payroll run.").FontColor(Muted).Italic();
                }
            });

            col.Item().Border(1).BorderColor(Line).Background(Color.FromHex("EEF2FF")).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Run totals").FontSize(8).FontColor(Muted).SemiBold();
                    c.Item().PaddingTop(2).Text($"{model.Lines.Count} employee{(model.Lines.Count == 1 ? "" : "s")}  ·  {hours.ToString("0.0", Us)} hours").FontSize(10);
                });
                row.ConstantItem(180).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("Gross payroll").FontSize(8).FontColor(Muted).SemiBold();
                    c.Item().AlignRight().Text(gross.ToString("C", Us)).FontSize(16).Bold().FontColor(Indigo);
                });
            });

            col.Item().PaddingTop(8).Text(
                "Disbursement is to be made by ACH / direct deposit on the pay date above, unless otherwise directed in writing.")
                .FontSize(9.5f).FontColor(Muted);

            col.Item().PaddingTop(10).Row(row =>
            {
                Signature(row, "Authorized by (People Operations)");
                row.ConstantItem(24);
                Signature(row, "Received by (Finance)");
            });
        });
    }

    private static void Footer(IContainer container, PayrollStatementModel model)
    {
        container.Column(col =>
        {
            col.Item().Height(1).Background(Line);
            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("KobNeti  ·  Confidential payroll record  ·  ").FontSize(8).FontColor(Muted);
                    text.Span($"Doc {model.PeriodId.ToString("N")[..8].ToUpperInvariant()}").FontSize(8).FontColor(Muted);
                });
                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(Muted);
                    text.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    text.Span(" of ").FontSize(8).FontColor(Muted);
                    text.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static void Fact(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label.ToUpperInvariant()).FontSize(7.5f).FontColor(Muted).SemiBold().LetterSpacing(0.6f);
            c.Item().PaddingTop(2).Text(value).FontSize(10).SemiBold();
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text, bool right = false)
    {
        var cell = header.Cell().Background(Ink).PaddingVertical(7).PaddingHorizontal(8);
        var align = right ? cell.AlignRight() : cell;
        align.Text(text.ToUpperInvariant()).FontSize(7.5f).FontColor(Colors.White).SemiBold().LetterSpacing(0.4f);
    }

    private static void BodyCell(
        TableDescriptor table,
        string text,
        bool shade,
        bool right = false,
        bool mono = false,
        bool bold = false)
    {
        var cell = table.Cell()
            .Background(shade ? Surface : Colors.White)
            .BorderBottom(1).BorderColor(Line)
            .PaddingVertical(7).PaddingHorizontal(8);
        var align = right ? cell.AlignRight() : cell;
        var span = align.Text(text).FontSize(9).FontColor(Ink);
        if (bold) span.SemiBold();
        if (mono) span.FontColor(Ink);
    }

    private static void Signature(RowDescriptor row, string label)
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Height(36).BorderBottom(1).BorderColor(Ink);
            c.Item().PaddingTop(6).Text(label).FontSize(8).FontColor(Muted);
            c.Item().PaddingTop(10).Row(inner =>
            {
                inner.RelativeItem().Column(d =>
                {
                    d.Item().Height(20).BorderBottom(1).BorderColor(Line);
                    d.Item().PaddingTop(4).Text("Name").FontSize(7.5f).FontColor(Muted);
                });
                inner.ConstantItem(12);
                inner.ConstantItem(70).Column(d =>
                {
                    d.Item().Height(20).BorderBottom(1).BorderColor(Line);
                    d.Item().PaddingTop(4).Text("Date").FontSize(7.5f).FontColor(Muted);
                });
            });
        });
    }

    private static string StatusLabel(string status) => status switch
    {
        PayPeriodStatus.Exported => "Finalized",
        PayPeriodStatus.Approved => "Approved",
        PayPeriodStatus.PendingApproval => "Pending approval",
        PayPeriodStatus.Calculated => "Calculated",
        _ => "Open"
    };
}

public sealed class PayrollStatementModel
{
    public string CompanyName { get; init; } = "KobNeti";
    public string PeriodLabel { get; init; } = "";
    public DateOnly StartsOn { get; init; }
    public DateOnly EndsOn { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string Status { get; init; } = "";
    public string Currency { get; init; } = "USD";
    public Guid PeriodId { get; init; }
    public IReadOnlyList<PayrollStatementLine> Lines { get; init; } = [];
}

public sealed class PayrollStatementLine
{
    public string Name { get; init; } = "";
    public string Role { get; init; } = "";
    public string Email { get; init; } = "";
    public decimal Hours { get; init; }
    public decimal Rate { get; init; }
    public decimal Amount { get; init; }
}
