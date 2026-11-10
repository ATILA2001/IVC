#nullable enable
using IVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVC.Data.Configurations;

// Mapeo del módulo de redeterminaciones. Autorizantes, Certificados y Redeterminaciones se
// vinculan por CODIGO_AUTORIZANTE (única en AUTORIZANTES, pero no es la clave primaria):
// sin navegación EF, se resuelve en los servicios como en el legado.
// Verificado contra IVC_TEST el 09/11/2026 (Database/esquema-columnas.sql).

public sealed class AutorizanteConfiguration : IEntityTypeConfiguration<Autorizante>
{
    public void Configure(EntityTypeBuilder<Autorizante> e)
    {
        e.ToTable("AUTORIZANTES");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        e.Property(x => x.CodigoAutorizante).HasColumnName("CODIGO_AUTORIZANTE").HasMaxLength(50);
        e.Property(x => x.Detalle).HasColumnName("DETALLE").HasMaxLength(255);
        e.Property(x => x.Expediente).HasColumnName("EXPEDIENTE").HasMaxLength(50);
        e.Property(x => x.MontoAutorizado).HasColumnName("MONTO_AUTORIZADO").HasPrecision(15, 2);
        e.Property(x => x.MesAprobacion).HasColumnName("MES").HasColumnType("date");
        e.Property(x => x.MesBase).HasColumnName("MES_BASE").HasColumnType("date");
        e.Property(x => x.ObraId).HasColumnName("OBRA");
        e.Property(x => x.ConceptoId).HasColumnName("CONCEPTO");
        e.Property(x => x.EstadoId).HasColumnName("ESTADO");
        // NOT NULL en la base y sin mapear en el legado: hay que enviarla en el INSERT.
        e.Property(x => x.AutorizacionGG).HasColumnName("AUTORIZACION_GG");

        e.HasIndex(x => x.CodigoAutorizante).IsUnique();
        e.HasOne(x => x.Obra).WithMany(o => o.Autorizantes).HasForeignKey(x => x.ObraId);
        e.HasOne(x => x.Concepto).WithMany().HasForeignKey(x => x.ConceptoId);
        e.HasOne(x => x.Estado).WithMany().HasForeignKey(x => x.EstadoId);
    }
}

public sealed class CertificadoConfiguration : IEntityTypeConfiguration<Certificado>
{
    public void Configure(EntityTypeBuilder<Certificado> e)
    {
        e.ToTable("CERTIFICADOS");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.CodigoAutorizante).HasColumnName("CODIGO_AUTORIZANTE").HasMaxLength(50).IsRequired();
        e.Property(x => x.ExpedientePago).HasColumnName("EXPEDIENTE_PAGO").HasMaxLength(50);
        e.Property(x => x.MontoTotal).HasColumnName("MONTO_TOTAL").HasPrecision(15, 2);
        e.Property(x => x.MesAprobacion).HasColumnName("MES_APROBACION").HasColumnType("date");
        e.Property(x => x.TipoPagoId).HasColumnName("TIPO_PAGO");
        e.Property(x => x.PorcEjecFisica).HasColumnName("PORC_EJEC_FISICA").HasPrecision(5, 2);

        e.HasIndex(x => x.CodigoAutorizante);
        e.HasOne(x => x.TipoPago).WithMany().HasForeignKey(x => x.TipoPagoId);
    }
}

public sealed class RedeterminacionConfiguration : IEntityTypeConfiguration<Redeterminacion>
{
    public void Configure(EntityTypeBuilder<Redeterminacion> e)
    {
        e.ToTable("REDETERMINACIONES");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.CodigoAutorizante).HasColumnName("CODIGO_AUTORIZANTE").HasMaxLength(50);
        e.Property(x => x.Expediente).HasColumnName("EXPEDIENTE").HasMaxLength(50);
        e.Property(x => x.Salto).HasColumnName("SALTO").HasColumnType("date");
        e.Property(x => x.Nro).HasColumnName("NRO");
        e.Property(x => x.Tipo).HasColumnName("TIPO").HasMaxLength(50);
        e.Property(x => x.EtapaId).HasColumnName("ETAPA");
        e.Property(x => x.Observaciones).HasColumnName("OBSERVACIONES").HasMaxLength(255);
        // Columna calculada en la base (varchar(82)): se lee, nunca se envía en INSERT/UPDATE.
        e.Property(x => x.CodigoRedet).HasColumnName("CODIGO_REDET").HasMaxLength(82).ValueGeneratedOnAddOrUpdate();
        e.Property(x => x.Porcentaje).HasColumnName("PORCENTAJE_PONDERACION").HasPrecision(5, 2);
        e.Property(x => x.Monto).HasColumnName("MONTO").HasPrecision(5, 2);
        e.Property(x => x.UsuarioId).HasColumnName("ID_USUARIO");

        e.HasIndex(x => x.CodigoAutorizante);
        e.HasOne(x => x.Etapa).WithMany().HasForeignKey(x => x.EtapaId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }
}

public sealed class ExpedienteReliqConfiguration : IEntityTypeConfiguration<ExpedienteReliq>
{
    public void Configure(EntityTypeBuilder<ExpedienteReliq> e)
    {
        e.ToTable("EXPEDIENTES_RELIQ");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.CodigoRedet).HasColumnName("CODIGO_REDET").HasMaxLength(50).IsRequired();
        e.Property(x => x.MesAprobacion).HasColumnName("MES_APROBACION").HasColumnType("date");
        e.Property(x => x.Expediente).HasColumnName("EXPEDIENTE").HasMaxLength(100);
        // UK_EXPEDIENTE_RELIQ en la base.
        e.HasIndex(x => new { x.CodigoRedet, x.MesAprobacion }).IsUnique();
    }
}
