# Catálogo de errores de la API KARider

Todas las respuestas de error usan el mismo formato **Problem Details (RFC 9457)**, con `Content-Type: application/problem+json`. El frontend debe tomar decisiones con el campo **`code`**, que es estable. El campo `detail` ya viene redactado en español y se puede mostrar tal cual al usuario.

```json
{
  "type": "urn:karider:error:viaje_sin_cupo",
  "title": "Conflicto",
  "status": 409,
  "detail": "Solo quedan 1 asiento(s) disponible(s) en este viaje.",
  "instance": "POST /api/v1/viajes/0192.../reservas",
  "code": "VIAJE_SIN_CUPO",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "timestamp": "2026-10-08T05:13:09Z"
}
```

En los errores de validación se agrega `errors` con los mensajes de cada campo (nombres en camelCase):

```json
{
  "status": 400,
  "code": "VALIDACION_FALLIDA",
  "detail": "La solicitud contiene datos inválidos. Revisa los campos indicados en «errors».",
  "errors": {
    "correoInstitucional": ["Solo se permiten correos institucionales con dominio @uttt.edu.mx."],
    "password": ["La contraseña debe incluir al menos un número."]
  }
}
```

`traceId` permite localizar la solicitud en los logs o en Application Insights. Fuera de Development **nunca** se exponen trazas ni mensajes internos.

## Estado HTTP según el tipo de error

| Estado | Significado en KARider |
|---|---|
| 400 | Datos inválidos o solicitud mal formada |
| 401 | Sin sesión, token inválido/expirado o credenciales incorrectas |
| 403 | Autenticado, pero sin permiso (rol, propiedad del recurso, correo sin verificar) |
| 404 | El recurso no existe o no es visible para el usuario |
| 409 | Conflicto con el estado actual (duplicados, sin cupo, concurrencia) |
| 422 | Se incumple una regla de negocio |
| 423 | Cuenta bloqueada temporalmente |
| 429 | Límite de solicitudes o de intentos excedido (incluye el encabezado `Retry-After`) |
| 500 / 503 | Error interno o base de datos no disponible |

## Generales (capa API)

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `VALIDACION_FALLIDA` | 400 | Uno o más campos no cumplen las reglas (detalle en `errors`) |
| `SOLICITUD_MAL_FORMADA` | 400 | JSON inválido o con tipos incorrectos (ej. texto en un campo numérico) |
| `RECURSO_NO_ENCONTRADO` | 404 | La ruta no existe (sin sesión responde 401, para no revelar qué rutas existen) |
| `METODO_NO_PERMITIDO` | 405 | Método HTTP no soportado en esa ruta |
| `TIPO_CONTENIDO_NO_SOPORTADO` | 415 | El cuerpo no se envió como `application/json` |
| `CARGA_DEMASIADO_GRANDE` | 413 | El cuerpo supera 1 MB |
| `LIMITE_SOLICITUDES_EXCEDIDO` | 429 | Demasiadas solicitudes por minuto (más estricto en `/auth`) |
| `CONCURRENCIA_CONFLICTO` | 409 | Otro usuario modificó el recurso al mismo tiempo (ej. el último asiento) |
| `REGISTRO_DUPLICADO` | 409 | Se violó una restricción única no catalogada |
| `REFERENCIA_INVALIDA` | 409 | Se hace referencia a un registro inexistente |
| `RESTRICCION_DATOS_VIOLADA` | 422 | Se violó una regla de integridad (check constraint) de la base de datos |
| `BASE_DATOS_NO_DISPONIBLE` | 503 | PostgreSQL no responde después de los reintentos |
| `ERROR_INTERNO` | 500 | Error no previsto (repórtalo con el `traceId`) |

## Autenticación y sesión

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `AUTH_TOKEN_REQUERIDO` | 401 | Falta el encabezado `Authorization: Bearer` |
| `AUTH_TOKEN_EXPIRADO` | 401 | El access token venció: hay que llamar a `/auth/refresh` |
| `AUTH_TOKEN_INVALIDO` | 401 | Token alterado, firmado con otra clave o mal formado |
| `AUTH_CREDENCIALES_INVALIDAS` | 401 | Correo o contraseña incorrectos (no se revela cuál de los dos) |
| `AUTH_REFRESH_TOKEN_INVALIDO` | 401 | Refresh token vencido, revocado o reutilizado (en ese caso se cierran todas las sesiones) |
| `AUTH_CORREO_NO_VERIFICADO` | 403 | El correo institucional aún no se verifica |
| `AUTH_CUENTA_INACTIVA` | 403 | La cuenta fue desactivada |
| `AUTH_ROL_NO_AUTORIZADO` | 403 | Se intentó iniciar sesión como conductor sin vehículo registrado |
| `AUTH_ROL_CONDUCTOR_REQUERIDO` | 403 | La acción es solo para conductores |
| `AUTH_PERMISO_DENEGADO` | 403 | Otro motivo de autorización |
| `AUTH_CUENTA_BLOQUEADA` | 423 | Demasiados intentos fallidos; el bloqueo es temporal |
| `AUTH_CODIGO_INVALIDO` | 400 | El código de 6 dígitos es incorrecto |
| `AUTH_CODIGO_EXPIRADO` | 400 | El código venció (15 min por defecto) |
| `AUTH_CODIGO_INTENTOS_EXCEDIDOS` | 429 | Se agotaron los intentos para ese código |
| `AUTH_CORREO_YA_VERIFICADO` | 409 | El correo ya estaba verificado |
| `AUTH_PASSWORD_ACTUAL_INCORRECTA` | 400 | Al cambiar la contraseña, la actual no coincide |

