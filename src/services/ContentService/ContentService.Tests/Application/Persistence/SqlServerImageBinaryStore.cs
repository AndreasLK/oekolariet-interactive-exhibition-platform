using System.Data;
using ContentService.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// SQL Server sequential streaming implementation of <see cref="IImageBinaryStore"/> backed by <see cref="SqlDataReader"/>.
/// </summary>
/// <remarks>
/// Streams <c>VARBINARY(MAX)</c> columns directly via <see cref="CommandBehavior.SequentialAccess"/> instead of loading <c>byte[]</c> arrays into EF Core entities.
/// </remarks>
public sealed class SqlServerImageBinaryStore : IImageBinaryStore
{
        /// <summary>
        /// SQL query reading only the raw binary column for the requested image ID.
        /// </summary>
        private const string SELECT_BINARY_SQL = "SELECT [Data] FROM [ImageBinaries] WHERE [ImageId] = @imageId";

        /// <summary>
        /// SQL parameter name bound to the requested image identifier.
        /// </summary>
        private const string IMAGE_ID_PARAMETER = "@imageId";

        /// <summary>
        /// Zero-based column ordinal of the binary data column in <see cref="SELECT_BINARY_SQL"/>.
        /// </summary>
        private const int DATA_COLUMN_ORDINAL = 0;

