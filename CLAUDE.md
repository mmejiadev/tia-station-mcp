# CLAUDE.md — tia-station-mcp

Reglas de trabajo para agentes en este repositorio.

Parte de estas reglas están heredadas de `repos/tiaportal-mcp` (licencia MIT), cuyo
`AGENTS.md`, `style.md` y `docs/error-model.md` hemos adoptado para mantener lógica
compartida y poder contribuir aguas arriba sin fricción.

## Contexto del proyecto

Servidor MCP en C# que expone la API TIA Portal Openness a un LLM, orientado a:

1. Generar y verificar código PLC (SCL) para una célula de 4 estaciones.
2. Cerrar el bucle generar → compilar → testear (PLCSIM Advanced) → corregir.
3. Versionar proyectos TIA en Git mediante export masivo a texto.

Entorno de destino: **TIA Portal V20**, **.NET Framework 4.8**, **Windows x64**.

## Política de ejecución de tests

- Ofrece ejecutar los tests, pero **ejecútalos solo tras confirmación explícita del usuario**.
- Los tests requieren condiciones locales (TIA Portal instalado, licencias, proyecto de
  pruebas). No asumas que van a pasar.
- Al ofrecer, indica los prerequisitos y los efectos secundarios posibles.
- Si el usuario declina o no responde, da instrucciones concisas para que los ejecute.

```powershell
dotnet test
```

## Seguridad operacional (regla propia, no heredada)

- **Nunca** descargues a un PLC físico sin confirmación explícita e inequívoca en ese mismo turno.
- Por defecto, todo download va contra **PLCSIM Advanced**.
- Las herramientas que escriben en el proyecto TIA deben ser idempotentes o hacer backup previo.
- Antes de sobrescribir un bloque existente, expórtalo primero.

## Estilo de código C#

- Target **.NET Framework 4.8**.
- Indentación de **cuatro espacios**, sin tabuladores.
- Llave de apertura **en línea nueva**.
- `PascalCase` para clases y miembros públicos; `camelCase` para parámetros y locales.
- Directivas `using` agrupadas arriba, separadas del namespace por una línea en blanco.
- Prefiere métodos asíncronos `Task`/`Task<T>` cuando la operación pueda ser larga.
- Usa `Microsoft.Extensions.Logging` para logging.

## Tests

- **MSTest** con atributos `[TestClass]` y `[TestMethod]`.
- Nombres de fichero con el patrón `Test<Area>.cs`.
- Los assets de test viven en la subcarpeta `assets/`.

## Modelo de errores

Categorías:

- **Validación** — entrada inválida, recurso ausente → `PortalErrorCode.InvalidParams` → MCP `InvalidParams`.
- **Estado inválido** — el proyecto o el ítem no permite la operación (p. ej. bloque
  inconsistente) → `PortalErrorCode.InvalidState` → MCP `InvalidParams` con guía.
- **Fallo de operación** — entorno, E/S, API subyacente → `PortalErrorCode.ExportFailed`
  → MCP `InternalError` con razón concisa.

Punto único de decoración: **no** adjuntes `Exception.Data` en el sitio del `throw`.
Cada método público de la capa portal adjunta el contexto en un **único bloque catch**
justo antes de relanzar:

```csharp
catch (Exception ex)
{
    var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

    pex.Data["softwarePath"] = softwarePath;
    pex.Data["blockPath"] = blockPath;
    pex.Data["exportPath"] = exportPath;

    _logger?.LogError(pex, "{MethodName} failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
    throw pex;
}
```

Consistencia: TIA Portal **no exporta bloques inconsistentes** (`IsConsistent == false`).
Los exports individuales lanzan `InvalidState` pidiendo compilar primero. Los exports
masivos saltan los inconsistentes y los reportan en una lista `Inconsistent` aparte.

## Formato y codificación — crítico

- Preserva el estilo de indentación existente (tabuladores vs espacios).
- **No cambies la codificación de los ficheros**; mantén el BOM UTF-8 donde exista.
- **Mantén los finales de línea CRLF de Windows.** Los ficheros C# de la capa portal y sus
  tests deben conservar CRLF: los scripts de despliegue de Siemens fallan al parsear LF.
- Los `.md` commiteados desde Windows también en CRLF y UTF-8 con BOM.

## Markdown

- `#` para encabezados, con una línea en blanco después de cada bloque de encabezado.
- Bloques de código vallados con pista de lenguaje: ```csharp, ```json, ```powershell.
- Longitud de línea razonable para lectura.

## Entorno — limitaciones conocidas

- El usuario debe pertenecer al grupo Windows **`Siemens TIA Openness`**.
- Variable de entorno `TiaPortalLocation` → `C:\Program Files\Siemens\Automation\Portal V20`.
- TIA Portal pide confirmación de whitelist la primera vez que se conecta una app externa.
- Transporte MCP actual: **stdio**. Con stdio, **todos los logs van a stderr**.
- Importar bloques **LAD** desde documentos SIMATIC SD requiere el `.s7res` acompañante con
  tags en en-US; si no, el import falla (limitación de Openness).
- `ExportBlock` exige ruta completa `Grupo/Subgrupo/Nombre`; un nombre suelto es ambiguo.

Si un comando falla por limitaciones del entorno, **no reintentes de forma destructiva**:
reporta el fallo exacto y sugiere alternativas.
