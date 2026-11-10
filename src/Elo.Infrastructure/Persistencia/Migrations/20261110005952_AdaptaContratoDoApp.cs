using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdaptaContratoDoApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sem SQL de propósito: no SQLite, Guid e texto já são gravados como TEXT, e o tamanho
            // máximo não é aplicado pelo banco. Mudou só o modelo, para o contrato do app:
            // Respostas.IdLocal virou texto (até 64 caracteres) e Respostas.Observacao foi para 200.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nada a desfazer no banco (veja o Up).
        }
    }
}
