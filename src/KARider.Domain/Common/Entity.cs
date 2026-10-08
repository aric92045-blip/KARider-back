namespace KARider.Domain.Common;

/// <summary>
/// Entidad base con identificador ordenable (UUID v7) y campos de auditoría.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? ActualizadoEn { get; set; }
}
