using KARider.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KARider.Infrastructure.Persistence.Configurations;

internal sealed class CodigoVerificacionConfiguration : IEntityTypeConfiguration<CodigoVerificacion>
{
    public void Configure(EntityTypeBuilder<CodigoVerificacion> builder)
    {
        builder.ToTable("codigos_verificacion");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodigoHash).HasMaxLength(128).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.UsuarioId, c.Proposito, c.CreadoEn });
        builder.Ignore(c => c.EstaUsado);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.IpCreacion).HasMaxLength(64);
        builder.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UsuarioId, t.RevocadoEn });
    }
}

internal sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("notificaciones");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(n => n.Mensaje).HasMaxLength(600).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(n => new { n.UsuarioId, n.Leida, n.CreadoEn });
    }
}

internal sealed class SuscripcionPushConfiguration : IEntityTypeConfiguration<SuscripcionPush>
{
    public void Configure(EntityTypeBuilder<SuscripcionPush> builder)
    {
        builder.ToTable("suscripciones_push");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Endpoint).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(100).IsRequired();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.HasIndex(s => s.UsuarioId);
    }
}
