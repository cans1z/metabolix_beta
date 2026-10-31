using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Metabolix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    GoogleId = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    ConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Meal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Food_FoodRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Food_Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Food_Kcal = table.Column<double>(type: "double precision", nullable: false),
                    Food_Protein = table.Column<double>(type: "double precision", nullable: false),
                    Food_Fat = table.Column<double>(type: "double precision", nullable: false),
                    Food_Carbs = table.Column<double>(type: "double precision", nullable: false),
                    Food_Unit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Food_Portion = table.Column<double>(type: "double precision", nullable: true),
                    Food_Barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Food_Custom = table.Column<bool>(type: "boolean", nullable: false),
                    Grams = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Foods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SearchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kcal = table.Column<double>(type: "double precision", nullable: false),
                    Protein = table.Column<double>(type: "double precision", nullable: false),
                    Fat = table.Column<double>(type: "double precision", nullable: false),
                    Carbs = table.Column<double>(type: "double precision", nullable: false),
                    Unit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Portion = table.Column<double>(type: "double precision", nullable: true),
                    Barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "russian")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "SearchName" })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Foods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Foods_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Height = table.Column<double>(type: "double precision", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    TargetWeight = table.Column<double>(type: "double precision", nullable: false),
                    Activity = table.Column<double>(type: "double precision", nullable: false),
                    Goal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Profiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Theme = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RatioProtein = table.Column<int>(type: "integer", nullable: false),
                    RatioFat = table.Column<int>(type: "integer", nullable: false),
                    RatioCarbs = table.Column<int>(type: "integer", nullable: false),
                    ManualKcal = table.Column<double>(type: "double precision", nullable: true),
                    ManualProtein = table.Column<double>(type: "double precision", nullable: true),
                    ManualFat = table.Column<double>(type: "double precision", nullable: true),
                    ManualCarbs = table.Column<double>(type: "double precision", nullable: true),
                    WaterGoal = table.Column<int>(type: "integer", nullable: false),
                    RemindMeal = table.Column<bool>(type: "boolean", nullable: false),
                    RemindWater = table.Column<bool>(type: "boolean", nullable: false),
                    RemindWeight = table.Column<bool>(type: "boolean", nullable: false),
                    RemindTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Settings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaterRecords",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Ml = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterRecords", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_WaterRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeightRecords",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kg = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightRecords", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_WeightRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteFoods",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteFoods", x => new { x.UserId, x.FoodId });
                    table.ForeignKey(
                        name: "FK_FavoriteFoods_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavoriteFoods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteRecipes",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteRecipes", x => new { x.UserId, x.RecipeId });
                    table.ForeignKey(
                        name: "FK_FavoriteRecipes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavoriteRecipes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Food_FoodRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Food_Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Food_Kcal = table.Column<double>(type: "double precision", nullable: false),
                    Food_Protein = table.Column<double>(type: "double precision", nullable: false),
                    Food_Fat = table.Column<double>(type: "double precision", nullable: false),
                    Food_Carbs = table.Column<double>(type: "double precision", nullable: false),
                    Food_Unit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Food_Portion = table.Column<double>(type: "double precision", nullable: true),
                    Food_Barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Food_Custom = table.Column<bool>(type: "boolean", nullable: false),
                    Grams = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeItem_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_Date",
                table: "DiaryEntries",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_SyncedAt",
                table: "DiaryEntries",
                columns: new[] { "UserId", "SyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteFoods_FoodId",
                table: "FavoriteFoods",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteRecipes_RecipeId",
                table: "FavoriteRecipes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Foods_GlobalBarcode",
                table: "Foods",
                column: "Barcode",
                unique: true,
                filter: "\"OwnerId\" IS NULL AND \"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Foods_OwnerBarcode",
                table: "Foods",
                columns: new[] { "OwnerId", "Barcode" },
                unique: true,
                filter: "\"OwnerId\" IS NOT NULL AND \"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Foods_SearchName",
                table: "Foods",
                column: "SearchName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Foods_SearchVector",
                table: "Foods",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItem_RecipeId",
                table: "RecipeItem",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId",
                table: "Recipes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaterRecords_UserId_SyncedAt",
                table: "WaterRecords",
                columns: new[] { "UserId", "SyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_UserId_SyncedAt",
                table: "WeightRecords",
                columns: new[] { "UserId", "SyncedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaryEntries");

            migrationBuilder.DropTable(
                name: "FavoriteFoods");

            migrationBuilder.DropTable(
                name: "FavoriteRecipes");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "RecipeItem");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "WaterRecords");

            migrationBuilder.DropTable(
                name: "WeightRecords");

            migrationBuilder.DropTable(
                name: "Foods");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
