# PEEK and POKE on S7-1200 and S7-1500

> Study document, written 2026-11-16 offline; **measured 2026-11-18** on TIA Portal V21 and PLCSIM Advanced V8 (see
> "Measured" below, which settles M1-M12 and M14 and corrects three statements of the study).
> Plan item 4 of the 2026-11-12 request. Companion to `STATUS.md`.
>
> Every statement is tagged:
>
> - **[S1]**, **[S2]**… — taken from a source listed in "Sources" at the end, quoted under 15
>   words where quoted.
> - **[INFERRED]** — follows from a source but is not written in it. Treat as a hypothesis.
> - **[MEASURE Mn]** — not known; settled by measurement `Mn` in the plan below.
>
> Nothing tagged INFERRED or MEASURE may be told to a student as fact, or stored in the
> knowledge layer, until the measurement has been run.

## What the instructions are

PEEK and POKE read and write a byte, word, double word or bit at an address that is computed while
the program runs: an area (inputs, outputs, bit memory, a data block), a data block number and a
byte offset. They are the S7-1200/S7-1500 replacement for the indirect addressing an S7-300 did
with `OPN DB [..]` and pointers in STL.

- They belong to SCL. The V21 help files them under SCL "Read/write access", and the basics page
  says: "In SCL, you can read or write a variable memory area" with them **[S2]**. Whether LAD or
  FBD offer them is **[MEASURE M13]**; the help has no LAD or FBD page for them **[S1]**.
- They are documented for **S7-1200, S7-1500 and S7-1200 G2** (topic titles) **[S1]**.
- They work on a **standard** memory area. Each topic says the instruction reads or writes "a
  memory address from a standard memory area without specifying a data type" **[S1]**.
- They know nothing about types: they "merely transfer data. They have no knowledge of data types
  at the addresses" **[S5, p. 160]**.

No firmware version is named in the V21 help. The S7-1200 System Manual of 04/2012 (firmware V3.0
era) already describes them **[S5, pp. 160–162]**, so the S7-1200 has had them for a long time.
The minimum firmware is therefore not an issue for the CPUs in class (1215C V4.x, 1511 V4.0); an
exact minimum is not in either source and is not worth measuring.

## The five instructions

All parameters are inputs. Offsets are byte numbers counted from the start of the area or DB.

| Instruction | Returns | Parameters | Help topic |
|---|---|---|---|
| `PEEK` (`PEEK_BYTE`, `PEEK_WORD`, `PEEK_DWORD`…) | A bit string; `BYTE` by default | `AREA`, `DBNUMBER`, `BYTEOFFSET` | PEEK: Read memory address |
| `PEEK_BOOL` | `BOOL` | `AREA`, `DBNUMBER`, `BYTEOFFSET`, `BITOFFSET` | PEEK_BOOL: Read memory bit |
| `POKE` | nothing | `AREA`, `DBNUMBER`, `BYTEOFFSET`, `VALUE` (bit string) | POKE: Write memory address |
| `POKE_BOOL` | nothing | `AREA`, `DBNUMBER`, `BYTEOFFSET`, `BITOFFSET`, `VALUE` (`BOOL`) | POKE_BOOL: Write memory bit |
| `POKE_BLK` | nothing | `AREA_SRC`, `DBNUMBER_SRC`, `BYTEOFFSET_SRC`, `AREA_DEST`, `DBNUMBER_DEST`, `BYTEOFFSET_DEST`, `COUNT` | POKE_BLK: Write memory area |

Source for the whole table: **[S1]**.

### Parameter types

| Parameter | Data type | Notes |
|---|---|---|
| `AREA`, `AREA_SRC`, `AREA_DEST` | `BYTE` | Area code, table below |
| `DBNUMBER`, `DBNUMBER_SRC`, `DBNUMBER_DEST` | `DINT` or `DB_ANY` | "Number of the data block if AREA = DB, otherwise "0"" **[S1]** |
| `BYTEOFFSET`, `BYTEOFFSET_SRC`, `BYTEOFFSET_DEST` | `DINT` | "Only the 16 least significant bits are used." **[S1]** |
| `BITOFFSET` | `INT` | Bit 0 to 7 **[INFERRED]**; a value of 8 or more is **[MEASURE M9]** |
| `VALUE` (POKE) | bit string (`BYTE`, `WORD`, `DWORD`…) | The width written follows the type of `VALUE` **[INFERRED]** |
| `VALUE` (POKE_BOOL) | `BOOL` | **[S1]** |
| `COUNT` (POKE_BLK) | `DINT` | "Number of bytes which are copied" **[S1]** |