        /// <summary>
        /// Database fixture providing the active SQL Server connection string.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerImageBinaryStore"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server test container fixture.</param>
        public SqlServerImageBinaryStore(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <summary>
        /// Gets the number of times <see cref="OpenReadAsync"/> was called during the current test.
        /// </summary>
        public int OpenReadCallCount { get; private set; }

        /// <inheritdoc />
        public async Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
        {
                this.OpenReadCallCount++;

                SqlConnection connection = new(this._fixture.ConnectionString);
                SqlCommand? command = null;
                SqlDataReader? reader = null;

                try
                {
                        await connection.OpenAsync(cancellationToken);
                        command = connection.CreateCommand();
                        command.CommandText = SELECT_BINARY_SQL;
                        command.Parameters.Add(new SqlParameter
                        {
                                ParameterName = IMAGE_ID_PARAMETER,
                                SqlDbType = SqlDbType.UniqueIdentifier,
                                Value = imageId
                        });

                        // SequentialAccess streams VARBINARY(MAX) chunks directly from TDS without buffering the whole payload in memory.
                        reader = await command.ExecuteReaderAsync(
                                behavior: CommandBehavior.SequentialAccess | CommandBehavior.SingleRow,
                                cancellationToken: cancellationToken);
                        if (!await reader.ReadAsync(cancellationToken)
                                || await reader.IsDBNullAsync(DATA_COLUMN_ORDINAL, cancellationToken))
                        {
                                // Release all SQL objects immediately when no row or a NULL binary column is returned.
                                await DisposeResourcesAsync(reader: reader, command: command, connection: connection);
                                return null;
                        }

                        // Transfer reader, command, and connection ownership to the returned stream so they stay open until the caller disposes it.
                        Stream sqlStream = reader.GetStream(DATA_COLUMN_ORDINAL);
                        return new ConnectionOwningStream(
                                innerStream: sqlStream,
                                reader: reader,
                                command: command,
                                connection: connection);
                }
                catch
                {
                        // Ensure partial SQL resources are closed on cancellation or query failure before rethrowing.
                        await DisposeResourcesAsync(reader: reader, command: command, connection: connection);
                        throw;
                }
        }

        /// <summary>
        /// Disposes reader, command, and connection cleanly when opening the binary stream aborts or returns no rows.
        /// </summary>
        /// <param name="reader">Optional active SQL data reader.</param>
        /// <param name="command">Optional SQL command.</param>
        /// <param name="connection">Open SQL connection.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous disposal of all SQL resources.</returns>
        private static async ValueTask DisposeResourcesAsync(
                SqlDataReader? reader,
                SqlCommand? command,
                SqlConnection connection)
        {
                // Dispose child reader and command before returning the underlying connection to the pool.
                if (reader is not null)
                {
                        await reader.DisposeAsync();
                }

                if (command is not null)
                {
                        await command.DisposeAsync();
                }

                await connection.DisposeAsync();
        }

        /// <summary>
        /// Stream wrapper tying the lifetime of <see cref="SqlDataReader"/>, <see cref="SqlCommand"/>, and <see cref="SqlConnection"/> to the returned stream.
        /// </summary>
        private sealed class ConnectionOwningStream : Stream
        {
                /// <summary>
                /// Underlying sequential SQL column stream.
                /// </summary>
                private readonly Stream _innerStream;

                /// <summary>
                /// Active SQL data reader kept open while streaming.
                /// </summary>
                private readonly SqlDataReader _reader;

                /// <summary>
                /// SQL command owning the reader.
                /// </summary>
                private readonly SqlCommand _command;

                /// <summary>
                /// Open SQL connection returned to the pool on disposal.
                /// </summary>
                private readonly SqlConnection _connection;

                /// <summary>
                /// Tracks whether resources have already been disposed.
                /// </summary>
                private bool _disposed;

                /// <summary>
                /// Initializes a new instance of the <see cref="ConnectionOwningStream"/> class.
                /// </summary>
                /// <param name="innerStream">The underlying SQL column stream.</param>
                /// <param name="reader">The active data reader.</param>
                /// <param name="command">The SQL command.</param>
                /// <param name="connection">The SQL connection.</param>
                public ConnectionOwningStream(
                        Stream innerStream,
                        SqlDataReader reader,
                        SqlCommand command,
                        SqlConnection connection)
                {
                        this._innerStream = innerStream;
                        this._reader = reader;
                        this._command = command;
                        this._connection = connection;
                }

                /// <inheritdoc />
                public override bool CanRead => this._innerStream.CanRead;

                /// <inheritdoc />
                public override bool CanSeek => false;

                /// <inheritdoc />
                public override bool CanWrite => false;

                /// <inheritdoc />
                public override long Length => throw new NotSupportedException();

                /// <inheritdoc />
                public override long Position
                {
                        get => throw new NotSupportedException();
                        set => throw new NotSupportedException();
                }

                /// <inheritdoc />
                public override void Flush()
                {
                        // No-op because this stream only supports sequential read operations.
                }

                /// <inheritdoc />
                public override int Read(byte[] buffer, int offset, int count) =>
                        this._innerStream.Read(buffer: buffer, offset: offset, count: count);

                /// <inheritdoc />
                public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                        this._innerStream.ReadAsync(buffer: buffer, offset: offset, count: count, cancellationToken: cancellationToken);

                /// <inheritdoc />
                public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                        this._innerStream.ReadAsync(buffer: buffer, cancellationToken: cancellationToken);

                /// <inheritdoc />
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

                /// <inheritdoc />
                public override void SetLength(long value) => throw new NotSupportedException();

                /// <inheritdoc />
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

                /// <inheritdoc />
                protected override void Dispose(bool disposing)
                {
                        // Guard against duplicate disposal and skip managed SQL resources during finalization.
                        if (this._disposed || !disposing)
                        {
                                base.Dispose(disposing);
                                return;
                        }

                        // Dispose stream, reader, command, and connection in reverse ownership order.
                        this._disposed = true;
                        this._innerStream.Dispose();
                        this._reader.Dispose();
                        this._command.Dispose();
                        this._connection.Dispose();
                        base.Dispose(disposing);
                }

                /// <inheritdoc />
                public override async ValueTask DisposeAsync()
                {
                        // Keep async disposal idempotent when callers dispose both the wrapper and parent result.
                        if (this._disposed)
                        {
                                return;
                        }

                        // Release inner stream, reader, command, and pooled connection asynchronously and suppress finalization.
                        this._disposed = true;
                        await this._innerStream.DisposeAsync();
                        await this._reader.DisposeAsync();
                        await this._command.DisposeAsync();
                        await this._connection.DisposeAsync();
                        GC.SuppressFinalize(this);
                }
        }
}
