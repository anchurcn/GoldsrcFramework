# GoldSrc Time System (Deep Dive)

This document explains, end to end, how time works in the GoldSrc engine:
where each clock comes from, how `deltaTime` is manufactured, how the client
simulation clock is kept in lock-step with the server, how snapshots are
interpolated, and what exactly each client-DLL export function receives.

It is written in two passes:

1. A short background in modern terms (`currentTime`, `lastTime`, `deltaTime`,
   time scaling, wall clock vs simulation clock), so the vocabulary is familiar.
2. A long, source-referenced walk through GoldSrc itself, using GoldSrc's own
   field names.

References are to the two trees vendored in this repository:

- Engine reference: `external/xash3d-fwgs` (Xash3D FWGS)
- Game reference: `external/hlsdk`

A companion document covers the intermission flag specifically:
`docs/Goldsrc-Intermission-DeepDive.md`.

---

## Part I — Background: time in a game loop, in modern terms

### 1.1 The canonical pattern everyone knows

Almost every game loop contains this shape:

```csharp
var currentTime = SystemTime.Now;          // sample the OS clock
float deltaTime = currentTime - lastTime;  // how long the previous frame took
lastTime = currentTime;

Update(deltaTime);                          // integrate everything by dt
Render();
```

Three roles:

- `currentTime` — a fresh sample of a high-resolution clock.
- `lastTime` — the previous sample.
- `deltaTime` (dt) — the difference. Movers use `pos += vel * dt`, decays use
  `x *= exp(-k * dt)`, animations advance phase by `dt`.

GoldSrc has this exact pattern at its core. It is just wrapped in three more
layers of processing (see §4).

### 1.2 Wall clock vs simulation clock

Two kinds of time exist in any serious engine:

- **Wall clock (real time)**: monotonic OS time. It never stops, never jumps,
  and is not affected by pauses or slow motion. Used for performance
  measurement, network RTT, UI, profiling.
- **Simulation clock (game time)**: the time the simulated world lives in. It
  can pause (`pause` in single-player), jump backward (a server correction),
  be scaled (slow motion), or be clamped (after a loading hitch).

Quick test to classify a clock: *"when the player pauses, should this value
keep ticking?"* If yes → wall clock. If no → simulation clock.

GoldSrc keeps both, plus two more specialized axes (see §3).

### 1.3 Time scaling

Slow motion is implemented by scaling dt before anything consumes it:

```csharp
scaledDt = rawDelta * timeScale;   // timeScale = 0.25 => quarter speed
gameTime += scaledDt;
```

Note the design decision hidden here: you scale **the delta**, and accumulate
it into a **separate scaled clock** — you never rewrite the wall clock itself.
GoldSrc does precisely this with `sys_timescale` and `host.realtime` (§4.2).

### 1.4 Frame pacing: capping FPS

To cap the frame rate, the loop computes the *target* frame length
(`1 / fpsMax`) and refuses to run a frame until enough wall time has passed:

```csharp
if (currentTime - lastFrameStart < targetFrameLength)
    return;   // not enough time passed — do nothing this iteration
```

Two possible policies on the "leftover": sleep, or spin. And two possible
policies about what to do with the *oversized* delta that eventually comes
out: keep it, or clamp it. GoldSrc's choices: sleep-or-spin
(`Host_Autosleep`), **whole-frame drop** (a frame that isn't old enough is
discarded entirely — not merely its render), and a hard clamp
(`bound(0.0001, 0.25)`) on the surviving delta (§4.2).

### 1.5 Fixed vs variable timestep

- **Variable dt**: each frame integrates by its measured length. Simple,
  smooth, but physics results depend on frame rate.
- **Fixed dt**: simulation always advances by e.g. 1/60 s; render frames
  interpolate between the last two sim states. Standard for modern physics.

GoldSrc is mostly a **variable-dt** design (rendering and client-side effects
run on `host.frametime`), with two fixed-dt islands bolted on:

- The **server** ticks at a fixed rate (`sv_fps`, default 20 Hz).
- **Input commands** carry an integer `msec` duration, and client-side
  prediction replays those commands at exactly that dt (§7).

### 1.6 Networked time: snapshots and interpolation