The PEEK width is chosen with a suffix, `PEEK_<data type>()`. Without one, it is taken from the
tags or typed constants used, and is `BYTE` otherwise **[S1]**.

In POKE_BLK, `DBNUMBER_SRC` and `DBNUMBER_DEST` must have the same type: "both tags must be either
data type DINT or data type DB_ANY" **[S1]**.

### Area codes

| Code | Area | PEEK, PEEK_BOOL | POKE, POKE_BOOL | POKE_BLK |
|---|---|---|---|---|
| `16#81` | Inputs, process image (`%I`) | yes | yes | yes |
| `16#82` | Outputs, process image (`%Q`) | yes | yes | yes |
| `16#83` | Bit memory (`%M`) | yes | yes | yes |
| `16#84` | Data block (`%DB`) | yes | yes | yes |
| `16#1` | Peripheral input (`%I:P`) | **S7-1500 only** | — | — |
| `16#2` | Peripheral output (`%Q:P`) | — | **S7-1500 only** | — |

Source: the area lists of the five V21 topics **[S1]**; the four main codes also in **[S5, p. 162]**.

- `16#1` appears only in the PEEK lists and `16#2` only in the POKE lists **[S1]**. Whether a
  POKE to `16#1` or a PEEK of `16#2` is refused by the compiler is **[MEASURE M8]**.
- That `16#81` reads the **process image** and not the peripheral is **[INFERRED]** from
  `16#1` being the separate peripheral code. **[MEASURE M7]** records both codes side by side;
  PLCSIM may not show the difference within a cycle, in which case it stays INFERRED.
- Outside the DB area, `DBNUMBER` must be 0: "you must assign the DBNUMBER parameter the value "0",
  as the instruction is invalid otherwise" **[S1]**. "Invalid" is not explained: compile error,
  ENO false or a silent no-op is **[MEASURE M6]**.

## The standard-access requirement

This is the part students trip over, because **every block a S7-1200/1500 creates is optimized by
default**: new blocks "provide optimized access by means of a default setting" **[S3]**.

What the sources say:

- S7-1200 manual: "you must use standard (not optimized) data blocks" **[S5, p. 160]**.
- V21 help: the instructions work on "a standard memory area" **[S1]**. It does not repeat the
  sentence about optimized blocks.
- In an optimized DB the elements get "no fixed address within the block" **[S4]**; in a standard
  DB the address "is shown in the "Offset" column" **[S4]**. A byte offset only means something in
  the second kind **[INFERRED]**.
- The block-access table **[S4]** lists, for optimized blocks: absolute addressing **no**, `AT`
  **no**, indirect `ANY` **no**, `POINTER`/`VARIANT` **symbolic only**, references **yes**, slice
  access **yes**, indexed arrays **yes**.

What is **not** documented, and has to be measured:

- Whether naming an optimized DB in `DBNUMBER` is a **compile error**, a **runtime access error**,
  or reads something meaningless. Third-party pages claim PEEK "returns zeros" on an optimized DB
  (not a Siemens source; not cited). **[MEASURE M2]**
- Whether it matters if the **calling** block is optimized. The requirement is about the area
  addressed, not the caller **[INFERRED]**; **[MEASURE M1]** uses an optimized caller on purpose.

To make a DB standard: block Properties → Attributes → clear "Optimized block access" **[S3]**. In
SCL external source the attribute is `{ S7_Optimized_Access := 'FALSE' }` **[INFERRED]** from the
external-source format the project already generates; **[MEASURE M1]** confirms it via `WriteScl`.
An instance DB takes its access from its FB and cannot be changed by hand **[S3]**.

## Errors and error handling

**The two help pages contradict each other.** This is the most important open question.

