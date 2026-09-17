using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartDocumentPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StableDocumentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_uploaded_at",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "ix_documents_uploaded_at_id",
                table: "documents",
                columns: new[] { "uploaded_at", "id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_uploaded_at_id",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "ix_documents_uploaded_at",
                table: "documents",
                column: "uploaded_at",
                descending: new bool[0]);
        }
    }
}
