using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Contapop.Billing.Service.Infrastructure.Attachments;

public sealed class InvoiceAttachmentStorage(BlobServiceClient blobServiceClient)
{
    private const string ContainerName = "billing-invoice-attachments";

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await container.GetBlobClient(blobName).UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);
    }

    public async Task<AttachmentDownload?> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new AttachmentDownload(response.Value.Content, response.Value.Details.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken) =>
        await blobServiceClient.GetBlobContainerClient(ContainerName).GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
}

public sealed record AttachmentDownload(Stream Content, string ContentType);
