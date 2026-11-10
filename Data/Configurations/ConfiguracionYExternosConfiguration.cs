#nullable enable
using IVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVC.Data.Configurations;

// Configuración de la app (ABM_PLANI, UsuariosVinculados, USUARIOS del legado) y bajadas
// externas de sólo lectura (PASES_SADE, GEDO, DEVENGADOS), mapeadas sin clave: EF no las
// trackea y cada fila es una instancia distinta. Verificado contra IVC_TEST el 09/11/2026.

public sealed class AbmPlaniConfiguration : IEntityTypeConfiguration<AbmPlani>
{
    public void Configure(EntityTypeBuilder<AbmPlani> e)
    {
        e.ToTable("ABM_PLANI");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("Id");
        e.Property(x => x.PlanificacionAbierta).HasColumnName("PLANIFICACION_ABIERTA");
        e.Property(x => x.FormulacionAbierta).HasColumnName("FORMULACION_ABIERTA");
    }
}

public sealed class UsuarioVinculadoConfiguration : IEntityTypeConfiguration<UsuarioVinculado>
{
    public void Configure(EntityTypeBuilder<UsuarioVinculado> e)
    {
        e.ToTable("UsuariosVinculados");
        e.HasKey(x => x.AuthUserId);
        // Tabla creada por el legado con EF6: nvarchar, a diferencia del resto de la base.
        e.Property(x => x.AuthUserId).HasColumnName("AuthUserId").HasMaxLength(450).IsUnicode(true);
        // nvarchar(50) NOT NULL: el servicio que sincroniza el nombre desde el claim debe truncar.
        e.Property(x => x.Nombre).HasColumnName("Nombre").HasMaxLength(50).IsUnicode(true).IsRequired();
        e.Property(x => x.PlanificacionAbierta).HasColumnName("PLANIFICACION_ABIERTA");
    }
}

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> e)
    {
        e.ToTable("USUARIOS");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.Nombre).HasColumnName("NOMBRE").HasMaxLength(255);
        e.Property(x => x.Correo).HasColumnName("CORREO").HasMaxLength(255);
        e.Property(x => x.Tipo).HasColumnName("TIPO");
        e.Property(x => x.Estado).HasColumnName("ESTADO");
        e.Property(x => x.AreaId).HasColumnName("AREA");
        e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId);
    }
}

public sealed class PaseSadeConfiguration : IEntityTypeConfiguration<PaseSade>
{
    public void Configure(EntityTypeBuilder<PaseSade> e)
    {
        e.ToTable("PASES_SADE");
        e.HasNoKey();
        // varchar en la base: el parámetro debe ser varchar para que no haya conflicto de
        // intercalación ni conversión implícita que anule el índice (lección de SAF).
        e.Property(x => x.Expediente).HasColumnName("EXPEDIENTE").HasMaxLength(209);
        e.Property(x => x.BuzonDestino).HasColumnName("BUZON DESTINO").HasMaxLength(612);
        e.Property(x => x.FechaUltimoPase).HasColumnName("FECHA ULTIMO PASE").HasColumnType("datetime");
    }
}

public sealed class GedoConfiguration : IEntityTypeConfiguration<Gedo>
{
    public void Configure(EntityTypeBuilder<Gedo> e)
    {
        e.ToTable("GEDO");
        e.HasNoKey();
        // Bajada externa en nvarchar(510), sin clave primaria real.
        e.Property(x => x.Expediente).HasColumnName("NRO_EXPEDIENTE").HasMaxLength(510).IsUnicode(true);
        e.Property(x => x.Acdir).HasColumnName("NRO_ESPECIAL").HasMaxLength(510).IsUnicode(true);
    }
}

public sealed class DevengadoConfiguration : IEntityTypeConfiguration<Devengado>
{
    public void Configure(EntityTypeBuilder<Devengado> e)
    {
        // La tabla tiene ID identity, pero la app sólo suma IMPORTE_PP por expediente:
        // sin clave para que EF no trackee las filas (igual que SAF).
        e.ToTable("DEVENGADOS");
        e.HasNoKey();
        e.Property(x => x.EeFinanciera).HasColumnName("EE_FINANCIERA").HasMaxLength(50);
        e.Property(x => x.ImportePp).HasColumnName("IMPORTE_PP").HasPrecision(15, 2);
    }
}
