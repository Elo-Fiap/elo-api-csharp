using Elo.Seguranca.Pii;
using Elo.Seguranca.Taint;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Elo.Infrastructure.Seguranca;

/// <summary>
/// Sink "Banco" da taint analysis: antes de cada SaveChanges, todo texto novo ou alterado passa pela
/// <see cref="GuardaDeSaida"/>; se tinha dado sensível, o que vai para o banco é o texto mascarado
/// (ex.: "***.456.***-00" no lugar do CPF). Cobre também os tipos "owned" (entram como entradas do ChangeTracker).
/// Campos de contato da liderança e identificadores técnicos são isentos (ver <see cref="MotorDePii.EhChaveIsenta"/>).
/// </summary>
public sealed class InterceptadorDeGravacaoProtegida(GuardaDeSaida guarda, MotorDePii motor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Proteger(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Proteger(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Proteger(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        foreach (var entrada in contexto.ChangeTracker.Entries().ToList())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            foreach (var propriedade in entrada.Properties)
            {
                ProtegerPropriedade(entrada, propriedade);
            }
        }
    }

    private void ProtegerPropriedade(EntityEntry entrada, PropertyEntry propriedade)
    {
        var nome = propriedade.Metadata.Name;
        if (propriedade.Metadata.ClrType != typeof(string)
            || propriedade.CurrentValue is not string valor
            || valor.Length == 0
            || (entrada.State == EntityState.Modified && !propriedade.IsModified)
            || motor.EhChaveIsenta(nome))
        {
            return;
        }

        var onde = $"{entrada.Metadata.ShortName()}.{nome}";
        var resultado = guarda.Verificar(valor, Sink.Banco, motor.ModoDaChave(nome), onde);
        if (resultado.TemPii)
        {
            propriedade.CurrentValue = resultado.TextoMascarado;
        }
    }
}
