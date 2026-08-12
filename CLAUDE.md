# CLAUDE.md — tia-station-mcp

Reglas de trabajo para agentes en este repositorio. **Son vinculantes, no orientativas.**

Parte están heredadas de `repos/tiaportal-mcp` (MIT), cuyo `AGENTS.md`, `style.md` y
`docs/error-model.md` hemos adoptado para mantener lógica compartida y poder contribuir
aguas arriba sin fricción.

## Al empezar una sesión

Lee **`docs/ESTADO.md`** antes de hacer nada. Su sección "▶ RETOMAR AQUÍ" dice en qué punto
quedó el trabajo, qué está bloqueado y cuál es la siguiente acción. Es la fuente de verdad
del proyecto y se actualiza al cierre de cada sesión.

Los repositorios de referencia están en `../repos/` y su análisis en `docs/REPOS-REFERENCIA.md`.

La usuaria escribe en español; respóndele en español.

## Contexto del proyecto

Servidor MCP en C# que expone la API TIA Portal Openness a un LLM, orientado a:

1. Generar y verificar código PLC (SCL) para una célula de 4 estaciones.
2. Cerrar el bucle generar → compilar → testear (PLCSIM Advanced) → corregir.
3. Versionar proyectos TIA en Git mediante export masivo a texto.

Entorno de destino: **TIA Portal V20**, **.NET Framework 4.8**, **Windows x64**.

---

# Arquitectura

## Regla de dependencia — la más importante del repositorio

Las dependencias fluyen en **una sola dirección**. Nunca al revés.

```
McpServer  ──►  Portal  ──►  Openness  ──►  Siemens.Engineering
 (protocolo)   (dominio)    (adaptador)      (API externa)
```

- `ModelContextProtocol/` **no puede** contener un `using Siemens.Engineering`.
  Si necesitas algo de Openness, se expone primero en la capa `Portal`.
- `Siemens/` **no puede** conocer MCP: ni `McpException`, ni tipos de respuesta MCP,
  ni serialización JSON. Lanza `PortalException` y ya.
- Los tipos de Openness (`PlcBlock`, `Device`, `Project`…) **no cruzan** hacia la capa MCP.
  Se traducen a DTOs propios en `Portal`.

Si una tarea parece exigir romper esto, la tarea está mal planteada. Pregunta antes.

## Tamaño y forma

| Regla | Límite |
|---|---|
| Longitud de método | ≤ 30 líneas |
| Longitud de clase | ≤ 300 líneas |
| Parámetros por método | ≤ 4 (más → objeto de parámetros) |
| Niveles de anidamiento | ≤ 2 |
| Complejidad ciclomática | ≤ 10 |

Un método hace **una cosa** y opera en **un solo nivel de abstracción**. Si tienes que
escribir un comentario para separar "bloques" dentro de un método, esos bloques son métodos.

Cuando `McpServer.cs` o `Portal.cs` superen las 300 líneas (en la base upstream tienen
90 KB y 95 KB), se dividen por área funcional en clases parciales o colaboradoras:
`PortalBlocks`, `PortalTags`, `PortalCompile`. **No replicamos el fichero monolítico.**

---

# Estilo de código C#

- Target **.NET Framework 4.8**, `LangVersion` moderno para disponer de nullable reference types.
- Indentación de **cuatro espacios**, sin tabuladores.
- Llave de apertura **en línea nueva**.
- `PascalCase` para clases y miembros públicos; `camelCase` para parámetros y locales;
  `_camelCase` para campos privados.
- Directivas `using` agrupadas arriba, separadas del namespace por una línea en blanco.
- `Task`/`Task<T>` para operaciones potencialmente largas. **Nunca `async void`** salvo
  manejadores de eventos.
- `CancellationToken` en toda operación que pueda tardar (compilar, exportar en masa, descargar).
- `Microsoft.Extensions.Logging` para logging.

## Nombres

