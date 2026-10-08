using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KARider.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InicialKARider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carreras",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carreras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "puntos_encuentro",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    latitud = table.Column<double>(type: "double precision", nullable: false),
                    longitud = table.Column<double>(type: "double precision", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_puntos_encuentro", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    matricula = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correo_institucional = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    carrera_id = table.Column<int>(type: "integer", nullable: false),
                    cuatrimestre = table.Column<int>(type: "integer", nullable: true),
                    foto_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correo_verificado = table.Column<bool>(type: "boolean", nullable: false),
                    correo_verificado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    calificacion_promedio = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    total_calificaciones = table.Column<int>(type: "integer", nullable: false),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.CheckConstraint("ck_usuarios_calificacion", "calificacion_promedio BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_usuarios_cuatrimestre", "cuatrimestre IS NULL OR cuatrimestre BETWEEN 1 AND 11");
                    table.ForeignKey(
                        name: "fk_usuarios_carreras_carrera_id",
                        column: x => x.carrera_id,
                        principalTable: "carreras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "codigos_verificacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposito = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    codigo_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    usado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codigos_verificacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_codigos_verificacion_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reemplazado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_creacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    recordarme = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "vehiculos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conductor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    placas = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    capacidad = table.Column<int>(type: "integer", nullable: false),
                    verificado = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehiculos", x => x.id);
                    table.CheckConstraint("ck_vehiculos_capacidad", "capacidad BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_vehiculos_usuarios_conductor_id",
                        column: x => x.conductor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "viajes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conductor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehiculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punto_encuentro_id = table.Column<int>(type: "integer", nullable: false),
                    destino = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    destino_latitud = table.Column<double>(type: "double precision", nullable: true),
                    destino_longitud = table.Column<double>(type: "double precision", nullable: true),
                    fecha_salida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    asientos_ofrecidos = table.Column<int>(type: "integer", nullable: false),
                    asientos_disponibles = table.Column<int>(type: "integer", nullable: false),
                    gasto_total = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    aporte_por_asiento = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ubicacion_latitud = table.Column<double>(type: "double precision", nullable: true),
                    ubicacion_longitud = table.Column<double>(type: "double precision", nullable: true),
                    ubicacion_actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_viajes", x => x.id);
                    table.CheckConstraint("ck_viajes_asientos", "asientos_disponibles >= 0 AND asientos_disponibles <= asientos_ofrecidos");
                    table.CheckConstraint("ck_viajes_gasto", "gasto_total > 0");
                    table.ForeignKey(
                        name: "fk_viajes_puntos_encuentro_punto_encuentro_id",
                        column: x => x.punto_encuentro_id,
                        principalTable: "puntos_encuentro",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_viajes_usuarios_conductor_id",
                        column: x => x.conductor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_viajes_vehiculos_vehiculo_id",
                        column: x => x.vehiculo_id,
                        principalTable: "vehiculos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "paradas_viaje",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paradas_viaje", x => x.id);
                    table.ForeignKey(
                        name: "fk_paradas_viaje_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
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

            migrationBuilder.InsertData(
                table: "carreras",
                columns: new[] { "id", "activa", "nombre" },
                values: new object[,]
                {
                    { 1, true, "Ingeniería en Desarrollo y Gestión de Software" },
                    { 2, true, "Ingeniería en Mecatrónica" },
                    { 3, true, "Ingeniería en Tecnologías de la Información e Innovación Digital" },
                    { 4, true, "Ingeniería en Mantenimiento Industrial" },
                    { 5, true, "Ingeniería en Procesos y Operaciones Industriales" },
                    { 6, true, "Ingeniería en Energías Renovables" },
                    { 7, true, "Ingeniería Química" },
                    { 8, true, "Ingeniería en Logística Internacional" },
                    { 9, true, "Ingeniería Civil" },
                    { 10, true, "Licenciatura en Contaduría" },
                    { 11, true, "Licenciatura en Negocios y Mercadotecnia" },
                    { 12, true, "Licenciatura en Administración" }
                });

            migrationBuilder.InsertData(
                table: "puntos_encuentro",
                columns: new[] { "id", "activo", "descripcion", "latitud", "longitud", "nombre" },
                values: new object[,]
                {
                    { 1, true, "Junto a la caseta de vigilancia, frente a los torniquetes de acceso peatonal.", 20.087499999999999, -99.347999999999999, "Puerta Principal (Torniquetes Acceso A)" },
                    { 2, true, "Explanada frente al Edificio B, área de Desarrollo y Gestión de Software.", 20.088100000000001, -99.347200000000001, "Edificio B (Explanada de Sistemas · IDGS)" },
                    { 3, true, "Entrada vehicular del estacionamiento principal del campus.", 20.087, -99.3489, "Estacionamiento principal" }
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
                name: "ix_carreras_nombre",
                table: "carreras",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_codigos_verificacion_usuario_id_proposito_creado_en",
                table: "codigos_verificacion",
                columns: new[] { "usuario_id", "proposito", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_usuario_id_leida_creado_en",
                table: "notificaciones",
                columns: new[] { "usuario_id", "leida", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_paradas_viaje_viaje_id_orden",
                table: "paradas_viaje",
                columns: new[] { "viaje_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_usuario_id_revocado_en",
                table: "refresh_tokens",
                columns: new[] { "usuario_id", "revocado_en" });

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

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_carrera_id",
                table: "usuarios",
                column: "carrera_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_correo_institucional",
                table: "usuarios",
                column: "correo_institucional",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_matricula",
                table: "usuarios",
                column: "matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehiculos_conductor_id",
                table: "vehiculos",
                column: "conductor_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehiculos_placas",
                table: "vehiculos",
                column: "placas",
                unique: true,
                filter: "activo = true");

            migrationBuilder.CreateIndex(
                name: "ix_viajes_conductor_id_fecha_salida",
                table: "viajes",
                columns: new[] { "conductor_id", "fecha_salida" });

            migrationBuilder.CreateIndex(
                name: "ix_viajes_estado_fecha_salida",
                table: "viajes",
                columns: new[] { "estado", "fecha_salida" });

            migrationBuilder.CreateIndex(
                name: "ix_viajes_punto_encuentro_id",
                table: "viajes",
                column: "punto_encuentro_id");

            migrationBuilder.CreateIndex(
                name: "ix_viajes_vehiculo_id",
                table: "viajes",
                column: "vehiculo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calificaciones");

            migrationBuilder.DropTable(
                name: "codigos_verificacion");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "paradas_viaje");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "reservas");

            migrationBuilder.DropTable(
                name: "suscripciones_push");

            migrationBuilder.DropTable(
                name: "viajes");

            migrationBuilder.DropTable(
                name: "puntos_encuentro");

            migrationBuilder.DropTable(
                name: "vehiculos");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "carreras");
        }
    }
}