The moment a game is networked, the client can no longer trust its own
simulation clock. The authoritative state arrives as discrete **snapshots**
(at 20 Hz with default `sv_fps`). If you render the newest snapshot directly,
remote entities stutter in 50 ms jumps.

The classic fix:

1. Each snapshot is stamped with server time (`svc_time`).
2. The client maintains a **render clock** that lags the newest snapshot by a
   fixed **interpolation window** (GoldSrc: `cl_interp = 0.1 s`).
3. Each render frame, entities are drawn **between the two snapshots that
   bracket** the render clock, at fraction `frac`.

This is exactly the model GoldSrc implements (§6). The render clock `cl.time`
is therefore a strange beast: the client *pushes* it forward with local dt,
but the server periodically *corrects* it (§5).

### 1.7 Mapping table: modern concept → GoldSrc field

| Modern concept | GoldSrc field | Notes |
|---|---|---|
| `SystemTime.Now` / QPC sample | `Platform_DoubleTime()` | seconds since process start, from QueryPerformanceCounter (win) |
| `lastTime` | `static oldtime` inside `Host_FilterTime` | plus `COM_Frame`'s `oldtime` for the raw delta |
| `deltaTime` | `host.frametime` | clamped to `[0.0001, 0.25]`, may be *replaced* in single-player |
| accumulated scaled real time | `host.realtime` | `realtime += rawDelta * sys_timescale` |
| time scale | `sys_timescale` (0–10) | scales wall-clock accumulation |
| slow-mo / scripted rate | `host_framerate` (single-player) | *replaces* frametime outright |
| fps cap | `fps_max` / `fps_override` + `Host_Autosleep` | whole-frame drop |
| simulation clock (client) | `cl.time` (+ `cl.oldtime`) | server-corrected; **not** owned by the client |
| simulation clock (server) | `sv.time` | authoritative |
| snapshot timestamp | `cl.mtime[0]`, `cl.mtime[1]` | from `svc_time` |
| interpolation window | `cl_interp` (0.1 s) | render lag behind newest snapshot |
| interpolation fraction | `frac` in `CL_InterpolateModel` / `CL_PureOrigin` | per-entity, from position history |
| fixed server tick | `sv_fps` (default 20) | `sv.time` accumulator |
| input command dt | `usercmd_t.msec` | integer milliseconds, ≤ 255 |
| prediction clock | `frame->time` + `msec` accumulation | per-outgoing-command |

---

## Part II — GoldSrc itself

## 2. The process model: one loop, three phases

Understanding the clocks requires understanding the frame. GoldSrc (listen
server, single-player included) runs **client and server in one process**, in
one loop:

```text
Host_Main()  (engine/common/host.c:1327)
└── while( 1 )                          ← the single game loop
     └── Host_Frame( rawDelta )
          ├── Host_ClientBegin()        phase 1: input sampling + outgoing packet
          │     ├── Cbuf_Execute()
          │     ├── CL_UpdateClientData()      → HUD_UpdateClientData(cl.time)   [OLD value!]
          │     └── CL_SendCommand()           → CL_CreateCmd → CL_CreateMove(ft, cmd)
          ├── Host_ServerFrame()        phase 2: server tick (consumes the cmd just built)
          └── Host_ClientFrame()        phase 3: net read + advance + interpolate + render
                ├── CL_SendCommand()           (only when the server is remote)
                ├── HUD_Frame(host.frametime)
                ├── CL_ReadPackets()           ★ the single advance point of cl.time
                ├── CL_RedoPrediction()
                ├── CL_EmitEntities()          (interpolation with the NEW cl.time)
                ├── SCR_UpdateScreen()         → V_CalcRefdef, HUD_Redraw(cl.time)
                └── CL_AdjustClock()           (frame's very last statement)
```

Two consequences that shape everything below:

- **Phase 1 runs before `cl.time` is advanced**, so everything in
  `Host_ClientBegin` sees the *previous* frame's clock value (§8.1). The
  ordering exists to save one frame of input latency: on a listen server the
  usercmd built in phase 1 is consumed by phase 2 *in the same iteration*
  (`cl_main.c:3864`: *"if running the server locally, make intentions now"*).
- Client↔server packets travel through a **loopback queue** in the same
  process, so "the network" never reorders or delays them in single-player.

