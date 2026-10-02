using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class RoundSurvey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_digest",
                columns: table => new
                {
                    survey_digest_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_digest", x => x.survey_digest_id);
                });

            migrationBuilder.CreateTable(
                name: "survey_response",
                columns: table => new
                {
                    survey_response_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    round_id = table.Column<int>(type: "integer", nullable: false),
                    player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    preset = table.Column<string>(type: "text", nullable: false),
                    round_duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    player_count = table.Column<int>(type: "integer", nullable: false),
                    left_count = table.Column<int>(type: "integer", nullable: true),
                    job = table.Column<string>(type: "text", nullable: true),
                    antag = table.Column<string>(type: "text", nullable: true),
                    dead = table.Column<bool>(type: "boolean", nullable: false),
                    time_in_round = table.Column<TimeSpan>(type: "interval", nullable: false),
                    playtime = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_response", x => x.survey_response_id);
                    table.ForeignKey(
                        name: "FK_survey_response_player_player_id",
                        column: x => x.player_user_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_survey_response_round_round_id",
                        column: x => x.round_id,
                        principalTable: "round",
                        principalColumn: "round_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_digest_start_days",
                table: "survey_digest",
                columns: new[] { "start", "days" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_response_player_user_id",
                table: "survey_response",
                column: "player_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_response_round_id_player_user_id_question",
                table: "survey_response",
                columns: new[] { "round_id", "player_user_id", "question" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_response_time",
                table: "survey_response",
                column: "time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_digest");

            migrationBuilder.DropTable(
                name: "survey_response");
        }
    }
}
