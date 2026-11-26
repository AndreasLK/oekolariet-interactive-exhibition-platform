using System.Buffers.Binary;
using ContentService.Application.Interfaces;
using ContentService.Infrastructure.Images;
using SkiaSharp;
using Xunit;

namespace ContentService.Tests.Infrastructure;

/// <summary>
/// Verifies decompression-bomb guards and pre-decode dimension limits in <see cref="SkiaImagePreviewGenerator"/>.
/// </summary>
public sealed class SkiaImagePreviewGeneratorTests
{
        /// <summary>
        /// Single-pixel dimension used as a baseline when constructing synthetic PNG headers.
        /// </summary>
        private const int BASELINE_PNG_DIMENSION_PX = 1;

        /// <summary>
        /// PNG header width exceeding the 25 MP decode cap when paired with <see cref="PIXEL_LIMIT_HEIGHT_PX"/>.
        /// </summary>
        private const int PIXEL_LIMIT_WIDTH_PX = 5_001;

        /// <summary>
        /// PNG header height exceeding the 25 MP decode cap when paired with <see cref="PIXEL_LIMIT_WIDTH_PX"/>.
        /// </summary>
        private const int PIXEL_LIMIT_HEIGHT_PX = 5_000;

        /// <summary>
        /// Square dimension whose Int32 product wraps negative without a 64-bit cast.
        /// </summary>
        private const int INT32_OVERFLOW_DIMENSION_PX = 50_000;

        /// <summary>
        /// Single-axis width exceeding the per-dimension decode limit even with a 1-pixel height.
        /// </summary>
        private const int EXCESSIVE_AXIS_WIDTH_PX = 20_000;

        /// <summary>
        /// Byte offset of the 4-byte big-endian width field inside a standard PNG IHDR chunk.
        /// </summary>
        private const int PNG_IHDR_WIDTH_OFFSET = 16;

        /// <summary>
        /// Byte offset of the 4-byte big-endian height field inside a standard PNG IHDR chunk.
        /// </summary>
        private const int PNG_IHDR_HEIGHT_OFFSET = 20;

        /// <summary>
        /// Byte offset where the PNG IHDR chunk type ("IHDR") begins for CRC-32 calculation.
        /// </summary>
        private const int PNG_IHDR_CRC_START_OFFSET = 12;

        /// <summary>
        /// Number of bytes covered by the PNG IHDR CRC-32 (4-byte chunk type + 13-byte IHDR payload).
        /// </summary>
        private const int PNG_IHDR_CRC_LENGTH_BYTES = 17;

        /// <summary>
        /// Byte offset of the 4-byte big-endian CRC-32 checksum following the PNG IHDR chunk.
        /// </summary>
        private const int PNG_IHDR_CRC_OFFSET = 29;

        /// <summary>
        /// PNG encode quality used when generating the 1x1 baseline PNG.
        /// </summary>
        private const int BASELINE_ENCODE_QUALITY = 100;

        /// <summary>
        /// Reflected CRC-32 polynomial used by the PNG specification.
        /// </summary>
        private const uint CRC32_POLYNOMIAL = 0xEDB88320u;

        /// <summary>
        /// Number of bits processed per byte in CRC-32 computation.
        /// </summary>
        private const int BITS_PER_BYTE = 8;

