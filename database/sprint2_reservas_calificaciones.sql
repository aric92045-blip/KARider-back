-- =============================================================================
-- KARider · SPRINT 2 (siguientes historias): reservas, calificaciones y notificaciones.
-- NO es necesario ejecutarlo a mano si la API arranca con Database__AplicarMigracionesAlIniciar=true.
-- Ejecutar solo después de sprint1_hu01-hu05.sql. Generado desde la migración de EF Core
-- "Sprint2_ReservasCalificacionesYNotificaciones".
-- =============================================================================

START TRANSACTION;
CREATE TABLE calificaciones (
    id uuid NOT NULL,
    viaje_id uuid NOT NULL,
    evaluador_id uuid NOT NULL,
    evaluado_id uuid NOT NULL,
    rol_evaluado character varying(30) NOT NULL,
    estrellas integer NOT NULL,
    comentario character varying(500),
    etiquetas text[] NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_calificaciones PRIMARY KEY (id),
    CONSTRAINT ck_calificaciones_estrellas CHECK (estrellas BETWEEN 1 AND 5),
    CONSTRAINT fk_calificaciones_usuarios_evaluado_id FOREIGN KEY (evaluado_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_calificaciones_usuarios_evaluador_id FOREIGN KEY (evaluador_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_calificaciones_viajes_viaje_id FOREIGN KEY (viaje_id) REFERENCES viajes (id) ON DELETE RESTRICT
);

CREATE TABLE notificaciones (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    tipo character varying(30) NOT NULL,
    titulo character varying(120) NOT NULL,
    mensaje character varying(600) NOT NULL,
    viaje_id uuid,
    reserva_id uuid,
    leida boolean NOT NULL,
    leida_en timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_notificaciones PRIMARY KEY (id),
    CONSTRAINT fk_notificaciones_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE
);

CREATE TABLE reservas (
    id uuid NOT NULL,
    viaje_id uuid NOT NULL,
    pasajero_id uuid NOT NULL,
    asientos integer NOT NULL,
    monto_aporte numeric(10,2) NOT NULL,
    folio character varying(30) NOT NULL,
    estado character varying(30) NOT NULL,
    aporte_pagado boolean NOT NULL,
    respondida_en timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_reservas PRIMARY KEY (id),
    CONSTRAINT ck_reservas_asientos CHECK (asientos BETWEEN 1 AND 2),
    CONSTRAINT fk_reservas_usuarios_pasajero_id FOREIGN KEY (pasajero_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_reservas_viajes_viaje_id FOREIGN KEY (viaje_id) REFERENCES viajes (id) ON DELETE RESTRICT
);

CREATE TABLE suscripciones_push (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    endpoint character varying(1000) NOT NULL,
    p256dh character varying(200) NOT NULL,
    auth character varying(100) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_suscripciones_push PRIMARY KEY (id),
    CONSTRAINT fk_suscripciones_push_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE CASCADE
);

CREATE INDEX ix_calificaciones_evaluado_id_creado_en ON calificaciones (evaluado_id, creado_en);

CREATE INDEX ix_calificaciones_evaluador_id ON calificaciones (evaluador_id);

CREATE UNIQUE INDEX ix_calificaciones_viaje_id_evaluador_id_evaluado_id ON calificaciones (viaje_id, evaluador_id, evaluado_id);

CREATE INDEX ix_notificaciones_usuario_id_leida_creado_en ON notificaciones (usuario_id, leida, creado_en);

CREATE UNIQUE INDEX ix_reservas_folio ON reservas (folio);

CREATE INDEX ix_reservas_pasajero_id_estado ON reservas (pasajero_id, estado);

CREATE UNIQUE INDEX ix_reservas_viaje_id_pasajero_id ON reservas (viaje_id, pasajero_id) WHERE estado IN ('Pendiente', 'Confirmada');

CREATE UNIQUE INDEX ix_suscripciones_push_endpoint ON suscripciones_push (endpoint);

CREATE INDEX ix_suscripciones_push_usuario_id ON suscripciones_push (usuario_id);

INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261008064332_Sprint2_ReservasCalificacionesYNotificaciones', '9.0.19');

COMMIT;

