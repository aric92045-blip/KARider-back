-- =============================================================================
-- KARider · Base de datos PostgreSQL 15 (Neon) · SPRINT 1
-- Historias de usuario cubiertas:
--   HU-01 Registro y verificación institucional de Pasajero
--         → carreras, usuarios, codigos_verificacion
--   HU-02 Registro y validación institucional de Conductor con vehículo
--         → usuarios, vehiculos, codigos_verificacion
--   HU-03 Inicio de sesión con selección de rol
--         → usuarios (rol, bloqueo por intentos), refresh_tokens
--   HU-04 Publicar ruta: puntos, horario y capacidad (Conductor)
--         → puntos_encuentro, viajes, paradas_viaje
--   HU-05 Calcular aporte solidario y publicar el viaje (Conductor)
--         → viajes (gasto_total, aporte_por_asiento, estado Borrador/Programado)
--
-- Cómo ejecutarlo en Neon:
--   1. Crea el proyecto con Postgres 15 y la base de datos "karider".
--   2. Abre el SQL Editor, selecciona la base "karider", pega este archivo completo y ejecútalo.
--   3. Se ejecuta UNA sola vez (si ya existen las tablas fallará sin cambiar nada,
--      porque todo va dentro de una transacción).
--
-- IMPORTANTE: generado desde la migración de EF Core "Sprint1_RegistroYPublicacionDeViajes".
--   - No lo edites a mano: cambia el modelo en código y genera una nueva migración.
--   - El INSERT final en __ef_migrations_history permite que la API reconozca este esquema.
--     Al arrancar con Database__AplicarMigracionesAlIniciar=true, la API aplicará sola las
--     migraciones de sprints siguientes (reservas, calificaciones, notificaciones), que el
--     código ya necesita (ver database/sprint2_reservas_calificaciones.sql).
--   - Regenerar: dotnet ef migrations script 0 Sprint1_RegistroYPublicacionDeViajes
--                -p src/KARider.Infrastructure -s src/KARider.API -o database/sprint1_hu01-hu05.sql
-- =============================================================================