        /// <summary>
        /// Ensures <see cref="SkiaImagePreviewGenerator"/> rejects decompression-bomb dimensions before decoding.
        /// </summary>
        /// <param name="declaredWidth">Width written into the synthetic PNG IHDR header.</param>
        /// <param name="declaredHeight">Height written into the synthetic PNG IHDR header.</param>
        [Theory]
        [InlineData(PIXEL_LIMIT_WIDTH_PX, PIXEL_LIMIT_HEIGHT_PX)]
        [InlineData(INT32_OVERFLOW_DIMENSION_PX, INT32_OVERFLOW_DIMENSION_PX)]
        [InlineData(EXCESSIVE_AXIS_WIDTH_PX, BASELINE_PNG_DIMENSION_PX)]
        public async Task TryGeneratePreviewAsync_OversizedHeaderDimensions_ReturnsNullWithoutDecoding(
                int declaredWidth,
                int declaredHeight)
        {
                byte[] bombHeaderBytes = CreatePngWithPatchedHeaderDimensions(
                        width: declaredWidth,
                        height: declaredHeight);
                SkiaImagePreviewGenerator generator = new();

                ImagePreviewPayload? preview = await generator.TryGeneratePreviewAsync(
                        sourceBytes: bombHeaderBytes,
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Null(@object: preview);
        }

        /// <summary>
        /// Encodes a minimal 1x1 PNG and patches its IHDR dimensions and CRC-32 so tests never allocate huge bitmaps.
        /// </summary>
        /// <remarks>
        /// Non-standard test fixture: Instead of committing multi-megabyte decompression-bomb PNGs to Git, we patch a
        /// 1x1 PNG's big-endian IHDR dimensions and recompute its CRC-32 checksum in memory.
        /// <para>
        /// What we gain: Zero binary repo bloat and instant test execution for 50,000x50,000 header tests.
        /// What we lose: Requires a small inline CRC-32 helper so <c>SkPngCodec</c> accepts the forged header.
        /// </para>
        /// </remarks>
        /// <param name="width">Declared PNG width in pixels.</param>
        /// <param name="height">Declared PNG height in pixels.</param>
        /// <returns>A valid-header PNG byte array declaring the requested dimensions.</returns>
        private static byte[] CreatePngWithPatchedHeaderDimensions(int width, int height)
        {
                using SKBitmap tinyBitmap = new(
                        width: BASELINE_PNG_DIMENSION_PX,
                        height: BASELINE_PNG_DIMENSION_PX);
                using SKImage tinyImage = SKImage.FromBitmap(bitmap: tinyBitmap);
                using SKData encodedData = tinyImage.Encode(
                        format: SKEncodedImageFormat.Png,
                        quality: BASELINE_ENCODE_QUALITY);

                byte[] pngBytes = encodedData.ToArray();

                // Overwrite big-endian Width and Height in IHDR so SKCodec reads the target dimensions without allocating pixels.
                BinaryPrimitives.WriteInt32BigEndian(
                        destination: pngBytes.AsSpan(start: PNG_IHDR_WIDTH_OFFSET),
                        value: width);
                BinaryPrimitives.WriteInt32BigEndian(
                        destination: pngBytes.AsSpan(start: PNG_IHDR_HEIGHT_OFFSET),
                        value: height);

                // Recompute the IHDR CRC-32 so SkPngCodec accepts the modified header.
                uint crc = ComputeCrc32(data: pngBytes.AsSpan(
                        start: PNG_IHDR_CRC_START_OFFSET,
                        length: PNG_IHDR_CRC_LENGTH_BYTES));
                BinaryPrimitives.WriteUInt32BigEndian(
                        destination: pngBytes.AsSpan(start: PNG_IHDR_CRC_OFFSET),
                        value: crc);

                return pngBytes;
        }

        /// <summary>
        /// Computes a standard PNG CRC-32 checksum over the provided byte span.
        /// </summary>
        /// <param name="data">Chunk type and payload bytes to checksum.</param>
        /// <returns>The computed 32-bit CRC value.</returns>
        private static uint ComputeCrc32(ReadOnlySpan<byte> data)
        {
                uint crc = uint.MaxValue;

                foreach (byte currentByte in data)
                {
                        crc ^= currentByte;

                        for (int bit = 0; bit < BITS_PER_BYTE; bit++)
                        {
                                bool isLowBitSet = (crc & 1u) != 0u;
                                crc = isLowBitSet
                                        ? (crc >> 1) ^ CRC32_POLYNOMIAL
                                        : crc >> 1;
                        }
                }

                return ~crc;
        }
}

