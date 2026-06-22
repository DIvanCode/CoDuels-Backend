using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class NewUsersEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnticheatScores");

            migrationBuilder.DropTable(
                name: "CodeRuns");

            migrationBuilder.DropTable(
                name: "GroupDuels");

            migrationBuilder.DropTable(
                name: "GroupMemberships");

            migrationBuilder.DropTable(
                name: "Outbox");

            migrationBuilder.DropTable(
                name: "PendingDuels");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "TournamentParticipants");

            migrationBuilder.DropTable(
                name: "UserActions");

            migrationBuilder.DropTable(
                name: "Duels");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropTable(
                name: "DuelConfigurations");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.RenameColumn(
                name: "AuthTicket",
                table: "Users",
                newName: "IdentityTicket");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdentityTicket",
                table: "Users",
                column: "IdentityTicket",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Nickname",
                table: "Users",
                column: "Nickname",
                unique: true);
            
            migrationBuilder.Sql("CREATE UNIQUE INDEX IX_Users_Nickname_Lower ON \"Users\" ((lower(\"Nickname\")));");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RefreshToken",
                table: "Users",
                column: "RefreshToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IdentityTicket",
                table: "Users");
            
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Users_Nickname_Lower;");

            migrationBuilder.DropIndex(
                name: "IX_Users_Nickname",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "IdentityTicket",
                table: "Users",
                newName: "AuthTicket");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.CreateTable(
                name: "CodeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ExecutionId = table.Column<string>(type: "text", nullable: true),
                    HandledStatusCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TaskKey = table.Column<string>(type: "varchar(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DuelConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsRated = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    ShouldShowOpponentSolution = table.Column<bool>(type: "boolean", nullable: false),
                    TasksCount = table.Column<int>(type: "integer", nullable: false),
                    TasksOrder = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelConfigurations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Retries = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RetryAt = table.Column<DateTime>(type: "timestamp", nullable: true),
                    RetryUntil = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<int>(type: "integer", nullable: false),
                    TaskKey = table.Column<string>(type: "varchar(1)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    BeginLine = table.Column<int>(type: "integer", nullable: true),
                    CharsCount = table.Column<int>(type: "integer", nullable: true),
                    CodeLength = table.Column<int>(type: "integer", nullable: true),
                    CursorLine = table.Column<int>(type: "integer", nullable: true),
                    EndLine = table.Column<int>(type: "integer", nullable: true),
                    PreviousCursorLine = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Duels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    User1Id = table.Column<int>(type: "integer", nullable: false),
                    User2Id = table.Column<int>(type: "integer", nullable: false),
                    WinnerId = table.Column<int>(type: "integer", nullable: true),
                    DeadlineTime = table.Column<DateTime>(type: "timestamp", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Tasks = table.Column<string>(type: "text", nullable: false),
                    User1FinalRating = table.Column<int>(type: "integer", nullable: true),
                    User1InitRating = table.Column<int>(type: "integer", nullable: false),
                    User1Solutions = table.Column<string>(type: "text", nullable: false),
                    User2FinalRating = table.Column<int>(type: "integer", nullable: true),
                    User2InitRating = table.Column<int>(type: "integer", nullable: false),
                    User2Solutions = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Duels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Duels_DuelConfigurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Duels_Users_User1Id",
                        column: x => x.User1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Duels_Users_User2Id",
                        column: x => x.User2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Duels_Users_WinnerId",
                        column: x => x.WinnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupMemberships",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    InvitedById = table.Column<int>(type: "integer", nullable: true),
                    InvitationPending = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMemberships", x => new { x.UserId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Users_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    DuelConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    MatchmakingType = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    GroupStageDuelIds = table.Column<string>(type: "text", nullable: true),
                    Nodes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_DuelConfigurations_DuelConfigurationId",
                        column: x => x.DuelConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tournaments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tournaments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnticheatScores",
                columns: table => new
                {
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TaskKey = table.Column<string>(type: "varchar(1)", nullable: false),
                    Score = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnticheatScores", x => new { x.DuelId, x.UserId, x.TaskKey });
                    table.ForeignKey(
                        name: "FK_AnticheatScores_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnticheatScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupDuels",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupDuels", x => new { x.GroupId, x.DuelId });
                    table.ForeignKey(
                        name: "FK_GroupDuels_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupDuels_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupDuels_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    HandledStatusCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsUpsolving = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Solution = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmitTime = table.Column<DateTime>(type: "timestamp", nullable: false),
                    TaskKey = table.Column<string>(type: "varchar(1)", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Submissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendingDuels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    FriendlyPendingDuel_ConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    FriendlyPendingDuel_User1Id = table.Column<int>(type: "integer", nullable: true),
                    FriendlyPendingDuel_User2Id = table.Column<int>(type: "integer", nullable: true),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: true),
                    ConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    User1Id = table.Column<int>(type: "integer", nullable: true),
                    User2Id = table.Column<int>(type: "integer", nullable: true),
                    IsAcceptedByUser1 = table.Column<bool>(type: "boolean", nullable: true),
                    IsAcceptedByUser2 = table.Column<bool>(type: "boolean", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    TournamentPendingDuel_ConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    TournamentId = table.Column<int>(type: "integer", nullable: true),
                    TournamentPendingDuel_User1Id = table.Column<int>(type: "integer", nullable: true),
                    TournamentPendingDuel_User2Id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingDuels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingDuels_DuelConfigurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingDuels_DuelConfigurations_FriendlyPendingDuel_Configu~",
                        column: x => x.FriendlyPendingDuel_ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingDuels_DuelConfigurations_TournamentPendingDuel_Confi~",
                        column: x => x.TournamentPendingDuel_ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingDuels_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_FriendlyPendingDuel_User1Id",
                        column: x => x.FriendlyPendingDuel_User1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_FriendlyPendingDuel_User2Id",
                        column: x => x.FriendlyPendingDuel_User2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_TournamentPendingDuel_User1Id",
                        column: x => x.TournamentPendingDuel_User1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_TournamentPendingDuel_User2Id",
                        column: x => x.TournamentPendingDuel_User2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_User1Id",
                        column: x => x.User1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_User2Id",
                        column: x => x.User2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentParticipants",
                columns: table => new
                {
                    TournamentId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentParticipants", x => new { x.TournamentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TournamentParticipants_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnticheatScores_UserId",
                table: "AnticheatScores",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_DuelId_TaskKey",
                table: "CodeRuns",
                columns: new[] { "DuelId", "TaskKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_UserId",
                table: "CodeRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelConfigurations_OwnerId",
                table: "DuelConfigurations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_ConfigurationId",
                table: "Duels",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_User1Id",
                table: "Duels",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_User2Id",
                table: "Duels",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_WinnerId",
                table: "Duels",
                column: "WinnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDuels_CreatedById",
                table: "GroupDuels",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GroupDuels_DuelId",
                table: "GroupDuels",
                column: "DuelId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_GroupId",
                table: "GroupMemberships",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_InvitedById",
                table: "GroupMemberships",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_ConfigurationId",
                table: "PendingDuels",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_CreatedById",
                table: "PendingDuels",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_FriendlyPendingDuel_ConfigurationId",
                table: "PendingDuels",
                column: "FriendlyPendingDuel_ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_FriendlyPendingDuel_User1Id",
                table: "PendingDuels",
                column: "FriendlyPendingDuel_User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_FriendlyPendingDuel_User2Id",
                table: "PendingDuels",
                column: "FriendlyPendingDuel_User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_GroupId",
                table: "PendingDuels",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentId",
                table: "PendingDuels",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_ConfigurationId",
                table: "PendingDuels",
                column: "TournamentPendingDuel_ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_User1Id",
                table: "PendingDuels",
                column: "TournamentPendingDuel_User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_User2Id",
                table: "PendingDuels",
                column: "TournamentPendingDuel_User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_User1Id",
                table: "PendingDuels",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_User2Id",
                table: "PendingDuels",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_UserId",
                table: "PendingDuels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_DuelId",
                table: "Submissions",
                column: "DuelId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId",
                table: "Submissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentParticipants_UserId",
                table: "TournamentParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatedById",
                table: "Tournaments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_DuelConfigurationId",
                table: "Tournaments",
                column: "DuelConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_GroupId",
                table: "Tournaments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActions_DuelId_UserId_TaskKey_SequenceId",
                table: "UserActions",
                columns: new[] { "DuelId", "UserId", "TaskKey", "SequenceId" });
        }
    }
}
