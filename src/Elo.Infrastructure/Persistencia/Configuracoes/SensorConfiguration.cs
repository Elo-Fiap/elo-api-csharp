using Elo.Domain.Sensores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Elo.Infrastructure.Persistencia.Configuracoes;

internal sealed class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> builder)
    {
        builder.ToTable("Sensores");
        builder.HasKey(sensor => sensor.Id);

        builder.Property(sensor => sensor.Codigo).HasMaxLength(Sensor.TamanhoMaximoCodigo).IsRequired();
        builder.Property(sensor => sensor.Local).HasMaxLength(Sensor.TamanhoMaximoLocal).IsRequired();
        builder.HasIndex(sensor => sensor.Codigo).IsUnique();

        builder.HasMany(sensor => sensor.Leituras)
            .WithOne()
            .HasForeignKey(leitura => leitura.SensorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(sensor => sensor.Leituras).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LeituraSensorConfiguration : IEntityTypeConfiguration<LeituraSensor>
{
    public void Configure(EntityTypeBuilder<LeituraSensor> builder)
    {
        builder.ToTable("LeiturasDeSensor");
        builder.HasKey(leitura => leitura.Id);
        builder.HasIndex(leitura => new { leitura.SensorId, leitura.MedidaEm });
    }
}
