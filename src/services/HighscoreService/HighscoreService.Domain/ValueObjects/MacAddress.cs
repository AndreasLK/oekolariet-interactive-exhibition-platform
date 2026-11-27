using System.Net.NetworkInformation;
using HighscoreService.Domain.Exceptions;

namespace HighscoreService.Domain.ValueObjects
{
        /// <summary>
        /// Represents a validated, canonically formatted IEEE 802 MAC address (AA:BB:CC:DD:EE:FF).
        /// </summary>
        public sealed record MacAddress
        {
                private const int EXPECTED_BYTE_COUNT = 6;
                private const string ZERO_MAC_ADDRESS = "00:00:00:00:00:00";

                /// <summary>
                /// Gets the normalized uppercase colon-separated MAC address string.
                /// </summary>
                public string Value { get; }

                private MacAddress(string value)
                {
                        this.Value = value;
                }

                /// <summary>
                /// Parses and normalizes a raw MAC address string into uppercase colon-separated form.
                /// </summary>
                /// <param name="rawMacAddress">The raw MAC address string (e.g. AA-BB-CC-DD-EE-FF or AA:BB:CC:DD:EE:FF).</param>
                /// <returns>A validated <see cref="MacAddress"/> instance.</returns>
                /// <exception cref="DomainValidationException">Thrown when the MAC address is missing, malformed, or all zeros.</exception>
                public static MacAddress Parse(string? rawMacAddress)
                {
                        if (string.IsNullOrWhiteSpace(rawMacAddress))
                        {
                                throw new DomainValidationException("Sender MAC address is required.");
                        }

                        // PhysicalAddress.TryParse accepts colon, dash, and bare hex formats cleanly.
                        string trimmed = rawMacAddress.Trim();
                        if (!PhysicalAddress.TryParse(trimmed, out PhysicalAddress? parsed))
                        {
                                throw new DomainValidationException($"Invalid sender MAC address format: '{trimmed}'.");
                        }

                        byte[] bytes = parsed.GetAddressBytes();
                        if (bytes.Length != EXPECTED_BYTE_COUNT)
                        {
                                throw new DomainValidationException($"Sender MAC address must be 6 bytes: '{trimmed}'.");
                        }

                        string normalized = string.Join(':', bytes.Select(b => b.ToString("X2")));
                        if (normalized == ZERO_MAC_ADDRESS)
                        {
                                throw new DomainValidationException("Sender MAC address cannot be all zeros.");
                        }

                        return new MacAddress(normalized);
                }
        }
}

