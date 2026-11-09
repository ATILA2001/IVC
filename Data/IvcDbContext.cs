#nullable enable
using Microsoft.EntityFrameworkCore;
using IVC.Data.Entities;

namespace IVC.Data;

/// <summary>
/// Base IVC (lectura y escritura). El esquema es preexistente y lo comparten el legado,
/// SAF y Power BI, así que este contexto NO genera migraciones: las entidades se mapean
/// a las tablas existentes con Fluent API (<c>Data/Configurations</c>) y las tablas nuevas
/// de la app (auditoría) se crean con los scripts versionados de <c>Database/</c>.
/// </summary>
public class IvcDbContext(DbContextOptions<IvcDbContext> options) : DbContext(options)
{
    // Planificación
    public DbSet<Obra> Obras => Set<Obra>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<MovimientoGestion> MovimientosGestion => Set<MovimientoGestion>();
    public DbSet<Formulacion> Formulaciones => Set<Formulacion>();
    public DbSet<Legitimo> Legitimos => Set<Legitimo>();

    // Redeterminaciones
    public DbSet<Autorizante> Autorizantes => Set<Autorizante>();
    public DbSet<Certificado> Certificados => Set<Certificado>();
    public DbSet<Redeterminacion> Redeterminaciones => Set<Redeterminacion>();
    public DbSet<ExpedienteReliq> ExpedientesReliq => Set<ExpedienteReliq>();

    // Catálogos
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Contrata> Contratas => Set<Contrata>();
    public DbSet<Barrio> Barrios => Set<Barrio>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Concepto> Conceptos => Set<Concepto>();
    public DbSet<EstadoAutorizante> EstadosAutorizante => Set<EstadoAutorizante>();
    public DbSet<EstadoRedet> EstadosRedet => Set<EstadoRedet>();
    public DbSet<TipoPago> TiposPago => Set<TipoPago>();
    public DbSet<Prioridad> Prioridades => Set<Prioridad>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<LineaGestion> LineasGestion => Set<LineaGestion>();
    public DbSet<LineaGestionFF> LineasGestionFF => Set<LineaGestionFF>();

    // Configuración e identidad
    public DbSet<AbmPlani> AbmPlani => Set<AbmPlani>();
    public DbSet<UsuarioVinculado> UsuariosVinculados => Set<UsuarioVinculado>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    // Bajadas externas (sólo lectura, sin clave)
    public DbSet<PaseSade> PasesSade => Set<PaseSade>();
    public DbSet<Gedo> Gedos => Set<Gedo>();
    public DbSet<Devengado> Devengados => Set<Devengado>();

    // Propia de la app
    public DbSet<CambioAuditoria> CambiosAuditoria => Set<CambioAuditoria>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Casi todas las cadenas de la base son varchar: con parámetros nvarchar SQL Server
        // convierte la columna y no usa el índice (o falla por intercalación). Las pocas
        // columnas nvarchar (FORMULACION.BREVE_DESCRIPCION, UsuariosVinculados, GEDO y
        // CambiosAuditoria) lo declaran explícito con IsUnicode(true).
        configurationBuilder.Properties<string>().AreUnicode(false);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IvcDbContext).Assembly);

        // Tabla propia de la app; ver Database/001_CambiosAuditoria.sql.
        modelBuilder.Entity<CambioAuditoria>(entity =>
        {
            entity.ToTable("CambiosAuditoria", "dbo");
            // El historial se consulta por fila: Id técnico o clave de negocio.
            entity.HasIndex(e => new { e.Vista, e.EntidadId, e.Fecha });
            entity.HasIndex(e => new { e.Vista, e.ClaveNegocio, e.Fecha });
            entity.Property(e => e.Usuario).HasMaxLength(150).IsUnicode(true);
            entity.Property(e => e.Vista).HasMaxLength(50).IsUnicode(true);
            entity.Property(e => e.ClaveNegocio).HasMaxLength(200).IsUnicode(true);
            entity.Property(e => e.Accion).HasMaxLength(10).IsUnicode(true);
            entity.Property(e => e.Campo).HasMaxLength(100).IsUnicode(true);
            entity.Property(e => e.ValorAnterior).HasMaxLength(500).IsUnicode(true);
            entity.Property(e => e.ValorNuevo).HasMaxLength(500).IsUnicode(true);
        });
    }
}
