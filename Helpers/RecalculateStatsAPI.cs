using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace BubbetsItems.Helpers;

[HarmonyPatch]
public static class RecalculateStatsAPI
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class StatHookEventArgs
    {
        #region shield

        /// <summary>Added to base shield.</summary> <remarks>MAX_SHIELD ~ (BASE_SHIELD + baseShieldAdd + levelShieldAdd * <inheritdoc cref="_levelMultiplier"/>) * (SHIELD_MULT + shieldMultAdd)</remarks>remarks>
        public float baseShieldAdd = 0f;

        /// <summary>Multiplied by level and added to base shield.</summary> <inheritdoc cref="baseShieldAdd"/>
        public float levelShieldAdd = 0f;

        /// <summary>Added to the direct multiplier to shields.</summary> <inheritdoc cref="baseShieldAdd"/>
        public float shieldMultAdd = 0f;

        #endregion

        #region regen

        /// <summary>Added to base health regen.</summary> <remarks>HEALTH_REGEN ~ (BASE_REGEN + baseRegenAdd + levelRegenAdd * <inheritdoc cref="_levelMultiplier"/>) * (REGEN_MULT + regenMultAdd) * regenTotalMult</remarks>
        public float baseRegenAdd = 0f;

        /// <summary>Multiplied by level and added to base health regen.</summary> <inheritdoc cref="baseRegenAdd"/>
        public float levelRegenAdd = 0f;

        /// <summary>Added to the direct multiplier to base health regen.</summary> <inheritdoc cref="baseRegenAdd"/>
        public float regenMultAdd = 0f;

        /// <summary>Base health regen is multiplied by this number. Multiply this value by your multiplier.</summary> <inheritdoc cref="baseRegenAdd"/>
        public float regenTotalMult = 1f;

        #endregion

        #region moveSpeed

        /// <summary>Added to base move speed.</summary> <remarks>MOVE_SPEED ~ (BASE_MOVE_SPEED + baseMoveSpeedAdd + levelMoveSpeedAdd * <inheritdoc cref="_levelMultiplier"/>) * (MOVE_SPEED_MULT + moveSpeedMultAdd) / (MOVE_SPEED_REDUCTION_MULT + moveSpeedReductionMultAdd)</remarks>
        public float baseMoveSpeedAdd = 0f;

        /// <summary>Multiplied by level and added to base move speed.</summary> <inheritdoc cref="baseMoveSpeedAdd"/>
        public float levelMoveSpeedAdd = 0f;

        /// <summary>Added to the direct multiplier to move speed.</summary> <inheritdoc cref="baseMoveSpeedAdd"/>
        public float moveSpeedMultAdd = 0f;

        /// <summary>Added reduction multiplier to move speed.</summary> <inheritdoc cref="baseMoveSpeedAdd"/>
        public float moveSpeedReductionMultAdd = 0f;

        /// <summary>Added to the direct multiplier to sprinting speed.</summary> <remarks>SPRINT SPEED ~ MOVE_SPEED * (BASE_SPRINT_MULT + sprintSpeedAdd) </remarks>
        public float sprintSpeedAdd = 0f;

        /// <summary>Amount of Root effects currently applied.</summary> <remarks>MOVE_SPEED ~ (moveSpeedRootCount > 0) ? 0 : MOVE_SPEED</remarks>
        public int moveSpeedRootCount = 0;

        #endregion

        #region attackSpeed

        /// <summary>Added to attack speed.</summary> <remarks>ATTACK_SPEED ~ (BASE_ATTACK_SPEED + baseAttackSpeedAdd + levelAttackkSpeedAdd * <inheritdoc cref="_levelMultiplier"/>) * (ATTACK_SPEED_MULT + attackSpeedMultAdd) / (ATTACK_SPEED_REDUCTION_MULT + attackSpeedReductionMultAdd)</remarks>
        public float baseAttackSpeedAdd = 0f;

        /// <summary>Multiplied by level and added to attack speed.</summary> <inheritdoc cref="baseAttackSpeedAdd"/>
        public float levelAttackSpeedAdd = 0f;

        /// <summary>Added to the direct multiplier to attack speed.</summary> <inheritdoc cref="baseAttackSpeedAdd"/>
        public float attackSpeedMultAdd = 0f;

        /// <summary>Added reduction multiplier to attack speed.</summary> <inheritdoc cref="baseAttackSpeedAdd"/>
        public float attackSpeedReductionMultAdd = 0f;

        #endregion

        #region armor

        /// <summary>Added to armor.</summary> <remarks>ARMOR ~ BASE_ARMOR + armorAdd + levelArmorAdd * <inheritdoc cref="_levelMultiplier"/></remarks>
        public float armorAdd = 0f;

        /// <summary>Multiplied by level and added to armor.</summary> <inheritdoc cref="armorAdd"/>
        public float levelArmorAdd = 0f;

        #endregion

        #region barrier

        /// <summary>Set to TRUE to freeze barrier decay.</summary> <remarks>BARRIER_DECAY_RATE ~ (shouldFreezeBarrier == true) ? 0 : BARRIER_DECAY_RATE</remarks>
        public int shouldFreezeBarrier = 0;

        /// <summary>Multiply to increase or decrease barrier decay rate.</summary> <remarks>BARRIER_DECAY_RATE ~ (BASE_DECAY_RATE + barrierDecayAdd) * (barrierDecayMult). Cannot be less than 0.</remarks>
        public float barrierDecayMult = 1;

        /// <summary>ADD to increase or decrease barrier decay rate. Expressed as a rate per second.</summary> <inheritdoc cref="barrierDecayMult"/>
        public float barrierDecayAdd = 0;

        #endregion

        #region luck

        /// <summary>Add to increase or decrease Luck. Can be negative.</summary> <remarks>LUCK ~ (MASTER_LUCK + luckAdd).</remarks>
        public float luckAdd = 0;

        #endregion
    }

    public delegate void StatHookEventHandler(CharacterBody sender, StatHookEventArgs args);

    public static void R2ApiHandler(CharacterBody characterBody, object args)
    {
        _statMods = new StatHookEventArgs();

        foreach (var (key, value) in _r2StatHookEventArgsFields)
        {
            if (_statHookFields.TryGetValue(key, out var field))
                field.SetValue(_statMods, value.GetValue(args));
        }

        if (_getStatCoefficients == null) return;
        foreach (var @delegate in _getStatCoefficients.GetInvocationList())
        {
            var @event = (StatHookEventHandler)@delegate;
            try
            {
                @event(characterBody, _statMods);
            }
            catch (Exception e)
            {
                BubbetsItemsPlugin.Log.LogError(
                    $"Exception thrown by : {@event.Method.DeclaringType?.Name}.{@event.Method.Name}:\n{e}");
            }
        }

        foreach (var (key, value) in _statHookFields)
        {
            if (_r2StatHookEventArgsFields.TryGetValue(key, out var field))
            {
                if (field.Name ==
                    nameof(StatHookEventArgs.shouldFreezeBarrier)) // Its dumb this isn't a int to begin with.
                {
                    field.SetValue(args, (int)value.GetValue(_statMods) > 0);
                }
                else
                    field.SetValue(args, value.GetValue(_statMods));
            }
        }
    }

    public static event StatHookEventHandler GetStatCoefficients
    {
        add
        {
            if (!_r2Hooked &&
                Chainloader.PluginInfos.TryGetValue(BubbetsItemsPlugin.RecalcStatsGuid, out var pluginInfo))
            {
                var pluginType = pluginInfo.Instance.GetType();
                var assembly = Assembly.GetAssembly(pluginType);
                var recalc = assembly.GetType(pluginType.Namespace + "." + nameof(RecalculateStatsAPI));
                _recalcStatsEvent = recalc?.GetEvent(nameof(GetStatCoefficients));
                if (_recalcStatsEvent != null)
                {
                    var handlerType = _recalcStatsEvent.EventHandlerType;
                    _r2StatHookEventArgs = handlerType.GetMethod("Invoke")!.GetParameters()[1].ParameterType;
                    foreach (var field in _r2StatHookEventArgs.GetFields())
                    {
                        _r2StatHookEventArgsFields[field.Name] = field;
                    }

                    foreach (var field in typeof(StatHookEventArgs).GetFields())
                    {
                        _statHookFields[field.Name] = field;
                    }

                    _r2Delegate = Delegate.CreateDelegate(handlerType,
                        typeof(RecalculateStatsAPI).GetMethod(nameof(R2ApiHandler),
                            BindingFlags.Static | BindingFlags.Public)!);

                    _recalcStatsEvent.AddEventHandler(null, _r2Delegate);
                    _r2Hooked = true;
                }
                else
                {
                    BubbetsItemsPlugin.Log.LogError("R2API RecalcStats event not found.");
                }
            }

            _getStatCoefficients += value;
        }

        remove
        {
            _getStatCoefficients -= value;
            if (_r2Hooked &&
                (_getStatCoefficients == null || _getStatCoefficients.GetInvocationList().Length == 0) &&
                Chainloader.PluginInfos.TryGetValue(BubbetsItemsPlugin.RecalcStatsGuid, out var pluginInfo))
            {
                _r2Hooked = false;
                _recalcStatsEvent?.RemoveEventHandler(null, _r2Delegate);
            }
        }
    }

    // ReSharper disable once InconsistentNaming
    private static event StatHookEventHandler? _getStatCoefficients;

    private static StatHookEventArgs? _statMods;
    private static bool _r2Hooked;
    private static Delegate? _r2Delegate;
    private static EventInfo? _recalcStatsEvent;
    private static Type? _r2StatHookEventArgs;
    private static Dictionary<string, FieldInfo> _r2StatHookEventArgsFields = new Dictionary<string, FieldInfo>();
    private static Dictionary<string, FieldInfo> _statHookFields = new Dictionary<string, FieldInfo>();

    private static void GetStatMods(CharacterBody characterBody)
    {
        _statMods = new StatHookEventArgs();
        if (_getStatCoefficients == null) return;
        foreach (var @delegate in _getStatCoefficients.GetInvocationList())
        {
            var @event = (StatHookEventHandler)@delegate;
            try
            {
                @event(characterBody, _statMods);
            }
            catch (Exception e)
            {
                BubbetsItemsPlugin.Log.LogError(
                    $"Exception thrown by : {@event.Method.DeclaringType?.Name}.{@event.Method.Name}:\n{e}");
            }
        }
    }

    private static void FindLocLevelMultiplierIndex(ILCursor c, out int locLevelMultiplierIndex)
    {
        c.Index = 0;
        int levelMultiplierIndex = -1;
        c.TryGotoNext(
            x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.level),
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance)!.GetGetMethod(true)),
            x => x.MatchLdcR4(1),
            x => x.MatchSub(),
            x => x.MatchStloc(out levelMultiplierIndex)
        );
        locLevelMultiplierIndex = levelMultiplierIndex;
        if (locLevelMultiplierIndex < 0)
        {
            BubbetsItemsPlugin.Log.LogError(
                $"{nameof(FindLocLevelMultiplierIndex)} failed! Level-scaled stats will be ignored!");
        }
    }

    [HarmonyILManipulator, HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats))]
    public static void HookRecalculateStats(ILContext il)
    {
        ILCursor c = new ILCursor(il);

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Action<CharacterBody>>(GetStatMods);

        FindLocLevelMultiplierIndex(c, out int locLevelMultiplierIndex);

        void EmitLevelMultiplier() => c.Emit(OpCodes.Ldloc, locLevelMultiplierIndex);

        void EmitFallbackLevelMultiplier() => c.Emit(OpCodes.Ldc_R4, 0f);

        Action emitLevelMultiplier = locLevelMultiplierIndex >= 0 ? EmitLevelMultiplier : EmitFallbackLevelMultiplier;

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Action<CharacterBody>>((body) => SetCustomStats(body));

        void SetCustomStats(CharacterBody body)
        {
            //get stats
            BodyCustomStats = GetCustomStatsFromBody(body);
            if (body.master)
            {
                body.master.luck -= BodyCustomStats.luckFromBody;
            }

            BodyCustomStats.ResetStats();

            if (body.master)
            {
                body.master.luck += _statMods.luckAdd;
                BodyCustomStats.luckFromBody = _statMods.luckAdd;
            }

            BodyCustomStats.barrierDecayFrozen = _statMods.shouldFreezeBarrier;
            BodyCustomStats.barrierDecayRateMult = _statMods.barrierDecayMult;
            if (BodyCustomStats.barrierDecayRateMult < 0)
                BodyCustomStats.barrierDecayRateMult = 0;
            BodyCustomStats.barrierDecayRateAdd = _statMods.barrierDecayAdd;
        }

        //ModifyHealthStat(c, emitLevelMultiplier);
        ModifyShieldStat(c, emitLevelMultiplier);
        ModifyHealthRegenStat(c, emitLevelMultiplier);
        ModifyMovementSpeedStat(c, emitLevelMultiplier);
        //ModifyJumpStat(c, emitLevelMultiplier);
        //ModifyDamageStat(c, emitLevelMultiplier);
        ModifyAttackSpeedStat(c, emitLevelMultiplier);
        //ModifyCritStat(c, emitLevelMultiplier);
        ModifyArmorStat(c, emitLevelMultiplier);
        //ModifyCurseStat(c);
        //ModifyCooldownStat(c);
        //ModifyLevelingStat(c);
        //ModifyJumpCountStat(c);
        ModifyLuckStat(c);
    }

    private static void ModifyShieldStat(ILCursor c, Action emitLevelMultiplier)
    {
        c.Index = 0;

        int locBaseShieldIndex = -1;
        bool ILFound = c.TryGotoNext(
            x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.baseMaxShield)),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.levelMaxShield))
        ) && c.TryGotoNext(x => x.MatchStloc(out locBaseShieldIndex)
        ) && c.TryGotoNext(
            x => x.MatchLdloc(locBaseShieldIndex),
            x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.maxShield),
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance)!.GetSetMethod(true))
        );

        if (ILFound)
        {
            c.Index++;
            c.EmitDelegate<Func<float>>(() => 1 + _statMods!.shieldMultAdd);
            c.Emit(OpCodes.Mul);

            c.GotoPrev(x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.levelMaxShield)));
            c.GotoNext(x => x.MatchStloc(out locBaseShieldIndex));
            emitLevelMultiplier();
            c.EmitDelegate<Func<float, float>>((levelMultiplier) =>
                _statMods!.baseShieldAdd + _statMods.levelShieldAdd * levelMultiplier);
            c.Emit(OpCodes.Add);
        }
        else
        {
            BubbetsItemsPlugin.Log.LogError($"{nameof(ModifyShieldStat)} failed.");
        }
    }

    private static void ModifyHealthRegenStat(ILCursor c, Action emitLevelMultiplier)
    {
        c.Index = 0;

        int locRegenMultIndex = -1;
        int locFinalRegenIndex = -1;
        bool ILFound = c.TryGotoNext(
            x => x.MatchLdloc(out locFinalRegenIndex),
            x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.regen),
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance)!.GetSetMethod(true))
        ) && c.TryGotoPrev(
            x => x.MatchAdd(),
            x => x.MatchLdloc(out locRegenMultIndex),
            x => x.MatchMul(),
            x => x.MatchStloc(out locFinalRegenIndex)
        );

        if (ILFound)
        {
            c.GotoNext(x => x.MatchLdloc(out locRegenMultIndex));
            emitLevelMultiplier();
            c.EmitDelegate<Func<float, float>>((levelMultiplier) =>
                _statMods.baseRegenAdd + _statMods.levelRegenAdd * levelMultiplier);
            c.Emit(OpCodes.Add);

            c.EmitDelegate<Func<float>>(() => _statMods.regenTotalMult);
            c.Emit(OpCodes.Mul);

            c.GotoNext(x => x.MatchMul());
            c.EmitDelegate<Func<float>>(() => _statMods.regenMultAdd);
            c.Emit(OpCodes.Add);
        }
        else
        {
            BubbetsItemsPlugin.Log.LogError($"{nameof(ModifyHealthRegenStat)} failed.");
        }
    }

    private static void ModifyMovementSpeedStat(ILCursor c, Action emitLevelMultiplier)
    {
        c.Index = 0;

        int locBaseSpeedIndex = -1;
        int locSpeedMultIndex = -1;
        int locSpeedDivIndex = -1;
        bool ILFound = c.TryGotoNext(
            x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.baseMoveSpeed)),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.levelMoveSpeed))
        ) && c.TryGotoNext(x => x.MatchStloc(out locBaseSpeedIndex)
        ) && c.TryGotoNext(
            x => x.MatchLdloc(locBaseSpeedIndex),
            x => x.MatchLdloc(out locSpeedMultIndex),
            x => x.MatchLdloc(out locSpeedDivIndex),
            x => x.MatchDiv(),
            x => x.MatchMul(),
            x => x.MatchStloc(locBaseSpeedIndex)
        ) && c.TryGotoNext(MoveType.After,
            x => x.MatchLdloc(out _),
            x => x.MatchOr(),
            x => x.MatchLdloc(out _),
            x => x.MatchOr()
        );

        if (ILFound)
        {
            c.EmitDelegate<Func<bool>>(() => _statMods!.moveSpeedRootCount > 0);
            c.Emit(OpCodes.Or);
            c.GotoPrev(x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.levelMoveSpeed)));
            c.GotoNext(x => x.MatchStloc(locBaseSpeedIndex));
            emitLevelMultiplier();
            c.EmitDelegate<Func<float, float>>((levelMultiplier) =>
                _statMods!.baseMoveSpeedAdd + _statMods.levelMoveSpeedAdd * levelMultiplier);
            c.Emit(OpCodes.Add);

            c.GotoNext(x => x.MatchStloc(locSpeedMultIndex));
            c.EmitDelegate<Func<float>>(() => _statMods!.moveSpeedMultAdd);
            c.Emit(OpCodes.Add);

            while (c.TryGotoNext(MoveType.After,
                       x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.sprintingSpeedMultiplier))))
            {
                c.EmitDelegate<Func<float>>(() => _statMods!.sprintSpeedAdd);
                c.Emit(OpCodes.Add);
            }

            c.GotoPrev(MoveType.After, x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(
                nameof(CharacterBody.isSprinting), BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.Static | BindingFlags.Instance)!.GetGetMethod(true)));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, CharacterBody, bool>>((isSprinting, sender) =>
            {
                return isSprinting && ((sender.sprintingSpeedMultiplier + _statMods!.sprintSpeedAdd) != 0);
            });
            c.GotoNext(x => x.MatchStloc(locSpeedDivIndex));
            c.EmitDelegate<Func<float>>(() => _statMods!.moveSpeedReductionMultAdd);
            c.Emit(OpCodes.Add);
        }
        else
        {
            BubbetsItemsPlugin.Log.LogError($"{nameof(ModifyMovementSpeedStat)} failed.");
        }
    }

    private static void ModifyAttackSpeedStat(ILCursor c, Action emitLevelMultiplier)
    {
        c.Index = 0;

        int locBaseAttackSpeedIndex = -1;
        int locAttackSpeedMultIndex = -1;
        bool ILFound = c.TryGotoNext(
            x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.baseAttackSpeed)),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.levelAttackSpeed))
        ) && c.TryGotoNext(x => x.MatchStloc(out locBaseAttackSpeedIndex)
        ) && c.TryGotoNext(
            x => x.MatchLdloc(locBaseAttackSpeedIndex),
            x => x.MatchLdloc(out locAttackSpeedMultIndex),
            x => x.MatchMul(),
            x => x.MatchStloc(locBaseAttackSpeedIndex)
        );

        if (ILFound)
        {
            c.GotoPrev(x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.baseAttackSpeed)));
            c.GotoNext(x => x.MatchStloc(locBaseAttackSpeedIndex));
            emitLevelMultiplier();
            c.EmitDelegate<Func<float, float>>((levelMultiplier) =>
                _statMods!.baseAttackSpeedAdd + _statMods.levelAttackSpeedAdd * levelMultiplier);
            c.Emit(OpCodes.Add);

            c.GotoNext(x => x.MatchStloc(locAttackSpeedMultIndex));
            c.EmitDelegate<Func<float>>(() => _statMods!.attackSpeedMultAdd);
            c.Emit(OpCodes.Add);


            c.GotoNext(x => x.MatchDiv(), x => x.MatchStloc(locAttackSpeedMultIndex));
            c.EmitDelegate<Func<float, float>>((origSpeedReductionMult) =>
                UnityEngine.Mathf.Max(UnityEngine.Mathf.Epsilon,
                    origSpeedReductionMult + _statMods!.attackSpeedReductionMultAdd));
        }
        else
        {
            BubbetsItemsPlugin.Log.LogError($"{nameof(ModifyAttackSpeedStat)} failed.");
        }
    }

    private static void ModifyArmorStat(ILCursor c, Action emitLevelMultiplier)
    {
        c.Index = 0;

        bool ILFound = c.TryGotoNext(x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.baseArmor))
        ) && c.TryGotoNext(x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.armor),
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance)!.GetSetMethod(true)));

        if (ILFound)
        {
            emitLevelMultiplier();
            c.EmitDelegate<Func<float, float>>((levelMultiplier) =>
                _statMods!.armorAdd + _statMods.levelArmorAdd * levelMultiplier);
            c.Emit(OpCodes.Add);
        }
        else
        {
            BubbetsItemsPlugin.Log.LogError($"{nameof(ModifyArmorStat)} failed.");
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.GetBarrierDecayRate))]
    private static void ModifyBarrierDecayRate(HealthComponent __instance, ref float __result)
    {
        var stats = GetCustomStatsFromBody(__instance.body);
        if (stats == null)
            return;

        if (stats.barrierDecayFrozen <= 0)
        {
            __result += stats.barrierDecayRateAdd;
            __result *= stats.barrierDecayRateMult;
        }

        /*
        if (stats.barrierDecayFrozen > 0)
        {
            __result = stats.barrierDecayRateAdd < 0 ? stats.barrierDecayRateAdd : 0;
            return;
        }
        __result += stats.barrierDecayRateAdd;
        __result *= stats.barrierDecayRateMult;
        */
    }

    private static void ModifyLuckStat(ILCursor c)
    {
        c.Index = 0;

        bool ILFound = c.TryGotoNext(MoveType.Before, x => x.MatchCallOrCallvirt<CharacterMaster>("set_luck"));

        if (ILFound)
        {
            c.EmitDelegate<Func<float>>(() => _statMods!.luckAdd);
            c.Emit(OpCodes.Add);
        }
        else
        {
            BubbetsItemsPlugin.Log.LogError($"{nameof(ModifyLuckStat)} failed.");
        }
    }

    public static void Initialize()
    {
        CharacterBody.onBodyDestroyGlobal += CustomStatsCleanup;
    }

    #region custom stats

    private static CustomStats BodyCustomStats;

    private static Dictionary<CharacterBody, CustomStats> characterCustomStats =
        new Dictionary<CharacterBody, CustomStats>();

    private static void CustomStatsCleanup(CharacterBody body)
    {
        characterCustomStats.Remove(body);
    }

    internal static CustomStats GetCustomStatsFromBody(CharacterBody body)
    {
        if (body == null)
            return null;
        if (!characterCustomStats.TryGetValue(body, out var customStats))
            characterCustomStats[body] = customStats = new CustomStats();
        return customStats;
    }

    public class CustomStats
    {
        public int barrierDecayFrozen = 0;
        public float barrierDecayRateAdd = 0;
        public float barrierDecayRateMult = 1;

        public float luckFromBody = 0;

        internal void ResetStats()
        {
            barrierDecayFrozen = 0;
            barrierDecayRateAdd = 0;
            barrierDecayRateMult = 1;

            luckFromBody = 0;
        }
    }

    #endregion
}