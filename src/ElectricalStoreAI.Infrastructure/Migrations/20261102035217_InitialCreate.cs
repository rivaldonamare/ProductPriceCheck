using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectricalStoreAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaleUnit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductReferenceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReferenceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductReferenceImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductImageEmbeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductReferenceImageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Vector = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImageEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImageEmbeddings_ProductReferenceImages_ProductReferenceImageId",
                        column: x => x.ProductReferenceImageId,
                        principalTable: "ProductReferenceImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImageEmbeddings_ProductReferenceImageId",
                table: "ProductImageEmbeddings",
                column: "ProductReferenceImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReferenceImages_ProductId",
                table: "ProductReferenceImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImageEmbeddings");

            migrationBuilder.DropTable(
                name: "ProductReferenceImages");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
