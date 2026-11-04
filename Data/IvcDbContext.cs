#nullable enable
using Microsoft.EntityFrameworkCore;
using IVC.Data.Entities;

namespace IVC.Data;

/// <summary>
/// Base IVC (lectura y escritura). El esquema es preexistente y lo comparten el legado,
/// SAF y Power BI, así que este contexto NO genera migraciones: las entidades se mapean
/// a las tablas existentes con Fluent API y las tablas nuevas de la app (auditoría)
/// se crean con los scripts versionados de <c>Database/</c>.
/// </summary>
public class IvcDbContext(DbContextOptions<IvcDbContext> options) : DbContext(options)
{
    public DbSet<CambioAuditoria> CambiosAuditoria => Set<CambioAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tabla propia de la app; ver Database/001_CambiosAuditoria.sql.
        modelBuilder.Entity<CambioAuditoria>(entity =>
        {
            entity.ToTable("CambiosAuditoria", "dbo");
            // El historial se consulta por fila: Id técnico o clave de negocio.
            entity.HasIndex(e => new { e.Vista, e.EntidadId, e.Fecha });
            entity.HasIndex(e => new { e.Vista, e.ClaveNegocio, e.Fecha });
            entity.Property(e => e.Usuario).HasMaxLength(150);
            entity.Property(e => e.Vista).HasMaxLength(50);
            entity.Property(e => e.ClaveNegocio).HasMaxLength(200);
            entity.Property(e => e.Accion).HasMaxLength(10);
            entity.Property(e => e.Campo).HasMaxLength(100);
            entity.Property(e => e.ValorAnterior).HasMaxLength(500);
            entity.Property(e => e.ValorNuevo).HasMaxLength(500);
        });
    }
}
