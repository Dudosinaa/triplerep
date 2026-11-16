$ErrorActionPreference = 'Stop'
$weaponRoot = Split-Path -Parent $PSScriptRoot
$weaponSource = Join-Path $weaponRoot 'Assets\Code\Scripts\Weapons\WeaponMagazine.cs'
$weaponTemp = Join-Path $weaponRoot 'Temp\WeaponValidation'
New-Item -ItemType Directory -Path $weaponTemp -Force | Out-Null
$env:TEMP = $weaponTemp
$env:TMP = $weaponTemp
Add-Type -TypeDefinition (Get-Content -LiteralPath $weaponSource -Raw)
$script:checks = 0
function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
function Ammo($weapon, [int]$rounds, [int]$reserve, [string]$label) {
    Check ($weapon.Rounds -eq $rounds -and $weapon.Reserve -eq $reserve) $label
}
$m = [WeaponMagazine]::new(8,32)
Ammo $m 8 32 'Initial ammunition'
Check (-not $m.TryBeginReload()) 'Full magazine rejects reload'
1..3 | ForEach-Object { Check ($m.TryFire()) 'Can fire' }
Ammo $m 5 32 'Three shots consumed'
Check ($m.TryBeginReload()) 'Partial reload begins'
Check (-not $m.TryBeginReload()) 'Repeated reload request rejected'
Check (-not $m.TryFire()) 'No shot while reloading'
Check (-not $m.AdvanceReload(0.64,0.65)) 'No ammo before insertion'
Ammo $m 5 32 'Ammo unchanged before insertion'
Check ($m.AdvanceReload(0.65,0.65)) 'Ammo applied at insertion'
Ammo $m 8 29 'Partial reload transfers exact deficit'
Check (-not $m.AdvanceReload(1,0.65)) 'No second transfer at clip end'
$m.EndReload()
Check (-not $m.IsReloading) 'Normal reload unlocks weapon'
Check (-not $m.TryBeginReload()) 'Repeated R on full magazine does nothing'
[void]$m.TryFire()
[void]$m.TryBeginReload()
$m.CancelReload()
Ammo $m 7 29 'Cancel before insertion preserves ammo'
Check (-not $m.IsReloading) 'Cancel releases reload lock'
Check (-not $m.AdvanceReload(1,0.65)) 'Late progress after cancellation cannot transfer ammo'
Check ($m.TryBeginReload()) 'Reload can restart after cancellation'
Check ($m.AdvanceReload(0.8,0.65)) 'Restarted reload transfers once'
$m.CancelReload()
Ammo $m 8 28 'Cancel after insertion retains transferred ammunition'
Check ($m.TryFire()) 'Can fire after cancellation'
1..7 | ForEach-Object { [void]$m.TryFire() }
Ammo $m 0 28 'Empty magazine'
Check ($m.TryBeginReload()) 'Empty reload begins'
Check (-not $m.AdvanceReload(0.48,0.486)) 'Empty reload waits for insertion phase'
Check ($m.AdvanceReload(0.49,0.486)) 'Empty reload inserts magazine'
Ammo $m 8 20 'Empty reload transfers eight rounds'
Check (-not $m.AdvanceReload(3,0.486)) 'Repeated progress cannot duplicate ammo'
$m.EndReload()
$low = [WeaponMagazine]::new(8,2)
1..5 | ForEach-Object { [void]$low.TryFire() }
[void]$low.TryBeginReload()
[void]$low.AdvanceReload(1,0.65)
$low.EndReload()
Ammo $low 5 0 'Limited reserve never creates extra rounds'
Check (-not $low.TryBeginReload()) 'No reload with zero reserve'
$nan = [WeaponMagazine]::new(8,32)
[void]$nan.TryFire()
[void]$nan.TryBeginReload()
Check (-not $nan.AdvanceReload([single]::NaN,0.65)) 'Invalid animation progress cannot commit'
Ammo $nan 7 32 'NaN leaves ammo unchanged'
$nan.CancelReload()
1..1000 | ForEach-Object { [void]$nan.TryBeginReload(); $nan.CancelReload() }
Ammo $nan 7 32 'Repeated cancellation does not create ammunition'
Check (-not $nan.IsReloading) 'Repeated cancellation leaves weapon unlocked'
$events = [WeaponMagazine]::new(8,32)
[void]$events.TryFire()
[void]$events.TryBeginReload()
$oldId = $events.ReloadOperationId
Check ($oldId -ne 0) 'Reload has a nonzero event token'
Check (-not $events.ApplyInsertionEvent(0)) 'Zero event token rejected'
Check (-not $events.ApplyInsertionEvent($oldId + 1)) 'Foreign animation event rejected'
$events.CancelReload()
Check (-not $events.ApplyInsertionEvent($oldId)) 'Cancelled animation event rejected'
[void]$events.TryBeginReload()
$newId = $events.ReloadOperationId
Check ($newId -ne $oldId) 'Restart gets a fresh animation-event token'
Check (-not $events.ApplyInsertionEvent($oldId)) 'Old animation cannot commit a restarted reload'
Ammo $events 7 32 'Stale event leaves ammunition unchanged'
Check ($events.ApplyInsertionEvent($newId)) 'Current Hands insertion event transfers ammunition'
Ammo $events 8 31 'Insertion event transfers the exact deficit'
Check (-not $events.ApplyInsertionEvent($newId)) 'Duplicate animation event rejected'
$events.CancelReload()
Ammo $events 8 31 'Cancellation after event retains ammo without refund or duplication'
Check (-not $events.IsReloading) 'Cancellation after event clears reload lock'
Write-Output ('PASS: ' + $script:checks + ' ammo/reload/event assertions; actual WeaponMagazine.cs compiled in memory.')
