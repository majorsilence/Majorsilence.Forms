using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointOfSale.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    PinHash = table.Column<string>(type: "TEXT", nullable: false),
                    PinSalt = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "INTEGER", nullable: false),
                    ReorderThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CashierUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    TenderType = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountTendered = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    ChangeDue = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    VoidedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleLineItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleLineItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatedSaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    PerformedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResultingQuantityOnHand = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Sales_RelatedSaleId",
                        column: x => x.RelatedSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleLineItems_ProductId",
                table: "SaleLineItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleLineItems_SaleId",
                table: "SaleLineItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CashierUserId",
                table: "Sales",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleNumber",
                table: "Sales",
                column: "SaleNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_VoidedByUserId",
                table: "Sales",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_PerformedByUserId",
                table: "StockAdjustments",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ProductId",
                table: "StockAdjustments",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_RelatedSaleId",
                table: "StockAdjustments",
                column: "RelatedSaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleLineItems");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