## 3. The clocks, one by one

### 3.1 `host.realtime` — the wall clock

Seconds, `double`, monotonic. Never pauses, never jumps (only its *rate* can
be scaled by `sys_timescale`). Everything that must survive pauses or measure
real durations keys off it: net resend timers, download progress, the
`senttime` prediction sentinel (§7), console history, etc.

### 3.2 `host.frametime` — the manufactured deltaTime

The length of the *previous real frame*, after processing: scaling, gating,
clamping. This is the value most client-side effects integrate by, and the
value `cl.time` advances by. Its full manufacturing chain is §4.

Related but distinct: `host.realframetime` is the *unsubstituted* twin —
when `host_framerate` replaces `frametime` (single-player slow-mo console
variable), `realframetime` still records what actually elapsed.

### 3.3 `cl.time` / `cl.oldtime` — the client simulation clock

The clock the *rendered world* lives in. Crucial property: **the client does
not own it.** It is advanced locally every frame (`cl.time +=
host.frametime`) but is *corrected* by server data — gently (≤ 7.5 ms per
frame) or violently (a hard reset when drift exceeds 0.1 s). It can move
non-linearly, jump backward, and freeze on `pause`. This is why HLSDK code
that needs a smooth accumulator deliberately does *not* use it (the famous
bob comment, §8.6).

`cl.oldtime` is simply *yesterday's* `cl.time`; their difference
(`cl.time - cl.oldtime`) is the sim-clock dt handed to e.g.
`HUD_TempEntUpdate`.

### 3.4 `cl.mtime[0]` / `cl.mtime[1]` — snapshot timestamps

Each incoming server datagram begins with `svc_time`; the client stores the
two most recent values: `cl.mtime[0]` (newest) and `cl.mtime[1]` (previous).
These are the fence posts of snapshot-space. `cl.time` is *supposed* to trail
`mtime[0]` by roughly one interpolation window.

### 3.5 The prediction clock (derived)

Client-side prediction does not use `cl.time` at all. Each outgoing command
slot (`cl.commands[i]`, a `runcmd_t`) owns a time that starts at the
acknowledged server time (`frame->time`) and advances by exactly
`cmd.msec / 1000.0` per command replayed — integer milliseconds, no drift.
See §7.

### 3.6 `sv.time` — the server clock (brief)

Authoritative, advances by `host.frametime` per server tick, gated by the
`sv_fps` accumulator (`sv_main.c:599-615`). Every snapshot is stamped with
it. In single-player it *is* the game's master clock; `host_framerate`
substitution (§4.2) means the server clock can be made to advance slower or
faster than real time for scripted sequences.

## 4. The `host.frametime` manufacturing chain

Four layers, each answering "where does dt come from?" at a different depth.

### 4.1 Layer 0 — the OS clock

`Platform_DoubleTime()` (`engine/platform/win/sys_win.c:24`):
QueryPerformanceCounter converted to seconds **since process start**. Not
Unix time; only differences are meaningful. Returns `double`.

### 4.2 Layer 1 — the main loop: the classic pattern, verbatim

`Host_Main` (`host.c:1327-1332`):

```c
oldtime = Sys_DoubleTime();          // (xash name; same function)
while( 1 )
{
    newtime = Sys_DoubleTime();
    time = newtime - oldtime;        // ← deltaTime = currentTime - lastTime
    oldtime = newtime;
    Host_Frame( time );
}
```

This is literally §1.1. The raw delta is passed *down*; it is not stored.

### 4.3 Layer 2 — `Host_FilterTime` (`host.c:618-641`): processing the delta

```c
static double Host_FilterTime( double time )
{
    host.realtime += time * sys_timescale.value;   // ① scaled accumulation
    ...
    if( !Host_Autosleep( target ))                 // ② fps gate
        return false;                              //    whole frame dropped
    ...
    host.realframetime = host.realtime - oldtime;  // ③ (differential again)
    host.frametime = host.realframetime;
    if( host_framerate.value > 0 && Host_IsSinglePlayerGame( ))
        host.frametime = host_framerate.value;     // ④ substitution
    host.frametime = bound( MIN_FRAMETIME, host.frametime, MAX_FRAMETIME );
    return true;
}
```

