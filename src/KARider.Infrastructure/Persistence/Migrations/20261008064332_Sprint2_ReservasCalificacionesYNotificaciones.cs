using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KARider.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_ReservasCalificacionesYNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_evaluado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estrellas = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    etiquetas = table.Column<List<string>>(type: "text[]", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calificaciones", x => x.id);
                    table.CheckConstraint("ck_calificaciones_estrellas", "estrellas BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_calificaciones_usuarios_evaluado_id",
                        column: x => x.evaluado_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_calificaciones_usuarios_evaluador_id",
                        column: x => x.evaluador_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_calificaciones_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reserva_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leida = table.Column<bool>(type: "boolean", nullable: false),
                    leida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_notificaciones_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reservas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pasajero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asientos = table.Column<int>(type: "integer", nullable: false),
                    monto_aporte = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    folio = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    aporte_pagado = table.Column<bool>(type: "boolean", nullable: false),
                    respondida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservas", x => x.id);
                    table.CheckConstraint("ck_reservas_asientos", "asientos BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "fk_reservas_usuarios_pasajero_id",
                        column: x => x.pasajero_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reservas_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suscripciones_push",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suscripciones_push", x => x.id);
                    table.ForeignKey(
                        name: "fk_suscripciones_push_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calificaciones_evaluado_id_creado_en",
                table: "calificaciones",
                columns: new[] { "evaluado_id", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_calificaciones_evaluador_id",
                table: "calificaciones",
                column: "evaluador_id");

            migrationBuilder.CreateIndex(
                name: "ix_calificaciones_viaje_id_evaluador_id_evaluado_id",
                table: "calificaciones",
                columns: new[] { "viaje_id", "evaluador_id", "evaluado_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_usuario_id_leida_creado_en",
                table: "notificaciones",
                columns: new[] { "usuario_id", "leida", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_reservas_folio",
                table: "reservas",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservas_pasajero_id_estado",
                table: "reservas",
                columns: new[] { "pasajero_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_reservas_viaje_id_pasajero_id",
                table: "reservas",
                columns: new[] { "viaje_id", "pasajero_id" },
                unique: true,
                filter: "estado IN ('Pendiente', 'Confirmada')");

            migrationBuilder.CreateIndex(
                name: "ix_suscripciones_push_endpoint",
                table: "suscripciones_push",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suscripciones_push_usuario_id",
                table: "suscripciones_push",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calificaciones");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "reservas");

            migrationBuilder.DropTable(
                name: "suscripciones_push");
        }
    }
}
