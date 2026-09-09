using System.Text;
using Contapop.Billing.Service.Application.Queries;

namespace Contapop.Billing.Service.Tests.Unit;

public sealed class InvoicePdfDocumentTests
{
    [Fact]
    public void Create_returns_a_complete_pdf_file()
    {
        var pdf = Encoding.ASCII.GetString(InvoicePdfDocument.Create(Guid.NewGuid(), "Acme SL", 12100));

        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.Contains("xref", pdf);
        Assert.EndsWith("%%EOF", pdf);
    }
}