Step by step:

1. **Scale**: `realtime += time × sys_timescale`. The wall clock itself is
   never rewritten; a *scaled* clock accumulates alongside. `sys_timescale`
   clamps to `[0, 10]`.
2. **Gate (fps cap)**: if less than `1/fps_max` (or the platform timer
   target, `cl_maxframetime` — see `Platform_SetTimer`, set from
   `cl_main.c:3880-3881`) has elapsed, the function returns **false and the
   entire frame is skipped** — no update, no render. The internal `oldtime`
   is not touched, so the delta keeps accumulating across dropped iterations
   and is paid out in one lump when a frame finally runs.
3. **Differential**: `frametime = realtime − oldtime` — a *second*
   now-minus-last, this time on the scaled clock.
4. **Substitute + clamp**: in single-player (not demo), a nonzero
   `host_framerate` *replaces* dt outright — the engine's built-in slow-mo /
   fast-forward. Then clamp to `[0.0001, 0.25] s`
   (`MIN_FRAMETIME`/`MAX_FRAMETIME`, `common.h:110-111`) so a 2-second
   loading hitch cannot teleport physics.

`Host_Frame` then dispatches the three phases of §2.

### 4.4 Layer 3 — consumers

`host.frametime` fans out to: `cl.time +=` (advance), `cmd.msec = frametime×1000`
(input), `HUD_Frame`, `V_CalcRefdef`'s `frametime` member, voice idling,
`sv.time` accumulation. Everything downstream treats it as read-only.

**Summary of the differences from a textbook loop**: the single dt is split
into three stages — raw delta (loop) → `realtime` (scaled accumulation) →
`frametime` (clamped / substitutable) — and the frame gate drops *whole
frames* rather than just renders.

## 5. `cl.time`: advance point and correction machinery

### 5.1 The single advance point

Exactly one place in the client advances the sim clock — `CL_ReadPackets`
(`cl_main.c:3072-3082`), i.e. **mid-frame**, not at frame start:

```c
static void CL_ReadPackets( void )
{
    cl.oldtime = cl.time;
    if( !cl.paused ) cl.time += host.frametime;   // ← the only advance
    ...
    CL_ReadNetMessage();                          // parse new snapshots after
}
```

Why not "set up the frame's time at the top of the frame"? Because at the
top of the frame, *this frame's* `cl.time` does not exist yet. Its final
value depends on (a) whether a new snapshot arrives this frame (`svc_time`)
and (b) how much correction the previous frame's end queued up. The engine
deliberately settles the clock as late as possible, so the entire second half
of the frame — prediction redo, interpolation, rendering, HUD — sees one
stable value. The first half of the frame (input sampling) pays for this by
seeing a value exactly one `host.frametime` old (§8.1).

### 5.2 Three alignment paths (`CL_ParseServerTime`, `cl_parse.c:130-155`)

On every snapshot the client compares its clock against server time:

1. **Single-player override**: `cl.time = sv.time`-equivalent directly
   (local server, no reason to smooth).
2. **Hard reset**: drift > 0.1 s (lag spike, teleport through time) → snap
   immediately, accept the visual pop.
3. **Small drift**: recorded into `cl.timedelta`, to be absorbed smoothly.

### 5.3 `CL_AdjustClock` (`cl_main.c:3812-3838`) — the gentle hand

Called as the **last statement of `Host_ClientFrame`** (`:3930`). Each frame
it may bend `cl.time` by at most `cl_fixtimerate` (default **7.5 ms**)
toward the server, effectively gluing `cl.time` to trail `mtime[0]` by about
half a snapshot interval. The correction takes effect on the *next* frame's
advance — even the advance point never sees the "final" answer, by design.

```text
drift > 100ms   : snap (path 2)
0 < drift ≤ 100 : bend ≤7.5ms/frame (CL_AdjustClock, end of frame)
single-player   : direct assignment (path 1)
```

The net effect: `cl.time ≈ cl.mtime[0]` at all times, with only the
interpolation window's worth of sanctioned lag.

## 6. Snapshot interpolation: `frac` and the position history

### 6.1 The render delay

