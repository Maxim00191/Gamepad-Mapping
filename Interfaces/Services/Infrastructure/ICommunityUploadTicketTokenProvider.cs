using System.Threading;
using System.Threading.Tasks;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ICommunityUploadTicketTokenProvider
{
    Task<string?> GetTurnstileTokenAsync(CancellationToken cancellationToken = default);
}
