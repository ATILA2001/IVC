#nullable enable
namespace IVC.Data.Entities;

/// <summary>Obra. Tabla Obras. Raíz del módulo de planificación.</summary>
public class Obra
{
    public int Id { get; set; }
    public string? Descripcion { get; set; }
    /// <summary>Número de contratación (columna NUMERO).</summary>
    public int? Numero { get; set; }
    /// <summary>Año de la contratación (columna AÑO).</summary>
    public int? Anio { get; set; }
    /// <summary>Número de obra (columna OBRA).</summary>
    public int? ObraNumero { get; set; }
    public int? Etapa { get; set; }

    /// <summary>NOT NULL en la base.</summary>
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public int? ContrataId { get; set; }
    public Contrata? Contrata { get; set; }
    public int? BarrioId { get; set; }
    public Barrio? Barrio { get; set; }
    /// <summary>NOT NULL en la base.</summary>
    public int AreaId { get; set; }
    public Area Area { get; set; } = null!;

    /// <summary>Fila de BD_PROYECTOS de la obra (0..1).</summary>
    public Proyecto? Proyecto { get; set; }
    public ICollection<Autorizante> Autorizantes { get; set; } = new List<Autorizante>();
    public ICollection<Formulacion> Formulaciones { get; set; } = new List<Formulacion>();
    public ICollection<Legitimo> Legitimos { get; set; } = new List<Legitimo>();
    public ICollection<MovimientoGestion> Movimientos { get; set; } = new List<MovimientoGestion>();
}
