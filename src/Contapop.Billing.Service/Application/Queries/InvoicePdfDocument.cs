using System.Text;

namespace Contapop.Billing.Service.Application.Queries;

public static class InvoicePdfDocument
{
    public static byte[] Create(Guid invoiceId, string counterpartyName, long totalAmountMinor)
    {
        var content = $"BT /F1 12 Tf 72 720 Td (Invoice {invoiceId}) Tj 0 -18 Td (Counterparty: {PdfText(counterpartyName)}) Tj 0 -18 Td (Total EUR: {totalAmountMinor / 100m:0.00}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer\n<< /Root 1 0 R /Size {objects.Length + 1} >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string PdfText(string value) => new(value.Where(character => character is >= ' ' and <= '~').Select(character => character is '(' or ')' or '\\' ? ' ' : character).ToArray());
}
