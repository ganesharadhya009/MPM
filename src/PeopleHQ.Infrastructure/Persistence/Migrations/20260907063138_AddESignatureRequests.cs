using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeopleHQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddESignatureRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ESignatureRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTitle = table.Column<string>(type: "text", nullable: false),
                    DocumentBlobUrl = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignedByName = table.Column<string>(type: "text", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignerIpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ESignatureRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ESignatureRequests");
        }
    }
}
