# The heap corruption, explained: an orphaned transfer into a freed buffer

Written 2026-11-25 from the two crash dumps of 24 November, then extended the same evening
when the whole chain was reproduced deliberately - see "Reproduced on demand" at the end,
which is the part to read if you only read one section. The dumps were the only ones ever
captured of this fault and have since been deleted; this document replaces them. The
findings below are what the code's battery, gating and transfer-abandonment decisions rest on,
so read this before changing any of them.

## What was happening

Between 23 and 24 November, N.I.N.A. 3.3.0.1070 died six times on the imaging PC with a GH5S
attached in extended (LUMIX Tether) mode. The Windows error reports named `VCRUNTIME140.dll`
with `0xC0000005` at four different offsets, plus one `0xC0000374`
(`STATUS_HEAP_CORRUPTION`) in `ntdll`. Faulting stacks were in innocent code — one was WPF
text layout — which is the signature of something writing outside what it owns and a later,
unrelated component taking the fall.

Months of plugin-side auditing found nothing: every buffer the plugin hands the SDK measured
clean, every marshalled struct matched Panasonic's headers in size, and a probe confirmed
`Get_Object` stayed inside the buffer it was given. That work was looking in the wrong place.

## What the dumps show

Two full dumps were captured once `LocalDumps` was configured: process 3784 (crashed 02:08 on
24 November) and process 13588 (09:55 the same day). They are the same fault.

```
ExceptionAddress: VCRUNTIME140!memcpy_avx_ermsb_Intel+0x32e
   ExceptionCode: c0000005 (Access violation on write)

VCRUNTIME140!memcpy_avx_ermsb_Intel+0x32e
Lmxptpif!LMX_func_api_ptpip_UpdateCer+0x52af
Lmxptpif!LMX_func_api_ptpip_UpdateCer+0x4b55
kernel32!BaseThreadInitThunk
ntdll!RtlUserThreadStart
```

(The `UpdateCer` symbol is just the nearest preceding export; the real function is unnamed.)

| | process 3784 | process 13588 |
|---|---|---|
| dump time | 24 Nov 02:08:13 | 24 Nov 09:55:01 |
| copy length (`r14` = `r15`) | 18,890,240 (`0x1203e00`) | 18,624,000 (`0x11c2e00`) |
| destination | `0x211b275a040` | `0x2d2c6bb0040` |
| bytes copied before the fault | 7,968 | 7,968 |
| threads in the process | 49 | 49 |
| threads with any `Lmxptpif` frame | 1 | 1 |
| managed or plugin frames on that thread | none | none |

Four things follow, each measured rather than inferred:

**The faulting thread belongs to the SDK.** Its stack bottoms out at `RtlUserThreadStart`, so
it is a raw native thread Panasonic's DLL created and runs itself. Across all 49 threads, those
two `Lmxptpif` frames are the only SDK frames in the process, and no thread was inside any SDK
call. The API call that queued this transfer had already returned.

**The length is correct; the destination is not.** Disassembling the call site shows the SDK
bounds-checks properly — it takes `min(dataAvailable, callerBufferSize)` and writes the copied
count back through a caller pointer:

```
mov  r14d, dword ptr [rbp-75h]     ; data available
mov  r15d, dword ptr [rbp+5Fh]     ; caller's buffer size
mov  rcx,  qword ptr [rbp-51h]     ; destination
cmp  r14d, r15d
ja   <use r15>
mov  r8d,  r14d                    ; length = min(...)
call <memcpy wrapper>              ; <-- the return address on our stack
```

Both size registers held the true frame size. So the SDK was told a buffer of 18.6 MB existed
at an address where only two pages were mapped.