CREATE TABLE IF NOT EXISTS __ef_migrations_history (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
CREATE TABLE carreras (
    id integer NOT NULL,
    nombre character varying(150) NOT NULL,
    activa boolean NOT NULL,
    CONSTRAINT pk_carreras PRIMARY KEY (id)
);

CREATE TABLE puntos_encuentro (
    id integer NOT NULL,
    nombre character varying(100) NOT NULL,
    descripcion character varying(300) NOT NULL,
    latitud double precision NOT NULL,
    longitud double precision NOT NULL,
    activo boolean NOT NULL,
    CONSTRAINT pk_puntos_encuentro PRIMARY KEY (id)
);

CREATE TABLE usuarios (
    id uuid NOT NULL,
    nombre_completo character varying(120) NOT NULL,
    matricula character varying(10) NOT NULL,
    telefono character varying(20) NOT NULL,
    correo_institucional character varying(150) NOT NULL,
    password_hash character varying(500) NOT NULL,
    rol character varying(30) NOT NULL,
    carrera_id integer NOT NULL,
    cuatrimestre integer,
    foto_url character varying(500),
    correo_verificado boolean NOT NULL,
    correo_verificado_en timestamp with time zone,
    activo boolean NOT NULL,
    calificacion_promedio numeric(3,2) NOT NULL,
    total_calificaciones integer NOT NULL,
    intentos_fallidos integer NOT NULL,
    bloqueado_hasta timestamp with time zone,
    security_stamp character varying(64) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_usuarios PRIMARY KEY (id),
    CONSTRAINT ck_usuarios_calificacion CHECK (calificacion_promedio BETWEEN 0 AND 5),
    CONSTRAINT ck_usuarios_cuatrimestre CHECK (cuatrimestre IS NULL OR cuatrimestre BETWEEN 1 AND 11),
    CONSTRAINT fk_usuarios_carreras_carrera_id FOREIGN KEY (carrera_id) REFERENCES carreras (id) ON DELETE RESTRICT
);

CREATE TABLE codigos_verificacion (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    proposito character varying(30) NOT NULL,
    codigo_hash character varying(128) NOT NULL,
    expira_en timestamp with time zone NOT NULL,
    intentos integer NOT NULL,
    usado_en timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_codigos_verificacion PRIMARY KEY (id),
    CONSTRAINT fk_codigos_verificacion_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE
);

CREATE TABLE refresh_tokens (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    token_hash character varying(128) NOT NULL,
    expira_en timestamp with time zone NOT NULL,
    revocado_en timestamp with time zone,
    reemplazado_por_id uuid,
    ip_creacion character varying(64),
    recordarme boolean NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
    CONSTRAINT fk_refresh_tokens_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE
);

CREATE TABLE vehiculos (
    id uuid NOT NULL,
    conductor_id uuid NOT NULL,
    modelo character varying(80) NOT NULL,
    color character varying(30) NOT NULL,
    anio integer NOT NULL,
    placas character varying(10) NOT NULL,
    capacidad integer NOT NULL,
    verificado boolean NOT NULL,
    activo boolean NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_vehiculos PRIMARY KEY (id),
    CONSTRAINT ck_vehiculos_capacidad CHECK (capacidad BETWEEN 1 AND 4),
    CONSTRAINT fk_vehiculos_usuarios_conductor_id FOREIGN KEY (conductor_id) REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE viajes (
    id uuid NOT NULL,
    conductor_id uuid NOT NULL,
    vehiculo_id uuid NOT NULL,
    punto_encuentro_id integer NOT NULL,
    destino character varying(150) NOT NULL,
    destino_latitud double precision,
    destino_longitud double precision,
    fecha_salida timestamp with time zone NOT NULL,
    asientos_ofrecidos integer NOT NULL,
    asientos_disponibles integer NOT NULL,
    gasto_total numeric(10,2) NOT NULL,
    aporte_por_asiento numeric(10,2) NOT NULL,
    notas character varying(500),
    estado character varying(30) NOT NULL,
    ubicacion_latitud double precision,
    ubicacion_longitud double precision,
    ubicacion_actualizada_en timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_viajes PRIMARY KEY (id),
    CONSTRAINT ck_viajes_asientos CHECK (asientos_disponibles >= 0 AND asientos_disponibles <= asientos_ofrecidos),
    CONSTRAINT ck_viajes_gasto CHECK (gasto_total > 0),
    CONSTRAINT fk_viajes_puntos_encuentro_punto_encuentro_id FOREIGN KEY (punto_encuentro_id) REFERENCES puntos_encuentro (id) ON DELETE RESTRICT,
    CONSTRAINT fk_viajes_usuarios_conductor_id FOREIGN KEY (conductor_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_viajes_vehiculos_vehiculo_id FOREIGN KEY (vehiculo_id) REFERENCES vehiculos (id) ON DELETE RESTRICT
);

CREATE TABLE paradas_viaje (
    id uuid NOT NULL,
    viaje_id uuid NOT NULL,
    orden integer NOT NULL,
    nombre character varying(100) NOT NULL,
    CONSTRAINT pk_paradas_viaje PRIMARY KEY (id),
    CONSTRAINT fk_paradas_viaje_viajes_viaje_id FOREIGN KEY (viaje_id) REFERENCES viajes (id) ON DELETE CASCADE
);

INSERT INTO carreras (id, activa, nombre)
VALUES (1, TRUE, 'Ingeniería en Desarrollo y Gestión de Software');
INSERT INTO carreras (id, activa, nombre)
VALUES (2, TRUE, 'Ingeniería en Mecatrónica');
INSERT INTO carreras (id, activa, nombre)
VALUES (3, TRUE, 'Ingeniería en Tecnologías de la Información e Innovación Digital');
INSERT INTO carreras (id, activa, nombre)
VALUES (4, TRUE, 'Ingeniería en Mantenimiento Industrial');
INSERT INTO carreras (id, activa, nombre)
VALUES (5, TRUE, 'Ingeniería en Procesos y Operaciones Industriales');
INSERT INTO carreras (id, activa, nombre)
VALUES (6, TRUE, 'Ingeniería en Energías Renovables');
INSERT INTO carreras (id, activa, nombre)
VALUES (7, TRUE, 'Ingeniería Química');
INSERT INTO carreras (id, activa, nombre)
VALUES (8, TRUE, 'Ingeniería en Logística Internacional');
INSERT INTO carreras (id, activa, nombre)
VALUES (9, TRUE, 'Ingeniería Civil');
INSERT INTO carreras (id, activa, nombre)
VALUES (10, TRUE, 'Licenciatura en Contaduría');
INSERT INTO carreras (id, activa, nombre)
VALUES (11, TRUE, 'Licenciatura en Negocios y Mercadotecnia');
INSERT INTO carreras (id, activa, nombre)
VALUES (12, TRUE, 'Licenciatura en Administración');

INSERT INTO puntos_encuentro (id, activo, descripcion, latitud, longitud, nombre)
VALUES (1, TRUE, 'Junto a la caseta de vigilancia, frente a los torniquetes de acceso peatonal.', 20.087499999999999, -99.347999999999999, 'Puerta Principal (Torniquetes Acceso A)');
INSERT INTO puntos_encuentro (id, activo, descripcion, latitud, longitud, nombre)
VALUES (2, TRUE, 'Explanada frente al Edificio B, área de Desarrollo y Gestión de Software.', 20.088100000000001, -99.347200000000001, 'Edificio B (Explanada de Sistemas · IDGS)');
INSERT INTO puntos_encuentro (id, activo, descripcion, latitud, longitud, nombre)
VALUES (3, TRUE, 'Entrada vehicular del estacionamiento principal del campus.', 20.087, -99.3489, 'Estacionamiento principal');

CREATE UNIQUE INDEX ix_carreras_nombre ON carreras (nombre);

CREATE INDEX ix_codigos_verificacion_usuario_id_proposito_creado_en ON codigos_verificacion (usuario_id, proposito, creado_en);

CREATE UNIQUE INDEX ix_paradas_viaje_viaje_id_orden ON paradas_viaje (viaje_id, orden);

CREATE UNIQUE INDEX ix_refresh_tokens_token_hash ON refresh_tokens (token_hash);

CREATE INDEX ix_refresh_tokens_usuario_id_revocado_en ON refresh_tokens (usuario_id, revocado_en);

CREATE INDEX ix_usuarios_carrera_id ON usuarios (carrera_id);

CREATE UNIQUE INDEX ix_usuarios_correo_institucional ON usuarios (correo_institucional);

CREATE UNIQUE INDEX ix_usuarios_matricula ON usuarios (matricula);

CREATE INDEX ix_vehiculos_conductor_id ON vehiculos (conductor_id);

CREATE UNIQUE INDEX ix_vehiculos_placas ON vehiculos (placas) WHERE activo = true;

CREATE INDEX ix_viajes_conductor_id_fecha_salida ON viajes (conductor_id, fecha_salida);

CREATE INDEX ix_viajes_estado_fecha_salida ON viajes (estado, fecha_salida);

CREATE INDEX ix_viajes_punto_encuentro_id ON viajes (punto_encuentro_id);

CREATE INDEX ix_viajes_vehiculo_id ON viajes (vehiculo_id);

INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261008064318_Sprint1_RegistroYPublicacionDeViajes', '9.0.19');


-- Documentación visible en Neon (Tables → Description)
COMMENT ON TABLE carreras IS 'HU-01/HU-02 · Catálogo de carreras UTTT';
COMMENT ON TABLE usuarios IS 'HU-01/HU-02/HU-03 · Estudiantes UTTT (Pasajero/Conductor). Correo @uttt.edu.mx único; contraseña PBKDF2';
COMMENT ON TABLE codigos_verificacion IS 'HU-01/HU-02 · Códigos de 6 dígitos (solo hash HMAC) para verificar correo o restablecer contraseña';
COMMENT ON TABLE refresh_tokens IS 'HU-03 · Sesiones: refresh tokens rotativos (solo hash), «recordar mis credenciales»';
COMMENT ON TABLE vehiculos IS 'HU-02 · Vehículo del conductor (modelo, color, año, placas, capacidad 1-4)';
COMMENT ON TABLE puntos_encuentro IS 'HU-04 · Puntos de encuentro del campus';
COMMENT ON TABLE viajes IS 'HU-04/HU-05 · Viaje publicado: ruta, horario, cupos y aporte = gasto_total / (asientos + 1)';
COMMENT ON TABLE paradas_viaje IS 'HU-04 · Paradas intermedias opcionales (máximo 5)';
COMMENT ON COLUMN usuarios.rol IS 'Pasajero | Conductor | Administrador';
COMMENT ON COLUMN usuarios.bloqueado_hasta IS 'HU-03 · Bloqueo temporal tras 5 intentos fallidos';
COMMENT ON COLUMN viajes.aporte_por_asiento IS 'HU-05 · Aporte solidario por asiento (el conductor también aporta su parte)';
COMMENT ON COLUMN viajes.estado IS 'Borrador | Programado | EnCurso | Completado | Cancelado';

COMMIT;