The client deliberately renders the past. With `cl_interp = 0.1 s` and
snapshots at 20 Hz (0.05 s apart), there are always ≥ 2 snapshots bracketing
the render instant:

```text
t_render = cl.time - cl_interp
```

### 6.2 Two different `frac` values — do not conflate them

1. **Global demo lerp**: `CL_LerpPoint` (`cl_main.c:434-456`) computes
   `lerpFrac = (cl.time - cl.mtime[0]) / cl_interp` — fence posts are the
   *snapshot timestamps*. In GoldSrc-style entity rendering this is mostly
   the Quake demo-playback path (`cl_frame.c:450-459`); it is *not* what
   moves monsters.
2. **Per-entity frac** (the real one): for each entity, from its own
   **position history**:

### 6.3 The position history `ph[64]`

Every networked entity owns a ring buffer of past samples
(`common/cl_entity.h`):

```c
typedef struct
{
    double   animtime;        // server timestamp of the sample
    vec3_t   origin;
    vec3_t   angles;
} position_history_t;         // :51-58
// inside cl_entity_t:
position_history_t  ph[HISTORY_MAX /* 64 */];   // :77-78, mask :63
```

`CL_UpdatePositions` (`cl_frame.c:42`, called from `:331`) pushes the current
curstate into the history each time a snapshot is parsed. This turns the
entity into a little timeline of its own.

### 6.4 The formula

Per entity, per render frame (`CL_InterpolateModel`, `cl_frame.c:438-513`):

```text
t    = cl.time - cl_interp                       (:470)
find t1, t2 in ent->ph[] with animtime(t1) ≤ t ≤ animtime(t2)
       via CL_FindInterpolationUpdates (:355)
frac = (t - t1) / (t2 - t1), clamped to [0, 1]   (:494-500)
curstate.origin = lerp(ph[t1].origin, ph[t2].origin, frac)
```

**The denominator is the snapshot interval** (e.g. 0.05 s), *not*
`cl_interp`. `cl_interp`'s only job is the render delay (the `- 0.1` in
`t`), i.e. guaranteeing the bracket always exists.

Remote players use the siblings `CL_PureOrigin` (`:392`, frac at `:411`,
clamp ceiling 1.2 to allow slight extrapolation) and
`CL_ComputePlayerOrigin` (`:522`), driven from `CL_LinkPacketEntities`
(`:1092`) inside `CL_EmitEntities` (`:1287`).

### 6.5 A monster, walked through numbers

Snapshot rate 20 Hz, render rate 100 fps, monster moving +5 u per snapshot:

```text
cl.time     : 100.00 → 100.01 → 100.02 → ...   (+0.01/frame)
t           :  99.90 →  99.91 →  99.92 → ...   (= cl.time - 0.1)
fence posts : animtime 99.90 (origin 0u) … animtime 99.95 (origin 5u)

frame 1: t=99.90  frac=(99.90-99.90)/0.05 = 0.0   → 0.0u
frame 2: t=99.91  frac=0.2                        → 1.0u
frame 3: t=99.92  frac=0.4                        → 2.0u
...
frame 6: t=99.95  frac=1.0                        → 5.0u
next snapshot arrives; new posts (99.95→100.00):
frame 7: t=99.96  frac=0.2                        → 6.0u   ← sawtooth resets,
                                                            but 1.0 endpoint ≡
                                                            next segment's 0.0
                                                            start (same origin)
```

The sawtooth is invisible: each `frac = 1.0` endpoint coincides with the next
segment's `frac = 0.0` start (identical position). The net effect is 5 u per
50 ms smeared into 1 u per 10 ms — constant apparent velocity regardless of
the snapshot/render rate ratio.

### 6.6 Single-player: it's all bypassed

With `maxclients ≤ 1`, `CL_InterpolateModel` returns early (`cl_frame.c:461`):
curstate *is* the final state (the "server" is in the same process, one frame
ahead at most). No history, no frac, no interp window on entities.

## 7. The prediction clock

Prediction answers "where does *my own* player end up if I apply the inputs I
sent but the server hasn't confirmed yet?" It must be **deterministic with
the server's replay**, so it cannot use a float wall-clock; it uses the
command stream itself.

### 7.1 msec — input dt as an integer

