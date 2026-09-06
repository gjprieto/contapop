namespace Contapop.Identity.Service.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
