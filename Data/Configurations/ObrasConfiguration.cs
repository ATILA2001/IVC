#nullable enable
using IVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IVC.Data.Configurations;

// Mapeo del módulo de planificación: Obras y sus dependientes directos.
// Nombres, tipos, largos, precisión y nulabilidad verificados contra IVC_TEST el 09/11/2026
// con Database/esquema-columnas.sql. Las cadenas son varchar salvo que se indique lo
// contrario (convención del contexto); las fechas de negocio son columnas "date".

public sealed class ObraConfiguration : IEntityTypeConfiguration<Obra>
{
    public void Configure(EntityTypeBuilder<Obra> e)
    {
        e.ToTable("OBRAS");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.Descripcion).HasColumnName("DESCRIPCION").HasMaxLength(255);
        e.Property(x => x.Numero).HasColumnName("NUMERO");
        e.Property(x => x.Anio).HasColumnName("AÑO");
        e.Property(x => x.ObraNumero).HasColumnName("OBRA");
        e.Property(x => x.Etapa).HasColumnName("ETAPA");
        e.Property(x => x.EmpresaId).HasColumnName("EMPRESA");
        e.Property(x => x.ContrataId).HasColumnName("CONTRATA");
        e.Property(x => x.BarrioId).HasColumnName("BARRIO");
        e.Property(x => x.AreaId).HasColumnName("AREA");

        // AREA y EMPRESA son NOT NULL en la base; CONTRATA y BARRIO admiten NULL.
        e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId);
        e.HasOne(x => x.Contrata).WithMany().HasForeignKey(x => x.ContrataId);
        e.HasOne(x => x.Barrio).WithMany().HasForeignKey(x => x.BarrioId);
        e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId);
    }
}

public sealed class ProyectoConfiguration : IEntityTypeConfiguration<Proyecto>
{
    public void Configure(EntityTypeBuilder<Proyecto> e)
    {
        e.ToTable("BD_PROYECTOS");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.ObraId).HasColumnName("ID_BASE");
        e.Property(x => x.Nombre).HasColumnName("PROYECTO").HasMaxLength(255);
        e.Property(x => x.SubProyecto).HasColumnName("SUBPROYECTO").HasMaxLength(255).IsRequired();
        e.Property(x => x.LineaGestionId).HasColumnName("LINEA_DE_GESTION");
        e.Property(x => x.Autorizado2025).HasColumnName("AUTORIZADO2025").HasPrecision(15, 2);
        e.Property(x => x.Autorizado2026).HasColumnName("AUTORIZADO2026").HasPrecision(15, 2);

        // ID_BASE tiene restricción UNIQUE en la base: una fila por obra.
        e.HasIndex(x => x.ObraId).IsUnique();
        e.HasOne(x => x.Obra).WithOne(o => o.Proyecto).HasForeignKey<Proyecto>(x => x.ObraId);
        e.HasOne(x => x.LineaGestion).WithMany().HasForeignKey(x => x.LineaGestionId);
    }
}

public sealed class MovimientoGestionConfiguration : IEntityTypeConfiguration<MovimientoGestion>
{
    public void Configure(EntityTypeBuilder<MovimientoGestion> e)
    {
        e.ToTable("MOVIMIENTOS_GESTION");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.ObraId).HasColumnName("ID_BASE");
        e.Property(x => x.Monto).HasColumnName("MOVIMIENTO").HasPrecision(15, 2);
        e.Property(x => x.Fecha).HasColumnName("FECHA").HasColumnType("date");
        e.HasOne(x => x.Obra).WithMany(o => o.Movimientos).HasForeignKey(x => x.ObraId);
    }
}

public sealed class FormulacionConfiguration : IEntityTypeConfiguration<Formulacion>
{
    public void Configure(EntityTypeBuilder<Formulacion> e)
    {
        e.ToTable("FORMULACION");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        e.Property(x => x.ObraId).HasColumnName("ID_BASE");
        e.Property(x => x.FechaPeriodo).HasColumnName("FECHA_PERIODO").HasColumnType("date");
        e.Property(x => x.Monto).HasColumnName("MONTO").HasPrecision(18, 2);
        e.Property(x => x.MesBase).HasColumnName("MES_BASE").HasColumnType("date");
        e.Property(x => x.Observaciones).HasColumnName("OBSERVACIONES").HasMaxLength(Formulacion.ObservacionesMaxLength);
        e.Property(x => x.ValorMedida).HasColumnName("VALOR_MEDIDA").HasPrecision(18, 2);
        e.Property(x => x.Ppi).HasColumnName("PPI");
        e.Property(x => x.Techos).HasColumnName("TECHOS").HasPrecision(18, 2);
        e.Property(x => x.UnidadMedidaId).HasColumnName("ID_UNIDAD_MEDIDA");
        e.Property(x => x.PrioridadId).HasColumnName("PRIORIDAD");
        // La única columna nvarchar de las tablas del legado (el resto es varchar).
        e.Property(x => x.BreveDescripcion).HasColumnName("BREVE_DESCRIPCION")
            .HasMaxLength(Formulacion.BreveDescripcionMaxLength).IsUnicode(true);
        e.Property(x => x.FechaInicio).HasColumnName("FECHA_INICIO").HasColumnType("date");
        e.Property(x => x.FechaFin).HasColumnName("FECHA_FIN").HasColumnType("date");

        e.HasOne(x => x.Obra).WithMany(o => o.Formulaciones).HasForeignKey(x => x.ObraId);
        e.HasOne(x => x.UnidadMedida).WithMany().HasForeignKey(x => x.UnidadMedidaId);
        e.HasOne(x => x.Prioridad).WithMany().HasForeignKey(x => x.PrioridadId);
    }
}

public sealed class LegitimoConfiguration : IEntityTypeConfiguration<Legitimo>
{
    public void Configure(EntityTypeBuilder<Legitimo> e)
    {
        e.ToTable("LEGITIMOS_ABONOS");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("ID");
        e.Property(x => x.ObraId).HasColumnName("OBRA");
        e.Property(x => x.CodigoAutorizante).HasColumnName("CODIGO_AUTORIZANTE").HasMaxLength(50).IsRequired();
        e.Property(x => x.Expediente).HasColumnName("EXPEDIENTE").HasMaxLength(50);
        e.Property(x => x.InicioEjecucion).HasColumnName("INICIO_EJECUCION").HasColumnType("date");
        e.Property(x => x.FinEjecucion).HasColumnName("FIN_EJECUCION").HasColumnType("date");
        e.Property(x => x.Certificado).HasColumnName("CERTIFICADO").HasPrecision(15, 2);
        e.Property(x => x.MesAprobacion).HasColumnName("MES_APROBACION").HasColumnType("date");
        e.Property(x => x.PorcEjecFisica).HasColumnName("PORC_EJEC_FISICA").HasPrecision(5, 2);
        e.HasOne(x => x.Obra).WithMany(o => o.Legitimos).HasForeignKey(x => x.ObraId);
    }
}
