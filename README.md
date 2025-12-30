# Production‑Ready WPF Multi‑Camera System (120 FPS) — Architecture Skeleton

 **What this repo is:** a **production‑oriented solution layout + concurrency/pipeline design** that you can extend with real camera SDKs (DirectShow, vendor SDKs, etc.).  


This project is designed for **performance‑critical, medical/diagnostic multi‑camera systems**: strict layering, deterministic timing model, bounded memory, and observability.

---

## Executive Summary

Multi‑camera **120 FPS per camera** is non‑trivial because you are balancing:

- **USB / driver variability** (hot‑plug, bandwidth contention, SDK edge cases)
- **Timing correctness** (monotonic timestamps, deterministic ordering)
- **Latency vs throughput** (bounded queues, explicit frame drop policy)
- **GC & memory pressure** (buffer reuse, avoiding per‑frame allocations)
- **UI isolation** (WPF must remain responsive; zero UI thread blocking)

This solution enforces architectural separation from day one:
- **HAL / Infrastructure** owns camera SDK specifics
- **FramePipeline** owns back‑pressure, drop policy, buffer lifecycle
- **Diagnostics** owns metrics and fault isolation
- **Presentation (WPF/MVVM)** consumes *only* prepared snapshots (never SDK calls)

---

## Solution Layout

```
src/
  App.Presentation.Wpf         # WPF UI (strict MVVM) – no camera SDK access
  App.Application              # Use-cases / orchestration (start/stop, lifecycles)
  App.Domain                   # Core models (FrameMetadata, Health)
  App.Common                   # Options, guard utilities
  App.Services.FramePipeline   # Per-camera pipelines + frame hub
  App.Services.Diagnostics     # Per-camera health, FPS, drops, worst Δt
  App.Infrastructure.Cameras   # Camera providers (Synthetic now, extend to SDKs)
  App.Infrastructure.Timing    # Monotonic clock (QPC-backed)
tests/
  App.Tests                    # Unit tests (lightweight skeleton)
docs/
  architecture/                # Architecture notes and diagrams
```

Dependency direction is **strict**: Presentation → Application → (Services/Domain) → Infrastructure.

---

## System Architecture Diagram

![Architecture](docs\architecture\architecture.png)

```mermaid
flowchart LR
  subgraph HAL[Infrastructure / HAL]
    CAM1[Camera Provider N
(SDK/DirectShow/Vendor)] -->|RawFrame| SINK1
    CAM2[Camera Provider N+1] -->|RawFrame| SINK2
  end

  subgraph PIPE[Services.FramePipeline]
    SINK1[CameraFramePipeline
(single-slot mailbox)] --> HUB[FrameHub
Latest-frame store]
    SINK2[CameraFramePipeline
(single-slot mailbox)] --> HUB
  end

  subgraph DIAG[Services.Diagnostics]
    MET[CameraDiagnostics
FPS / WorstΔt / Drops / Faults]
  end

  HUB --> UI[WPF MVVM
(DispatcherTimer polls latest)]
  CAM1 -->|fault| MET
  CAM2 -->|fault| MET
  HUB -->|OnFrameCaptured| MET
```

**Thread boundaries**
- **Capture threads** (owned by camera providers): never touch UI.
- **Pipeline consumer** per camera: isolates processing, applies back‑pressure policy.
- **UI thread**: reads only the *latest* immutable snapshot and paints it.

---

## Performance Design Decisions

### 1) Frame drop philosophy (explicit and deterministic)
Policy: **prefer newest** (bounded latency).
- Each camera pipeline uses a **single-slot mailbox**.
- If a newer frame arrives before the consumer picks the old one, the old frame is **dropped and disposed**.
- This yields deterministic memory and predictable UI responsiveness.

### 2) Timing model
- Capture timestamps use **monotonic QPC** (`Stopwatch.GetTimestamp`) via `IMonotonicClock`.
- Frame metadata carries both:
  - `CaptureTimestampQpc` (monotonic)
  - `CaptureTimestampTicks` (wall clock for logs/correlation)

### 3) Memory strategy
- Frames use pooled buffers (`MemoryPool<byte>.Shared`).
- UI uses a per-tile `WriteableBitmap` and updates via `BackBuffer` copy (**no per-frame allocations in UI**).

---

## Failure & Edge‑Case Handling

Designed for production realities:
- Camera provider faults are isolated and routed to diagnostics (`OnCameraFaultAsync`)
- Hot‑plug and provider switching are supported by the **factory** pattern (extendable)
- USB saturation → controlled degradation:
  - drops increase
  - health becomes **Degraded**
  - UI stays responsive

---

## Configuration & Deployment

The UI reads `appsettings.json` (copied to output):

`src/App.Presentation.Wpf/appsettings.json`

Example:
```json
{
  "App": {
    "Environment": "Dev",
    "Diagnostics": { "UiUpdateHz": 10, "HealthPublishHz": 5 },
    "CameraSystem": {
      "Cameras": [
        { "CameraId": "CAM-L", "Provider": "Synthetic", "Width": 640, "Height": 480, "TargetFps": 120, "PixelFormat": "BGRA32" },
        { "CameraId": "CAM-R", "Provider": "Synthetic", "Width": 640, "Height": 480, "TargetFps": 120, "PixelFormat": "BGRA32" }
      ]
    }
  }
}
```

---

## How to Run (Visual Studio)

1. Open `App.MultiCam120Fps.sln` in Visual Studio 2022+
2. Set startup project: `App.Presentation.Wpf`
3. Restore NuGet packages
4. Run

You should see **two synthetic camera tiles**, each targeting 120 FPS, with:
- FPS (smoothed)
- Worst-case Δt
- Dropped frames
- Health state

---

## Extending to Real Cameras

Add a provider inside `App.Infrastructure.Cameras`:

- Implement `ICameraDevice` (start/stop + capture loop)
- Use `ICameraFrameSink.OnFrameAsync(RawFrame frame)` to deliver frames
- Register the provider in `CameraFactory` based on profile `Provider`

Keep these invariants:
- Timestamp as close to delivery as possible
- Own buffer lifecycle explicitly (pooled buffers)
- Never call UI directly

---

## Roadmap

This architecture is intentionally ready for:
- **GPU offload** (add a processing stage between pipeline and hub)
- **Headless/service mode** (host orchestrator without WPF)
- **AI/CV integration** (subscribe to hub snapshots)
- **Network streaming** (WebRTC/RTSP writers consume from hub)
- **Per-camera priority & QoS** (scheduler service)

---


## License

MIT
