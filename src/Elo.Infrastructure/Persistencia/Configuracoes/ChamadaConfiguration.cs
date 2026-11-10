using Elo.Domain.Bairros;
using Elo.Domain.Chamadas;
using Elo.Domain.Ruas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elo.Infrastructure.Persistencia.Configuracoes;

internal sealed class ChamadaConfiguration : IEntityTypeConfiguration<Chamada>
{
    public void Configure(EntityTypeBuilder<Chamada> builder)
    {
        builder.ToTable("Chamadas");
        builder.HasKey(chamada => chamada.Id);

        // Herança TPH (Table Per Hierarchy): Real e Treino ficam na mesma tabela,
        // e a coluna "Discriminador" diz qual subclasse o EF Core deve criar ao ler.
        builder.HasDiscriminator<string>("Discriminador")
            .HasValue<ChamadaReal>("Real")
            .HasValue<ChamadaTreino>("Treino");
        builder.Property<string>("Discriminador").HasMaxLength(10);

        // Propriedades calculadas no C# não viram coluna.
        builder.Ignore(chamada => chamada.Tipo);
        builder.Ignore(chamada => chamada.Pergunta);
        builder.Ignore(chamada => chamada.FimDaJanela);

        builder.HasOne<Bairro>()
            .WithMany()
            .HasForeignKey(chamada => chamada.BairroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(chamada => new { chamada.BairroId, chamada.IniciadaEm });

        // Garantia no banco: no máximo UMA chamada sem encerramento por bairro (índice único filtrado).
        builder.HasIndex(chamada => chamada.BairroId, "UX_Chamadas_UmaAbertaPorBairro")
            .IsUnique()
            .HasFilter("\"EncerradaEm\" IS NULL");

        // Foto do indicador no encerramento (value object em colunas da própria tabela).
        builder.OwnsOne(chamada => chamada.Resultado, resultado =>
        {
            resultado.Property(r => r.TotalDeRuas).HasColumnName("ResultadoTotalDeRuas");
            resultado.Property(r => r.RuasComResposta).HasColumnName("ResultadoRuasComResposta");
            resultado.Property(r => r.RuasConhecidasNaJanela).HasColumnName("ResultadoRuasConhecidasEm15Min");
            resultado.Property(r => r.RuasQuePediramAjuda).HasColumnName("ResultadoRuasQuePediramAjuda");
            resultado.Property(r => r.MinutosAtePrimeiraRespostaEmMedia).HasColumnName("ResultadoMinutosAtePrimeiraResposta");
        });

        builder.HasMany(chamada => chamada.Respostas)
            .WithOne()
            .HasForeignKey(resposta => resposta.ChamadaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(chamada => chamada.Acoes)
            .WithOne()
            .HasForeignKey(acao => acao.ChamadaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(chamada => chamada.Respostas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(chamada => chamada.Acoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ChamadaRealConfiguration : IEntityTypeConfiguration<ChamadaReal>
{
    public void Configure(EntityTypeBuilder<ChamadaReal> builder)
    {
        builder.OwnsOne(chamada => chamada.Aviso, aviso =>
        {
            aviso.Property(a => a.Fonte).HasColumnName("AvisoFonte").HasMaxLength(AvisoOficial.TamanhoMaximoFonte);
            aviso.Property(a => a.Identificador).HasColumnName("AvisoIdentificador").HasMaxLength(AvisoOficial.TamanhoMaximoIdentificador);
            aviso.Property(a => a.Evento).HasColumnName("AvisoEvento").HasMaxLength(AvisoOficial.TamanhoMaximoEvento);
            aviso.Property(a => a.Severidade).HasColumnName("AvisoSeveridade").HasConversion<string>().HasMaxLength(20);
        });

        builder.Navigation(chamada => chamada.Aviso).IsRequired();
    }
}

internal sealed class ChamadaTreinoConfiguration : IEntityTypeConfiguration<ChamadaTreino>
{
    public void Configure(EntityTypeBuilder<ChamadaTreino> builder)
    {
        builder.Property(chamada => chamada.DuracaoMinutos).HasColumnName("TreinoDuracaoMinutos");
    }
}

internal sealed class RespostaConfiguration : IEntityTypeConfiguration<Resposta>
{
    public void Configure(EntityTypeBuilder<Resposta> builder)
    {
        builder.ToTable("Respostas");
        builder.HasKey(resposta => resposta.Id);

        builder.Property(resposta => resposta.Situacao).HasConversion<string>().HasMaxLength(20);
        builder.Property(resposta => resposta.Canal).HasConversion<string>().HasMaxLength(20);
        builder.Property(resposta => resposta.Observacao).HasMaxLength(Resposta.TamanhoMaximoObservacao);
        builder.Ignore(resposta => resposta.PediuAjuda);

        // Reenvio offline: o mesmo IdLocal nunca entra duas vezes.
        builder.HasIndex(resposta => resposta.IdLocal).IsUnique();
        builder.HasIndex(resposta => new { resposta.ChamadaId, resposta.RuaId, resposta.RespondidaEm });
        builder.HasIndex(resposta => resposta.RecebidaEm); // a rotina de retenção apaga por esta data

        builder.HasOne<Rua>()
            .WithMany()
            .HasForeignKey(resposta => resposta.RuaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AcaoCoordenadorConfiguration : IEntityTypeConfiguration<AcaoCoordenador>
{
    public void Configure(EntityTypeBuilder<AcaoCoordenador> builder)
    {
        builder.ToTable("AcoesDoCoordenador");
        builder.HasKey(acao => acao.Id);

        builder.Property(acao => acao.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(acao => new { acao.ChamadaId, acao.RuaId });

        builder.HasOne<Rua>()
            .WithMany()
            .HasForeignKey(acao => acao.RuaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
