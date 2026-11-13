using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_confirmation_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    guest_cart_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_confirmation_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_confirmation_session_orders",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_confirmation_session_orders", x => new { x.session_id, x.order_id });
                    table.ForeignKey(
                        name: "fk_order_confirmation_session_orders_order_confirmation_sessio",
                        column: x => x.session_id,
                        principalTable: "order_confirmation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_confirmation_session_orders_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_confirmation_session_orders_order_id",
                table: "order_confirmation_session_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_confirmation_sessions_token_hash",
                table: "order_confirmation_sessions",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_confirmation_session_orders");

            migrationBuilder.DropTable(
                name: "order_confirmation_sessions");
        }
    }
}