**The destination is the SDK's own native memory, not the plugin's array.** Checked against the
managed heap in both dumps with `dotnet-dump analyze -c "eeheap -gc"`: the destination falls
inside no managed segment, clearing the nearest by 88 MB in 3784 and 272 MB in 13588. The
plugin's per-frame `byte[]` buffers are plainly visible elsewhere, in large-object-heap
segments. In both dumps the destination's whole neighbourhood — 27.5 MB and 17.9 MB — is
`MEM_FREE` at dump time, including the pages that were successfully written, while the *source*
is a correctly sized, still-committed native heap large block holding the image.

**The write happens long after the plugin has finished with the frame.** Matching the copy
length against the session logs identifies the exact frame, and each appears once:

| | process 3784 | process 13588 |
|---|---|---|
| frame reported | 02:05:51.639 | 09:53:43.461 |
| plugin's `Get_Object` returned | 02:05:51.768, **`ret=0`** | 09:53:43.650, **`ret=0`** |
| the plugin abandoned it | `Skip_Object_Transfer` | `Skip_Object_Transfer` |
| the SDK performed the copy | 02:08:13 | 09:55:01 |
| **delay** | **141 s** | **77 s** |

In **both** dumps the orphaned transfer is the one whose `Get_Object` returned 0 with a
zero-filled buffer and which the plugin then abandoned. An earlier revision of this document
attributed the 13588 copy to a *successful* transfer 82 s earlier; that was a hex conversion
error on `r14` (`0x11c2e00` is 18,624,000, not 18,623,488) and it is corrected above. The
corrected reading is the simpler one: the SDK completes transfers it has already reported as
failed.

## The mechanism

The SDK retains a transfer request whose destination buffer it has already released, and
completes it one to two minutes later on its own thread. Whatever has since taken that address
receives up to a frame's worth of image data, until the copy runs past the end of what is
mapped and faults. That is the heap corruption. It explains every signature observed: four
access violations at four different `memcpy` offsets are four different amounts of progress
before hitting unmapped memory, the single `0xC0000374` is a run whose damage landed entirely
on live heap and was detected later, and the WPF text-layout crash was a victim rather than a
cause.

**No version of this plugin can make that write safe.** It happens inside Panasonic's DLL, on a
thread the plugin does not own, after the plugin's call has returned. The only available
defence is to avoid provoking the state that orphans a transfer.

## What provokes it

In both dumps the collapse begins with two requests failing together in the same millisecond —
a battery read refused with `err=0` and an all-zero scratch, and `Get_Object` returning 0 with
a zero-filled buffer. Two requests bouncing at once is the signature of a channel that already
had two users: the battery read was issued from NINA's independent 2-second device-update timer
and landed inside an image transfer. The SDK then wedges — the 148-second mount-poll stall in
the 3784 log is this — and the stale copy fires as it unwedges.

Across the 26 session logs on that machine, the crash rate tracks the arrival of that polled
read:

| | sessions with frames | frames | crashes |
|---|---|---|---|
| before the battery poll existed | 4 | 437 | 1 (the separate SD-card-full fault) |
| while the battery was polled from NINA's timer | 6 | 337 | 5 |
| after the gate and the read-once change | 2 | 92 | 0 |

Two honest limits on that table. The clean 141- and 184-frame sessions predate the battery
feature, so they are not evidence that a read is safe — only that the crashes began when the
polled read did. And the same SDK call site crashed once before the battery read existed, on
23 November at 08:33, by the separately diagnosed zero-size-object path. So a concurrent
control request is an aggravator, not the only way to abandon a transfer.

## What the code does about it

1. **One gate over every native call** (`LumixCam._sdkGate`). Two plugin-initiated requests can
   no longer interleave on the channel, which is the precise overlap both dumps show. It cannot
   constrain the SDK's own thread, so it removes the trigger, not the fault.
2. **`BatteryLevel` is a plain field.** NINA asks for it from its 2-second cycle and from
   `metaData.FromCamera` on every frame and live-view frame; none of that reaches the SDK.
3. **The reading is refreshed only from an idle point** — the top of `StartExposure`, after the
   previous frame has been downloaded and before the next shutter fires, at most every five
   minutes, skipped during live view and skipped when an exposure was still in flight on entry.
   The exposure-mode poll and the card-capacity read already live at that point and have never
   been implicated across sessions of up to 234 frames.
