using System;
using System.Net.Http;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface ICommunityUploadWorkerRequestSigner
{
    void ApplySignatureHeaders(
        HttpRequestMessage request,
        Uri endpointUri,
        string requestBody,
        string signingKey);
}
