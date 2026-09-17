using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDocumentPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processing_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quality_score = table.Column<double>(type: "double precision", nullable: true),
                    blur_detected = table.Column<bool>(type: "boolean", nullable: true),
                    document_detected = table.Column<bool>(type: "boolean", nullable: true),
                    processing_time_ms = table.Column<long>(type: "bigint", nullable: true),
                    original_image_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    processed_image_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rotation_degrees = table.Column<double>(type: "double precision", nullable: true),
                    orientation = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documents_uploaded_at",
                table: "documents",
                column: "uploaded_at",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}
