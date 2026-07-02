# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ECMVC** is a lightweight Unity game architecture framework (Unity 2022.3.38f1) combining two independent systems:
- **EC (Entity-Component)**: Composition-based game object behavior management
- **MVC**: Event-driven UI system with strict layer separation

## Build & Test

This is a Unity project — there is no CLI build script. All building and testing is done via the Unity Editor.

- Open the project in Unity 2022.3.38f1 (project root: `E:\unity\ECMVC`)
- Main scene: `Assets/Scenes/SampleScene.unity`
- Run tests: **Window > General > Test Runner** in the Unity Editor
- CLI test run: `Unity -runTests -testPlatform editmode -projectPath <path>`

## Architecture

Code lives in `Assets/ECMVC/` and is split into two layers:

```
Architecture/   ← Reusable framework — do not modify lightly
  Components/   ComponentBehaviour (base), CustomComponent (Lua/MoonSharp bridge)
  Entities/     EntityBehaviour (core), UnitEntity (game-specific base)
  MVC/          ModelBase, ViewBase, ControllerBase, CounterBootstrap (example)

Example/        ← Reference implementation — safe to reference or modify
  Components/   CHealth
  Entities/     IHealthData, ExampleEntity
  MVC/          CounterModel, CounterView, CounterController
```

### EC System

`EntityBehaviour` (MonoBehaviour) manages a `Dictionary<Type, ComponentBehaviour>` and dispatches Unity lifecycle events to registered components. Components are plain C# classes; they never inherit from MonoBehaviour.

**Three-step pattern:**
1. Define a data interface (`IHealthData`) — pure data properties only, no concrete types
2. Implement `ComponentBehaviour`, implement lifecycle interfaces (`IStart`, `IUpdate`, etc.)
3. Implement `UnitEntity`, implement the data interface, wire up in `Awake` then call `base.Awake()` last

**Component API on EntityBehaviour:**
```csharp
AddComponentQ(component)      // register + subscribe to lifecycle
GetComponentQ<T>()            // returns null if absent
HasComponentQ<T>()
RemoveComponentQ<T>()         // unsubscribes from lifecycle
```

**Critical rule:** `base.Awake()` must be called **after** all `AddComponentQ` calls — it triggers `IAwake` on every registered component.

**Performance detail:** `IUpdate`, `IFixedUpdate`, `ILateUpdate` are cached as arrays with a boolean guard to avoid per-frame list iteration. Collision/trigger/GUI lists are lazily allocated.

**Tag system:** `UnitEntity` exposes a comma-separated Inspector field parsed into `HashSet<string>` on Awake. If subclassing `EntityBehaviour` directly, call `InitTags("Tag1,Tag2")` manually.

**Child entity management:** `GetAllChildEntities()` re-traverses the scene hierarchy and resets `parentEntity` references — do not mix with `AddChildEntity()` (which operates on the cache directly).

### MVC System

Strict flow: `Controller → Model → (event) → View`. Controller never touches UI elements; View never knows about Controller.

- **Model**: owns data, fires C# events on change
- **View**: subscribes in `Bind(model)`, implements `BindEvents()`/`UnbindEvents()` — `ViewBase.OnDestroy` auto-calls `UnbindEvents()`
- **Controller**: public methods bound directly to Button `OnClick` in Inspector
- **Bootstrap**: a single MonoBehaviour that creates the Model and wires it to View and Controller in `Awake`. `Bind` must complete before `Start`.

One Model can bind to multiple Views (C# events multicast). Display-only panels with no user input can skip Controller entirely.

### Lua Hot-Reload

`CustomComponent` bridges C# components with MoonSharp Lua scripts for hot-reload workflows. The Lua runtime is in `Assets/Plugins/`.
