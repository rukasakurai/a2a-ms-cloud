using A2A;

namespace A2aDemo;

// =============================================================================
// A2A agent cards (discovery metadata) for the agents this sample publishes.
//
// The agent card is what makes an agent discoverable over A2A: an A2A client
// fetches it from the well-known endpoint to learn the agent's name, skills,
// supported modes, the protocol version, and the JSON-RPC URL to call. Each card
// advertises A2A protocol v1.0 via a single supported interface (see A2aProtocol).
// =============================================================================
internal static class AgentCards
{
    // The declarative Prompt agent: a specialist that answers weather questions.
    public static AgentCard Weather(string jsonRpcUrl) => new()
    {
        Name = "WeatherPromptAgent",
        Description = "Specialist Prompt agent that answers weather questions.",
        Version = "1.0.0",
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities { Streaming = false },
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = jsonRpcUrl,
                ProtocolBinding = A2aProtocol.JsonRpcBinding,
                ProtocolVersion = A2aProtocol.Version,
            },
        ],
        Skills =
        [
            new AgentSkill
            {
                Id = "weather_report",
                Name = "Weather report",
                Description = "Provides a brief weather report for a requested location.",
                Tags = ["weather", "forecast"],
                Examples =
                [
                    "What is the weather like in Amsterdam?",
                    "Will I need an umbrella in Seattle?",
                ],
            },
        ],
    };

    // The code-built Hosted agent: a travel concierge that itself calls the
    // weather specialist over A2A (acting as an A2A client) and adds packing
    // advice from a local tool. Publishing this card makes the Hosted agent
    // discoverable and callable over A2A (acting as an A2A server).
    public static AgentCard Concierge(string jsonRpcUrl) => new()
    {
        Name = "HostedConciergeAgent",
        Description = "Hosted (code-built) agent that combines a weather report with packing advice for a trip.",
        Version = "1.0.0",
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities { Streaming = false },
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = jsonRpcUrl,
                ProtocolBinding = A2aProtocol.JsonRpcBinding,
                ProtocolVersion = A2aProtocol.Version,
            },
        ],
        Skills =
        [
            new AgentSkill
            {
                Id = "travel_concierge",
                Name = "Travel concierge",
                Description = "Gives a combined weather report and packing recommendation for a trip.",
                Tags = ["travel", "weather", "packing"],
                Examples =
                [
                    "I'm planning a trip to Amsterdam — what's the weather and what should I pack?",
                ],
            },
        ],
    };
}
