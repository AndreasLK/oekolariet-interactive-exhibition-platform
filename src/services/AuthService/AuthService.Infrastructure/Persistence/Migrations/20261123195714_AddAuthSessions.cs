using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261123195714_AddAuthSessions : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
                migrationBuilder.CreateTable(
                    name: "AuthSessions",
                    columns: table => new
                    {
                            Id = table.Column<Guid>(type: "TEXT", nullable: false),
                            UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                            CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                            LastUserActivityAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                            AbsoluteExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                            RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                            SuspectedReplayDetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                            IdleTimeoutTicks = table.Column<long>(type: "INTEGER", nullable: false),
                            CurrentRefreshTokenHash = table.Column<string>(type: "TEXT", nullable: true),
                            Version = table.Column<Guid>(type: "TEXT", nullable: false)
                    },
                    constraints: table =>
                    {
                            table.PrimaryKey("PK_AuthSessions", x => x.Id);
                            table.ForeignKey(
                        name: "FK_AuthSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    });

                migrationBuilder.CreateTable(
                    name: "AuthConsumedRefreshTokens",
                    columns: table => new
                    {
                            AuthSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                            TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                    },
                    constraints: table =>
                    {
                            table.PrimaryKey("PK_AuthConsumedRefreshTokens", x => new { x.AuthSessionId, x.TokenHash });
                            table.ForeignKey(
                        name: "FK_AuthConsumedRefreshTokens_AuthSessions_AuthSessionId",
                        column: x => x.AuthSessionId,
                        principalTable: "AuthSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateIndex(
                    name: "IX_AuthConsumedRefreshTokens_TokenHash",
                    table: "AuthConsumedRefreshTokens",
                    column: "TokenHash",
                    unique: true);

                migrationBuilder.CreateIndex(
                    name: "IX_AuthSessions_CurrentRefreshTokenHash",
                    table: "AuthSessions",
                    column: "CurrentRefreshTokenHash",
                    unique: true);

                migrationBuilder.CreateIndex(
                    name: "IX_AuthSessions_UserId",
                    table: "AuthSessions",
                    column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
                migrationBuilder.DropTable(
                    name: "AuthConsumedRefreshTokens");

                migrationBuilder.DropTable(
                    name: "AuthSessions");
        }
}