4. **Transfers that cannot be used are still skipped** (`Skip_Object_Transfer`), but note what
   the dumps prove about that: it was already being called on the failed-transfer path when
   both crashes happened. Process 3784 is exactly that path — skip called at 02:05:51.771,
   crash at 02:08:13. **Telling the SDK to abandon a transfer does not stop it finishing it.**
   Do not treat that call as a defence.

## If it happens again

`LocalDumps` is configured for `NINA.exe` and proven by a deliberate test crash: full dumps,
up to 10 of them, in `C:\Users\Robert\AppData\Local\NINA\CrashDump`. To tell within minutes
whether a new crash is this same fault:

```
cdb -z <dump> -c ".symopt+ 0x40; .exr -1; .ecxr; kb 60; q"
```

It is this fault if the exception address is in `VCRUNTIME140!memcpy*`, the caller is
`Lmxptpif`, the stack has no managed frame, and `r14`/`r15` hold a plausible frame size. Then
find that size in the session log: the gap between the `Get_Object` that returned and the dump
time is the delay. Anything else is new.

## Reproduced on demand, 2026-11-25

Everything above was inferred from two post-mortems. On the evening of 25 November the whole
chain was reproduced deliberately, on the same camera, in three experiments — each a single
command, none of them waiting for luck. The analysis no longer rests on two dumps.

### 1. The SDK is not thread-safe on one context

`scripts\lumix-race-probe.ps1` on the imaging PC, NINA closed, 2,000 calls per phase. Every
call is a read; no shutter is actuated.

| phase | threads | lock | result |
|---|---|---|---|
| 2 | one | n/a | 6.2 s, zero symptoms |
| 3 | two | none | **deadlocks inside `Lmxptpif` within seconds** |
| 4 | two | one lock | 7.0 s, zero symptoms |

The only variable between 3 and 4 is the lock. Phase 3 had also deadlocked on 24 November, so
this is two independent reproductions. The lock costs ~13% on that benchmark — back-to-back SDK
reads from two threads — so nothing at the plugin's real call rate.

**This is the evidence for the gate.** It was the right fix for a demonstrated defect.

### 2. One overlapping call during a transfer breaks the transfer

`scripts\lumix-late-write-probe.ps1`, written for this. It fires one frame, waits for the
transfer event, then starts a battery thread with no lock and calls `Get_Object` into a 32 MB
native buffer pre-filled with `0xCC`:

```
[0000104ms] capture 1: Rec_Ctrl_Release ret=1 err=0x00000000
[0001411ms] callback: event 0x10000043 handle 0x12345678
[0001417ms] Get_Object_DataSize ret=1 size=18610176 err=0x00000000
[0001418ms] battery thread started, NO lock - colliding with the fetch now
[0001433ms] Get_Object ret=0 err=0x00000000 magic=CC CC CC CC
[0001470ms] battery thread stopped after 1 calls, 1 refused
```

Reproduced on both runs where the capture succeeded. Two facts that correct the record:

- **One colliding call is enough.** The battery thread got exactly one call in before the fetch
  failed, and that call was refused too — both requests bounce off each other. The trigger is a
  single unlucky overlap, not sustained polling, which is how a 2-second device-update timer
  found it inside 40 frames.
- **The SDK writes nothing; it does not zero the buffer.** The `0xCC` guard survived intact.
  NINA's `magic=00 00 00 00` at the same moment was the plugin's own freshly allocated array.

### 3. The SDK then writes into memory it has freed

The decisive one. Watching the *caller's* buffer proves nothing, because the destination in both
dumps was the SDK's own allocation — so instead make the SDK's own freed memory fault on write,
with page heap. Applied to a renamed copy of the PowerShell host, so the setting touches nothing
else on the machine:

