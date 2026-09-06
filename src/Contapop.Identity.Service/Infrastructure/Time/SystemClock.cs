using Contapop.Identity.Service.Application.Abstractions;

namespace Contapop.Identity.Service.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
