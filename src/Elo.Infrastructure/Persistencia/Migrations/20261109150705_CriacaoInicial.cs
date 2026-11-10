using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bairros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Cidade = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Subprefeitura = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DadosSimulados = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bairros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chamadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BairroId = table.Column<int>(type: "INTEGER", nullable: false),
                    IniciadaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EncerradaEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EncerramentoPrevistoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResultadoTotalDeRuas = table.Column<int>(type: "INTEGER", nullable: true),
                    ResultadoRuasComResposta = table.Column<int>(type: "INTEGER", nullable: true),
                    ResultadoRuasConhecidasEm15Min = table.Column<int>(type: "INTEGER", nullable: true),
                    ResultadoRuasQuePediramAjuda = table.Column<int>(type: "INTEGER", nullable: true),
                    ResultadoMinutosAtePrimeiraResposta = table.Column<double>(type: "REAL", nullable: true),
                    Discriminador = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AvisoFonte = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    AvisoIdentificador = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    AvisoEvento = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AvisoSeveridade = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TreinoDuracaoMinutos = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chamadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chamadas_Bairros_BairroId",
                        column: x => x.BairroId,
                        principalTable: "Bairros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ruas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BairroId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Microarea = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NivelRisco = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    LiderancaApelido = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    LiderancaWhatsApp = table.Column<string>(type: "TEXT", maxLength: 13, nullable: true),
                    PlanoRota = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PlanoPontoDeEncontro = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PlanoAbrigo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PlanoItensParaLevar = table.Column<string>(type: "TEXT", nullable: true),
                    PlanoAtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ruas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ruas_Bairros_BairroId",
                        column: x => x.BairroId,
                        principalTable: "Bairros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcoesDoCoordenador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChamadaId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RegistradaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcoesDoCoordenador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcoesDoCoordenador_Chamadas_ChamadaId",
                        column: x => x.ChamadaId,
                        principalTable: "Chamadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcoesDoCoordenador_Ruas_RuaId",
                        column: x => x.RuaId,
                        principalTable: "Ruas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Respostas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdLocal = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChamadaId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Situacao = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Pessoas = table.Column<int>(type: "INTEGER", nullable: true),
                    Observacao = table.Column<string>(type: "TEXT", maxLength: 280, nullable: true),
                    Canal = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RespondidaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Respostas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Respostas_Chamadas_ChamadaId",
                        column: x => x.ChamadaId,
                        principalTable: "Chamadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Respostas_Ruas_RuaId",
                        column: x => x.RuaId,
                        principalTable: "Ruas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sensores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Local = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    LimiteAlertaCm = table.Column<double>(type: "REAL", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sensores_Ruas_RuaId",
                        column: x => x.RuaId,
                        principalTable: "Ruas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeiturasDeSensor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SensorId = table.Column<int>(type: "INTEGER", nullable: false),
                    NivelCm = table.Column<double>(type: "REAL", nullable: false),
                    LimiteNaHoraCm = table.Column<double>(type: "REAL", nullable: false),
                    AcimaDoLimite = table.Column<bool>(type: "INTEGER", nullable: false),
                    MedidaEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecebidaEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeiturasDeSensor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeiturasDeSensor_Sensores_SensorId",
                        column: x => x.SensorId,
                        principalTable: "Sensores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesDoCoordenador_ChamadaId_RuaId",
                table: "AcoesDoCoordenador",
                columns: new[] { "ChamadaId", "RuaId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesDoCoordenador_RuaId",
                table: "AcoesDoCoordenador",
                column: "RuaId");

            migrationBuilder.CreateIndex(
                name: "IX_Bairros_Nome_Cidade",
                table: "Bairros",
                columns: new[] { "Nome", "Cidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chamadas_BairroId_IniciadaEm",
                table: "Chamadas",
                columns: new[] { "BairroId", "IniciadaEm" });

            migrationBuilder.CreateIndex(
                name: "UX_Chamadas_UmaAbertaPorBairro",
                table: "Chamadas",
                column: "BairroId",
                unique: true,
                filter: "\"EncerradaEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasDeSensor_SensorId_MedidaEm",
                table: "LeiturasDeSensor",
                columns: new[] { "SensorId", "MedidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_ChamadaId_RuaId_RespondidaEm",
                table: "Respostas",
                columns: new[] { "ChamadaId", "RuaId", "RespondidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_IdLocal",
                table: "Respostas",
                column: "IdLocal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_RecebidaEm",
                table: "Respostas",
                column: "RecebidaEm");

            migrationBuilder.CreateIndex(
                name: "IX_Respostas_RuaId",
                table: "Respostas",
                column: "RuaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ruas_BairroId_Nome",
                table: "Ruas",
                columns: new[] { "BairroId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sensores_Codigo",
                table: "Sensores",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sensores_RuaId",
                table: "Sensores",
                column: "RuaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcoesDoCoordenador");

            migrationBuilder.DropTable(
                name: "LeiturasDeSensor");

            migrationBuilder.DropTable(
                name: "Respostas");

            migrationBuilder.DropTable(
                name: "Sensores");

            migrationBuilder.DropTable(
                name: "Chamadas");

            migrationBuilder.DropTable(
                name: "Ruas");

            migrationBuilder.DropTable(
                name: "Bairros");
        }
    }
}