```
copy C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe C:\nina\scripts\lumixprobe.exe
.\gflags.exe /p /enable lumixprobe.exe /full          (elevated; writes HKLM)
.\lumixprobe.exe -ExecutionPolicy Bypass -File .\lumix-late-write-probe.ps1
.\gflags.exe /p /disable lumixprobe.exe                (always, afterwards)
```

The probe died seconds after going quiet. Windows recorded:

```
EventType          = AutoVerifierV2
Faulting module    = VCRUNTIME140.dll
Faulting version   = 14.50.35719.0
Exception code     = c0000005
Exception offset   = 0x00000000000114f3
IsFatal            = 1
```

`AutoVerifierV2` is page heap reporting a **write to freed memory**, caught at the instruction
rather than minutes later in whatever had taken that address. The faulting module and exception
code match both 24 November dumps: `memcpy` in the C runtime, called from the SDK.

### What the three experiments establish together

| step | how it is known |
|---|---|
| concurrent ungated calls wedge the SDK | measured — race probe, phases 2/3/4 |
| one overlap during a transfer makes `Get_Object` fail writing nothing | measured — late-write probe, twice |
| the plugin abandons that transfer | the plugin's own code path |
| the SDK later writes into memory it has freed | **measured — page-heap fault in `VCRUNTIME140`** |
| in NINA that memory belongs to something else, so the heap corrupts | inferred, from the two dumps |

Only the last line is still inference, and it is the least surprising one: a write to freed
memory lands on whatever now owns it.

### Prerequisites, if the probe is ever re-run

A capture needs **all five** callbacks registered (`OBJCT_ADD`, `OBJCT_REQ_TRNSFER`,
`REC_CTRL_RELEASE`, `SHUTTER`, `ISO`) **and** `SetupFilesConfig_Set_Target(1 = PC)`. Without the
save target, `Rec_Ctrl_Release` returns 0 and no transfer event ever arrives — the first run
failed exactly that way. The probe writes that one camera setting and nothing else.

Several constants must be read from `Library/lumixcam.cs` rather than recalled: ONESHOT is
`0x03000011`, `SHUTTER` `0x02000030`, `ISO` `0x02000020`. `SS_Get_Param` is not an export at
all — the Tether spelling is `SS_GetParam`. `SelectPnPDevice` takes **seven** parameters, and
the device index must be read from the device buffer at offset 520 with a 262,144-byte buffer.
Check every entry point against the DLL's export table before running anything; four of these
were wrong on the first attempt and one of them crashed the probe before it did any work.

### The wedge is process-local

Recovery needs no power cycle. Closing the wedged process released everything, and NINA then
connected, ran live view and downloaded a frame immediately afterwards. The deadlock lives in
the DLL's per-process state, not in the camera: the PTP session drops when the process exits and
the body accepts a fresh one at once. The fix for a wedge is to restart the host process.

### For an upstream report

Panasonic have everything they need in the above: a one-command reproduction of the refused
transfer, a one-command reproduction of the deadlock with a matched control showing a single
lock fixes it, and a page-heap-verified write-after-free with the faulting module, exception
code and offset. The defect is that `LMX_func_api_Get_Object` can report failure while leaving
an internal transfer outstanding, and the DLL then completes that transfer into a buffer it has
already released.


## Two further defects, found by testing the fix, 2026-11-26

Both came out of verifying the plugin-manifest maintainer's review findings on the rig. Neither
is the heap corruption above; both were found by the same method — drive the thing faster than a
hand can, then read the ordering in the log.

### A reconnect could race the previous disconnect's teardown

`Disconnect()` returns as soon as it has *started* its native teardown, so that N.I.N.A.'s UI is
never blocked by an SDK call that may stall. It kept no handle on that teardown, so nothing could
wait for it.

By hand this is safe, but only by luck: the teardown takes about 600 ms, and the gap between two
clicks was 520 ms and 580 ms in two attempts, so it never overlapped. N.I.N.A.'s Advanced API
reconnects within a millisecond, and then it overlaps every time. One reconnect was enough:

