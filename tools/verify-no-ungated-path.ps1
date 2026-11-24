# Completeness check: does ANY method in the assembly call into Lmxptpif.dll without holding
# the gate?
# =========================================================================================
# The previous check proved the 32 methods I listed are gated. That is circular - it only
# verifies the list. This one works the other way round:
#
#   1. find every P/Invoke declared in the assembly that binds to Lmxptpif.dll
#   2. find every method anywhere in the assembly whose IL calls one of them
#   3. require each of those callers to contain a Monitor.Enter
#
# A caller without one is a path to the native DLL that the fix does not cover. That
# includes the Pub_* standard-SDK path in lumixcam.cs, which the extended path falls back
# to and which I did not enumerate by hand.
param(
    [string]$Dll = 'C:\Users\rober\source\repos\LumixPlugin\bin\Release\net8.0-windows\LumixPlugin.dll',
    [string]$NinaDir = "C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy"
)
$ErrorActionPreference = 'Stop'

$resolver = {
    param($sender, $e)
    $n = ($e.Name -split ',')[0]
    $c = Join-Path $NinaDir ($n + '.dll')
    if (Test-Path $c) { return [System.Reflection.Assembly]::LoadFrom($c) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

$cs = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

public class NoUngated {
    public static List<string> PInvokes = new List<string>();
    public static List<string> Rows = new List<string>();

    public static void Run(Assembly asm) {
        BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // 1. every P/Invoke bound to the Lumix native DLL
        HashSet<string> native = new HashSet<string>();
        foreach (Type t in asm.GetTypes()) {
            MethodInfo[] ms;
            try { ms = t.GetMethods(f); } catch (Exception) { continue; }
            foreach (MethodInfo mi in ms) {
                if ((mi.Attributes & MethodAttributes.PinvokeImpl) == 0) { continue; }
                DllImportAttribute di = null;
                try {
                    object[] a = mi.GetCustomAttributes(typeof(DllImportAttribute), false);
                    if (a.Length > 0) { di = (DllImportAttribute)a[0]; }
                } catch (Exception) { }
                string lib = di != null ? di.Value : "(unknown)";
                if (lib != null && lib.IndexOf("Lmxptpif", StringComparison.OrdinalIgnoreCase) >= 0) {
                    string key = t.FullName + "::" + mi.Name;
                    native.Add(key);
                    PInvokes.Add(key + "   [" + lib + "]");
                }
            }
        }

        // 2. every method whose IL calls one of them, and 3. whether it enters a monitor.
        // Constructors are included: the provider's .ctor calls LMX_func_api_Init, and an
        // earlier version of this scan used GetMethods only and missed it.
        foreach (Type t in asm.GetTypes()) {
            List<MethodBase> ms = new List<MethodBase>();
            try {
                foreach (MethodInfo x in t.GetMethods(f)) { ms.Add(x); }
                foreach (ConstructorInfo x in t.GetConstructors(f)) { ms.Add(x); }
            } catch (Exception) { continue; }
            foreach (MethodBase mi in ms) {
                MethodBody body = null;
                try { body = mi.GetMethodBody(); } catch (Exception) { }
                if (body == null) { continue; }
                byte[] il;
                try { il = body.GetILAsByteArray(); } catch (Exception) { continue; }
                if (il == null) { continue; }

                bool enter = false;
                bool tryEnter = false;
                List<string> hits = new List<string>();
                for (int i = 0; i + 4 < il.Length; i++) {
                    if (il[i] != 0x28 && il[i] != 0x6F) { continue; }
                    MethodBase mb = null;
                    try { mb = t.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)); }
                    catch (Exception) { continue; }
                    if (mb == null || mb.DeclaringType == null) { continue; }
                    if (mb.DeclaringType.FullName == "System.Threading.Monitor") {
                        // TryEnter counts as gated-with-a-timeout, and is reported separately:
                        // one entry point deliberately uses it to avoid a teardown deadlock.
                        if (mb.Name == "Enter")    { enter = true; continue; }
                        if (mb.Name == "TryEnter") { tryEnter = true; continue; }
                    }
                    string key = mb.DeclaringType.FullName + "::" + mb.Name;
                    if (native.Contains(key) && !hits.Contains(key)) { hits.Add(key); }
                }
                if (hits.Count > 0) {
                    // Name the callees, short form, so an ungated caller can be fixed directly.
                    List<string> shortNames = new List<string>();
                    foreach (string h in hits) {
                        int p = h.IndexOf("::");
                        shortNames.Add(p >= 0 ? h.Substring(p + 2) : h);
                    }
                    shortNames.Sort();
                    string state = enter ? "GATED  " : (tryEnter ? "TRYGATE" : "UNGATED");
                    Rows.Add(state + "|" +
                             t.Name + "." + mi.Name + "|" + hits.Count + "|" +
                             string.Join(" ", shortNames.ToArray()));
                }
            }
        }
    }
}
'@
Add-Type -TypeDefinition $cs -Language CSharp

$asm = [System.Reflection.Assembly]::LoadFrom($Dll)
('assembly : ' + (Get-Item $Dll).Length + ' bytes')
[NoUngated]::Run($asm)

''
('P/Invokes bound to Lmxptpif.dll : ' + [NoUngated]::PInvokes.Count)
''
'--- every method that calls one of them ---'
$ungated = @()
$tryGate = @()
$gated = 0
foreach ($row in [NoUngated]::Rows) {
    $p = $row -split '\|'
    switch ($p[0]) {
        'UNGATED' { ('{0}  {1,-44} -> {2}' -f $p[0], $p[1], $p[3]); $ungated += ($p[1] + '  ->  ' + $p[3]) }
        'TRYGATE' { ('{0}  {1,-44} -> {2}' -f $p[0], $p[1], $p[3]); $tryGate += ($p[1] + '  ->  ' + $p[3]) }
        default   { $gated++ }
    }
}
('(' + $gated + ' callers with an unconditional Monitor.Enter, not listed)')

''
('callers with Monitor.Enter    : ' + $gated)
('callers with Monitor.TryEnter : ' + $tryGate.Count)
if ($tryGate.Count) { $tryGate | ForEach-Object { '    ' + $_ } }
('callers with NO monitor       : ' + $ungated.Count)
if ($ungated.Count) { $ungated | ForEach-Object { '    ' + $_ } }
''
if ($ungated.Count -eq 0) {
    'CONFIRMED: no method in the assembly reaches Lmxptpif.dll without taking the gate.'
    if ($tryGate.Count) {
        ('NOTE: ' + $tryGate.Count + ' use TryEnter with a timeout rather than blocking. That is')
        'deliberate where blocking could deadlock against the SDK''s own callback thread, but it'
        'means those calls are serialised only when the gate happens to be free.'
    }
} else {
    'HOLE FOUND: the methods above call the native DLL with no monitor. The fix is incomplete.'
    exit 1
}
