using Microsoft.AspNetCore.Http;

namespace SlackSms.Infrastructure.SignalWire;

/// <summary>Why an inbound SignalWire webhook was accepted or rejected.</summary>
public enum SignalWireRequestStatus
{
    Valid,
    InvalidSignature,

    /// <summary>The posted AccountSid is not our SignalWire project.</summary>
    WrongAccount,
}

/// <summary>Authenticates inbound SignalWire webhooks: signature first, then the owning project.</summary>
public interface ISignalWireRequestVerifier
{
    SignalWireRequestStatus Verify(IFormCollection form, string? signature);
}
