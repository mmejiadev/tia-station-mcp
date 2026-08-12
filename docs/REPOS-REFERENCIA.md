# Repositorios de referencia

Clonados en `../repos/`. Análisis de qué aporta cada uno.

## 1. `tiaportal-mcp` — heilingbrunner ⭐ BASE DEL PROYECTO

**Licencia MIT.** Servidor MCP en C# .NET 4.8 para TIA Portal V20.

Es nuestra base. Ver `ESTADO.md` para el análisis completo de arquitectura y tools.

Ficheros de reglas que hemos adoptado:

- `AGENTS.md` — política de tests, confirmación, entorno, encoding
- `style.md` — convenciones C#, tests MSTest, markdown
- `docs/error-model.md` — modelo de errores y decoración de excepciones
- `gemini.md`, `TODO.md` — contexto adicional

Trae `tests/assets/TestProject1.zap20`: **proyecto TIA V20 real para pruebas.**

## 2. `vscode-tiaportal-mcp` — heilingbrunner

Extensión de VS Code que empaqueta el servidor anterior para GitHub Copilot.
TypeScript + esbuild. Útil como referencia de **empaquetado y distribución**:
cómo arrancar el `.exe` del servidor desde una extensión y exponerlo al cliente MCP.

Relevante si más adelante queremos distribuir nuestro trabajo a compañeros de clase.

## 3. `CodeGeneratorOpenness` — mking2203

Generador de código vía Openness con interfaz gráfica. Solución completa
(`CodeGeneratorOpenness.sln`) con carpeta `OPNS` y `Sample`.

Aporta: patrones de **importación de bloques y tipos de datos**, manipulación del
árbol de carpetas del proyecto, y export/import de textos del proyecto.
Buena referencia para la fase 3.

## 4. `TiaExportBlocks` — cezar1

El más pequeño y legible: un único `Program.cs`. Conecta a TIA y exporta:

- funciones SCL → `.scl`
- bloques de datos → `.db`
- UDTs → `.udt`
- tablas de variables PLC → `.xml`

**Lectura obligatoria antes de la fase 2.** Es exactamente el export masivo para Git,
resuelto en un fichero. Además incluye la carpeta `dll` con las referencias.

Ojo: exporta **tablas de variables**, algo que `tiaportal-mcp` no hace. Fuente directa
para esa carencia.

## 5. `TIA-Openness-From-Python` — JL00001

Python, 7 ficheros. Genera **SCL y LAD** e importa la lógica a TIA Portal.
También crea dispositivos en "Devices & Network".

Ficheros clave: `xmlHeader.py`, `XML_Objects.py`, `SclObject.py`, `fb_block.py`, `FC_Object.py`.

Aporta: la **estructura del XML SimaticML** desmenuzada en objetos manejables. Aunque no
usemos Python, es la mejor documentación práctica del formato XML que hay en estos repos.
Consultar cuando toque generar LAD.

## 6. `tia-portal-openness-unified-library` — tia-portal-applications

Librería base para herramientas Openness. Incluye `UnifiedOpennessConnector`, un objeto
`IDisposable` que debe usarse con `using` para garantizar la liberación del objeto TIA Portal.

Aporta: **patrón de conexión y ciclo de vida** correcto. Trae `.gitlab-ci.yml`, útil como
referencia de CI. Cubre también acceso a HMI.

## 7. `TIAOpennessManager` — StaniB88

⚠️ **Solo 4 ficheros: no hay código fuente.** Es un repositorio de distribución de
binarios (`update.xml`, `CHANGELOG.md`). La aplicación es cerrada.

Sigue siendo útil como **referencia de funcionalidad objetivo**: editor SCL con resaltado,
diff inline, integración Git (status, commit, push, pull, diff). Es aproximadamente el
producto final que queremos, pero tendremos que implementarlo.

---

## Resumen de dónde mirar según la fase

| Necesito... | Mirar en |
|---|---|
| Conexión y ciclo de vida | `tia-portal-openness-unified-library`, `tiaportal-mcp/Siemens/Openness.cs` |
| Export masivo a texto (fase 2) | `TiaExportBlocks/Program.cs` |
| Tablas de variables | `TiaExportBlocks/Program.cs` |
| Import de bloques (fase 3) | `CodeGeneratorOpenness`, `tiaportal-mcp` |
| Formato XML SimaticML | `TIA-Openness-From-Python/XML_Objects.py` |
| Empaquetado y distribución | `vscode-tiaportal-mcp` |
| Objetivo de producto | `TIAOpennessManager/README.md` |
