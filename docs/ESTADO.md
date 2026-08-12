# Estado del proyecto

> Documento vivo. Actualízalo al final de cada sesión de trabajo.
> Última actualización: **2026-08-12**

## Objetivo

Servidor MCP que genere, verifique y despliegue código PLC para el proyecto final del
CFGS de Automatización y Robótica Industrial: **coordinación de 4 estaciones**
(paletización, graficación, conteo, robots).

Plazo: **mes y medio** desde 2026-08-12 → objetivo ~2026-09-25 (inicio de clases).

## Entorno verificado (2026-08-12)

| Componente | Estado |
|---|---|
| TIA Portal V20 | Instalado (`TIAP20`) |
| TIA Portal V12 | Instalado (`TIAP12`, legado) |
| Openness PublicAPI | V17, V18, V19, V20 en `C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\` |
| PLCSIM Advanced | Instalado (`PLCSIMADV`) |
| PLCSIM V20 | Instalado |
| WinCC Unified | Instalado |
| .NET | `C:\Program Files\dotnet\dotnet.exe` |
| Node.js | v24.15.0 |
| Git | 2.55.0.windows.4 |

### Verificado 2026-08-12

- ✅ .NET Framework **4.8.1** (release 533509) — suficiente para el target 4.8
- ✅ El grupo local `Siemens TIA Openness` **existe** en la máquina
- ❌ **BLOQUEANTE**: el usuario actual pertenece a `MANUELA\Siemens TIA Engineer`
  pero **NO** a `MANUELA\Siemens TIA Openness`. Sin esto, Openness no conecta.
- ❌ `TiaPortalLocation` no definida (ni proceso ni usuario)
- ⬜ Whitelist de aplicación externa en TIA Portal (se comprueba al primer conectar)

### Cómo desbloquear

PowerShell **como administrador**, y después **cerrar sesión y volver a entrar**
(la pertenencia a grupos solo se refresca al crear el token de sesión):

```powershell
Add-LocalGroupMember -Group "Siemens TIA Openness" -Member "$env:USERNAME"
[System.Environment]::SetEnvironmentVariable('TiaPortalLocation', 'C:\Program Files\Siemens\Automation\Portal V20', 'User')
```

## Decisiones tomadas

| Decisión | Motivo |
|---|---|
| MCP en **C# puro**, no TS + sidecar | Openness es .NET; un puente solo añade serialización |
| Base: fork de **heilingbrunner/tiaportal-mcp** (MIT) | 30 tools ya hechos, mismo target, licencia permisiva |
| Adoptar su `AGENTS.md` + `style.md` + `error-model.md` | Lógica compartida, posibilidad de contribuir aguas arriba |
| Downloads solo a **PLCSIM Advanced** por defecto | Seguridad: un bloque mal generado en hardware real es un accidente |
| SCL sobre LAD para código generado | El XML SimaticML de LAD es enorme y frágil |

## Análisis de la base: `tiaportal-mcp`

**Licencia MIT.** .NET Framework 4.8. TIA V20 por defecto (`--tia-major-version` para otras).
Transporte solo `stdio`.

### Arquitectura (2 capas)

```
src/TiaMcpServer/
├── ModelContextProtocol/
│   ├── McpServer.cs      (90 KB) — definición de los 30 tools
│   ├── McpPrompts.cs     (10 KB) — prompts que guían al LLM
│   ├── Responses.cs             — objetos de respuesta
│   └── Types.cs
└── Siemens/
    ├── Portal.cs         (95 KB) — API de alto nivel sobre Openness
    ├── Openness.cs              — wrapper de la API Openness
    ├── PortalException.cs / PortalErrorCode.cs
    └── State.cs
```

La separación es limpia: la capa `Siemens/` no sabe nada de MCP, y la capa
`ModelContextProtocol/` no toca Openness directamente. Mantener esa frontera.

### Los 30 tools existentes

- **Conexión (3):** `Connect`, `Disconnect`, `GetState`
- **Proyecto (6):** `GetProject`, `OpenProject`, `SaveProject`, `SaveAsProject`, `CloseProject`, `GetProjectTree`
- **Dispositivos (3):** `GetDeviceInfo`, `GetDeviceItemInfo`, `GetDevices`
- **Software (3):** `GetSoftwareInfo`, `CompileSoftware`, `GetSoftwareTree`
- **Bloques (7):** `GetBlockInfo`, `GetBlocks`, `GetBlocksWithHierarchy`, `ExportBlock`, `ImportBlock`, `ExportBlocks`
- **Tipos/UDT (5):** `GetTypeInfo`, `GetTypes`, `ExportType`, `ImportType`, `ExportTypes`
- **Documentos SIMATIC SD, V20+ (4):** `ExportAsDocuments`, `ExportBlocksAsDocuments`, `ImportFromDocuments`, `ImportBlocksFromDocuments`

`CompileSoftware` existe → **el bucle cerrado generar/compilar es viable desde el día uno.**

### Lo que NO tiene (verificado por grep: 0 coincidencias)

Este es nuestro valor añadido. No hay ninguna referencia a:

- `TagTable` → **sin gestión de tablas de variables**
- `ExternalSource` / `GenerateBlocksFromSource` → **no se puede escribir SCL directamente**
- `Download` → **no despliega al PLC**
- `PlcSim` / `Simulation` → **sin integración con PLCSIM Advanced**
- `Watch` / `ForceTable` → sin tablas de observación

## Hoja de ruta

| Fase | Contenido | Estado |
|---|---|---|
| 0 | Clonar y analizar repos de referencia | ✅ Hecho |
| 1 | Compilar `tiaportal-mcp` y conectar con TIA real | ⬜ Siguiente |
| 2 | Export masivo → Git (snapshot del proyecto a texto) | ⬜ |
| 3 | `WriteScl` vía external source + `Compile` con errores parseados | ⬜ |
| 4 | Integración PLCSIM Advanced → tests automatizados | ⬜ |
| 5 | Generador del patrón "estación" desde especificación | ⬜ |
| 6 | Documentación, demo, memoria del proyecto | ⬜ |

## Activos disponibles

- **`repos/tiaportal-mcp/tests/assets/TestProject1.zap20`** (3,6 MB) — proyecto TIA V20 real,
  utilizable como banco de pruebas inmediato. Resuelve la falta de proyecto propio.
- Documentos SCE de Siemens (gratuitos, en español) — proyectos didácticos adicionales.

## Diseño: el patrón "estación"

Interfaz estándar para instanciar 4 veces. Es el núcleo del proyecto final.

```
FB_Station
  IN : Start, Reset, Enable, ModeAuto, ModeManual
  OUT: Busy, Done, Error, ErrorId, Ready
  IN_OUT: PieceId          // trazabilidad entre estaciones
  STATIC: Step (secuencia interna, GRAFCET)
```

Un `FB_Coordinator` gestiona el handshake entre las 4 instancias: quién tiene la pieza,
cuándo se libera, y qué hace la estación N si la N+1 está en fallo.
