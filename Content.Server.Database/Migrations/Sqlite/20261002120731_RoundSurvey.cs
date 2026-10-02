using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
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
                    survey_digest_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    days = table.Column<int>(type: "INTEGER", nullable: false),
                    time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_digest", x => x.survey_digest_id);
                });

            migrationBuilder.CreateTable(
                name: "survey_response",
                columns: table => new
                {
                    survey_response_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    round_id = table.Column<int>(type: "INTEGER", nullable: false),
                    player_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    question = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<int>(type: "INTEGER", nullable: false),
                    time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    preset = table.Column<string>(type: "TEXT", nullable: false),
                    round_duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    player_count = table.Column<int>(type: "INTEGER", nullable: false),
                    left_count = table.Column<int>(type: "INTEGER", nullable: true),
                    job = table.Column<string>(type: "TEXT", nullable: true),
                    antag = table.Column<string>(type: "TEXT", nullable: true),
                    dead = table.Column<bool>(type: "INTEGER", nullable: false),
                    time_in_round = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    playtime = table.Column<TimeSpan>(type: "TEXT", nullable: false)
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
