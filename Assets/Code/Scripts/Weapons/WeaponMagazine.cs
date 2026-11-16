using System;

// Ammo is independent of visuals. A reload can transfer ammunition only once.
public sealed class WeaponMagazine
{
    public int Capacity { get; }
    public int Rounds { get; private set; }
    public int Reserve { get; private set; }
    public bool IsReloading { get; private set; }
    public bool ReloadCommitted { get; private set; }
    public int ReloadOperationId { get; private set; }
    private int operationSerial;
    public WeaponMagazine(int capacity, int reserve)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (reserve < 0) throw new ArgumentOutOfRangeException(nameof(reserve));
        Capacity = Rounds = capacity;
        Reserve = reserve;
    }
    public bool TryFire()
    {
        if (IsReloading || Rounds == 0) return false;
        Rounds--;
        return true;
    }
    public bool TryBeginReload()
    {
        if (IsReloading || Rounds >= Capacity || Reserve <= 0) return false;
        IsReloading = true;
        ReloadCommitted = false;
        operationSerial = unchecked(operationSerial + 1);
        if (operationSerial == 0) operationSerial = 1;
        ReloadOperationId = operationSerial;
        return true;
    }
    public bool AdvanceReload(float normalizedTime, float commitAt)
    {
        if (!IsReloading || ReloadCommitted || float.IsNaN(normalizedTime) || normalizedTime < commitAt)
            return false;
        int loaded = Math.Min(Capacity - Rounds, Reserve);
        Rounds += loaded;
        Reserve -= loaded;
        ReloadCommitted = true;
        return true;
    }
    public bool ApplyInsertionEvent(int operationId)
    {
        if (operationId == 0 || operationId != ReloadOperationId) return false;
        return AdvanceReload(1f, 1f);
    }
    public void EndReload() { IsReloading = false; ReloadOperationId = 0; }
    public void CancelReload() { IsReloading = false; ReloadOperationId = 0; }
}
