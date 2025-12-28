using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace doc_bursa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountGroupBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountGroupId",
                table: "Accounts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.DropTable(
                name: "MasterGroupAccountGroups");

            migrationBuilder.CreateTable(
                name: "MasterGroupAccountGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MasterGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterGroupAccountGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterGroupAccountGroups_AccountGroups_AccountGroupId",
                        column: x => x.AccountGroupId,
                        principalTable: "AccountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MasterGroupAccountGroups_MasterGroups_MasterGroupId",
                        column: x => x.MasterGroupId,
                        principalTable: "MasterGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountGroupId",
                table: "Accounts",
                column: "AccountGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterGroupAccountGroups_AccountGroupId",
                table: "MasterGroupAccountGroups",
                column: "AccountGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterGroupAccountGroups_MasterGroupId_AccountGroupId",
                table: "MasterGroupAccountGroups",
                columns: new[] { "MasterGroupId", "AccountGroupId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountGroups_AccountGroupId",
                table: "Accounts",
                column: "AccountGroupId",
                principalTable: "AccountGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountGroups_AccountGroupId",
                table: "Accounts");

            migrationBuilder.DropTable(
                name: "MasterGroupAccountGroups");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountGroupId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AccountGroupId",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "MasterGroupAccountGroups",
                columns: table => new
                {
                    MasterGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountGroupId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterGroupAccountGroups", x => new { x.MasterGroupId, x.AccountGroupId });
                    table.ForeignKey(
                        name: "FK_MasterGroupAccountGroups_AccountGroups_AccountGroupId",
                        column: x => x.AccountGroupId,
                        principalTable: "AccountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MasterGroupAccountGroups_MasterGroups_MasterGroupId",
                        column: x => x.MasterGroupId,
                        principalTable: "MasterGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterGroupAccountGroups_AccountGroupId",
                table: "MasterGroupAccountGroups",
                column: "AccountGroupId");
        }
    }
}
