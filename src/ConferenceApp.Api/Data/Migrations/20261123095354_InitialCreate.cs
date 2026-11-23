using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ConferenceApp.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261123095354_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conferences",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conferences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Speakers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Bio = table.Column<string>(type: "text", nullable: true),
                Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Links = table.Column<List<string>>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Speakers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Rooms",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ConferenceId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Capacity = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Rooms", x => x.Id);
                table.ForeignKey(
                    name: "FK_Rooms_Conferences_ConferenceId",
                    column: x => x.ConferenceId,
                    principalTable: "Conferences",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Tracks",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ConferenceId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tracks", x => x.Id);
                table.ForeignKey(
                    name: "FK_Tracks_Conferences_ConferenceId",
                    column: x => x.ConferenceId,
                    principalTable: "Conferences",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Sessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ConferenceId = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Abstract = table.Column<string>(type: "text", nullable: true),
                Day = table.Column<DateOnly>(type: "date", nullable: false),
                StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                RoomId = table.Column<int>(type: "integer", nullable: false),
                TrackId = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Sessions_Conferences_ConferenceId",
                    column: x => x.ConferenceId,
                    principalTable: "Conferences",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Sessions_Rooms_RoomId",
                    column: x => x.RoomId,
                    principalTable: "Rooms",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_Sessions_Tracks_TrackId",
                    column: x => x.TrackId,
                    principalTable: "Tracks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "SessionSpeaker",
            columns: table => new
            {
                SessionsId = table.Column<int>(type: "integer", nullable: false),
                SpeakersId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionSpeaker", x => new { x.SessionsId, x.SpeakersId });
                table.ForeignKey(
                    name: "FK_SessionSpeaker_Sessions_SessionsId",
                    column: x => x.SessionsId,
                    principalTable: "Sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SessionSpeaker_Speakers_SpeakersId",
                    column: x => x.SpeakersId,
                    principalTable: "Speakers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Conferences_Slug",
            table: "Conferences",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Rooms_ConferenceId",
            table: "Rooms",
            column: "ConferenceId");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_ConferenceId",
            table: "Sessions",
            column: "ConferenceId");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_RoomId_Day_StartTime",
            table: "Sessions",
            columns: new[] { "RoomId", "Day", "StartTime" });

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_TrackId",
            table: "Sessions",
            column: "TrackId");

        migrationBuilder.CreateIndex(
            name: "IX_SessionSpeaker_SpeakersId",
            table: "SessionSpeaker",
            column: "SpeakersId");

        migrationBuilder.CreateIndex(
            name: "IX_Tracks_ConferenceId",
            table: "Tracks",
            column: "ConferenceId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SessionSpeaker");

        migrationBuilder.DropTable(
            name: "Sessions");

        migrationBuilder.DropTable(
            name: "Speakers");

        migrationBuilder.DropTable(
            name: "Rooms");

        migrationBuilder.DropTable(
            name: "Tracks");

        migrationBuilder.DropTable(
            name: "Conferences");
    }
}
