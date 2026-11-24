# The IL scan said "all 32 load the same gate" but printed the field name as "L", which is a
# single character out of a string that got indexed as a char array. The COUNT was right, the
# DISPLAY was not - and "one shared monitor" is the whole point of the fix, so it gets checked
# directly here rather than inferred from a mangled line.
#
# For every gated method, list the exact static fields its IL loads whose name mentions "gate".
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

public class GateField {
    /// Returns "methodName|Declaring.Field[,Declaring.Field]" for each method that contains a
    /// Monitor.Enter, so the caller can see the actual field identity rather than a summary.
    public static List<string> Scan(Type t, string[] names) {
        List<string> rows = new List<string>();
        BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        foreach (string name in names) {
            MethodInfo mi = null;
            foreach (MethodInfo c in t.GetMethods(f)) { if (c.Name == name) { mi = c; break; } }
            if (mi == null) { rows.Add(name + "|<<NOT FOUND>>"); continue; }
            MethodBody body = mi.GetMethodBody();
            if (body == null) { rows.Add(name + "|<<NO BODY>>"); continue; }
            byte[] il = body.GetILAsByteArray();
            Module mod = t.Module;
            bool enter = false;
            List<string> flds = new List<string>();
            for (int i = 0; i < il.Length - 4; i++) {
                if (il[i] == 0x28) {
                    try {
                        MethodBase mb = mod.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                        if (mb != null && mb.DeclaringType != null &&
                            mb.DeclaringType.FullName == "System.Threading.Monitor" &&
                            mb.Name == "Enter") { enter = true; }
                    } catch (Exception) { }
                } else if (il[i] == 0x7E) {
                    try {
                        FieldInfo fi = mod.ResolveField(BitConverter.ToInt32(il, i + 1));
                        if (fi != null && fi.IsStatic && !fi.FieldType.IsValueType) {
                            string s = fi.DeclaringType.Name + "." + fi.Name;
                            if (!flds.Contains(s)) { flds.Add(s); }
                        }
                    } catch (Exception) { }
                }
            }
            rows.Add(name + "|" + (enter ? "ENTER" : "no-enter") + "|" + string.Join(",", flds.ToArray()));
        }
        return rows;
    }
}
'@
Add-Type -TypeDefinition $cs -Language CSharp

$names = @(
    'EnsureTetherBuffers','TryReadTetherWord',
    'LMX_func_api_SS_Get_Param','LMX_func_api_SS_Set_Param',
    'LMX_func_api_ISO_Get_Param','LMX_func_api_ISO_Set_Param',
    'LMX_func_api_Rec_Ctrl_Release',
    'LMX_func_api_Get_Object','LMX_func_api_Get_Object_FormatType',
    'LMX_func_api_Get_Object_DataSize','LMX_func_api_Get_Object_FileName',
    'LMX_func_api_Skip_Object_Transfer',
    'LMX_func_api_Ctrl_LiveView_Start','LMX_func_api_Ctrl_LiveView_Stop',
    'LMX_func_api_Get_LiveView_data',
    'LMX_func_api_SS_Get_Capability','LMX_func_api_ISO_Get_Capability',
    'LMX_func_api_CameraMode_Get_Mode_Pos','LMX_func_api_CameraMode_Get_Capability',
    'Ext_SetImageQuality','Ext_GetImageQuality','Ext_TryGetBatteryInfo',
    'LMX_func_api_SetupFilesConfig_Get_Target','LMX_func_api_SetupFilesConfig_Set_Target',
    'Ext_TryGetFreeSpaceInImages','Ext_TryGetStatusOfCards',
    'Ext_Enumerate','Ext_Connect','Ext_Disconnect',
    'Ext_CloseSessionOnly','Ext_CloseDeviceOnly','Ext_EnsureBulb'
)

$asm = [System.Reflection.Assembly]::LoadFrom($Dll)
$t = $asm.GetType('LumixWrapper.LumixCam', $true)
('assembly : ' + (Get-Item $Dll).Length + ' bytes')
''

$rows = [GateField]::Scan($t, $names)
$gateSet = [System.Collections.Generic.HashSet[string]]::new()
$noEnter = @()
foreach ($row in $rows) {
    $parts = $row -split '\|'
    $m = $parts[0]; $e = $parts[1]; $f = if ($parts.Count -gt 2) { $parts[2] } else { '' }
    $gates = @($f -split ',' | Where-Object { $_ -match 'Gate' })
    foreach ($g in $gates) { [void]$gateSet.Add($g) }
    ('{0,-42} {1,-9} {2}' -f $m, $e, ($gates -join ' '))
    if ($e -ne 'ENTER') { $noEnter += $m }
}
''
('methods scanned        : ' + $rows.Count)
('without Monitor.Enter  : ' + $noEnter.Count + $(if ($noEnter.Count) { ' -> ' + ($noEnter -join ', ') } else { '' }))
('distinct gate fields   : ' + $gateSet.Count)
foreach ($g in $gateSet) { ('    ' + $g) }
''
if ($noEnter.Count -eq 0 -and $gateSet.Count -eq 1) {
    'CONFIRMED: every entry point enters a monitor, and all of them on exactly one field.'
} else {
    'NOT CONFIRMED - read the table above.'
    exit 1
}