- Each PEEK/POKE topic offers EN/ENO "to recognize possible access errors" and shows
  `IF NOT ENO THEN …` **[S1]**.
- The error-handling page lists PEEK, PEEK_BOOL, POKE, POKE_BOOL and POKE_BLK as exceptions: ENO "is
  set to TRUE in these instructions even if it may result in an access error" **[S6]**. Its
  reliable alternative is `GET_ERR_ID` after the call **[S6]**.

If [S6] is right, the `IF NOT ENO` pattern in [S1] never fires and a student who copies it believes
the access is checked when it is not. **[MEASURE M6]** decides it on PLCSIM Advanced: a PEEK of a
DB that does not exist, and one past the end of a DB, reading `ENO` and `GET_ERR_ID` together.

Other expected faults, all **[INFERRED]** until measured:

| Fault | Likely effect | Measure |
|---|---|---|
| `DBNUMBER` of a DB that does not exist | Access error | M6 |
| Offset past the end of the DB, or `COUNT` past it | Access error | M6 |
| `DBNUMBER` ≠ 0 with an area that is not a DB | "Invalid" **[S1]**, undefined | M6 |
| `BYTEOFFSET` ≥ 65536 | Wraps, only 16 bits used **[S1]** | M10 |
| Optimized DB addressed | Undocumented | M2 |
| Peripheral code on an S7-1200 | Refused, "S7-1500 only" **[S1]** | M8 |
| Byte order | Big-endian, high byte at the lower offset | M3 |

What the CPU does on an access error without local error handling (STOP, OB 121, or an entry in the
diagnostic buffer and carry on) depends on the CPU family and its settings and is **[MEASURE M6]**.
Nothing in this document claims either.

### Typos in the help itself

Three examples in the V21 help look wrong. They are noted so nobody copies them, and each is
checked in a measurement:

- The POKE_BOOL topic's EN/ENO example calls `POKE(` with a `BITOFFSET` **[S1]**. Probably meant
  `POKE_BOOL(`. **[MEASURE M4]**
- The POKE_BLK topic's main example closes the call with `);` after `BYTEOFFSET_SRC` and goes on
  with more parameters **[S1]**. It will not compile as printed.
- The PEEK_BOOL topic's table gives the BOOL function value as `3` **[S1]**.

## Safety

- The help warns that with indirect addressing "memory areas may inadvertently be overwritten with
  incorrect values" and that the system "can then react in an unexpected manner" **[S2]**.