- Sin abreviaturas: `blockPath`, no `blkPth`. Sin `tmp`, `aux`, `data`, `obj`, `res`.
- Prohibidos los sufijos vacíos: `Manager`, `Helper`, `Utils`, `Processor`, `Handler`
  cuando no describen nada. Si una clase se llama `BlockHelper`, no sabes qué hace.
  Nómbrala por su responsabilidad: `BlockExporter`, `SclSourceBuilder`.
- Los booleanos se leen como afirmación: `isConsistent`, `hasTagTable`, `canDownload`.
- Los métodos empiezan por verbo: `ExportBlock`, no `BlockExport`.

## Inmutabilidad y estado

- `readonly` por defecto en campos. Un campo mutable debe justificarse.
- DTOs y objetos de respuesta: **inmutables**, sin setters públicos.
- Sin estado estático mutable. Sin singletons. **Sin variables globales.**
- Inyección de dependencias por constructor. Nada de `new` de colaboradores dentro de
  la lógica de negocio.

## Flujo de control

- **Guard clauses al principio**, retorno temprano. Nada de pirámides de `if`.
- `else` después de un `return` está prohibido.
- Nada de números ni cadenas mágicas: constantes con nombre.
- Sin `switch` gigantes sobre tipos: polimorfismo o diccionario de estrategias.

```csharp
// mal
if (block != null)
{
    if (block.IsConsistent)
    {
        // 20 líneas
    }
}

// bien
if (block == null)
{
    throw new PortalException(PortalErrorCode.NotFound, $"Block not found: {blockPath}");
}

if (!block.IsConsistent)
{
    throw new PortalException(PortalErrorCode.InvalidState, "Compile the block before exporting");
}

// 20 líneas, sin anidar
```

## Comentarios

- El código explica **qué** hace. Los comentarios explican **por qué**.
- Un comentario que parafrasea la línea siguiente se borra.
- Los comentarios que documentan rarezas de Openness **sí se quedan** y son valiosos.
  Ejemplo: por qué hay que comprobar `IsConsistent`, por qué LAD necesita `.s7res`.
- `///` XML doc en todo miembro público de la capa `Portal`.
- Sin código comentado. Para eso está Git.
- Sin `TODO` sin fecha y responsable. Si no lo vas a hacer, no lo escribas.

---

# Reglas de dominio — Openness

Estas no son estilo, son correctitud. Romperlas causa fallos reales.

- **Todo objeto TIA se libera con `using` o `Dispose()`.** Si no, TIA Portal queda como
  proceso zombie ocupando la licencia y hay que matarlo desde el administrador de tareas.
  Es el fallo más común y el más molesto de diagnosticar.
- **Nunca asumas que un bloque es consistente.** Comprueba `IsConsistent` antes de exportar.
  TIA Portal no exporta bloques inconsistentes y el error nativo no lo explica.
- **Rutas siempre completas**: `Grupo/Subgrupo/Nombre`. Un nombre suelto es ambiguo.
- **Toda escritura va precedida de un export del estado anterior.** Si vamos a sobrescribir
  un bloque, primero se guarda una copia. Sin excepciones.
- **Cero rutas hardcodeadas.** Todo por configuración o parámetro.
- Nunca supongas que hay proyecto abierto: valida el estado primero.

---

# Seguridad operacional

- **Nunca** descargues a un PLC físico sin confirmación explícita e inequívoca en ese
  mismo turno de conversación. Una autorización previa no se extiende a la siguiente vez.
- Por defecto, todo despliegue va contra **PLCSIM Advanced**.
- Las herramientas que escriben en el proyecto deben ser idempotentes o hacer backup previo.

---

# Errores

Categorías:

- **Validación** — entrada inválida, recurso ausente → `PortalErrorCode.InvalidParams` → MCP `InvalidParams`.
- **Estado inválido** — el ítem no permite la operación (p. ej. bloque inconsistente)
  → `PortalErrorCode.InvalidState` → MCP `InvalidParams` con guía accionable.
