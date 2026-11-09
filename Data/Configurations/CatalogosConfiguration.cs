#nullable enable
using IVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVC.Data.Configurations;

// Catálogos: tablas chicas de ID + NOMBRE (varchar(255) salvo UNIDADES_MEDIDA, varchar(50)).
// Verificado contra IVC_TEST el 09/11/2026.

public sealed class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> e) => Catalogo.Configurar(e, "EMPRESAS");
}

public sealed class ContrataConfiguration : IEntityTypeConfiguration<Contrata>
{
    public void Configure(EntityTypeBuilder<Contrata> e) => Catalogo.Configurar(e, "CONTRATA");
}

public sealed class BarrioConfiguration : IEntityTypeConfiguration<Barrio>
{
    public void Configure(EntityTypeBuilder<Barrio> e) => Catalogo.Configurar(e, "BARRIOS");
}

public sealed class ConceptoConfiguration : IEntityTypeConfiguration<Concepto>
{
    public void Configure(EntityTypeBuilder<Concepto> e) => Catalogo.Configurar(e, "CONCEPTOS");
}

public sealed class EstadoAutorizanteConfiguration : IEntityTypeConfiguration<EstadoAutorizante>
{
    public void Configure(EntityTypeBuilder<EstadoAutorizante> e) => Catalogo.Configurar(e, "ESTADOS_AUTORIZANTES");
}

public sealed class EstadoRedetConfiguration : IEntityTypeConfiguration<EstadoRedet>
{
    public void Configure(EntityTypeBuilder<EstadoRedet> e) => Catalogo.Configurar(e, "ESTADOS_REDET");
}

public sealed class TipoPagoConfiguration : IEntityTypeConfiguration<TipoPago>
{
    public void Configure(EntityTypeBuilder<TipoPago> e) => Catalogo.Configurar(e, "TIPO_PAGO");
}

public sealed class PrioridadConfiguration : IEntityTypeConfiguration<Prioridad>
{
    // PRIORIDADES no tiene PRIMARY KEY en la base (ID es identity igual): EF necesita una clave y usa ID.
    public void Configure(EntityTypeBuilder<Prioridad> e) => Catalogo.Configurar(e, "PRIORIDADES");
}

public sealed class UnidadMedidaConfiguration : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> e) => Catalogo.Configurar(e, "UNIDADES_MEDIDA", largoNombre: 50);
}

public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> e)
    {
        Catalogo.Configurar(e, "AREAS");
        e.Property(x => x.AuthAreaId).HasColumnName("AuthAreaId");
    }
}

public sealed class LineaGestionConfiguration : IEntityTypeConfiguration<LineaGestion>
{
    public void Configure(EntityTypeBuilder<LineaGestion> e)
    {
        Catalogo.Configurar(e, "LINEA_DE_GESTION");
        e.Property(x => x.Tipo).HasColumnName("TIPO").HasMaxLength(255);
        e.Property(x => x.Grupo).HasColumnName("GRUPO").HasMaxLength(255);
        e.Property(x => x.Reparticion).HasColumnName("REPARTICION").HasMaxLength(255);
    }
}

public sealed class LineaGestionFFConfiguration : IEntityTypeConfiguration<LineaGestionFF>
{
    public void Configure(EntityTypeBuilder<LineaGestionFF> e)
    {
        e.ToTable("LINEA_DE_GESTION_FF");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.LineaGestionId).HasColumnName("ID_LINEA_GESTION");
        e.Property(x => x.Nombre).HasColumnName("NOMBRE_FF").HasMaxLength(255);
        e.Property(x => x.Fuente).HasColumnName("FUENTE").HasMaxLength(255);
        e.HasOne(x => x.LineaGestion).WithMany(l => l.FuentesFinanciamiento).HasForeignKey(x => x.LineaGestionId);
    }
}

internal static class Catalogo
{
    /// <summary>Tabla de catálogo: columnas ID (identity) y NOMBRE.</summary>
    public static void Configurar<T>(EntityTypeBuilder<T> e, string tabla, int largoNombre = 255) where T : class
    {
        e.ToTable(tabla);
        e.HasKey("Id");
        e.Property<int>("Id").HasColumnName("ID").ValueGeneratedOnAdd();
        e.Property<string?>("Nombre").HasColumnName("NOMBRE").HasMaxLength(largoNombre);
    }
}
