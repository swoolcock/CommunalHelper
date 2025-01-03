using MonoMod.Utils;

namespace Celeste.Mod.CommunalHelper.Entities.StrawberryJam;

[TrackedAs(typeof(CassetteListener), false)]
public class TickingCassetteListener : CassetteListener
{
    // "new" keyword is in case this functionality gets added to the base class in Everest
    public new Action<CassetteBlockManager> OnTick;

    public TickingCassetteListener(int index, float tempo = 1) : base(index, tempo)
    {
        Active = true;
    }

    public override void Update()
    {
        if (SceneAs<Level>()?.Tracker.GetEntity<CassetteBlockManager>() is not { } cassetteBlockManager) return;
        var data = DynamicData.For(cassetteBlockManager);
        int beatIndex = data.Get<int>("beatIndex");
        int beatsPerTick = data.Get<int>("beatsPerTick");
        int ticksPerSwap = data.Get<int>("ticksPerSwap");
        if (beatIndex % beatsPerTick == 0 &&
            beatIndex % (beatsPerTick * ticksPerSwap) != 0) {
            OnTick?.Invoke(cassetteBlockManager);
        }
    }
}