- POKE and POKE_BLK write wherever the numbers point, including `%Q` (16#82) and, on an S7-1500,
  the peripheral outputs directly (16#2). A wrong offset can switch an output that nothing in the
  program names **[INFERRED]**.
- Nothing about the target is visible symbolically. Whether `GetCrossReferences` shows the tag
  behind a `POKE(AREA := 16#84, DBNUMBER := 5, BYTEOFFSET := 20, …)` is **[MEASURE M11]**; the
  expectation is that it does not, so a reviewer reading cross-references would miss the write.
- Optimized blocks are sold on exactly this point: "Access errors, as with indirect addressing or
  from the HMI, for example, are not possible." **[S4]**. Choosing PEEK/POKE gives that up.
- On a real machine, a POKE to outputs is a write to the actuators. The project rule stands: every
  deployment targets PLCSIM Advanced by default, and Workshop Mode needs a supervisor present.

## When to use something else

PEEK/POKE is the right tool when the DB **number** is only known at run time and the DB is standard,
or for raw byte buffers (telegrams). For most class exercises a typed alternative exists and keeps
the block optimized:

| Need | Typed alternative | Works in optimized blocks | Source |
|---|---|---|---|
| Element *i* of a list | `"Data".Values[#i]` | yes | [S4] |
| A bit or byte inside a WORD/DWORD | Slice access `#w.X3`, `#d.B1` | yes | [S4], [S7] |
| A WORD seen as `Array[0..15] of Bool` | `AT` overlay | only for tags with retain "Set in IDB" | [S8] |
| A DB chosen at run time | `DB_ANY` parameter, `#db.%DBX30.0` | **[MEASURE M12]**; absolute address implies standard | [S9] |
| Any type passed to a block | `VARIANT`, `MOVE_BLK_VARIANT`, `VariantGet` | symbolic only | [S4] |
| A tag chosen at run time (S7-1500, S7-1200 G2) | References, `REF()` | yes | [S4] |
| Bytes to/from a structure | `Serialize`, `Deserialize`, `READ_BIG`/`WRITE_BIG` | **[MEASURE M12]** | help titles [S1] |

Notes from the sources:

- `AT` is allowed "In code blocks with optimized access for tags with the retain setting "Set in
  IDB"" **[S8]**, and in FCs on S7-1200/1500 both tags must have the same width **[S8]**.
- "Structures, constants and tags overlaying AT cannot be addressed with slice access." **[S7]**
- In SCL, slice access to an *integer* tag needs the IEC check disabled **[S7]**.
- `#<DB_ANY-BlockParameter>.%<absoluteAddress>` is the documented syntax **[S9]**.

## How a student would use them in SCL

Illustrative only, **not compiled**. The measured version is M1–M5 below.

```scl
// Read a WORD at byte 0 of standard DB number #dbNumber, then copy 4 bytes to %MB100.
#word := PEEK_WORD(AREA := 16#84, DBNUMBER := #dbNumber, BYTEOFFSET := 0);
POKE_BLK(AREA_SRC := 16#84, DBNUMBER_SRC := #dbNumber, BYTEOFFSET_SRC := 4,
         AREA_DEST := 16#83, DBNUMBER_DEST := 0, BYTEOFFSET_DEST := 100,
         COUNT := 4);
```

A classroom checklist, all following from the sources above:

1. The addressed DB has "Optimized block access" **cleared**. Look at the Offset column.
2. `DBNUMBER` is `0` for I, Q and M.
3. The offset comes from the Offset column, not from counting variables by hand.
4. Error checking uses `GET_ERR_ID` until M6 shows whether ENO can be trusted.
5. Prefer an array or a slice if the DB does not have to change at run time.

---

## Measured, 2026-11-18

On a copy of the repository's test fixture (`TestProject1`, CPU 1511-1 PN V4.0 as `PLC_0`), with a CPU 1214C V4.7 added
for compile-only checks; PLCSIM Advanced V8 in TCP/IP mode, instance `CPU1511`. The fixture below was written with
`WriteScl`, compiled, downloaded and run; values read from the controller. Every result here replaces the
**[INFERRED]** or **[MEASURE Mn]** it names above.

| # | Result | What it settles |
|---|---|---|
| M1 | `PEEK` byte 16#12, `PEEK_WORD` 16#1234, `PEEK_DWORD` 16#DEADBEEF, `PEEK_BOOL` byte 3 bit 0 TRUE; from an **optimized** caller | Reads work; big-endian; the caller's access does not matter; `S7_Optimized_Access := 'FALSE'` through `WriteScl` gives a standard DB (`MemoryLayout` Standard) |
| M2 | An optimized DB **by number**: compiles; at run time reads **0, ENO FALSE**, no fault. **By name** (`DBNUMBER := "PP_Opt"`): compile error "Global access to a data block with memory reserve is not permitted here" | Optimized DBs cannot be read this way; by number it fails quietly |
| M3 | `POKE` of WORD 16#ABCD at offset 8: `Buf[0]` = 16#AB, `Buf[1]` = 16#CD | Big-endian writes |
| M4 | `POKE_BOOL` byte 2 bit 4: `B2` = 16#10. The help's `POKE(` with `BITOFFSET`: compile error "The formal parameter 'BITOFFSET' is invalid" | The help's example is wrong; use `POKE_BOOL` |
| M5 | `POKE_BLK` 4 bytes DB→M: `%MD900` = 16#DEADBEEF | Works |
| M6 | A DB that does not exist, an offset past the end, `DBNUMBER` ≠ 0 with the M area: each returns **0 with ENO FALSE**; **`GET_ERR_ID` returns 0** after each; the CPU **stays in RUN**, also in a block without local error handling (no `GET_ERR_ID`) | **[S1] is right and [S6] is not, for these instructions on this CPU: `IF NOT ENO` catches the error; `GET_ERR_ID` does not.** No STOP, no OB 121 |
| M7/M8 | `PEEK(AREA := 16#1)` on the 1511: 0, ENO TRUE. `POKE` to 16#1 and `PEEK` of 16#2 compile on the 1511; `PEEK(AREA := 16#1)` **compiles on the 1214C** too | The compiler does not enforce the help's "S7-1500 only" or the read/write split; run-time behaviour of those on a 1200 not measured (no 1200 in PLCSIM Advanced) |
| M9 | `BITOFFSET := 8` as a constant compiles | Not refused at compile time; run time not measured |
| M10 | Offset 65539 reads byte 3 (16#A5) | Only the 16 low bits of `BYTEOFFSET` are used |
| M11 | `GetCrossReferences` on `PP_Std`, used only through `PEEK`/`POKE`: no incoming references | A reviewer reading cross-references does not see these accesses |
| M12 | Slice access (`#w.%X3`, `#w.%B1`) compiles in an optimized FC; a `DB_ANY` given an optimized DB's name is refused as in M2 | — |
| M13 | Not measured: it needs TIA Portal's window | — |
| M14 | A standard DB's name as `DBNUMBER` (`DB_ANY`) compiles | The help's `DB_ANY` form works |

Friction met on the way, for the MCP: `WriteScl` refused a second write because its backup could not export the fixture's
STL `Main` while the program was uncompiled (STL has no document export, and an uncompiled block no source); compiling
after each write avoided it. And the MCP's `Connect` attaches to a TIA Portal a person has open: the measurement ran in a
TIA Portal of its own, started by the measuring script.

## Measurements to run in PLCSIM Advanced

For the lead agent, through the MCP: `WriteScl` → `CompileSoftware` → `GetBlockInfo` (for the DB
numbers and `MemoryLayout`) → `DownloadToSimulation` → `ReadSimulationTags` / `WriteSimulationTag`
→ `GetCrossReferences`. Use a **new throwaway project** with one CPU 1511-1 PN (V4.0) on PLCSIM
Advanced V8, and a CPU 1215C V4.7 for the compile-only checks. None of this has been run.

### Fixture

Three DBs. The numbers TIA Portal assigns are read back with `GetBlockInfo` and written into
`"PP_Cfg"` before the probe runs, so no number is hardcoded.

```scl
DATA_BLOCK "PP_Std"
{ S7_Optimized_Access := 'FALSE' }
VERSION : 0.1
NON_RETAIN
   STRUCT
      W0 : Word;                  // offset 0
      B2 : Byte;                  // offset 2
      B3 : Byte;                  // offset 3
      D4 : DWord;                 // offset 4
      Buf : Array[0..15] of Byte; // offsets 8..23
   END_STRUCT;
BEGIN
   W0 := 16#1234;
   B3 := 16#A5;
   D4 := 16#DEADBEEF;
END_DATA_BLOCK

DATA_BLOCK "PP_Opt"
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
NON_RETAIN
   STRUCT
      W0 : Word;
      D4 : DWord;
   END_STRUCT;
BEGIN
   W0 := 16#1234;
   D4 := 16#DEADBEEF;
END_DATA_BLOCK

DATA_BLOCK "PP_Cfg"
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
NON_RETAIN
   STRUCT
      StdNumber : DInt;
      OptNumber : DInt;
      MissingNumber : DInt;  // a number no DB has, e.g. 60000
      Run : Bool;
      ResByte : Byte;
      ResWord : Word;
      ResDWord : DWord;
      ResBool : Bool;
      ResOptWord : Word;
      ResEno : Bool;
      ResErrId : Word;
      ResWrap : Byte;
   END_STRUCT;
END_DATA_BLOCK
```

The probe is an **optimized** FC, on purpose (M1 checks that the caller's access does not matter).
Call it from `Main` with `"PP_Probe"();`.

```scl
FUNCTION "PP_Probe" : Void
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
VAR_TEMP
   eno : Bool;
END_VAR
BEGIN
   IF NOT "PP_Cfg".Run THEN
      RETURN;
   END_IF;

   // M1: reads from a standard DB
   "PP_Cfg".ResByte  := PEEK(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 0);
   "PP_Cfg".ResWord  := PEEK_WORD(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 0);
   "PP_Cfg".ResDWord := PEEK_DWORD(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 4);
   "PP_Cfg".ResBool  := PEEK_BOOL(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 3, BITOFFSET := 0);

   // M2: the same read from an optimized DB
   "PP_Cfg".ResOptWord := PEEK_WORD(AREA := 16#84, DBNUMBER := "PP_Cfg".OptNumber, BYTEOFFSET := 0);

   // M10: offset 65536 + 3 should land on offset 3 if only 16 bits are used
   "PP_Cfg".ResWrap := PEEK(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 65539);

   // M6: a DB that does not exist; ENO and GET_ERR_ID read together
   "PP_Cfg".ResByte := PEEK(AREA := 16#84, DBNUMBER := "PP_Cfg".MissingNumber, BYTEOFFSET := 0, ENO => #eno);
   "PP_Cfg".ResEno := #eno;
   "PP_Cfg".ResErrId := GET_ERR_ID();
END_FUNCTION
```

M6 overwrites `ResByte`, so run M1 with `MissingNumber := 0` first, read the results, then set it.

### The list

| # | What | How | Expected (to confirm or refute) |
|---|---|---|---|
| M1 | Basic reads, standard DB, optimized caller; `S7_Optimized_Access := 'FALSE'` honoured by `WriteScl` | Fixture; `GetBlockInfo` on `PP_Std` shows `MemoryLayout` = Standard; set `Run` | Compiles 0/0. `ResByte` = 16#12 (big-endian), `ResWord` = 16#1234, `ResDWord` = 16#DEADBEEF, `ResBool` = TRUE (bit 0 of 16#A5) |
| M2 | Optimized DB as target | Same run, `ResOptWord` | **Unknown.** Record: compile result, value, ENO, CPU state, diagnostic buffer |
| M3 | POKE to a standard DB, byte order | `POKE(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 8, VALUE := WORD#16#ABCD);` then read `"PP_Std".Buf[0]` and `Buf[1]` | `Buf[0]` = 16#AB, `Buf[1]` = 16#CD |
| M4 | POKE_BOOL, and the help's `POKE(` with `BITOFFSET` | `POKE_BOOL(AREA := 16#84, DBNUMBER := "PP_Cfg".StdNumber, BYTEOFFSET := 2, BITOFFSET := 4, VALUE := TRUE);` then the help's form with `POKE(` | First: `"PP_Std".B2` = 16#10. Second: expected a compile error |
| M5 | POKE_BLK, DB to M | `POKE_BLK(AREA_SRC := 16#84, DBNUMBER_SRC := "PP_Cfg".StdNumber, BYTEOFFSET_SRC := 4, AREA_DEST := 16#83, DBNUMBER_DEST := 0, BYTEOFFSET_DEST := 100, COUNT := 4);` | `%MD100` = 16#DEADBEEF |
| M6 | Access errors and ENO | Fixture with `MissingNumber := 60000`; then offset 100 on `PP_Std` (24 bytes long); then `AREA := 16#83, DBNUMBER := 1` | Decides [S1] vs [S6]: is `ResEno` FALSE, is `ResErrId` ≠ 0? Does the CPU stay in RUN? Record the diagnostic buffer and whether OB 121 is called |
| M7 | 16#81 is the process image | `WriteSimulationTag` %I0.0 := TRUE; `PEEK_BOOL(AREA := 16#81, DBNUMBER := 0, BYTEOFFSET := 0, BITOFFSET := 0)`; and `PEEK(AREA := 16#1, …)` on the 1511 | Both TRUE; whether they ever differ within a cycle is not observable in PLCSIM, so only record the values |
| M8 | Peripheral codes | On the 1215C: `PEEK(AREA := 16#1, …)`, `POKE(AREA := 16#2, …)`. On the 1511: `POKE(AREA := 16#1, …)` and `PEEK(AREA := 16#2, …)` | 1215C: compile error or runtime error, record which. 1511: record whether the unlisted directions are refused |
| M9 | `BITOFFSET` out of range | `PEEK_BOOL(…, BYTEOFFSET := 3, BITOFFSET := 8)` | Unknown: compile error, wraps to bit 0 of byte 3, or bit 0 of byte 4 |
| M10 | 16-bit offset | Fixture, `ResWrap` | 16#A5 if only the 16 low bits are used |
| M11 | Cross-references | `GetCrossReferences` on `"PP_Std".W0` and on `PP_Std` | Expected: the DB is not shown as used by `PP_Probe`. Record exactly what is shown |
| M12 | Alternatives on optimized blocks | In an optimized FB: slice `#w.X3`; `AT` over a WORD as `Array[0..15] of Bool` with retain "Set in IDB"; a `DB_ANY` parameter used as `#db.%DBW0` with `PP_Opt` and with `PP_Std`; `Serialize` of a struct into `Array of Byte` | Slice compiles; AT compiles; `%DBW0` via DB_ANY expected to fail on `PP_Opt` at run time (record how); Serialize compiles |
| M13 | LAD/FBD | In a LAD block's instruction search, look for PEEK | Expected absent. One screenshot is enough |
| M14 | `DB_ANY` constant in `DBNUMBER` | `PEEK(AREA := 16#84, DBNUMBER := "PP_Std", BYTEOFFSET := 0)` | Help lists DB_ANY as allowed **[S1]**; record whether a DB name is accepted |

After each run: compile messages verbatim, values read, CPU operating state
(`GetOperationMode`), and the diagnostic buffer entry if any. Delete the throwaway project afterwards.

---

## Proposal: how the MCP could support it

Ordered by value for the time spent. Item 2 is built inside `WriteScl` (no standalone `CheckScl` tool),
and item 3's plan finding with it; item 1 is not built.

### 1. Knowledge entries, citing and not authoring

After the measurements, the facts above become short entries carrying their source exactly as this
document does: help topic title, V21 help package and item id, and for measured facts the date and
the `Mn` that produced them. The knowledge layer's rule applies: **the system cites, it does not
author**. An entry that is still INFERRED is not stored.

The help is installed with TIA Portal, so the citation points at the reader's own installation
(`Help\en-US\ProgSCL2MenUS.zip` under the TIA Portal folder) rather than at a copy in this
repository. No Siemens text is committed beyond the short quotes here.

### 2. An SCL check: `SclMemoryAccessRules`

A read-only checker in `TiaMcpServer.Portable`, modelled on `UnifiedScriptChecker`: tokenize the SCL,
find calls to the five instructions, report findings with a rule id, severity and line. It needs no
TIA Portal, so its tests run in CI.

Rules, each tied to a source:

| Rule id | Finds | Severity | Basis |
|---|---|---|---|
| `peek-poke-optimized-db` | `DBNUMBER` resolves to a DB whose `MemoryLayout` is Optimized | Error | [S5], [S1]; behaviour from M2 |
| `peek-poke-dbnumber-not-zero` | A constant `AREA` other than 16#84 with a non-zero constant `DBNUMBER` | Error | [S1] |
| `peek-poke-unknown-area` | A constant `AREA` outside the table | Error | [S1] |
| `peek-poke-peripheral-on-1200` | `16#1`/`16#2` on an S7-1200 | Error | [S1]; M8 |
| `peek-poke-peripheral` (built 2026-11-24; proposed as `peek-poke-peripheral-direction`) | `16#2` in PEEK or PEEK_BOOL, `16#1` in POKE or POKE_BOOL, either in POKE_BLK | Warning: M8 shows they compile; run time not measured | [S1]; M8 |
| `peek-poke-missing-db` (built 2026-11-24) | A constant `DBNUMBER` with `AREA` 16#84 that no DB of the program has | Error | M6 |
| `peek-poke-offset-over-16-bit` | A constant offset ≥ 65536 | Warning | [S1] |
| `peek-poke-eno-check` | `IF NOT ENO` after one of the five | Warning, if M6 confirms [S6] | [S6] |
| `poke-writes-outputs` | POKE, POKE_BOOL or POKE_BLK with a constant `AREA` of 16#82 or 16#2 | Warning | [S2] |
| `poke-blk-dbnumber-types` | `DBNUMBER_SRC` and `DBNUMBER_DEST` of different types | Error | [S1] |

Only `peek-poke-optimized-db` and `peek-poke-missing-db` need the project. They run in the adapter: the
`Portal` layer reads every DB's number, and the `MemoryLayout` of only the DBs the source names by a
constant, and hands the checker a `ProgramDataBlocks` of plain numbers. No Openness type crosses into
`Portable`. When the source names no DB by a constant, the project is not read at all.

As built (2026-11-24), `WriteScl` runs the rules that need only the text **before** the write and
names their findings (rule and line) in the change plan's value, so a supervisor confirming it in
Workshop Mode sees them; after the write it runs every rule against the program and answers with all
the findings. `peek-poke-peripheral-on-1200` and `poke-blk-dbnumber-types` are not built.

When `DBNUMBER` is a variable, the checker says it cannot tell, never "fine". The absence of a
decision is not a pass.

Where it would surface: a `findings` list in the `WriteScl` answer, and a standalone
`CheckScl` tool over an exported block, mirroring `CheckUnifiedScripts`. A checker that only
reports changes nothing, so it does not need `GuardedTool.Run`.

### 3. Governance

- `WriteScl` already goes through `GuardedTool.Run`. A block that **contains** POKE is still one
  block to the guard; the guard sees the block, not what the code will write once it runs.
- The checker's `poke-writes-outputs` finding should be shown **in the plan** the person confirms,
  so a POKE to outputs is visible before the write, not after.
- In Workshop Mode, a block with a POKE whose area is not constant (the target cannot be known
  offline) should need a separate acknowledgement, item by item as the knowledge layer requires.
  This is a proposal for when Workshop Mode exists; Study Mode only reports.
- The checker must not make POKE look safe. Its footer says it checked the call, not the address
  the call will reach at run time.

### 4. Not proposed

- No tool that pokes memory in a running PLC. `WriteSimulationTag` already writes a named tag in
  PLCSIM; an address-based write would bypass every name the governance layer keys on.
- No automatic rewrite of PEEK/POKE into arrays or slices. Suggest it in a finding; the student
  writes it.

---

## Sources

| Id | Document | Where |
|---|---|---|
| S1 | TIA Portal V21 online help (en-US), SCL instructions, "Read/write access (S7-1200, S7-1500, S7-1200 G2)": topics PEEK, PEEK_BOOL, POKE, POKE_BOOL, POKE_BLK | Installed help package `ProgSCL2MenUS`, items `15889037451/33825679627`, `33802708747`, `33825681163`, `33825680395`, `33825681931`, `50372357387` |
| S2 | TIA Portal V21 online help, "Basics of indirect addressing (S7-1200, S7-1500)" | Package `ProgPLCBasicsenUS`, item `75696202379/19675351307` |
| S3 | TIA Portal V21 online help, "Setting up block access" | Package `ProgPLCBasicsenUS`, item `75696202379/38833270155` |
| S4 | TIA Portal V21 online help, "Basics of block access" (table of addressing options) | Package `ProgPLCBasicsenUS`, item `75696202379/19674036747` |
| S5 | Siemens, *S7-1200 Programmable controller, System Manual*, 04/2012, A5E02486680-06, section 6.5 "Programming language", "Indexed addressing with PEEK and POKE instructions" | pp. 160–162 |
| S6 | TIA Portal V21 online help, "Example of how to handle program execution errors", section "Exceptions" | Package `ProgPLCBasicsenUS`, item `75696202379/94414897547` |
| S7 | TIA Portal V21 online help, "Addressing areas of a tag with slice access (S7-1200, S7-1500)" | Package `ProgPLCBasicsenUS`, item `75696202379/19924075403` |
| S8 | TIA Portal V21 online help, "Overlaying tags with AT" | Package `ProgPLCBasicsenUS`, item `75696202379/19675317003` |
| S9 | TIA Portal V21 online help, "Indirect addressing of a data block via DB_ANY data type (S7-1200, S7-1500)" | Package `ProgPLCBasicsenUS`, item `75696202379/150251656331` |

The help packages were read as files from the TIA Portal V21 installation's `Help\en-US` folder;
TIA Portal was not started. S5 is the public PDF from Siemens Industry Online Support.

Third-party web pages were consulted and are **not** cited: several contradict each other and
the Siemens help on area codes and on which CPUs support the instructions.
