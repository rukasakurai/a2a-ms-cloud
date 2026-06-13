namespace A2aDemo;

// =============================================================================
// A2A protocol version this sample targets.
//
// A2A had breaking changes between major versions, so the version is pinned
// explicitly rather than assumed. We target **A2A protocol v1.0** (open standard
// a2aproject/A2A v1.0.0, released 2026-03-12 - https://github.com/a2aproject/A2A/releases).
//
// Alignment with dependencies:
//   * A2A .NET SDK ('A2A' 1.0.0-preview2) implements the v1.0 protocol shape
//     (agent card uses 'supportedInterfaces' with per-interface 'protocolVersion';
//     'extendedAgentCard' moved under 'capabilities').
//   * Microsoft Foundry supports A2A v1.0 and v0.3 and recommends v1.0 for new
//     integrations. NOTE: when *calling* a Foundry-native A2A endpoint, Foundry
//     serves v0.3 by default unless the client negotiates v1.0 (the 'A2A-Version: 1.0'
//     header, '?a2a-version=1.0', or by fetching the v1.0 agent card).
//     See https://learn.microsoft.com/azure/foundry/agents/how-to/enable-agent-to-agent-endpoint
// =============================================================================
internal static class A2aProtocol
{
    // Protocol version string advertised in the agent card and used by clients to
    // negotiate. Foundry and the A2A SDK use "1.0" / "0.3" (not "1.0.0").
    public const string Version = "1.0";

    // Transport binding used by this sample (JSON-RPC over HTTP).
    public const string JsonRpcBinding = "JSONRPC";
}
