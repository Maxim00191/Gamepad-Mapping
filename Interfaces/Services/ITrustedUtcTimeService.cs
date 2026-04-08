using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Services;

public interface ITrustedUtcTimeService
{
    Task<DateTimeOffset> GetUtcNowAsync(CancellationToken cancellationToken = default);
}
