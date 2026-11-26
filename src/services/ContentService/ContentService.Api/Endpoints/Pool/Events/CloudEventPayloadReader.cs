using System.Text.Json;

namespace ContentService.Api.Endpoints.Pool;

/// <summary>
/// Extracts strongly typed fields from Dapr CloudEvent envelopes or raw JSON event payloads.
/// </summary>
/// <remarks>
/// Non-standard CloudEvent parsing: Reads <see cref="JsonElement"/> directly instead of registering <c>app.UseCloudEvents()</c> middleware.
/// Gain: Accepts both Dapr CloudEvent <c>data</c> envelopes and raw JSON payloads in tests without extra middleware.
/// Loss: Does not handle binary-mode CloudEvents (HTTP <c>ce-*</c> headers) or non-JSON CloudEvent content types.
/// </remarks>
internal static class CloudEventPayloadReader
{
        /// <summary>
        /// Unwraps the <c>data</c> property from a CloudEvent envelope when present; otherwise returns <paramref name="rawEvent"/> directly.
        /// </summary>
        /// <param name="rawEvent">The incoming JSON event payload.</param>
        /// <returns>The unwrapped payload <see cref="JsonElement"/>.</returns>
        public static JsonElement UnwrapCloudEventData(JsonElement rawEvent)
        {
                if (rawEvent.ValueKind == JsonValueKind.Object
                        && rawEvent.TryGetProperty(propertyName: "data", value: out JsonElement dataElement)
                        && dataElement.ValueKind == JsonValueKind.Object)
                {
                        return dataElement;
                }

                return rawEvent;
        }

        /// <summary>
        /// Attempts to read a non-empty <see cref="Guid"/> property from a JSON object (case-insensitive).
        /// </summary>
        /// <param name="element">The JSON object element.</param>
        /// <param name="propertyName">The property name to locate.</param>
        /// <param name="value">When this method returns <see langword="true"/>, contains the parsed non-empty GUID.</param>
        /// <returns><see langword="true"/> if a valid non-empty GUID was found; otherwise <see langword="false"/>.</returns>
        public static bool TryReadGuid(JsonElement element, string propertyName, out Guid value)
        {
                value = Guid.Empty;
                string? text = ReadString(element: element, propertyName: propertyName);
                return Guid.TryParse(input: text, result: out value) && value != Guid.Empty;
        }

        /// <summary>
        /// Reads a string property from a JSON object using case-insensitive property matching.
        /// </summary>
        /// <param name="element">The JSON object element.</param>
        /// <param name="propertyName">The property name to locate.</param>
        /// <returns>The string value if found; otherwise <see langword="null"/>.</returns>
        public static string? ReadString(JsonElement element, string propertyName)
        {
                if (element.ValueKind != JsonValueKind.Object)
                {
                        return null;
                }

                foreach (JsonProperty prop in element.EnumerateObject())
                {
                        if (string.Equals(a: prop.Name, b: propertyName, comparisonType: StringComparison.OrdinalIgnoreCase)
                                && prop.Value.ValueKind == JsonValueKind.String)
                        {
                                return prop.Value.GetString();
                        }
                }

                return null;
        }

        /// <summary>
        /// Reads a 64-bit integer property from a JSON object using case-insensitive property matching, defaulting to <c>0</c>.
        /// </summary>
        /// <param name="element">The JSON object element.</param>
        /// <param name="propertyName">The property name to locate.</param>
        /// <returns>The parsed <see cref="long"/> value, or <c>0</c> if absent or non-numeric.</returns>
        public static long ReadInt64(JsonElement element, string propertyName)
        {
                if (element.ValueKind != JsonValueKind.Object)
                {
                        return 0;
                }

                foreach (JsonProperty prop in element.EnumerateObject())
                {
                        if (string.Equals(a: prop.Name, b: propertyName, comparisonType: StringComparison.OrdinalIgnoreCase)
                                && prop.Value.TryGetInt64(out long parsed))
                        {
                                return parsed;
                        }
                }

                return 0;
        }
}

