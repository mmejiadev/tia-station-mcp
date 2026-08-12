# tia-station-mcp

Servidor MCP para Siemens TIA Portal orientado a la generación verificada de código PLC
y a la coordinación de células multi-estación.

Proyecto final del CFGS de Automatización y Robótica Industrial.

## Qué pretende

Cerrar el bucle completo, no solo generar código:

```
especificación → generar SCL → importar a TIA → compilar → leer errores
                      ↑                                        │
                      └────────────── corregir ←───────────────┘
                                          │
                                          ▼
                              test en PLCSIM Advanced
                                          │
                                          ▼
                                    export a Git
```

Un LLM generando código PLC no acierta el 100 % de las veces. La fiabilidad no viene del
modelo: viene del **compilador y de los tests**. Por eso el bucle cerrado es el núcleo del
diseño y no un extra.

## Estado

Fase 0 completada (análisis). Ver [`docs/ESTADO.md`](docs/ESTADO.md).

## Base y atribución

Construido sobre [heilingbrunner/tiaportal-mcp](https://github.com/heilingbrunner/tiaportal-mcp)
(MIT), del que heredamos arquitectura, convenciones de código y modelo de errores.

Análisis de los siete repositorios de referencia en
[`docs/REPOS-REFERENCIA.md`](docs/REPOS-REFERENCIA.md).

## Requisitos

- Windows x64
- TIA Portal V20 con componente Openness
- Usuario en el grupo Windows `Siemens TIA Openness`
- .NET Framework 4.8
- PLCSIM Advanced (para la fase de tests)

## Aportación sobre la base

Lo que `tiaportal-mcp` no cubre y añadimos aquí:

- Tablas de variables (`PlcTagTable`) — export/import
- Escritura directa de SCL vía external source
- Integración con PLCSIM Advanced para tests automatizados
- Snapshot completo del proyecto a texto para Git
- Generador del patrón "estación" instanciable

## Seguridad

Por defecto, **todo despliegue va contra PLCSIM Advanced**. La descarga a un PLC físico
requiere confirmación explícita. Ver [`CLAUDE.md`](CLAUDE.md).
