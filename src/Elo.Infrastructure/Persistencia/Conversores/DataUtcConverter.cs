using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Elo.Infrastructure.Persistencia.Conversores;

/// <summary>
/// O SQLite não guarda se a data é UTC ou local. Este conversor garante que toda data
/// saia do banco marcada como UTC (e que o JSON da API mostre o "Z" no final).
/// </summary>
internal sealed class DataUtcConverter() : ValueConverter<DateTime, DateTime>(
    paraOBanco => paraOBanco.Kind == DateTimeKind.Utc ? paraOBanco : paraOBanco.ToUniversalTime(),
    doBanco => DateTime.SpecifyKind(doBanco, DateTimeKind.Utc));