- **Fallo de operación** — entorno, E/S, API subyacente → `PortalErrorCode.ExportFailed`
  → MCP `InternalError` con razón concisa.

Reglas duras:

- **Nunca un `catch` vacío.** Nunca tragarse una excepción.
- **Nunca `catch (Exception)` sin relanzar** fuera del punto único de decoración.
- No uses excepciones para flujo de control normal.
- El mensaje al usuario es conciso y accionable; el detalle estructurado va al log.

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

Consistencia: los exports individuales lanzan `InvalidState` pidiendo compilar primero.
Los exports masivos saltan los inconsistentes y los reportan en una lista `Inconsistent`.

---

# Tests

- **MSTest** con `[TestClass]` y `[TestMethod]`.
- Ficheros con el patrón `Test<Area>.cs`. Assets en `assets/`.
- Nombres: `Metodo_Escenario_ResultadoEsperado`.
  Ejemplo: `ExportBlock_BloqueInconsistente_LanzaInvalidState`.
- Estructura **AAA**: Arrange, Act, Assert, separadas por línea en blanco.
- **Un concepto verificado por test.** Varios `Assert` valen si comprueban el mismo concepto.
- Toda corrección de bug entra con un test que fallaba antes.
- Los tests no dependen del orden de ejecución ni comparten estado.
- La capa `Portal` es la que debe estar cubierta: es donde vive la lógica.

## Política de ejecución

- Ofrece ejecutar los tests, pero **ejecútalos solo tras confirmación explícita**.
- Requieren TIA Portal instalado, licencias y assets. No asumas que van a pasar.
- Si la usuaria declina, da instrucciones concisas para que los ejecute ella.

```powershell
dotnet test
```

---

# Formato y codificación — crítico

- Preserva el estilo de indentación existente.
- **No cambies la codificación**; mantén el BOM UTF-8 donde exista.
- **Mantén CRLF de Windows.** Los ficheros C# de la capa portal y sus tests deben
  conservar CRLF: los scripts de despliegue de Siemens fallan al parsear LF.
- Los `.md` commiteados desde Windows también en CRLF y UTF-8 con BOM.

## Markdown

- `#` para encabezados, con línea en blanco después de cada bloque de encabezado.
- Bloques de código vallados con pista de lenguaje.

---

# Definición de "hecho"

Una tarea no está terminada hasta que:

1. Compila **sin warnings**.
2. Tiene tests que cubren el camino feliz y al menos un caso de error.
3. Los miembros públicos tienen XML doc.
4. No hay código muerto, comentado ni `TODO` huérfano.
5. Los errores están mapeados según el modelo de arriba.
6. `docs/ESTADO.md` refleja el nuevo estado.

---

# Entorno — limitaciones conocidas

- Usuario en el grupo Windows **`Siemens TIA Openness`** (requiere re-login para el token).
- `TiaPortalLocation` → `C:\Program Files\Siemens\Automation\Portal V20`.
- TIA Portal pide confirmación de whitelist la primera vez que conecta una app externa.
- Transporte MCP actual: **stdio**. Con stdio, **todos los logs van a stderr**.
- Importar bloques **LAD** desde documentos SIMATIC SD requiere el `.s7res` acompañante
  con tags en en-US; si no, falla (limitación de Openness).
- `ExportBlock` exige ruta completa; un nombre suelto es ambiguo.

Si un comando falla por limitaciones del entorno, **no reintentes de forma destructiva**:
reporta el fallo exacto y sugiere alternativas.

---

# Una nota sobre el rigor

Estas reglas existen para que el código aguante crecer y para que los fallos aparezcan
en compilación en vez de en un PLC. No existen para producir código ceremonioso.

Si aplicar una regla al pie de la letra hace el código **menos** claro, dilo y propón la
alternativa en vez de aplicarla a ciegas. Tres capas de abstracción para leer un fichero
no son clean code: son lo contrario.