`CL_CreateCmd` (`cl_main.c:760-774`) converts the frame into integer
milliseconds:

```c
accurate_ms = host.frametime * 1000;
ms = (int)accurate_ms;
cl.frametime_remainder += accurate_ms - ms;      // accumulate rounding error
if( cl.frametime_remainder >= 1.0 ) { ms += ...; }   // pay out whole ms
ms = Q_min( ms, 255 );                           // usercmd_t field is 1 byte
```

The remainder accumulator guarantees long-term exactness: 100 fps frames are
10.0 ms exactly, but 95 fps (10.526…) frames alternate 10/11 ms with zero
drift.

### 7.2 The prediction timeline

For each outgoing slot (`cl.commands[i]`, `runcmd_t`):

- **Origin**: `frame->time` — the timestamp of the last *acknowledged*
  command (from the latest snapshot).
- **Advance**: each replayed command adds exactly `cmd.msec / 1000.0`
  (`HUD_PostRunCmd`, `cl_pmove.c:940`: `*time += cmd->msec / 1000.0`).
- **Freshness sentinel**: `pcmd->senttime = host.realtime` when built; if
  `senttime >= host.realtime` the command hasn't physically been sent yet —
  prediction stops there (never predicts into the future).

During replay, `pmove->frametime = msec / 1000` and `pmove->time` is the
millisecond prediction clock (`CL_SetupPMove`, `cl_pmove.c:798-799`).

### 7.3 Reconciliation (`CL_CheckPredictionError`, `cl_pmove.c:192`)

When a snapshot acknowledges a command, compare the server's authoritative
origin against the predicted origin at that command:

- error > 64 u → **teleport** the local view (no smoothing; something big
  happened: knockback, push, teleporter);
- error ≤ 0.25 u → ignore (sub-tick jitter);
- in between → smooth the visual error out over frames.

## 8. What each export function actually receives

The account table. "Axis" = which clock the parameter belongs to; "Value in
frame N" = relative to the advance point (§5.1).

