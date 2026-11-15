using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PainelAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Sensores",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Sensores",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Ruas",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Ruas",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracado",
                table: "Ruas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoIbge",
                table: "Bairros",
                type: "TEXT",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Bairros",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Bairros",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PercentualSemApp",
                table: "Bairros",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SemAppMedidoEm",
                table: "Bairros",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                table: "Bairros",
                type: "TEXT",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Sensores");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Sensores");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Ruas");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Ruas");

            migrationBuilder.DropColumn(
                name: "Tracado",
                table: "Ruas");

            migrationBuilder.DropColumn(
                name: "CodigoIbge",
                table: "Bairros");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Bairros");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Bairros");

            migrationBuilder.DropColumn(
                name: "PercentualSemApp",
                table: "Bairros");

            migrationBuilder.DropColumn(
                name: "SemAppMedidoEm",
                table: "Bairros");

            migrationBuilder.DropColumn(
                name: "Uf",
                table: "Bairros");
        }
    }
}