| Time | Line |
| --- | --- |
| 11:44:48.3829 | `Disconnect: CloseSession` — the old teardown |
| 11:44:48.3884 | `Connect: transfer buffer capacity ...` — the new session is already open |
| 11:44:48.3885 | `Connect: Connected=True` |
| 11:44:48.4022 | `Disconnect: CloseSession done; CloseDevice` |
| 11:44:48.5334 | `Disconnect: CloseDevice done` |

The old teardown closes the session and the device *after* the new `Connect` has opened them.
N.I.N.A. then reports the camera connected on a session that no longer exists, and the next
exposure dies with no driver log line at all — it surfaces only as N.I.N.A.'s own "did not set
image as ready" timeout, 2.1 s into a 3 s exposure. Any such timeout that followed a quick
reconnect is a candidate for this cause.

Nothing crashed. The SDK gate still serialises the calls, so they never overlap inside the DLL:
the damage is ordering, not concurrency. That distinction matters, because it means the gate was
doing its job and the defect was above it.

A control ruled out the alternative explanation before any of this was attributed to the race: a
clean reconnect, with the teardown allowed to finish, followed by the identical API capture,
produced a normal frame — bulb open, `Get_Object ret=1`, `magic=49 49 55 00`, saved.

`Disconnect` now keeps its teardown task in a static field — the SDK has one process-wide
session, so a teardown left by one driver instance must hold up the next instance's connect — and
`Connect` waits for it, bounded at 15 s, before touching the SDK. Verified: four zero-gap
reconnects, waits of 357, 501, 480 and 483 ms logged, `CloseDevice` finishing before Connect's
first native call in every case, and all four following exposures saved.

One variant remains unreachable on this rig: live view *running* when a zero-gap reconnect lands.
The Advanced API has no live-view endpoint, so the only thing fast enough to hit the race cannot
start live view. The teardown stops live view as its first step and the new wait covers the whole
teardown, so that path sits inside the window the fix protects — but that is read from the code,
not measured.

### Card state is cached for the life of the process, and not by this plugin

With the card pulled while N.I.N.A. was running, `Ext_TryGetFreeSpaceInImages` and
`Ext_TryGetStatusOfCards` kept returning the card-present answer: status `0x00010002` and the
same 1404 frames as when the card was in. A full reconnect did not refresh it, although that
closes and reopens both the device and the session. Restarting N.I.N.A. did: the same empty slot
then read status `0x00000002` and `0xFFFFFFFF`, and the no-card warning fired correctly.

So the staleness is in the Tether DLL at process scope. There is nothing to fix in the plugin — a
card removed or inserted mid-session cannot be noticed — which is why `DescribeCardForFailure()`
is worded "was last reported" and must stay in that tense.

This also cost the attempt to provoke a failed frame. With the body genuinely reporting no card
and the save target on PC+SD, the GH5S still delivers the frame to the PC: 18,667,520 bytes,
`magic=49 49 55 00`, saved. The only observable difference is `[LumixBulb] open (0x12) ret=2`
rather than `ret=1`, on both card-out frames, and the capture proceeds regardless.

**So the `FailExposure` path is unexercised on this hardware.** Card removal does not make this
body offer an object with no data, and the other trigger — an object that is not a usable image —
needs the SDK collision that the gate now prevents by design. Both are covered statically only.
That is a gap in the evidence, stated rather than papered over: if a body exists that announces a
placeholder object or refuses a cardless transfer, the path wants testing there.

#### Why the empty-object guard could not be provoked at all

Two save-target configurations were tried on a GH5S with the slot genuinely empty, the body
reporting status 0x00000002 and 0xFFFFFFFF both times.

| Save target | What the camera did |
| --- | --- |
| PC+SD | delivered the frame to the PC regardless: 18,667,520 bytes, magic 49 49 55 00, saved |
| SD only | raised NO object event at all; the exposure ended on N.I.N.A.s own exposure+20s timeout, with no [LumixCapture] line in the log |