| Export | Engine call site | Parameter(s) | Axis | Value in frame N | HLSDK uses it for |
|---|---|---|---|---|---|
| `HUD_UpdateClientData` | `cl_main.c:732` (ClientBegin) | `cl.time` | sim | **old** (N−1's) | nothing — parameter unused; function is a bidirectional data channel (fov write-back, button edges) |
| `CL_CreateMove` | `cl_main.c:801` (ClientBegin) | `host.frametime` | wall | current dt | keyboard turn rate, sensitivity scaling; `msec` filled by engine afterwards |
| `HUD_Frame` | `cl_main.c:3887` (pre-advance) | `host.frametime` (xash) | wall | current dt | voice-manager timers (delta consumed as time) |
| `HUD_TempEntUpdate` | `cl_tent.c:391-394` | `cl.time - cl.oldtime`, `cl.time` | sim | **new** | gravity steps, particle lifetimes (`die - time`), flicker frequencies |
| `HUD_Redraw` | `cl_game.c:903` | `cl.time` | sim | **new** | `m_flTimeDelta = flTime - m_fOldTime` drives HUD element animations (negative values clamped: clock went backward) |
| `V_CalcRefdef` | `cl_view.c:129-130` | `refparams.time = cl.time`, `refparams.frametime = host.frametime` | both | **new** / current | one struct, two axes — see below |
| `HUD_PlayerMove` | `cl_pmove.c:798-799` | `pmove->time` (ms), `pmove->frametime = msec/1000` | prediction | replay clock | all physics uses frametime only; `pmove->time` essentially unread |
| `HUD_PostRunCmd` | `cl_pmove.c:938-940` | prediction `*time` | prediction | replay clock | client weapon prediction animation timing; advances `*time += msec/1000` |

### 8.1 The one-frame-old value, and why

`HUD_UpdateClientData` runs in `Host_ClientBegin` — the front of the frame —
hence before the §5.1 advance point: it receives frame N−1's `cl.time`,
exactly one `host.frametime` older than what `HUD_Redraw` receives later in
the same `Host_Frame`. Three reasons this is intentional:

1. `cl.time`'s final frame-N value doesn't *exist* at frame start (pending
   snapshot arrival + pending `CL_AdjustClock` correction).
2. `ClientBegin` is placed first so the listen server consumes this frame's
   usercmd in the same iteration (input latency).
3. It's harmless: stock HLSDK never reads the parameter. The function's real
   job is the bidirectional exchange — engine passes
   viewangles/origin/weaponbits/fov in; client DLL writes back `fov` (the
   zoom pipeline: `CHud::m_iFOV` → `cl.local.scr_fov` →
   `refdef.fov_x = bound(10, scr_fov, 150)`, `cl_view.c:305`) and optionally
   viewangles, which become *this frame's* `usercmd.viewangles`.

### 8.2 The two-domain rule

- **Absolute-time parameters** (`cl.time` family) feed *phases, curves and
  lifetimes*: `sin(time × period)` view sway, `die - time` particle death,
  animation phase selection.
- **Delta parameters** (`host.frametime`, `msec/1000`, `cl.time -
  cl.oldtime`) feed *integration*: `pos += vel × dt`, decays, accumulators.

### 8.3 `V_CalcRefdef`: one struct, two axes

`ref_params_t` deliberately carries both: `fd->time = cl.time` (absolute,
for sway curves, `view.cpp:371-389`) and `fd->frametime = host.frametime`
(for punch decay / drift / bob accumulation, `:182, :303, :326-328, :695`).

### 8.4 HLSDK's defense against clock non-linearity

Because `cl.time` can bend (±7.5 ms/frame) and jump (hard reset), HLSDK
never assumes it's smooth:

- `hud_redraw.cpp`: `m_flTimeDelta < 0` → treated as 0 (clock moved back).
- `view.cpp:161` comment, verbatim in spirit: the bob accumulator uses
  `frametime` accumulation (`bobtime += frametime`) *"since the clock
  (pparams->time) isn't quite linear"* — the sway *curve* samples the
  absolute clock, but the *phase* advances by dt.
- `entity.cpp:413` (`HUD_TempEntUpdate`): `frametime ≤ 0` skips simulation
  entirely — the pause hack.

### 8.5 `HUD_TempEntUpdate`'s gravity trick

`gravity = -frametime × cl_gravity` (`:429`): instead of accumulating
velocity then integrating, each frame's velocity *is* the per-frame gravity
step, so `origin += velocity × ...` composes into proper parabolic motion
across variable frame rates.

### 8.6 Frame map: which half sees which clock

```text
                ┌──────────── 前半 · wall-clock domain ────────────┐ ┌──── 后半 · sim-clock domain ────┐
Host_ClientBegin               Host_ServerFrame   HUD_Frame   CL_ReadPackets ★   Emit/Refdef/Redraw
HUD_UpdateClientData(old T)                                   cl.oldtime=T;  time=T+ft          time = T+ft
CL_CreateMove(host.frametime)                                                     frametime = host.frametime
```

(English rendering: the *front half* of the frame — input sampling and the
server tick — can only rely on `host.frametime` and yesterday's `cl.time`;
the *back half* — prediction redo, interpolation, rendering — consumes the
freshly advanced clock. `CL_AdjustClock` at the very end bends it again, for
next frame.)

## 9. Design lessons for a modern engine

For reference when building the Stride-based GoldsrcFramework:

1. **"Advance time at tick start" is correct — for clocks you own.** A
   locally-owned simulation clock should be `time += fixedDt` at tick top,
   exactly like the textbook loop. GoldSrc's mid-frame advance is not a
   pattern to copy; it is the price of a *server-slaved* render clock.
2. **Separate the axes explicitly.** Wall clock / frame dt / sim clock /
   snapshot time / prediction time are five different concepts. GoldSrc
   mixes them in one global struct and pays in subtlety (unused params,
   one-frame-old values, defensive clamps). A modern engine should name and
   type them distinctly.
3. **Never feed a corrected clock into an integrator.** Anything that
   accumulates (bob, decay, cooldowns) must integrate by dt on a clock it
   owns; anything that samples a curve may use the absolute clock. This is
   the single most transferable rule in this document.
4. **Clamp dt, and know your policy on dropped frames.** GoldSrc: whole
   frame drop at the gate + `[0.0001, 0.25]` clamp. Modern equivalents:
   vsync-driven presentation with a spike clamp.
5. **Integer dt for anything replayed.** Prediction determinism comes from
   `msec` being an integer with an explicit rounding-remainder
   accumulator — not from float dt.
6. **Interpolation needs a dedicated buffer.** The `ph[64]` per-entity
   history is a clean, minimal design: stamped samples in, bracket search,
   one lerp. Worth reproducing (in an ECS: a ring buffer component updated
   on snapshot reception).

## 10. Code index

| Topic | Location |
|---|---|
| Main loop / raw delta | `engine/common/host.c:1327-1332` (`Host_Main`) |
| dt processing / fps gate / clamp / substitution | `engine/common/host.c:618-641` (`Host_FilterTime`) |
| MIN/MAX_FRAMETIME | `engine/common/common.h:110-111` |
| OS clock | `engine/platform/win/sys_win.c:24` (`Platform_DoubleTime`) |
| Frame phases | `engine/client/cl_main.c:3847` (`Host_ClientBegin`), `:3876` (`Host_ClientFrame`) |
| `cl.time` advance (single point) | `engine/client/cl_main.c:3072-3082` (`CL_ReadPackets`) |
| Clock bending | `engine/client/cl_main.c:3812-3838` (`CL_AdjustClock`), called `:3930` |
| Server-time alignment paths | `engine/client/parse/cl_parse.c:130-155` (`CL_ParseServerTime`) |
| msec manufacture | `engine/client/cl_main.c:760-774` (`CL_CreateCmd`) |
| `CL_CreateMove` call | `engine/client/cl_main.c:801` |
| `HUD_UpdateClientData` call | `engine/client/cl_main.c:719-738` (`CL_UpdateClientData`), `:3862` |
| `HUD_Frame` call | `engine/client/cl_main.c:3887` |
| `HUD_TempEntUpdate` call | `engine/client/cl_tent.c:391-394` |
| `HUD_Redraw` call | `engine/client/cl_game.c:903` |
| `V_CalcRefdef` time/frametime fill | `engine/client/cl_view.c:129-130`; fov `:305` |
| Position history struct | `engine/common/cl_entity.h:51-58`, `ph[64]` `:77-78`, mask `:63` |
| History push | `engine/client/cl_frame.c:42` (`CL_UpdatePositions`, called `:331`) |
| Bracket search | `engine/client/cl_frame.c:355` (`CL_FindInterpolationUpdates`) |
| Entity interpolation | `engine/client/cl_frame.c:438-513` (`CL_InterpolateModel`; t `:470`, frac `:494`, sp bypass `:461`) |
| Remote-player interpolation | `engine/client/cl_frame.c:392` (`CL_PureOrigin`), `:522` (`CL_ComputePlayerOrigin`), `:1092`, `:1287` |
| Global demo lerp | `engine/client/cl_main.c:434-456` (`CL_LerpPoint`), consumer `cl_frame.c:450-459` |
| pmove clocks | `engine/client/dll_int/cl_pmove.c:798-799` (`CL_SetupPMove`) |
| Prediction replay / time advance | `engine/client/dll_int/cl_pmove.c:925-940` (`CL_RunUsercmd`) |
| Prediction reconciliation | `engine/client/dll_int/cl_pmove.c:192` (`CL_CheckPredictionError`) |
| Server tick accumulator | `engine/server/sv_main.c:599-615` |
| HLSDK: update channel | `external/hlsdk/cl_dll/hud_update.cpp:31-52`, `cdll_int.cpp:230` |
| HLSDK: bob non-linearity comment | `external/hlsdk/cl_dll/view.cpp:161`, decay `:182/:303/:326-328/:695`, sway `:371-389` |
| HLSDK: HUD dt clamp | `external/hlsdk/cl_dll/hud_redraw.cpp:93-135` |
| HLSDK: temp entities | `external/hlsdk/cl_dll/entity.cpp:368-438` |
| HLSDK: input dt | `external/hlsdk/cl_dll/input.cpp:596, :652` |
| HLSDK: weapon prediction clock | `external/hlsdk/cl_dll/hl/hl_weapons.cpp:901-940` |

*(Line numbers verified against the vendored trees in this repository;
`engine/` paths are relative to `external/xash3d-fwgs/`.)*
