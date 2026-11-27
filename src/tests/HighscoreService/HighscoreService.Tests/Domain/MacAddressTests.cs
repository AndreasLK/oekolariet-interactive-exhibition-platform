using HighscoreService.Domain.Exceptions;
using HighscoreService.Domain.ValueObjects;

namespace HighscoreService.Tests.Domain
{
        /// <summary>
        /// Unit tests for <see cref="MacAddress"/> parsing, canonicalization, and validation.
        /// </summary>
        public sealed class MacAddressTests
        {
                /// <summary>
                /// Verifies colon, dash, and bare hex MAC address formats normalize to uppercase colon notation.
                /// </summary>
                /// <param name="raw">Raw MAC address input.</param>
                [Theory]
                [InlineData("aa-bb-cc-dd-ee-ff")]
                [InlineData("AA:BB:CC:DD:EE:FF")]
                [InlineData("aabbccddeeff")]
                [InlineData("  aa:bb:cc:dd:ee:ff  ")]
                public void Parse_ValidFormats_NormalizesToUppercaseColonFormat(string raw)
                {
                        // Act
                        MacAddress parsed = MacAddress.Parse(raw);

                        // Assert
                        Assert.Equal("AA:BB:CC:DD:EE:FF", parsed.Value);
                }

                /// <summary>
                /// Verifies missing, malformed, wrong-length, or all-zero MAC addresses throw <see cref="DomainValidationException"/>.
                /// </summary>
                /// <param name="invalid">Invalid MAC address candidate.</param>
                [Theory]
                [InlineData(null)]
                [InlineData("")]
                [InlineData("   ")]
                [InlineData("not-a-mac")]
                [InlineData("AA:BB:CC:DD:EE")]
                [InlineData("00-00-00-00-00-00")]
                public void Parse_InvalidFormats_ThrowsDomainValidationException(string? invalid)
                {
                        // Act & Assert
                        Assert.Throws<DomainValidationException>(() => MacAddress.Parse(invalid));
                }
        }
}

