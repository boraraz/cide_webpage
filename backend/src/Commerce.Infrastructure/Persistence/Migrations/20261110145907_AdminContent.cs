using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "height",
                table: "product_media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "width",
                table: "product_media",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_media", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    draft_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    draft_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_pages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_revisions_content_pages_page_id",
                        column: x => x.page_id,
                        principalTable: "content_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_revision_media",
                columns: table => new
                {
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_revision_media", x => new { x.revision_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_content_revision_media_content_media_media_id",
                        column: x => x.media_id,
                        principalTable: "content_media",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_revision_media_content_revisions_revision_id",
                        column: x => x.revision_id,
                        principalTable: "content_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_media_object_key",
                table: "content_media",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_pages_draft_revision_id",
                table: "content_pages",
                column: "draft_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_pages_key",
                table: "content_pages",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_pages_published_revision_id",
                table: "content_pages",
                column: "published_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_revision_media_media_id",
                table: "content_revision_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_revisions_page_id_version",
                table: "content_revisions",
                columns: new[] { "page_id", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_content_pages_content_revisions_draft_revision_id",
                table: "content_pages",
                column: "draft_revision_id",
                principalTable: "content_revisions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_content_pages_content_revisions_published_revision_id",
                table: "content_pages",
                column: "published_revision_id",
                principalTable: "content_revisions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION content_reject_revision_change()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'content revision is immutable' USING ERRCODE = '55000';
                END;
                $$;

                CREATE TRIGGER content_revisions_immutable
                BEFORE UPDATE OR DELETE ON content_revisions
                FOR EACH ROW EXECUTE FUNCTION content_reject_revision_change();

                CREATE TRIGGER content_revision_media_immutable
                BEFORE UPDATE OR DELETE ON content_revision_media
                FOR EACH ROW EXECUTE FUNCTION content_reject_revision_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS content_revision_media_immutable ON content_revision_media;
                DROP TRIGGER IF EXISTS content_revisions_immutable ON content_revisions;
                DROP FUNCTION IF EXISTS content_reject_revision_change();
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_content_pages_content_revisions_draft_revision_id",
                table: "content_pages");

            migrationBuilder.DropForeignKey(
                name: "fk_content_pages_content_revisions_published_revision_id",
                table: "content_pages");

            migrationBuilder.DropTable(
                name: "content_revision_media");

            migrationBuilder.DropTable(
                name: "content_media");

            migrationBuilder.DropTable(
                name: "content_revisions");

            migrationBuilder.DropTable(
                name: "content_pages");

            migrationBuilder.DropColumn(
                name: "version",
                table: "products");

            migrationBuilder.DropColumn(
                name: "height",
                table: "product_media");

            migrationBuilder.DropColumn(
                name: "width",
                table: "product_media");
        }
    }
}
