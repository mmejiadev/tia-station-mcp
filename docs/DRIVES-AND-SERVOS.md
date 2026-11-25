# Drives and servomotors on S7-1500 with TIA Portal V21

> Study document, written 2026-11-24 offline: **nothing here has been measured**. TIA Portal was not started; the sources
> are the files installed with TIA Portal V21 and PLCSIM Advanced V8.0 on the development machine, read as files, and the
> registry, read only. Companion to `STATUS.md`; modelled on `PEEK-POKE.md`.
>
> The roadmap item "Drives and servomotors" is not in `docs/ROADMAP.md` on `main` (H5 "Technology objects" and H9 "SINAMICS
> Startdrive" are). The request it answers: SINAMICS drives (G120, S120, V90) on PROFINET with their telegrams, drive
> parameters through Startdrive, technology objects linked to the drive, the PLCopen motion instructions in SCL, and an axis
> run in simulation. **Done** when a positioning axis is created on a 1511 with a simulated drive, homed and moved from SCL
> in PLCSIM Advanced, and the MCP reads back its position. Physical drives only in Workshop Mode.
>
> Every statement is tagged:
>
> - **[A1]**, **[H3]**, **[P1]**… — taken from a source listed in "Sources" at the end. API names are quoted exactly as the
>   installed XML documentation has them; prose quotes are kept under 15 words.
> - **[INFERRED]** — follows from a source but is not written in it. Treat as a hypothesis.
> - **[MEASURE Mn]** — not known; settled by measurement `Mn` in the plan below.
> - **[V21]** — the source says the fact is new in, or specific to, Openness V21 / Startdrive V21.
>
> Nothing tagged INFERRED or MEASURE may be told to a student as fact, or stored in the knowledge layer, until the
> measurement has been run.

## What is installed on this machine

| Product | State | Source |
|---|---|---|
| TIA Portal V21 | `V21.0 Upd2` (`TechnVersion=V21.00.02.00`) | [R1] `TIAP21\EditionMain` |
| SINAMICS Startdrive | **Installed**: "SINAMICS Startdrive Advanced V21.0 Upd1", features `FeatStartdrive` and `FeatStartdriveACX` | [R1] |
| Startdrive device support | "SINAMICS Startdrive G110M, G120, G120C, G120D, G120P, G115D" and "… G130, G150, S120, S150, G200, G210, G210C, G210X, G220, S200, S210, S220, Innomotics MVD"; G120 firmware packages FW44 to FW4714 | [R1] |
| SINAMICS V90 PN | **Not** a Startdrive device. Hardware support package folders `Data\SinamicsV90PN\V90_PN_V1_0` … `V1_5`, each with `Siemens.Simatic.Hwcn.BusinessLogic.HSP0185.mpk` | Folder listing [F1] |
| Motion technology support | "Support Base Package TO-01" and "TO-02" | [R1] |
| S7-PLCSIM Advanced | V8.0 (manual V8.0, 11/2025), runtime API under `PLCSIMADV` | [P1], [F1] |
| S7-PLCSIM V21 (classic) | Installed | [R1] `PLCSIM_V21` |
| Startdrive license | **Not checked** (the license manager was not opened) | **[MEASURE M9]** |

`docs/ENTERPRISE-READINESS.md` says Startdrive was "Installed on the development machine; not explored" (written on V20).
On V21 it is installed, in its "Advanced" edition.

## 1. What Openness V21 exposes

V21 split the API into modules. For technology objects: "add a reference to … Siemens.Engineering.Step7.dll", which "Contains
the name spaces" `Siemens.Engineering.SW.TechnologicalObjects` and `…TechnologicalObjects.Motion` **[H1, "Upgrading to
Openness V21"] [V21]**. Drives are in a separate assembly, `Siemens.Engineering.Startdrive.dll`, with its XML documentation
next to it in `PublicAPI\V21\net48` **[A2]**.

The server already compiles against it: the Openness NuGet package references every `*.dll` in that folder by wildcard
**[N1]**, so on this machine `Siemens.Engineering.Startdrive` is referenced today **[INFERRED]**. Whether the DLL is present on
a V21 machine **without** Startdrive is **[MEASURE M14]**; if not, code using it must be compiled conditionally, as the
PLCSIM API already is (`PLCSIM_AVAILABLE`).

### 1.1 Technology objects (`Siemens.Engineering.Step7.dll`)

Entry point: `PlcSoftware.TechnologicalObjectGroup` **[A1]**, which the help assigns to a `TechnologicalInstanceDBGroup`
**[H1, "Compiling technology object"]**; `TechnologicalInstanceDBSystemGroup` is documented as the system group **[A1]**.

| Type | Members (exact names) | Source |
|---|---|---|
| `TechnologicalInstanceDBGroup` | `TechnologicalObjects` (composition), `Groups` (user groups), `Name`, `Parent` | [A1] |
| `TechnologicalInstanceDBComposition` | `Create(String, String, Version)` — params `ofSystemLibElement`, `ofSystemLibVersion`; `Find(String)`; `Import(FileInfo, ImportOptions)`; `CreateFrom(MasterCopy)` and `CreateFrom(MasterCopy, MasterCopyMode)` | [A1] |
| `TechnologicalInstanceDB` | `Name`, `Parameters`, `OfSystemLibElement`, `OfSystemLibVersion`, `Export(FileInfo, ExportOptions)`, `Export(FileInfo, ExportOptions, DocumentInfoOptions)` | [A1] |
| `TechnologicalParameterComposition` | `Find(String)` | [A1] |
| `TechnologicalParameter` | `Name`, `Value` | [A1] |
| `TechnologicalInstanceDBUserGroupComposition` | `Create(String)`, `Find(String)`, `CreateFrom(MasterCopy…)` | [A1] |

- `Delete()` on a TO is shown in the help ("Deleting technology object": `technologicalObject.Delete();`) **[H1]** but is not
  documented as a member of `TechnologicalInstanceDB` in `Step7.xml` **[A1]**. Whether it is callable directly or only
  through `IEngineeringObject` is **[MEASURE M16]**.
- Compiling: `technologicalObject.GetService<ICompilable>().Compile()`, or the same on `TechnologicalObjectGroup` **[H1,
  "Compiling technology object"]**. After compiling, "the motion control versions of other technology objects are adapted
  to those of the last technology object created" **[H1]**.
- Export requires a consistent TO, checked with "the IsConsistent attribute", and the PLC offline; "Hardware configuration or
  tag tables are not exported with the technology object" **[H2, "Exporting technology objects"]**. `IsConsistent` is not a
  documented property in `Step7.xml` **[A1]**, so it is read as an attribute **[INFERRED]**, **[MEASURE M3]**.

#### Hardware connection, `…TechnologicalObjects.Motion`

Obtained as services on the TO **[H1]**: `GetService<AxisHardwareConnectionProvider>()` (axis),
`EncoderHardwareConnectionProvider` (external encoder), `MeasuringInputHardwareConnectionProvider`,
`OutputCamHardwareConnectionProvider`.

| Type | Members | Source |
|---|---|---|
| `AxisHardwareConnectionProvider` | `ActorInterface`, `SensorInterface`, `TorqueInterface` | [A1] |
| `AxisEncoderHardwareConnectionInterface` | `Connect(DeviceItem)`, `Connect(DeviceItem, DeviceItem)`, `Connect(DeviceItem, DeviceItem, ConnectOption)`, `Connect(Channel)`, `Connect(Int32, Int32, ConnectOption)` (bit addresses), `Connect(String)` (DB member), `Connect(PlcTag)` (analog); `Disconnect()`; read-only `IsConnected`, `InputModule`, `OutputModule`, `InputOutputModule`, `InputAddress`, `OutputAddress`, `Channel`, `PathToDBMember`, `OutputTag`, `ConnectOption`, `SensorIndexInActorTelegram` | [A1] |
| `ConnectOption` | `Default`, `AllowAllModules` ("Show all modules" in the UI) | [A1] |
| `TorqueHardwareConnectionInterface` | as above, for telegram 750 | [A1] |
| `CamDataSupport` | `LoadCamData`, `SaveCamData`, `SaveCamDataPointList`, binary variants; formats `MCD`, `Scout`, `PointList` | [A1] |
| `SynchronousAxisMasterValues` | `SetPointCoupling`, `ActualValueCoupling`, `DelayedCoupling` | [A1] |
| `TOMapping`, `DBMemberMapping`, `InterpreterProgramSupport` | S7-1500T interpreter | [A1] |

Which connection to use, per the help **[H1, "Connecting drives"]**:

- A drive from a **GSD/GSDML** file (or an HSP device such as the V90 PN **[INFERRED]**): `Connect(DeviceItem)` on the
  telegram submodule. The help's example finds a device item named "SIEMENS telegram 105, PZD-10/10".
- Any drive: `Connect(addressIn, addressOut, option)` with **bit** addresses: "Bit address = Byte address * 8". Required for
  drives configured with "Startdrive < V19".
- If the actor telegram contains an encoder, the sensor interface "is connected automatically"; telegram 750 likewise for
  torque data. Sensor interfaces are `SensorInterface[m]`, 0 ≤ m ≤ 3.

#### Connection to a Startdrive drive **[V21]**

The `Connect(Telegram)` overloads moved in V21 from `AxisEncoderHardwareConnectionInterface` to
`AxisEncoderHardwareConnectionSDRInterface`, and from `TorqueHardwareConnectionInterface` to
`TorqueHardwareConnectionSDRInterface`, which live in `Siemens.Engineering.Startdrive.dll` **[H1, "Upgrading to Openness
V21"]**, **[A2]**. The requirement reads "Startdrive as of V21 is installed" **[H1, "Connecting drives configured by
Startdrive"]**.

| Type (in `Startdrive.dll`) | Members | Source |
|---|---|---|
| `AxisHardwareConnectionSDRProvider` | `ActorInterface`, `SensorInterface`, `TorqueInterface` | [A2] |
| `AxisEncoderHardwareConnectionSDRInterface` | `Connect(Telegram)`, `Connect(Telegram, ConnectOption)`; extra read-only attribute `Telegram` | [A2], [H1] |
| `TorqueHardwareConnectionSDRInterface` | `Connect(Telegram)`, `Connect(Telegram, ConnectOption)` | [A2] |
| `EncoderHardwareConnectionSDRProvider` | `SensorInterface` | [A2] |

The help's example, condensed **[H1]**:

```csharp
var mainTelegram = startdriveDevice.DeviceItems.First(di => di.PositionNumber == 0)
    .GetService<DriveObjectContainer>().DriveObjects[0]
    .Telegrams.Find(TelegramType.MainTelegram);
axis.GetService<AxisHardwareConnectionSDRProvider>().ActorInterface.Connect(mainTelegram);
```

### 1.2 Drives (`Siemens.Engineering.Startdrive.dll`, namespace `Siemens.Engineering.MC.Drives`)

| Type | Members | Source |
|---|---|---|
| `DriveObjectContainer` (service on the drive's `DeviceItem`, per the example above) | `DriveObjects` | [A2], [H1] |
| `DriveObject` | `Parameters`, `ReadParameters`, `Telegrams`, `Security`, `DriveObjectNumber` | [A2] |
| `DriveParameterComposition` | `Find(String)`, `Find(Int32, Int32)` ("Find parameter by numeric values") | [A2] |
| `DriveParameter` | `Name`, `Number`, `ArrayIndex`, `ArrayLength`, `Value`, `MinValue`, `MaxValue`, `Unit`, `ParameterText`, `EnumValueList`, `Bits` | [A2] |
| `TelegramComposition` | `Find(TelegramType)`; `CanInsertMainTelegram(Int32)` / `InsertMainTelegram(Int32)`; the same for `Supplementary`, `Additional(Int32, Int32)`, `Torque`; `CanInsertTelegram(Int32, TelegramType)`, `InsertTelegram(…)`, `EraseTelegram(TelegramType)` | [A2] |
| `Telegram` | `TelegramNumber`, `Type`, `Addresses`, `HwIdentifiers`, `PKW`; `CanChangeTelegram(Int32)`; `CanChangeSize`, `ChangeSize`, `GetSize`, `GetSizeInBytes` (by `AddressIoType`) | [A2] |
| `Enums.TelegramType` | `MainTelegram`, `SupplementaryTelegram`, `AdditionalTelegram`, `SafetyTelegram`, `TorqueTelegram`, `EdgeTelegram` | [A2] |
| `OnlineDriveObjectContainer` / `OnlineDriveObject` | `OnlineDriveObjects`; `Parameters`, `ReadParameters`, `DriveObjectNumber` — **online** parameter access | [A2] |
| `DriveItemHardwareModule` | `ComponentName`, `PositionNumber`, `TypeIdentifier`, `TypeName`, `ChangeType(String)` | [A2] |
| `DFI.DriveFunctionInterface` | `Commissioning` (`SetMotorCode(Int32, Int32)`, `SetSimoGearMlfb(String)`), `HardwareProjection` (`SetMotorType`, `SetEncoder`, `ProjectMotorConfiguration`, `ProjectEncoderConfiguration`), `DriveObjectFunctions` (activation, `ChangeDriveObjectType`), `FunctionInUse` | [A2] |
| `DFI.DriveDomainFunctions` (online) | `PerformFactoryReset(ResetMode)` — `ParameterReset`, `SafetyParameterReset` | [A2] |
| `SafetyAcceptanceTestProvider`, `SafetyAcceptanceTestReport` | Safety acceptance test selection and `CreateProtocol(FileInfo, FileOperations)` | [A2] |
| `TechnologyExtension*` | Install / activate drive technology extensions | [A2] |
| `Download.Configurations.StartDriveDownloadCheckConfiguration`, `AcceptDownloadOfUnencryptedSensitiveData`, `ReplaceDownloadedData`; `Upload.Configurations.OverrideTelegramMismatch` | Download/upload checks specific to drives | [A2] |

The XML has **no remarks or examples** for any Startdrive type **[A2]**, and no Startdrive Openness manual is installed (the
`Manuals\en-US` folder holds only the Safety manual) **[F1]**. How a drive object is reached from a `Device`, beyond the one
example above, is therefore **[MEASURE M9]**. How a SINAMICS device is **added** (`Devices.CreateWithItem` with a catalog type
identifier, as for any device **[INFERRED]**) is **[MEASURE M9]** as well.

### 1.3 Which technology objects, which versions

From "Overview of technology objects and versions" **[H1]**:

| CPU | Motion TOs creatable through Openness | TO version | CPU FW |
|---|---|---|---|
| S7-1200 | `TO_PositioningAxis`, `TO_CommandTable` | ≥ V6.0 | ≥ V4.2 |
| S7-1200 G2 | `TO_SpeedAxis`, `TO_PositioningAxis`, `TO_ExternalEncoder`, `TO_SynchronousAxis`, `TO_OutputCam`, `TO_CamTrack`, `TO_MeasuringInput`, `TO_Cam`, `TO_Kinematics` | ≥ V8.0 | ≥ V1.0 |
| S7-1500 | `TO_SpeedAxis`, `TO_PositioningAxis`, `TO_ExternalEncoder`, `TO_SynchronousAxis`, `TO_OutputCam`, `TO_CamTrack`, `TO_MeasuringInput` | ≥ V3.0 | ≥ V2.0 |
| S7-1500T | adds `TO_Cam`, `TO_Kinematics`, `TO_LeadingAxisProxy`, `TO_Cam_10k`, `TO_Interpreter`, `TO_InterpreterMapping`, `TO_InterpreterProgram`, `TO_Cam_600Seg`, `TO_Cam_6kSeg` | various | various |

PID TOs (`PID_Compact`, `PID_3Step`, `PID_Temp`, `CONT_C`…) are in the same table. "Only technology objects that are listed …
can be created. An exception is thrown for unsupported" ones **[H1, "Creating technology object"]**. The cam TOs cannot have
parameters written through Openness; interpreter mapping and program cannot even be read **[H1, footnotes 3, 4]**.

**The version passed to `Create` is the technology version, and it is bounded by the CPU firmware.** The newest in the V21
help is **V10.0** (MC instructions `… V10`) **[H5]** **[V21]**. "Only one technology version can be operated on a CPU", and
the firmware table gives **[H4, "Overview of versions"]**:

| CPU firmware | Technology versions |
|---|---|
| V4.1 | V10.0, V9.0, V8.0, V7.0 |
| V4.0 | V9.0, V8.0, V7.0 |
| V3.1 | V8.0 … V3.0 |

V9.0 and later only on S7-1500 CPUs "with article number *10-0AB0"; *03-0AB0 CPUs from V7.0 **[H4, footnotes 3, 4]**. The
repository's fixture is a **CPU 1511-1 PN V4.0**, so `Create("…", "TO_PositioningAxis", new Version("10.0"))` should fail
there and V7.0–V9.0 may work depending on the article number **[INFERRED]**, **[MEASURE M1]**.

### 1.4 Parameters

"Most parameters of S7-1500 Motion Control technology objects are directly mapped to data block tags" **[H1, "Basics"]**:
`Parameters.Find("Actor.DriveParameter.ReferenceSpeed").Value = 3000.0` is the help's own example. Excluded: read-only tags,
`VREF` tags, `InternalToTrace` and `ControlPanel` **[H1]**. Writing a read-only or wrongly typed value throws an
`EngineeringException` **[H1, "Writing parameters"]**.

Some read-only DB tags are **writable through Openness** (configuration only), among them `Actor.Type` (0 analog, 1 PROFIdrive),
`VirtualAxis.Mode`, `Sensor[n].Existent`, `Sensor[n].Type` (0 incremental, 1 absolute, 2 cyclic absolute) **[H1, "Axis and
Encoder"]**. Others are not DB tags at all and start with `_`: `_Actor.Interface.Telegram` (telegram number),
`_Actor.DataAdaptionOffline`, `_Units.LengthUnit`, `_Properties.MotionType` (0 linear, 1 rotary),
`_PositionLimits_HW.MinSwitchAddress` (a `PlcTag`), `_Sensor[n].ActiveHoming.DigitalInputAddress` **[H1]**. Exported TOs
carry the first kind in `Member` elements and the second in `ParameterData` **[H2]**.

- `Simulation.Mode` is not in the "writable read-only" list because it is not read-only: its effectiveness is `RES` (applied at
  a restart of the TO) **[H3, "Simulation" tag; Legend]**. So it should be writable through `Parameters` like any DB tag
  **[INFERRED]**, **[MEASURE M2]**.
- For S7-1200 the help gives no list, only that the TO's parameter view has a column "Name in Openness" **[H1, "Parameters for
  TO_PositioningAxis and TO_CommandTable"]**.

## 2. Creating and wiring an axis through Openness

The sequence the sources support, none of it run yet:

1. `plcSoftware.TechnologicalObjectGroup.TechnologicalObjects.Create("PosAxis_1", "TO_PositioningAxis", version)` **[H1]**
   (version per 1.3, **[MEASURE M1]**).
2. Set units and axis type: `_Units.LengthUnit`, `_Properties.MotionType` **[H1]**.
3. Either **simulation without a drive**: `Simulation.Mode = 1` **[H3]**; "the drive and encoder connection does not need to be
   configured" **[H3, "Axis in simulation"]** — or **a drive**:
   - add the drive device and put it on the CPU's PROFINET IO system (existing MCP tools `AddDevice`,
     `ConnectDeviceToSubnet`, `AssignDeviceToIoSystem` **[INFERRED]** they suffice, **[MEASURE M9]**);
   - make sure it has a positioning telegram: 3, 4, 5, 6, 102, 103, 105, 106 carry setpoint and encoder in one telegram
     **[H3, "Configuring PROFIdrive telegrams"]**; `Telegram.CanChangeTelegram(105)` / `InsertMainTelegram(105)` for a
     Startdrive drive **[A2]**;
   - connect: `AxisHardwareConnectionSDRProvider.ActorInterface.Connect(telegram)` for Startdrive **[H1]**,
     `AxisHardwareConnectionProvider.ActorInterface.Connect(deviceItem)` for GSD/HSP drives **[H1]**.
4. Compile the TO (`ICompilable`) and then the software **[H1]**.
5. Read back: `IsConnected`, `Telegram` / `InputAddress` on the interface **[A1], [A2]**; export the TO as SimaticML
   **[H2]** for the backup the project rules require before every write.

Which telegrams fit which TO **[H3]**:

| TO | Telegrams |
|---|---|
| Speed axis | 1, 2 (and 3–6, 102, 103, 105, 106 without evaluating the encoder) |
| Positioning / synchronous axis | 3, 4, 5, 6, 102, 103, 105, 106 (setpoint + encoder); or 1–6, 102… for the setpoint with 81, 83 for the encoder |
| External encoder | 81, 83 |

Telegram 1 is "STW1/ZSW1, speed setpoint 16 bit" with no encoder value, so it cannot close a position loop **[H3]**. Telegram 105
adds DSC and torque limiting **[H3]**. Telegram 750 is the additional torque telegram **[H1], [H3]**.

## 3. Simulation: what runs with no drive

The function manual distinguishes three things **[H3, "Virtual axis", "Axis in simulation"]**:

| | Setpoints to a drive | Actual value | Changeable at run time |
|---|---|---|---|
| Real axis | yes | from the encoder | — |
| **Axis in simulation** (`<TO>.Simulation.Mode = 1`) | no | "formed with a time delay from the setpoints" (control loop + simplified drive model) | yes, "during runtime of the user program"; leaving simulation needs "a valid drive and encoder connection" |
| **Virtual axis** (`<TO>.VirtualAxis.Mode = 1`) | never | technology ≥ V8.0: setpoint adopted "with an application cycle delay"; ≤ V7.0: like simulation | only "by a new download to the CPU in STOP" |

In both simulated forms: `MC_Power` enables "immediately without waiting for feedback from the drive", `MC_Home` homing jobs
"are executed immediately without simulated axis motion", hardware limit and home switches "have no effect" **[H3]**. For
simulation mode "or with SIMATIC S7 PLCSIM", **encoder 1** must close the position loop **[H3]**. A virtual axis is meant as a
leading axis; the simulation mode is the one meant for "programming the machine application" before the hardware exists
**[H3]**. **So the "done" criterion is met with `Simulation.Mode = 1` and no drive at all**; a drive in the project is optional
and only proves the wiring **[INFERRED]**.

PLCSIM Advanced V8.0 **[P1, §3.2, §6.3, §6.4]**:

- "With S7-PLCSIM V16 and higher, the axes are always in simulation mode irrespective of the axis configuration. With
  S7-PLCSIM Advanced V3.0 and higher, the axes can also be operated in "Real" mode over the API." (footnote 3, §3.2; quoted
  longer than usual because the wording matters). So in **Advanced** the TO's own setting decides **[INFERRED]**.
- "In a simulated PLC instance, the technology objects are connected to the process image", so external simulation software
  (SIMIT) can play the drive **[P1, §6.3]**. Without such software, a real-mode axis whose telegram nobody answers should fail
  `MC_Power` (`StatusDrive.CommunicationOK` false) **[INFERRED]**, **[MEASURE M7]**.
- PLCSIM Advanced does not simulate technology modules (their motion instructions "are not supported") **[P1, §6.3]**, nor the
  SINAMICS Integrated of a Drive Controller **[P1, §6.4]**. **There is no SINAMICS drive simulation in PLCSIM Advanced**; the
  telegram addresses can be read and forced instead **[P1, §6.4]**.
- With "Use zero mark via PROFIdrive frame", PLCSIM Advanced "responds immediately" to active (mode 2, 3, 8) and passive (4, 5)
  `MC_Home` **[P1, §6.3]**. (The V10 instruction help uses different mode numbers, 2, 3, 5, 8, 10 **[H5]**; which applies is
  part of **[MEASURE M5]**.)
- Motion OBs: OB 91 MC-Servo overflows can put the instance into STOP; remedies are the virtual time scale or a longer
  application cycle **[P1, §6.3]**. The instance and global setting `StrictMotionTiming` is in the runtime API **[P2]**, not
  yet in the MCP.
- CPU support: PLCSIM Advanced simulates S7-1500 (standard, F, T, C, R/H), ET 200SP, Drive Controllers and **S7-1200 G2**,
  but not the classic S7-1200 **[P1, §3.3]**. On S7-1200 G2 it "does not support motion PTO" **[P1, §8.44]**.

## 4. The program side: PLCopen instructions in SCL

Versions V10 in the V21 help **[H5]** **[V21]**. Parameters (I = input, O = output) **[H5]**:

| Instruction | Inputs | Outputs | Starts on |
|---|---|---|---|
| `MC_Power` | `Axis`, `Enable`, `StartMode` (DINT, 1 = position-controlled), `StopMode` (INT: 0 emergency, 1 fast, 2 max dynamics, 3 coast) | `Status`, `Busy`, `Error`, `ErrorID` | level of `Enable` |
| `MC_Reset` | `Axis`, `Execute`, `Restart` | `Done`, `Busy`, `CommandAborted`, `Error`, `ErrorID` | rising edge |
| `MC_Home` | `Axis`, `Execute`, `Position` (LREAL), `Mode` (INT 0–13), `Sensor` | `ReferenceMarkPosition`, `Done`, `Busy`, `CommandAborted`, `Error`, `ErrorID` | rising edge |
| `MC_MoveAbsolute` | `Axis`, `Execute`, `Position`, `Velocity`, `Acceleration`, `Deceleration`, `Jerk`, `BufferMode`, `Direction` | `Done`, `Busy`, `Active`, `CommandAborted`, `Error`, `ErrorID` | rising edge |
| `MC_MoveRelative` | `Axis`, `Execute`, `Distance`, `Velocity`, `Acceleration`, `Deceleration`, `Jerk`, `BufferMode` | as MoveAbsolute | rising edge |
| `MC_MoveVelocity` | `Axis`, `Execute`, `Velocity`, `Acceleration`, `Deceleration`, `Jerk`, `Direction`, `Current`, `PositionControlled` | `InVelocity`, `Busy`, `CommandAborted`, `Error`, `ErrorID` | rising edge |
| `MC_Halt` | `Axis`, `Execute`, `Deceleration`, `Jerk`, `AbortAcceleration`, `Mode` | `Done`, `Busy`, `CommandAborted`, `Error`, `ErrorID` | rising edge |

Rules from the sources:

- **Execute is an edge.** "With a positive edge at the "Execute" parameter, the job is started and the existing values for the
  input parameters are transferred. Subsequently changed parameter values are not transferred until the next job start"
  **[H4, "Motion Control instruction parameters"]**. Holding `Execute` TRUE does not repeat the job **[INFERRED]**.
- **Done may last one cycle.** If `Execute` is reset before completion, `Done`, `Error` and `CommandAborted` are TRUE "for only
  one execution cycle" **[H4, "…with "Done" parameter"]**. Status bits are "updated with a delay" because the TO runs in
  another execution level **[H4]**.
- **Enable is a level.** For `MC_Power` and `Enable`-type instructions changed inputs are taken every call while `Enable` is TRUE
  **[H4]**. `Enable = FALSE` "aborts all Motion Control jobs" with the chosen `StopMode` **[H5, MC_Power]**.
- **A new job aborts the running one** (`CommandAborted`) and takes over its dynamics **[H4]**.
- **Prerequisites of `MC_Power`**: drive ready, `StatusDrive.CommunicationOK`, `StatusSensor[n].CommunicationOK`, sensor
  `State = 2`, data adaptation finished **[H5]**.
- **Homing before absolute moves**: "Homing is a requirement for … absolute positioning" **[H3, "Short description of
  homing"]**. Modes **[H3]**: 0 set actual position, 1 relative offset, 2/8/10 passive, 3/5 active, 6/7 absolute encoder
  adjustment, 11/12 set position setpoint, 13 incremental encoder adjustment. Modes 2, 3, 5, 8, 10 need the axis enabled; 0,
  1, 6, 7, 11, 12, 13 do not **[H5, MC_Home]**. `StatusWord.X5` (`HomingDone`) shows it **[H3]**.
- `Velocity = 0.0` is "Not permitted" in `MC_MoveAbsolute`; a negative value means the configured default
  (`<TO>.DynamicDefaults.Velocity`) **[H5]**. `Direction` is evaluated only with modulo **[H5]**.
- **Errors**: `Error` plus `ErrorID` (WORD) on each instruction; the TO has `StatusWord` (bit 0 `Enable`, bit 1 `Error`, bit 5
  `HomingDone`) and `ErrorWord` **[H5], [H3]**. Alarms that disable the axis are acknowledged with `MC_Reset` **[H5]**. The
  list of error IDs is in a separate manual ("S7-1500/S7-1500T Motion Control alarms and error IDs") that is **not** in the
  installed help **[H5]**, **[F1]**.
- **Multi-instances of `MC_Power`**: put them "in a separate function block", or a download in RUN is refused while the axis is
  enabled ("Download in RUN not possible.") **[H5, MC_Power note]**, **[H6]**.
- The `Axis` input takes the TO; "as of technology version V3.0" also a `DB_ANY` **[H4]**.

A sequence, illustrative only and **not compiled** (the measured version is M4–M6):

```scl
// Multi-instances in an FB; "PosAxis_1" is the TO. Each instance is called once per cycle,
// unconditionally; the step decides when its Execute rises.
#power(Axis := "PosAxis_1", Enable := #enable, StartMode := 1, StopMode := 0);
// Mode 0 = direct homing: actual position := 0.0, no motion
#home(Axis := "PosAxis_1", Execute := #step = 10, Position := 0.0, Mode := 0);
// Velocity < 0 = the configured default velocity
#move(Axis := "PosAxis_1", Execute := #step = 20, Position := 100.0, Velocity := -1.0);
#reset(Axis := "PosAxis_1", Execute := #step = 90 AND #acknowledge, Restart := FALSE);

CASE #step OF
    0:
        IF #power.Status THEN #step := 10; END_IF;
    10:
        IF #home.Done THEN #step := 20;
        ELSIF #home.Error THEN #lastError := #home.ErrorID; #step := 90; END_IF;
    20:
        IF #move.Done THEN #step := 30;
        ELSIF #move.Error OR #move.CommandAborted THEN #lastError := #move.ErrorID; #step := 90; END_IF;
    30:
        ;  // "PosAxis_1".ActualPosition should read 100.0
    90:
        IF #reset.Done THEN #step := 0; END_IF;
END_CASE;
```

Common mistakes, each traceable to a rule above:

1. `Execute := TRUE` written as a constant: the job runs once after the first edge and never again **[INFERRED from H4]**.
2. An instance called only inside an `IF` branch: its outputs stop updating while the branch is not taken **[INFERRED]**.
3. Moving before `MC_Power.Status` is TRUE, or `MC_MoveAbsolute` before any homing **[H5], [H3]**.
4. Checking `Done` after resetting `Execute` early and missing the one-cycle pulse **[H4]**.
5. Ignoring `Error`/`ErrorID` and `CommandAborted` **[H4]**.
6. `Velocity := 0.0` **[H5]**.
7. `MC_Power` multi-instance in the same instance DB as the rest of the program **[H5], [H6]**.

## 5. Safety

- **A servo moves metal.** A program that runs correctly in simulation drives a real axis exactly as written, including a wrong
  sign or unit. The project rules stand: every deployment targets **PLCSIM Advanced** by default; physical drives only in
  **Workshop Mode**, with a supervisor present and access to the emergency stop.
- The TO's `StopMode` and alarm reactions are not an emergency stop. The help notes the "Remove enable" reaction is not even
  output to the drive in simulation **[H3]**, so a test in simulation proves nothing about the real stop path **[INFERRED]**.
- In simulation, hardware limit switches and home switches "have no effect" **[H3]**: travel limits are untested until the
  axis is real.
- Leaving simulation mode at run time is allowed when the drive is connected **[H3]**. A tool that writes `Simulation.Mode := 0`
  turns a simulated axis into a moving one **[INFERRED]**: that write is a safety-relevant change, never a parameter tweak.
- Startdrive's API can reach a drive **online** (`OnlineDriveObject.Parameters`), reset it (`PerformFactoryReset`) and drive
  safety acceptance tests **[A2]**. None of it belongs in Study Mode.

## 6. Measurements to run in PLCSIM Advanced

On a **copy** of the fixture (`TestProject1`, CPU 1511-1 PN V4.0 as `PLC_0`) or a throw-away project, PLCSIM Advanced V8 in
TCP/IP mode, in a TIA Portal of the measurement's own, with nobody else's TIA Portal open. None of this has been run.

| # | What | How | Expected (to confirm or refute) |
|---|---|---|---|
| M1 | Create a positioning axis; version bounds | `TechnologicalObjects.Create("PosAxis_1", "TO_PositioningAxis", v)` for v = 10.0, 9.0, 8.0, 7.0 on the 1511 V4.0; record each exception text; read the CPU article number | 10.0 refused on FW V4.0; record which of 7–9 pass |
| M2 | Parameters | Enumerate `Parameters` (count, names); `Find` `Simulation.Mode`, `VirtualAxis.Mode`, `_Properties.MotionType`, `_Units.LengthUnit`, `_Actor.Interface.Telegram`; write `Simulation.Mode = 1`; read back | `Simulation.Mode` writable (RES); record types of `Value` (uint vs int) |
| M3 | Compile, consistency, export, delete | `GetService<ICompilable>().Compile()` on the TO and the group; read `IsConsistent` as attribute; `Export(…, WithDefaults)`; `Delete()` on a second TO | Compiles 0/0; export lists `Simulation.Mode` under `Member` |
| M4 | SCL against the TO | The FB above through `WriteScl`, called from `Main`; compile; check that `"PosAxis_1"` resolves and that OB 91/92 appear | Compiles; record what OBs TIA adds |
| M5 | Run in PLCSIM Advanced | Download, RUN, set `#enable`; observe `power.Status`, `home.Done`, `move.Done`; homing modes 0 and 3 | Mode 0 done at once; record mode 3 in simulation |
| M6 | Read back the position | `ReadSimulationTags` on `"PosAxis_1".ActualPosition` and `.StatusWord`; same through `ReadOpcUaValues` | 100.0; `StatusWord` bit 5 set. **Record whether a TO DB is readable at all through the PLCSIM API** |
| M7 | Real mode without a drive | `Simulation.Mode = 0`, no drive connected, then with a drive connected but unanswered | Compile error or `MC_Power.Error`; record `ErrorID`, `StatusDrive.CommunicationOK` |
| M8 | Virtual vs simulated axis | Same move with `VirtualAxis.Mode = 1` instead | Actual = setpoint delayed (V8.0+); record following error |
| M9 | Add a SINAMICS drive | `FindCatalogEntries` for S120 CU320-2 PN, G120 CU250S-2 PN and V90 PN; `AddDevice`; network to the CPU; `GetService<DriveObjectContainer>()` on each device item; `Telegrams.Find(MainTelegram).TelegramNumber`; `CanChangeTelegram(105)`; hardware compile; note any license prompt | Startdrive drives expose `DriveObjectContainer`; V90 PN (HSP) does not |
| M10 | Connect the axis to the drive | `AxisHardwareConnectionSDRProvider.ActorInterface.Connect(telegram)` (S120/G120); `AxisHardwareConnectionProvider.ActorInterface.Connect(deviceItem)` (V90); read `IsConnected`, `SensorInterface[0].IsConnected`, `InputAddress` | Sensor connected automatically for telegram 105 |
| M11 | Drive in project, axis simulated | M10's project with `Simulation.Mode = 1`, download to PLCSIM Advanced (the drive itself is not simulated) | Downloads; record the diagnostic for the missing PROFINET device; the move still runs |
| M12 | Offline drive parameters | `DriveObject.Parameters.Find("p1082")` and `Find(1082, 0)`; read `Value`, `Unit`, `MinValue`; write within limits on the copy | Readable offline; record the name format Startdrive expects |
| M13 | Execute and Done semantics | Hold `Execute` TRUE after `Done`; reset it mid-move; second `MC_MoveAbsolute` mid-move | One-cycle `Done`; `CommandAborted` on the first |
| M14 | Startdrive DLL without Startdrive | On the institute's V21 PC (or a clean VM), list `PublicAPI\V21\net48` | Decides whether drive code needs a build constant |
| M15 | Cross references | `GetCrossReferences` on `PosAxis_1` | The FB is listed as using the TO |
| M16 | Download in RUN with `MC_Power` enabled | Change the FB while the axis is enabled; download in RUN | "Download in RUN not possible." **[H6]** |
| M17 | Strict Motion Timing | Read the instance's `StrictMotionTiming`; force OB 91 overflow by a short application cycle | Record STOP or not |

After each run: compile messages verbatim, values read, CPU state, diagnostic buffer. Delete the throw-away project.

## 7. Proposal: how the MCP could support it

Nothing here is implemented. Each item is a **proposal**, ordered by value for the time spent. Every tool that changes the
project goes through `GuardedTool.Run`, names its target through `ChangeTarget`, and gets a refusal test in
`Test16GuardedWrites`.

### 7.1 Tools

| Tool (proposal) | Does | Guard |
|---|---|---|
| `GetTechnologyObjects` | Lists TOs per PLC: name, `OfSystemLibElement`, `OfSystemLibVersion`, group path, connection state (`IsConnected`, telegram number or address) | Read-only |
| `GetTechnologyObjectParameters` | `Parameters` of one TO, filtered by prefix; values as text plus type | Read-only |
| `CreateTechnologyObject` | `{ plc, name, type, version, group? }`; refuses a type/version pair outside the help's table and a version the CPU firmware cannot run (1.3) before calling Openness | Guarded write |
| `SetTechnologyObjectParameters` | `{ to, set: [{ name, value }] }`; exports the TO first (backup); refuses `Simulation.Mode` and `VirtualAxis.Mode` changes towards 0 unless Workshop Mode confirms them item by item | Guarded write |
| `ConnectAxisToDrive` | `{ to, drive, telegram? }`; Startdrive drive → SDR `Connect(Telegram)`; GSD/HSP → `Connect(DeviceItem)`; answers what got connected (sensor included) | Guarded write |
| `GetDriveTelegrams` | Telegrams of a Startdrive drive object: number, type, addresses | Read-only |
| `GetDriveParameters` | Offline `DriveObject.Parameters`, by name or number | Read-only |
| `ExportTechnologyObjects` / `ImportTechnologyObjects` | SimaticML, for Git versioning and backups **[H2]** | Import guarded |

Not proposed: online drive parameter writes (`OnlineDriveObject`), `PerformFactoryReset`, safety acceptance tests, technology
extension installation. They act on a physical drive or its safety configuration; if ever, only in Workshop Mode and with
their own design.

The Startdrive part needs `Siemens.Engineering.Startdrive.dll` at build time; after M14, behind a build constant like
`PLCSIM_AVAILABLE` if the DLL can be missing.

### 7.2 An SCL check: `SclMotionRules`

A read-only checker in `TiaMcpServer.Portable`, like the PEEK/POKE proposal: tokenize SCL, find calls of `MC_*` instances,
report findings with rule id, severity and line. No TIA Portal needed, so its tests run in CI. Surfaced as `findings` in the
`WriteScl` answer and in the change plan the person confirms.

| Rule id (proposal) | Finds | Severity | Basis |
|---|---|---|---|
| `mc-execute-constant` | `Execute := TRUE` as a constant | Warning | [H4] |
| `mc-velocity-zero` | constant `Velocity := 0.0` in a move | Error | [H5] |
| `mc-move-without-home` | `MC_MoveAbsolute` on an axis with no `MC_Home` anywhere in the block set | Warning | [H3] |
| `mc-move-without-power` | a move on an axis with no `MC_Power` call | Warning | [H5] |
| `mc-error-unread` | an instance whose `Error`/`ErrorID` is never read | Warning | [H4] |
| `mc-conditional-call` | an instance called only inside `IF`/`CASE` branches | Info | [INFERRED] |
| `mc-same-instance-twice` | one instance called twice in a block | Warning | [INFERRED] |
| `mc-power-multiinstance` | `MC_Power` as a multi-instance in an FB that holds other instances | Info | [H5], [H6] |
| `mc-home-mode-needs-enable` | `Mode` 2, 3, 5, 8 or 10 without a preceding `MC_Power.Status` check | Info | [H5] |
| `mc-simulation-off` | SCL writing `.Simulation.Mode` or `.VirtualAxis.Mode` | Warning, shown in the plan | [H3] |

As with PEEK/POKE: when the checker cannot tell (an axis passed as `DB_ANY`, an instance in an array), it says so, never
"fine". Its footer says it checked the code, not the machine.

### 7.3 Governance

- A positioning axis that is not in simulation is a target that moves. The plan for a download should list each motion TO with
  its `Simulation.Mode` / `VirtualAxis.Mode`, read before the download, so a person sees "real axis" before confirming.
- In Study Mode the tools refuse to set an axis out of simulation. In Workshop Mode it is a separate acknowledgement per axis.
- The existing rule that every deployment targets PLCSIM Advanced already blocks a physical drive; nothing here relaxes it.

### 7.4 Knowledge entries

After the measurements, the facts above become short entries citing this document's sources: help package and item id,
API file and member, and for measured facts the date and `Mn`. Nothing tagged INFERRED is stored.

---

## Open questions

1. Is Startdrive's license present, and does a project with a SINAMICS drive compile hardware without one? (M9)
2. Can the PLCSIM Advanced runtime API read a technology object's DB (`ActualPosition`)? The MCP's read-back depends on it;
   OPC UA is the fallback. (M6)
3. Which article number is the fixture's 1511, and therefore which technology version it accepts? (M1)
4. Is `Siemens.Engineering.Startdrive.dll` shipped without Startdrive? (M14)
5. The PLCSIM Advanced manual's homing mode numbers (2, 3, 8 / 4, 5) differ from the V10 instruction's (2, 3, 5, 8, 10).
   Old numbering, or another instruction version? (M5)
6. S7-1200 classic (1214C/1215C, the class CPUs): TOs can be created through Openness, but PLCSIM Advanced does not simulate
   that CPU and the classic S7-PLCSIM is not driven by the MCP. Is a compile-only check enough for class work?

## Sources

| Id | Document | Where |
|---|---|---|
| A1 | Openness V21 API documentation, `Siemens.Engineering.Step7.xml` | `Portal V21\PublicAPI\V21\net48` |
| A2 | Openness V21 API documentation, `Siemens.Engineering.Startdrive.xml` | same folder |
| N1 | NuGet package `Siemens.Collaboration.Net.TiaPortal.Packages.Openness` 21.0.1765349347, `targets\ReferenceSiemensEngineeringAssemblies.targets` (`*.dll` wildcard) | local NuGet cache |
| R1 | Registry, `HKLM\SOFTWARE\Siemens\Automation\_InstalledSW` (`TIAP21`, `PLCSIM_V21`, Startdrive entries), read only | this machine, 2026-11-24 |
| F1 | Folder listings: `Portal V21\Data\SinamicsV90PN`, `Portal V21\Manuals\en-US`, `Portal V21\Help\en-US` | this machine |
| H1 | TIA Portal V21 help, "Technology objects" (Openness): topics "Creating technology object", "Overview of technology objects and versions", "Compiling technology object", "Deleting technology object", "Writing parameters of technology object", "Basics", "Axis and Encoder", "Connecting drives", "Connecting drives configured by Startdrive", "Upgrading to Openness V21", "Parameters for TO_PositioningAxis and TO_CommandTable" | Package `TOOpennessenUS`, items `101848406667/95672631819`, `/95673198603`, `/95673427083`, `/95672068875`, `/95763532171`, `/95541428363`, `/148066652299`, `/95541437323`, `/189483902475`, `/189530885899`, `/96025987979` |
| H2 | TIA Portal V21 help, import/export of technology objects: "Exporting technology objects", "Importing technology objects", "Specific parameters for axis and encoder technology objects" | Package `ImExTOOpennessenUS`, items `119843023627/120639064971`, `/120639066891`, `/120639116939` |
| H3 | TIA Portal V21 help, S7-1500/S7-1500T axis functions: "Virtual axis", "Axis in simulation", "Actual value calculation for the axis in simulation", ""Simulation" tag (speed axis)", ""VirtualAxis" tag (speed axis)", "Legend", "Configuring PROFIdrive telegrams", "Short description of homing", ""StatusWord" tag (positioning axis)" | Package `TFTOSMCAxisenUS`, items `122095899659/101100511115`, `/86303261579`, `/151802094091`, `/175192536715`, `/175206743819`, `/41218952075`, `/146774832523`, `/39199873291`, `/39377751179` |
| H4 | TIA Portal V21 help, S7-1500/S7-1500T Motion Control overview: "Overview of versions", "Motion Control instruction parameters", "Motion Control instructions with "Done" parameter" | Package `TFTOSMCenUS`, items `28446067595/59694335115`, `/39730333323`, `/39730280715` |
| H5 | TIA Portal V21 help, S7-1500 Motion Control instructions V10: MC_Power, MC_Home, MC_MoveAbsolute, MC_MoveRelative, MC_MoveVelocity, MC_Halt, MC_Reset | Package `ProgFBSMCenUS`, items `28292530443/113382001291`, `/113383286027`, `/113383568523`, `/113383738251`, `/113383827979`, `/113383302795`, `/113383258891` |
| H6 | TIA Portal V21 help, "Help on message "Download in RUN not possible."" | Package `TFTOSMCErrorenUS`, item `158378857355/158378866699` |
| P1 | Siemens, *S7-PLCSIM Advanced Function Manual*, V8.0, 11/2025, A5E37039512-AK: §3.2 "S7-PLCSIM products", §3.3 "Supported CPUs", §6.3 "Simulating Motion Control", §6.4 "Simulating the SIMATIC Drive Controller", §8.44 | Installed PDF, `PLCSIMADV\documents\en-US`, pp. 21–24, 73–76, 99 |
| P2 | Siemens, *S7-PLCSIM Advanced API Function Manual*, V8.0, 11/2025, A5E53246567-AE: `GetStrictMotionTiming` / `SetStrictMotionTiming` | Installed PDF, same folder |

The help packages were read as files from `Portal V21\Help\en-US`; TIA Portal was not started. No web source is cited. The
help itself points to the S7-1500 Motion Control function manuals (Siemens Industry Online Support 109751049) and to FAQ
109750297 for the `MC_Power` prerequisites; neither was read for this study.
