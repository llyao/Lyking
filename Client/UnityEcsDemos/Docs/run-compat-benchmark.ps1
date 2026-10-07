param(
    [int]$EntityCount = 10000,
    [int]$WarmupFrames = 30,
    [int]$SampleFrames = 120
)

$ErrorActionPreference = 'Stop'

if ($EntityCount -lt 0) { throw 'EntityCount 必须是非负数。' }
if ($WarmupFrames -lt 0) { throw 'WarmupFrames 不能为负数。' }
if ($SampleFrames -le 0) { throw 'SampleFrames 必须是正数。' }

$root = Split-Path -Parent $PSScriptRoot
$sourceFiles = @(
    (Join-Path $root 'Assets/Scripts/Benchmark/BenchmarkConfig.cs'),
    (Join-Path $root 'Assets/Scripts/Benchmark/BenchmarkEntity.cs'),
    (Join-Path $root 'Assets/Scripts/Benchmark/ISimulationBackend.cs'),
    (Join-Path $root 'Assets/Scripts/Backends/GameObjectBackend.cs'),
    (Join-Path $root 'Assets/Scripts/Backends/DotsEntitiesBackend.cs'),
    (Join-Path $root 'Assets/Scripts/Backends/DotsJobsBackend.cs'),
    (Join-Path $root 'Assets/Scripts/Backends/EntitasStyleBackend.cs'),
    (Join-Path $root 'Assets/Scripts/Backends/HydraVetBrainBackend.cs'),
    (Join-Path $root 'Assets/Scripts/Backends/UnityMassBackend.cs')
)

Add-Type -Path $sourceFiles
Add-Type -TypeDefinition @"
public static class LykingCompatBenchmarkHelpers
{
    public static void SetEntityCount(object config, int count)
    {
        config.GetType().GetField("entityCount").SetValue(config, count);
    }

    public static void Validate(object config)
    {
        config.GetType().GetMethod("Validate").Invoke(config, null);
    }

    public static float FixedStep(object config)
    {
        return (float)config.GetType().GetProperty("FixedStep").GetValue(config);
    }
}
"@

$config = [Lyking.UnityEcsDemos.Benchmark.BenchmarkConfig]::CreateDefault()
[LykingCompatBenchmarkHelpers]::SetEntityCount($config, $EntityCount)
[LykingCompatBenchmarkHelpers]::Validate($config)
$fixedStep = [LykingCompatBenchmarkHelpers]::FixedStep($config)

foreach ($typeName in @(
    'GameObjectBackend',
    'DotsEntitiesBackend',
    'DotsJobsBackend',
    'EntitasStyleBackend',
    'HydraVetBrainBackend',
    'UnityMassBackend'
)) {
    $type = [Type]::GetType("Lyking.UnityEcsDemos.Backends.$typeName, DynamicAssembly")
    if ($null -eq $type) {
        $type = [AppDomain]::CurrentDomain.GetAssemblies() |
            ForEach-Object { $_.GetType("Lyking.UnityEcsDemos.Backends.$typeName", $false) } |
            Where-Object { $null -ne $_ } |
            Select-Object -First 1
    }

    $backend = [Activator]::CreateInstance($type)
    $backend.Initialize($config)
    for ($frame = 0; $frame -lt $WarmupFrames; $frame++) {
        $backend.Step($fixedStep)
    }

    $watch = [Diagnostics.Stopwatch]::StartNew()
    for ($frame = 0; $frame -lt $SampleFrames; $frame++) {
        $backend.Step($fixedStep)
    }
    $watch.Stop()
    $average = $watch.Elapsed.TotalMilliseconds / [double]$SampleFrames
    "{0,-18} 状态={1,-28} 实体={2,6} 平均更新={3,8:0.000} ms" -f $backend.BackendName, $backend.PackageStatus, $backend.EntityCount, $average
    $backend.Dispose()
}
