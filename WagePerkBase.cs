namespace JacksonPerks;

// 特性基类（统一生命周期 + IsActive 判定）
internal abstract class WagePerkBase
{
    public abstract string PerkId { get; }
    public abstract int Cost { get; }

    public virtual void OnDayStart() { }
    public virtual void OnGameLoaded() { }
    public virtual void OnNewRun() { }
    public virtual void OnSaveGame() { }

    public virtual bool IsActive()
    {
        return PerkStatePersistence.GetInt(PerkId, "active", 0) > 0;
    }
}