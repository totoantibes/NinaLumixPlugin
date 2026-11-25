# The heap corruption, explained: an orphaned transfer into a freed buffer

Written 2026-11-25 from the two crash dumps of 24 November. They are the only dumps ever
captured of this fault; this document exists so the dumps themselves can be deleted. The
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
| copy length (`r14` = `r15`) | 18,890,240 | 18,623,488 |
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
| frame reported | 02:05:51.639 | 09:53:38.887 |
| plugin's `Get_Object` returned | 02:05:51.768, `ret=0` | 09:53:39.021, `ret=1` |
| the SDK performed the copy | 02:08:13 | 09:55:01 |
| **delay** | **141 s** | **82 s** |

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

Worth reporting upstream to Panasonic as a use-after-free in the Tether SDK's transfer path.