The only difference an absent card made to a capture was bulb open returning ret=2 instead of
ret=1, on every card-out frame, with the capture proceeding normally.

So an absent card does not produce an object that holds no data. The guards own comment records
the condition that did: a FULL card, where the write has somewhere to go and nothing fits. That
cannot be arranged on this rig without a small or nearly-full card in the body.

A refusal at exposure time was considered and rejected. The plugin does know at connect that the
slot is empty and the save target writes to it - that is what the existing warning says. But the
card reading is cached at process scope, as measured above, so the belief can be stale in BOTH
directions: a card inserted mid-session would still read as absent, and a hard refusal would then
block valid exposures. Waiting 20s for a frame that will not come is the lesser failure. The
warning plus N.I.N.A.s timeout is the behaviour that stays.

### What needs a restart of N.I.N.A., and what does not

Three settings look alike and are not, which cost time on 2026-11-26. Measured, not assumed:

| Setting | Takes effect on | Evidence |
| --- | --- | --- |
| Save target (PC / SD / PC+SD) | a RECONNECT | settled by the OBJECT HANDLE, not by whether an image arrives. One process: 13 frames on target 1 all raised 0x12345678, the cardless PC transfer handle; the next frame after a reconnect applied target 0 raised real card objects (0x11434000, 0x19434EA2) instead. The camera changed behaviour with no restart |
| Extended vs standard mode | a RESTART | the mode is bound once, in the providers constructor, and the first P/Invoke caches the resolved native handle for the whole assembly, with no public API to drop it |
| What the camera reports about the card | believe it only on the FIRST connect of the process | after a session is reopened the reading is intermittently wrong, and wrong in more than one way - see the section below. The free count itself is live |

The save target is written in Connect, and the camera acts on it from that point. Getting to
that answer took two wrong turns worth recording, because both are easy to repeat.

The first was citing a frame taken with NO CARD in the body as confirmation that PC+SD had been
applied. With an empty slot, PC+SD and PC-only behave identically, so it confirmed nothing.

The second was the test designed to settle it: set SD only, reconnect, and expect NO image to
reach N.I.N.A. if the camera honoured it. That assumption was wrong. With SD only the camera
writes to the card and raises OBJCT_ADD, and the driver pulls that object and hands it to
N.I.N.A. just the same - so an image arrives either way and the test cannot discriminate.

What does discriminate is the OBJECT HANDLE in the [LumixCapture] line. A PC transfer is always
0x12345678, LMX_DEF_OBJ_CARDLESS_TRNSFER_HDL. A card object is a real handle. Read that, not
whether a frame appeared.

The other two rows are process-scoped and cannot be changed while N.I.N.A. runs. The plugin now warns at connect when
the mode setting and the loaded mode disagree, because that one used to present as a 120s
exposure timing out with nothing in the log to explain it.

### The placeholder object, caught live

The SD-only frame above also produced the first hardware sighting of the object the
unusable-image guard exists for:

    13:46:56.6114  object=0x11434000 format=0x3 size=131072
    13:47:03.6045  Get_Object ret=0 err=0; magic=49 49 55 00
    13:47:03.6050  WARNING  object 0x11434000 is not a usable image (ret=0, format=0x3,
                            size=131072) - ignoring, waiting for the real frame
    13:47:04.1992  object=0x19434EA2 format=0x2 size=18678272
    13:47:04.9731  Get_Object ret=1 err=0; magic=49 49 55 00   -> saved

A 128 KB format-0x3 object, exactly as the code comment describes, ignored, with the real frame
arriving 0.6 s later and saving normally. The non-terminal branch of the bounded wait therefore
works on hardware; the terminal branch, failing the frame, still has not fired here.

