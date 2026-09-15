# builder-pattern-nxopen

**The Builder pattern in C#, modeled on how Siemens NXOpen builds CAM operations.**

A console kata from the [PATHNC AUTOMATION](https://github.com/[SEU-USUARIO]/pathnc-automation) study series · C# 7.3 · .NET Framework 4.7.2 · no dependencies.

## Why this example

Anyone who has written NXOpen code knows the rhythm:

```csharp
var builder = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(op);
builder.CutParameters.PartStock.Value = 0.5;
builder.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = ...;
NXObject result = builder.Commit();
builder.Destroy();
```

That is the Builder pattern. This kata reproduces it without NX so the *idea* can be studied and extended:

- **Immutable products** — `Tool`, `MillingOperation` (with `Feeds`, `CutParameters`, `NonCutting`). Only the builder can create them; nothing can change them afterwards.
- **Builders with defaults and sub-builders** — `ToolBuilder`, `MillingOperationBuilder` exposing `Feeds`, `CutParameters` and `NonCutting` as properties, exactly like NX.
- **`Commit()` that validates** — including cross-object rules that no constructor could check: depth per cut vs. tool flute length, bull-nose cutters that must not plunge, drills in milling operations, Z-level requiring the Profile pattern. All errors are collected and reported at once.
- **`Destroy()`** — using a builder after destroying it throws, as `NXException` does in NX.
- **A Director** — `OperationTemplates.Roughing` / `WallFinish` are ready-made recipes (cutting speed → RPM → feed), the equivalent of the `FBM_*_ROUGH` / `*_FINISH` classes in PATHNC. The builder knows *how* to build; the template knows *what* a roughing operation is.

## Run

1. Visual Studio → New Project → **Console App (.NET Framework)**.
2. Replace `Program.cs` with `BuilderPattern.cs` (or add the file and set `BuilderDemoNamespace.BuilderDemo` as the startup object).
3. F5.

Expected output: two operations printed with tool, feeds, cut and non-cutting parameters; one operation rejected with a list of validation errors; one rejection for using a destroyed builder.

```
CAVITY_MILL_D16 [CavityMill] method=MILL_ROUGH
  tool     : T1 ENDMILL_D16 (EndMill Ø16.0, 4 flutes)
  feeds    : S3600 F1150 (engage 690, retract 5000)
  cut      : FollowPart, stepover 40%, ap 1.00, stock 0.50/0.50, climb
  non-cut  : Ramp 3.0°, clearance 50, safe 3.0
...
Rejected as expected:
MillingOperationBuilder cannot commit:
  - depth per cut exceeds the tool flute length
  - bull nose cutters should not plunge — use ramp or helical
```

## Structure

```
BuilderPattern.cs
├── Products      Tool, Feeds, CutParameters, NonCutting, MillingOperation   (sealed, internal ctors)
├── Builders      BuilderBase<T>, ToolBuilder, MillingOperationBuilder
│                 └── sub-builders: FeedsBuilder, CutParametersBuilder, NonCuttingBuilder
├── Collection    CamSetup — creates builders, owns the products (≈ CAMOperationCollection)
├── Director      OperationTemplates — Roughing, WallFinish
└── Demo          BuilderDemo.Main
```

## Exercises

- Add `CreateMillingOperationBuilder(MillingOperation existing)` to `CamSetup` so a builder can **edit** an operation (start from its current values, replace it on commit) — the NX `CreateXBuilder(op)` form.
- Add a `DrillingOperationBuilder` with its own sub-builders (cycle type, peck depth, dwell) sharing `BuilderBase<T>`.
- Make `CamSetup` enforce unique operation names (`CAVITY_MILL`, `CAVITY_MILL_1`, …) as NX does.

## Related

- *NXOpen CAM com C# — do journal ao produto* and *PATHNC Katas* (PDF write-ups in the main repository)
- Kata 12 in `pathnc-katas` continues this example with editing builders and unique names.

## License

MIT