## Usuarios y vehículos

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `USUARIO_CORREO_DUPLICADO` | 409 | Ya existe una cuenta con ese correo |
| `USUARIO_MATRICULA_DUPLICADA` | 409 | Ya existe una cuenta con esa matrícula |
| `USUARIO_CARRERA_INVALIDA` | 400 | La carrera no está en el catálogo |
| `USUARIO_NO_ENCONTRADO` | 404 | El usuario no existe o está inactivo |
| `VEHICULO_REQUERIDO_CONDUCTOR` | 400 | Registro como conductor sin datos del auto |
| `VEHICULO_PLACAS_DUPLICADAS` | 409 | Las placas ya están registradas en otro vehículo activo |
| `VEHICULO_NO_ENCONTRADO` | 404 | El vehículo no existe o no es del usuario |
| `VEHICULO_NO_REGISTRADO` | 422 | Se intenta publicar un viaje sin vehículo activo |
| `VEHICULO_CAPACIDAD_INSUFICIENTE` | 422 | Se ofrecen más asientos de los que permite el vehículo |
| `VEHICULO_CON_VIAJES_ACTIVOS` | 409 | No se puede dar de baja un vehículo con viajes activos |

## Viajes

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `VIAJE_NO_ENCONTRADO` | 404 | El viaje no existe (los borradores solo los ve su autor) |
| `VIAJE_NO_ES_PROPIETARIO` | 403 | Solo el conductor que lo publicó puede modificarlo o gestionar sus reservas |
| `VIAJE_FECHA_PASADA` | 422 | La fecha de salida ya pasó |
| `VIAJE_PUNTO_ENCUENTRO_INVALIDO` | 400 | El punto de encuentro no existe o está deshabilitado |
| `VIAJE_NO_EDITABLE` | 422 | El viaje está en curso, completado o cancelado |
| `VIAJE_TRANSICION_INVALIDA` | 422 | Cambio de estado no permitido (ej. de Programado a Completado) |
| `VIAJE_SIN_CUPO` | 409 | No hay asientos suficientes |
| `VIAJE_CUPOS_INVALIDOS` | 422 | Los cupos más los asientos ya reservados superan la capacidad |
| `VIAJE_NO_DISPONIBLE` | 422 | El viaje ya no acepta reservas |
| `VIAJE_UBICACION_NO_DISPONIBLE` | 404 | El conductor aún no comparte su ubicación |
| `VIAJE_UBICACION_NO_PERMITIDA` | 422 | Solo se comparte ubicación en viajes programados o en curso |
| `VIAJE_ACCESO_DENEGADO` | 403 | Solo el conductor o los pasajeros confirmados pueden ver ubicación y aportes |

## Reservas

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `RESERVA_NO_ENCONTRADA` | 404 | La reserva no existe |
| `RESERVA_VIAJE_PROPIO` | 422 | El conductor intenta reservar en su propio viaje |
| `RESERVA_DUPLICADA` | 409 | El pasajero ya tiene una reserva activa en ese viaje |
| `RESERVA_ASIENTOS_INVALIDOS` | 400 | Se piden menos de 1 o más de 2 asientos |
| `RESERVA_ESTADO_INVALIDO` | 422 | La acción no aplica al estado actual (ej. aceptar una reserva ya cancelada) |
| `RESERVA_ACCESO_DENEGADO` | 403 | Solo el pasajero o el conductor del viaje pueden verla o modificarla |
| `RESERVA_FOLIO_DUPLICADO` | 409 | Colisión improbable de folio: basta con reintentar |

## Calificaciones y notificaciones

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `CALIFICACION_VIAJE_NO_COMPLETADO` | 422 | El viaje aún no se marca como completado |
| `CALIFICACION_NO_PARTICIPANTE` | 403 | Quien califica no participó en el viaje |
| `CALIFICACION_EVALUADO_INVALIDO` | 422 | El conductor solo califica a sus pasajeros y el pasajero solo al conductor |
| `CALIFICACION_AUTOEVALUACION` | 422 | Un usuario intenta calificarse a sí mismo |
| `CALIFICACION_DUPLICADA` | 409 | Ya existe una calificación para ese compañero en ese viaje |
| `CALIFICACION_ESTRELLAS_INVALIDAS` | 400 | Las estrellas están fuera del rango 1 a 5 |
| `CALIFICACION_ETIQUETA_INVALIDA` | 400 | La etiqueta no está en el catálogo |
| `NOTIFICACION_NO_ENCONTRADA` | 404 | La notificación no existe o pertenece a otro usuario |
