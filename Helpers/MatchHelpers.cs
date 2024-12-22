using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace BubbetsItems.Helpers
{
    public static class MatchHelpers
    {
        public static bool MatchNearbyDamage(this ILCursor c, out int masterLdlocIndex, out int damageLdlocIndex)
        {
            masterLdlocIndex = 0;
            damageLdlocIndex = 0;

            var found = true;
            var masterNum = -1;
            found &= c.TryGotoNext(MoveType.After, x => x.OpCode == OpCodes.Ldsfld && (x.Operand as FieldReference)?.Name == nameof(RoR2Content.Items.NearbyDamageBonus));
            var where = c.Index;
            found &= c.TryGotoPrev(x => x.MatchLdloc(out masterNum));
            if (!found) return found;
            c.Index = where;
            var num2 = -1;
            found &= c.TryGotoNext(x => x.MatchLdloc(out num2),
                x => x.MatchLdcR4(1f),
                x => x.MatchLdloc(out _));
            
            if (!found) return found;
            c.Index = where;
            masterLdlocIndex = masterNum;
            damageLdlocIndex = num2;
            return found;
        }
    }
}