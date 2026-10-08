# Despliegue en Azure con GitHub Actions

Arquitectura de producción: **Azure App Service (Linux, .NET 9)** para la API y **PostgreSQL 15 externo** (Azure Database for PostgreSQL Flexible Server o cualquier proveedor administrado). El despliegue se ejecuta desde GitHub con OIDC, sin guardar contraseñas de Azure en el repositorio.

## 1. Crear los recursos (una sola vez)

```bash
RG=rg-karider
APP=KARider-back           # nombre del App Service actual
PLAN=plan-karider
az group create -n $RG -l centralus
az appservice plan create -g $RG -n $PLAN --is-linux --sku B1        # escalar verticalmente: P1v3, P2v3…
az webapp create -g $RG -p $PLAN -n $APP --runtime "DOTNETCORE:9.0"
az webapp update -g $RG -n $APP --https-only true
az webapp config set -g $RG -n $APP --always-on true --http20-enabled true --min-tls-version 1.2 \
  --generic-configurations '{"healthCheckPath": "/health/ready"}'
```

Base de datos (si se usa Azure; con otro proveedor solo hace falta la cadena de conexión):

```bash
az postgres flexible-server create -g $RG -n karider-db --version 15 --tier Burstable --sku-name Standard_B1ms \
  --admin-user karideradmin --admin-password '<CONTRASEÑA_SEGURA>' --public-access 0.0.0.0
az postgres flexible-server db create -g $RG -s karider-db -d karider
```

> `--public-access 0.0.0.0` permite el acceso desde servicios de Azure. Si las migraciones se ejecutan desde GitHub Actions (`MIGRAR_EN_CI=true`), la base de datos también debe aceptar las IPs de los runners. La alternativa es aplicar las migraciones al iniciar (sección 4).

## 2. Variables de entorno en App Service

En **Configuración → Variables de entorno** (o con `az webapp config appsettings set`). Nunca van en `appsettings.json`.

> ⚠️ **Sin las tres variables obligatorias la API no arranca**, y Azure responde **HTTP 503** (la prueba de humo del workflow falla con «La API no respondió saludable»). El motivo exacto aparece en **Supervisión → Secuencia de registro**, por ejemplo: `No se encontró la cadena de conexión a PostgreSQL`.

| Variable | Obligatoria | Ejemplo / nota |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | **Sí** | `Host=<servidor>;Port=5432;Database=karider;Username=<usuario>;Password=<contraseña>;SSL Mode=Require;Maximum Pool Size=100;Timeout=15` |
| `Jwt__SigningKey` | **Sí** | Valor aleatorio de 32 caracteres o más (`scripts/generar-secretos.ps1`) |
| `Hashing__Pepper` | **Sí** | Otro valor aleatorio de 32 caracteres o más, distinto del anterior |
| `Database__AplicarMigracionesAlIniciar` | Recomendada | `true` en el primer despliegue para crear las tablas (con una sola instancia) |
| `Cors__AllowedOrigins__0` | Para el frontend | URL del frontend PWA, ej. `https://karider.azurestaticapps.net` |
| `AllowedHosts` | No | Por defecto `*`. Para restringir, usa el dominio predeterminado exacto (Información general → Dominio predeterminado) |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` (valor por defecto en Azure) |
| `Smtp__Host`, `Smtp__Port`, `Smtp__Usuario`, `Smtp__Password`, `Smtp__Remitente` | Servidor de correo para los códigos |
| `WebPush__PublicKey`, `WebPush__PrivateKey`, `WebPush__Subject` | Claves VAPID (`npx web-push generate-vapid-keys`) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Opcional, activa la telemetría |

**Recomendado:** guardar los secretos en Azure Key Vault y referenciarlos con `@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/JwtSigningKey/)`, habilitando la identidad administrada del App Service.

## 3. Conectar GitHub con Azure (OIDC)

**Ya está configurado.** El App Service **KARider-back** se conectó desde el Centro de implementación del portal de Azure, que creó la credencial federada (ligada a la rama `main`) y estos secrets en el repositorio:

| Secret | Uso |
|---|---|
| `AZUREAPPSERVICE_CLIENTID_DB5D6F…` | Identidad que despliega |
| `AZUREAPPSERVICE_TENANTID_AC7CF7…` | Tenant de Azure |
| `AZUREAPPSERVICE_SUBSCRIPTIONID_8297A0…` | Suscripción |

`.github/workflows/deploy-azure.yml` reutiliza esos secrets y **reemplaza** al workflow que generó el portal (`main_karider-back.yml`, que solo existe en `main`). Ese archivo compila la ruta antigua `KARider.API/` y fallaría con la estructura `src/`, así que **hay que eliminarlo de `main` al integrar esta rama**. Si no se elimina, se intentarían dos despliegues en cada push.

Opcionales en **Settings → Secrets and variables → Actions**:

| Tipo | Nombre | Valor |
|---|---|---|
| Variable | `AZURE_WEBAPP_NAME` | Solo si la app deja de llamarse `KARider-back` |
| Variable | `MIGRAR_EN_CI` | `true` para aplicar las migraciones desde GitHub Actions |
| Secret | `DATABASE_CONNECTION_STRING` | Necesario solo si `MIGRAR_EN_CI=true` |

Si en algún momento se recrea la conexión a mano, la credencial federada debe tener el subject `repo:aric92045-blip/KARider-back:ref:refs/heads/main`.

## 4. Flujo de trabajo

1. Cada PR hacia `develop` o `main` ejecuta **CI** (`.github/workflows/ci.yml`): compila, ejecuta las pruebas, detecta cambios de modelo sin migración y revisa si hay paquetes vulnerables.
2. Al hacer merge a `main` (o al lanzarlo a mano), **Deploy Azure** (`deploy-azure.yml`):
   - compila, prueba, publica y genera el bundle de migraciones (`efbundle`);
   - aplica las migraciones a PostgreSQL si `MIGRAR_EN_CI=true`;
   - despliega en App Service y espera a que `/health/ready` responda 200.

**Migraciones:** hay dos opciones.
- **Desde CI** (`MIGRAR_EN_CI=true`): es lo más seguro con varias instancias.
- **Al iniciar** (`Database__AplicarMigracionesAlIniciar=true`): es lo más simple con una sola instancia, porque no requiere abrir el firewall de la base de datos a GitHub.

## 5. Alternativa con contenedores

El `Dockerfile` de la raíz genera una imagen que corre sin privilegios en el puerto 8080. Sirve para Azure Container Apps o App Service for Containers usando las mismas variables de entorno.
