using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ITrustedUtcTimeService
{
    Task<DateTimeOffset> GetUtcNowAsync(CancellationToken cancellationToken = default);
}