It also shows why the header check cannot stand alone. The magic read 49 49 55 00 - a VALID RW2
header - because Get_Object wrote nothing and the shared pinned buffer still held the previous
frame's bytes. Only ret=0 caught it. Code that trusted the header would have handed N.I.N.A. the
previous frame a second time.

That Get_Object took 7.0 s to return 0. Noted, not explained, and deliberately NOT attributed to
the collision documented earlier in this file: same symptom, different trigger, no evidence
connecting them.

### The card reading cannot be believed after a reconnect

This began as "the reading is cached at process scope", on the strength of one measurement. That
characterisation was wrong, and the fuller set of measurements is worth keeping because the wrong
version nearly shipped a bug.

All on one GH5S on 2026-11-26, with the card's physical state known at each point:

| Time | Connect kind | Card physically | Status word | Free frames | Verdict |
| --- | --- | --- | --- | --- | --- |
| 12:08:21 | reconnect | OUT | 0x10002 | 1404 | WRONG - stale present |
| 12:16:40 | first of process | OUT | 0x2 | 0xFFFFFFFF | correct |
| 14:09:36 | first of process | IN | 0x10002 | 1404 | correct |
| 14:15:38 | first of process | IN | 0x10002 | 1404 | correct |
| 14:20:27 | reconnect | IN | 0x2 | 0xFFFFFFFF | WRONG - reads as empty |
| 14:36:05 | first of process | IN | 0x10002 | 1403 | correct |
| 14:38:49 | reconnect | IN | 0x10002 | 1402 | correct |

Three things follow, and only the first was in the earlier version of this file.

**A reconnect reading is not trustworthy.** Every wrong reading came after a session was reopened,
and every first-connect reading was right.

**It is wrong in two different ways,** which rules out simple caching. At 12:08 it answered stale
present with a plausible count while the slot was empty. At 14:20 it answered 0x2 / 0xFFFFFFFF -
byte-identical to the genuine empty slot at 12:16 - while a card was in. So "there is no card" and
"this session cannot say" cannot be told apart.

**It is intermittent, not consistent.** The reconnect at 14:38:49 answered correctly. A signal that
is usually right and occasionally indistinguishably wrong is the worst kind to act on, and acting
on it is exactly what cost a build: a PC fallback gated on an apparently empty slot overrode a
perfectly good card at 14:20 and silently stopped writing to it.

**The free count itself is live.** It read 1404, then 1403 after one frame went to the card, then
1402 after another. The earlier claim that it was cached was wrong; what fails is the whole read,
not the freshness of the number.

### Falling back to PC when the card cannot be written

With the save target on SD only and an empty slot, the camera raises no object event at all, so
every exposure runs to N.I.N.A.'s own exposure+20s timeout and the session produces nothing. The
plugin now resolves the save target before setting it, and falls back to PC for that session when
the card demonstrably cannot be written.

Two limits, both of which come from the table above:

**Only on the first connect of the process.** Both places that act on an apparently empty slot are
gated on it - the fallback, and the pre-existing "there is no card in the camera" warning, which
was firing spuriously on reconnects before this branch existed. This costs the fallback nothing,
because the case it exists for is a card absent or full when N.I.N.A. starts, and that IS the first
connect.

**The stored setting is never changed.** The override lasts for the session, so the user's choice
survives, and the warning names the restart needed to use the card again.

Verified on a GH5S:

| Configuration | Result |
| --- | --- |
| Card out, SD only, first connect | fallback fired, target 1 forced, frame arrived via 0x12345678 and saved - where the same configuration previously produced no frame at all |
| Card in, SD only, first connect then a reconnect | no fallback either time, target 0 both times, both frames via real card objects (0x19434EA4, 0x19434EA5) |

One earlier observation is refined here too. `[LumixBulb] open` returning ret=2 rather than ret=1
was noted as tracking card absence. It does not: ret=2 appeared only on the two PC+SD frames taken
with no card, while SD-only-with-no-card and the PC fallback both gave ret=1. It looks like "the
target includes the card and the card is unusable" - two samples, so a correlation and not a law.
