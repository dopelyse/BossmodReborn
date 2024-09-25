﻿namespace BossMod.Endwalker.VariantCriterion.C01ASS.C013Shadowcaster;

static class Portals
{
    private const float _portalLength = 10;

    // returns null if this is not arrow appear eanim
    public static WPos? DestinationForEAnim(Actor actor, uint state)
    {
        if ((OID)actor.OID != OID.Portal)
            return null;

        var rotation = state switch
        {
            0x00400080 => -90, // CW arrows appear
            0x01000200 => +90, // CCW arrows appear
            _ => 0, // other known: 0x04000800 = CW arrows end, 0x10002000 = CCW arrows end, 0x00100020 = arrows disappear, 0x00040008 = disappear
        };
        if (rotation == 0)
            return null;

        return actor.Position + _portalLength * (actor.Rotation + rotation.Degrees()).ToDirection();
    }
}

class PortalsAOE(BossModule module, AID aid, OID movedOID, float activationDelay, AOEShape shape) : Components.GenericAOEs(module, ActionID.MakeSpell(aid))
{
    private readonly IReadOnlyList<Actor> _movedActors = module.Enemies(movedOID);
    private readonly float _activationDelay = activationDelay;
    private readonly AOEShape _shape = shape;
    private readonly List<(WPos pos, Angle rot, DateTime activation)> _origins = [];

    public override IEnumerable<AOEInstance> ActiveAOEs(int slot, Actor actor) => _origins.Select(o => new AOEInstance(_shape, o.pos, o.rot, o.activation));

    public override void OnActorEAnim(Actor actor, uint state)
    {
        var dest = Portals.DestinationForEAnim(actor, state);
        if (dest == null)
            return;

        var movedActor = _movedActors.FirstOrDefault(a => a.Position.AlmostEqual(actor.Position, 1));
        if (movedActor != null)
            _origins.Add((dest.Value, movedActor.Rotation, WorldState.FutureTime(_activationDelay)));
    }
}

abstract class PortalsBurn(BossModule module, AID aid, OID oid) : PortalsAOE(module, aid, oid, 11.6f, new AOEShapeCircle(12));
class NPortalsBurn(BossModule module) : PortalsBurn(module, AID.NBurn, OID.NBallOfFire);
class SPortalsBurn(BossModule module) : PortalsBurn(module, AID.SBurn, OID.SBallOfFire);

abstract class PortalsMirror(BossModule module, AID aid, OID oid) : PortalsAOE(module, aid, oid, 11.7f, new AOEShapeRect(100, 5, 100));
class NPortalsMirror(BossModule module) : PortalsMirror(module, AID.NBlazingBenifice, OID.NArcaneFont);
class SPortalsMirror(BossModule module) : PortalsMirror(module, AID.SBlazingBenifice, OID.SArcaneFont);

class PortalsWave(BossModule module) : BossComponent(module)
{
    public bool Done { get; private set; }
    private readonly List<(WPos n, WPos s)> _portals = [];
    private readonly int[] _playerPortals = new int[PartyState.MaxPartySize]; // 0 = unassigned, otherwise 'z direction sign' (-1 if own portal points N, +1 for S)

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var dir = _playerPortals[pcSlot];
        if (dir != 0)
        {
            foreach (var p in _portals)
            {
                Arena.AddCircle(dir > 0 ? p.s : p.n, 1, Colors.Safe, 2);
            }
        }
    }

    public override void OnStatusGain(Actor actor, ActorStatus status)
    {
        if ((SID)status.ID == SID.PlayerPortal)
        {
            var slot = Raid.FindSlot(actor.InstanceID);
            if (slot >= 0)
            {
                _playerPortals[slot] = status.Extra switch
                {
                    0x1CD or 0x1CE => -1,
                    0x1D2 or 0x1D3 => +1,
                    _ => 0
                };
            }
        }
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if ((OID)actor.OID == OID.Portal && state == 0x00100020)
        {
            Done = true;
            return;
        }

        var dest = Portals.DestinationForEAnim(actor, state);
        if (dest == null || !Module.InBounds(dest.Value))
            return;

        var n = actor.Position;
        var s = dest.Value;
        if (n.Z > s.Z)
            Utils.Swap(ref n, ref s);
        _portals.Add((n, s));
    }
}
