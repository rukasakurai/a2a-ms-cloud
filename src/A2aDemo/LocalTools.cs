using System.ComponentModel;

namespace A2aDemo;

// =============================================================================
// Local tools for the Hosted agent.
//
// A "Hosted agent" in this sample is an agent you BUILD in code with the
// Microsoft Agent Framework, as opposed to a declarative Prompt agent registered
// server-side in the Foundry project. To make that distinction concrete, the
// Hosted agent carries its own logic that runs in this process: this local
// function tool. It is deterministic C# (no model call), demonstrating that the
// Hosted agent's behavior is defined by code you host, not only by instructions
// stored in the project.
// =============================================================================
internal static class LocalTools
{
    [Description("Suggest what to pack based on a short description of the weather conditions.")]
    public static string PackingTips(
        [Description("A short description of the weather conditions, e.g. 'cloudy with light rain, 12C'.")] string conditions)
    {
        string c = (conditions ?? string.Empty).ToLowerInvariant();
        var tips = new List<string>();

        if (c.Contains("rain") || c.Contains("shower") || c.Contains("drizzle") || c.Contains("umbrella"))
        {
            tips.Add("a waterproof jacket and a compact umbrella");
        }
        if (c.Contains("snow") || c.Contains("freezing") || c.Contains("cold") || HasTemperatureBelow(c, 8))
        {
            tips.Add("warm layers and a hat");
        }
        if (c.Contains("sun") || c.Contains("clear") || c.Contains("hot") || HasTemperatureAtLeast(c, 24))
        {
            tips.Add("sunglasses and sunscreen");
        }
        if (c.Contains("wind"))
        {
            tips.Add("a windbreaker");
        }

        if (tips.Count == 0)
        {
            tips.Add("comfortable layers you can add or remove");
        }

        return "Packing suggestions: " + string.Join(", ", tips) + ".";
    }

    private static bool HasTemperatureBelow(string text, int threshold) =>
        TryReadTemperature(text, out int t) && t < threshold;

    private static bool HasTemperatureAtLeast(string text, int threshold) =>
        TryReadTemperature(text, out int t) && t >= threshold;

    // Reads the first integer that is immediately followed by a Celsius marker
    // (e.g. "12c" or "12 °c"), so packing advice can react to the temperature.
    private static bool TryReadTemperature(string text, out int temperature)
    {
        temperature = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]) && text[i] != '-')
            {
                continue;
            }

            int start = i;
            if (text[i] == '-')
            {
                i++;
            }
            while (i < text.Length && char.IsDigit(text[i]))
            {
                i++;
            }

            int j = i;
            while (j < text.Length && (text[j] == ' ' || text[j] == '°'))
            {
                j++;
            }

            if (j < text.Length && text[j] == 'c'
                && int.TryParse(text.AsSpan(start, i - start), out temperature))
            {
                return true;
            }
        }

        return false;
    }
}
