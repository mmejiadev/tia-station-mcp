# Classic WinCC feature atlas — what to draw by hand

Written 2026-10-21. Row C0 of `HMI-COVERAGE.md`. Classic WinCC has no screen object model in
Openness: a screen enters and leaves TIA Portal only as XML, and the schema is not shipped. The XML of
a feature is therefore only known once TIA Portal has exported it. Everything the class projects
already contain has been taken from their exports (`class-corpus`); what follows is what they do not
contain, and so what has to be drawn once by hand.

## How to make it

1. A new project in TIA Portal V20 with one **TP900 Comfort** (`6AV2 124-0JC01-0AX0`, 17.0.0.0),
   named `Atlas`, panel `HMI_1`.
2. Four internal HMI tags: `AtlasBool` (Bool), `AtlasInt` (Int), `AtlasReal` (Real), `AtlasText`
   (WString). Internal, so no PLC or connection is needed.
3. One screen per row below, named exactly as the row says, with the items it lists, each left with
   TIA Portal's defaults unless the row says otherwise.
4. Export the panel with `ExportHmi` to `tests/TiaMcpServer.Spec.Test/assets/hmi/atlas/` and archive
   the project.

The exports become the reference of each generated item, compared with `CompareHmiScreens` the way
`Build_ClassShapes_MatchTheExports` compares the rectangle, the line and the graphic view today.

## Screens

| Screen | Items | Coverage row |
|---|---|---|
| `AtlasShapes` | one polyline (3 points), one polygon (4 points), one ellipse, one rectangle with rounded corners | C9 |
| `AtlasGroup` | a group of a button and an I/O field (the tab order inside a group) | C5 |
| `AtlasButtons` | a graphic button, a button with text and graphic, an invisible button | C1 |
| `AtlasFields` | a graphic I/O field, an I/O field in input mode with limits, a hidden-input field, a binary and a hex field | C6, C13 |
| `AtlasControls` | a switch, a slider, a gauge, a clock | C13 |
| `AtlasViews1` | an alarm view, a trend view (one trend on `AtlasReal`), an f(x) trend view | C14 |
| `AtlasViews2` | a recipe view, a user view, a status/force view, a system diagnostics view | C14 |
| `AtlasViews3` | an HTML browser, a media player, a PDF view | C14 |
| `AtlasSymbols` | three symbols from the symbol library: a valve, a pump, a motor | C11 |
| `AtlasTemplate` | a screen template with a header text, used by two screens `AtlasT1` and `AtlasT2` | C8 |
| `AtlasLayers` | items on layers 0, 1 and 5, layer 5 hidden in the engineering system, layers named | C15 |
| `AtlasPopup` | a pop-up screen and a slide-in screen, each opened by a button | C15 |
| `AtlasMove` | four rectangles: horizontal, vertical, diagonal and direct movement, each on `AtlasInt` | A3 |
| `AtlasDynamics` | a rectangle flashing on `AtlasBool`, a button disabled by `AtlasBool` (operability), a button with authorisation | A5, A6 |
| `AtlasEvents` | buttons calling `InvertBit`, `IncreaseValue`, `DecreaseValue`, `ActivatePreviousScreen`, `StopRuntime`, `SetLanguage` | E2, E3, E5 |
| `AtlasGraphicList` | a graphic list of two pictures and a graphic I/O field showing it | T10, A9 |

## Done so far without the atlas

Taken from the class exports on 2026-10-21 and checked attribute for attribute against them:
rectangle, line, graphic view, group of shapes, plain circle, layers. Before that (2026-10-13/14):
button, I/O field, symbolic I/O field, text field, pilot light (circle with animation), bar, date/time
field, appearance and visibility animations, the button events in `HMI-COVERAGE.md` row E1.
