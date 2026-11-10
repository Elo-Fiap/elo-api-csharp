using Elo.Domain.Bairros;
using Elo.Domain.Ruas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elo.Infrastructure.Persistencia.Configuracoes;

internal sealed class BairroConfiguration : IEntityTypeConfiguration<Bairro>
{
    public void Configure(EntityTypeBuilder<Bairro> builder)
    {
        builder.ToTable("Bairros");
        builder.HasKey(bairro => bairro.Id);

        builder.Property(bairro => bairro.Nome).HasMaxLength(Bairro.TamanhoMaximoNome).IsRequired();
        builder.Property(bairro => bairro.Cidade).HasMaxLength(Bairro.TamanhoMaximoCidade).IsRequired();
        builder.Property(bairro => bairro.Subprefeitura).HasMaxLength(Bairro.TamanhoMaximoSubprefeitura).IsRequired();
        builder.HasIndex(bairro => new { bairro.Nome, bairro.Cidade }).IsUnique();

        // Bairro 1 -> N Ruas. Excluir o bairro exclui as ruas (o serviço só permite se não houver chamadas).
        builder.HasMany(bairro => bairro.Ruas)
            .WithOne()
            .HasForeignKey(rua => rua.BairroId)
            .OnDelete(DeleteBehavior.Cascade);

        // A coleção é lida e escrita pelo campo privado _ruas (encapsulamento preservado).
        builder.Navigation(bairro => bairro.Ruas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RuaConfiguration : IEntityTypeConfiguration<Rua>
{
    public void Configure(EntityTypeBuilder<Rua> builder)
    {
        builder.ToTable("Ruas");
        builder.HasKey(rua => rua.Id);

        builder.Property(rua => rua.Nome).HasMaxLength(Rua.TamanhoMaximoNome).IsRequired();
        builder.Property(rua => rua.Microarea).HasMaxLength(Rua.TamanhoMaximoMicroarea).IsRequired();
        builder.Property(rua => rua.NivelRisco).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(rua => new { rua.BairroId, rua.Nome }).IsUnique();

        // Value objects (owned types): ficam em colunas da própria tabela Ruas.
        builder.OwnsOne(rua => rua.Lideranca, lideranca =>
        {
            lideranca.Property(l => l.Apelido).HasColumnName("LiderancaApelido").HasMaxLength(Lideranca.TamanhoMaximoApelido);
            lideranca.Property(l => l.WhatsApp).HasColumnName("LiderancaWhatsApp").HasMaxLength(13);
        });

        builder.OwnsOne(rua => rua.Plano, plano =>
        {
            plano.Property(p => p.Rota).HasColumnName("PlanoRota").HasMaxLength(PlanoDaRua.TamanhoMaximoRota);
            plano.Property(p => p.PontoDeEncontro).HasColumnName("PlanoPontoDeEncontro").HasMaxLength(PlanoDaRua.TamanhoMaximoLocal);
            plano.Property(p => p.Abrigo).HasColumnName("PlanoAbrigo").HasMaxLength(PlanoDaRua.TamanhoMaximoLocal);
            plano.PrimitiveCollection(p => p.ItensParaLevar).HasColumnName("PlanoItensParaLevar"); // lista salva como JSON
            plano.Property(p => p.AtualizadoEm).HasColumnName("PlanoAtualizadoEm");
        });

        builder.HasMany(rua => rua.Sensores)
            .WithOne()
            .HasForeignKey(sensor => sensor.RuaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(rua => rua.Sensores).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
