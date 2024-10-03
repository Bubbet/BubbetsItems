using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using MaterialHud;
using MonoMod.Cil;
using RoR2;
using RoR2.EntityLogic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using ZedMod;

namespace BubbetsItems
{
    [HarmonyPatch]
    public static class VoidLunarShopController
    {
        //Only exists on server context
        public static GameObject ShopInstance = null!;
        private static GameObject? _shopPrefab;
        private static ExplicitPickupDropTable _voidCoinTable = null!;
        private static InteractableSpawnCard _voidBarrelSpawncard = null!;
        private static GameObject? _rerollPrefab;
        private static GameObject? _terminalPrefab;
        private static GameObject? _returnPrefab;

        public static GameObject ShopPrefab
        {
            get
            {
                if (_shopPrefab is null)
                {
                    _shopPrefab = BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("LunarVoidShop");
                    var transform = _shopPrefab.transform.Find("Counter/Misc");
                    transform.Find("BazaarBoulder").GetComponent<PrefabChildLoader>().prefabAddress = "RoR2/Base/arena/BBBoulderMediumRound1.prefab";
                    transform.Find("BazaarBoulder (1)").GetComponent<PrefabChildLoader>().prefabAddress = "RoR2/Base/arena/BBBoulderMediumRound1.prefab";
                    transform.Find("BazaarBoulder (2)").GetComponent<PrefabChildLoader>().prefabAddress = "RoR2/Base/arena/BBBoulderMediumRound1.prefab";
                    transform.Find("Infection").GetComponent<PrefabChildLoader>().prefabAddress =
                        "RoR2/Base/bazaar/Bazaar_LunarInfectionLarge.prefab";
                    transform.Find("Infection (1)").GetComponent<PrefabChildLoader>().prefabAddress =
                        "RoR2/Base/bazaar/Bazaar_LunarInfectionLarge.prefab";
                    transform.Find("Infection (2)").GetComponent<PrefabChildLoader>().prefabAddress =
                        "RoR2/Base/bazaar/Bazaar_LunarInfectionLarge.prefab";
                    transform.Find("Infection (3)").GetComponent<PrefabChildLoader>().prefabAddress =
                        "RoR2/Base/bazaar/Bazaar_LunarInfectionLarge.prefab";
                    transform.Find("CrabFoam (3)").GetComponent<PrefabChildLoader>().prefabAddress =
                        "RoR2/Base/arena/Arena_CrabFoam.prefab";
                }

                return _shopPrefab;
            }
        }

        public static GameObject RerollPrefab =>
            _rerollPrefab ??= BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("Reroll");

        public static GameObject TerminalPrefab
        {
            get
            {
                if (_terminalPrefab is null)
                {
                    _terminalPrefab = BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("LunarVoidTerminal");
                    _terminalPrefab.transform.Find("Display/voidPod/voidEffect").GetComponent<SkinnedMeshRenderer>()
                        .material = Addressables.LoadAssetAsync<Material>("RoR2/DLC1/VoidMegaCrab/matVoidCrabMatterOpaque.mat").WaitForCompletion();
                }

                return _terminalPrefab;
            }
        }

        public static GameObject ReturnPrefab
        {
            get
            {
                if (_returnPrefab is null)
                {
                    _returnPrefab = BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("VoidShopWarpReturn");
                    _returnPrefab.GetComponentInChildren<PrefabLoader>().prefabAddress = "RoR2/Base/bazaar/Bazaar_NewtStatue.prefab";
                }

                return _returnPrefab;
            }
        }

        public static void Init()
        {
            Stage.onStageStartGlobal += SceneLoaded;
            RoR2Application.onLoad += MakeTokens;
        }

        public static Dictionary<PlayerCharacterMasterController, float> voidCoinChances = new();

        public static void MakeTokens()
        {
            Language.english.SetStringByToken("BUB_VOIDLUNARSHOP_NAME", "Void Bud");
            Language.english.SetStringByToken("BUB_VOIDLUNARSHOP_CONTEXT", "Open Void Bud");
            Language.english.SetStringByToken("BUB_VOIDLUNARSHOP_REROLL_NAME", "Slab");
            Language.english.SetStringByToken("BUB_VOIDLUNARSHOP_REROLL_CONTEXT", "Refresh Shop");
            Language.english.SetStringByToken("BUB_RETURN_TO_PORTALBLUE", "Return to Blue Portal");

            if (!Chainloader.PluginInfos.ContainsKey("com.Anreol.ReleasedFromTheVoid")) EnableVoidCoins();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(VoidCoinDef), nameof(VoidCoinDef.GrantPickup))]
        public static void ShareOnPickup(VoidCoinDef __instance, ref PickupDef.GrantContext context)
        {
            if (!BubbetsItemsPlugin.Conf.VoidCoinShareOnPickup.Value) return;
            foreach (var master in CharacterMaster.readOnlyInstancesList)
            {
                if (master == context.body.master) continue;
                master.GiveVoidCoins(__instance.coinValue);
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ArenaMissionController), nameof(ArenaMissionController.EndRound))]
        public static void DropCoinInFields(ArenaMissionController __instance)
        {
            if (!Run.instance.IsExpansionEnabled(BubbetsItemsPlugin.BubSotvExpansion)) return;
            if (!BubbetsItemsPlugin.Conf.VoidCoinVoidFields.Value) return;
            var participatingPlayerCount = Run.instance.participatingPlayerCount;
            if (participatingPlayerCount == 0 || !__instance.rewardSpawnPosition) return;

            var num2 = participatingPlayerCount * Mathf.Floor(__instance.currentRound / 9f + 1f);
            var angle = 360f / num2;
            angle += angle * 0.5f;
            var vector = Quaternion.AngleAxis(Random.Range(0, 360), Vector3.up) *
                         (Vector3.up * 40f + Vector3.forward * 5f);
            var rotation = Quaternion.AngleAxis(angle, Vector3.up);
            var l = 0;
            while (l < num2)
            {
                var position = __instance.rewardSpawnPosition.transform.position;
                PickupDropletController.CreatePickupDroplet(
                    PickupCatalog.FindPickupIndex(DLC1Content.MiscPickups.VoidCoin.miscPickupIndex), position, vector);
                l++;
                vector = rotation * vector;
            }
        }

        public static void SceneLoaded(Stage stage)
        {
            if (!NetworkServer.active) return;
            if (stage.sceneDef.nameToken != "MAP_BAZAAR_TITLE") return;
            if (!Run.instance.IsExpansionEnabled(BubbetsItemsPlugin.BubSotvExpansion)) return;

            //NetworkServer.Spawn(GameObject.Instantiate(RerollPrefab));
            //*
            ShopInstance = GameObject.Instantiate(ShopPrefab, new Vector3(284.3365f, -445.1391f, -139.8904f),
                Quaternion.Euler(0, 330, 0));
            NetworkServer.Spawn(ShopInstance);
            var terminals = new List<GameObject>();
            terminals.Add(GameObject.Instantiate(TerminalPrefab));
            terminals[0].transform.SetParent(ShopInstance.transform);
            terminals[0].transform.localPosition = new Vector3(11, 1, 0);
            terminals[0].transform.rotation = Quaternion.Euler(0, 300, 0);
            terminals.Add(GameObject.Instantiate(TerminalPrefab));
            terminals[1].transform.SetParent(ShopInstance.transform);
            terminals[1].transform.localPosition = new Vector3(4.35f, 0.96f, -2.84f);
            terminals[1].transform.rotation = Quaternion.Euler(0, 330, 0);
            terminals.Add(GameObject.Instantiate(TerminalPrefab));
            terminals[2].transform.SetParent(ShopInstance.transform);
            terminals[2].transform.localPosition = new Vector3(-2.478197f, 1.026f, -0.0069f);
            var reroll = GameObject.Instantiate(RerollPrefab);
            reroll.transform.SetParent(ShopInstance.transform);
            reroll.transform.localPosition = new Vector3(18, -2.2f, 20);
            reroll.transform.rotation = Quaternion.Euler(0, 200f, 0);
            var rerollrepeat = reroll.GetComponentInChildren<Repeat>();
            var rerollcounter = reroll.GetComponentInChildren<Counter>();
            for (var i = 0; i < terminals.Count; i++)
            {
                var i1 = i;
                rerollrepeat.repeatedEvent.AddListener(() =>
                    terminals[i1].GetComponentInChildren<DelayedEvent>()
                        .CallDelayedIfActiveAndEnabled(i1 * 0.25f + 0.25f));
                terminals[i1].GetComponent<PurchaseInteraction>().onPurchase.AddListener(_ => rerollcounter.Add(1));
                NetworkServer.Spawn(terminals[i]);
            }

            rerollcounter.threshold = terminals.Count;
            NetworkServer.Spawn(reroll);

            var ret = GameObject.Instantiate(ReturnPrefab);
            ret.transform.SetParent(ShopInstance.transform);
            ret.transform.localPosition = new Vector3(-12.0164f, 1.7473f, 11.9221f);
            ret.transform.rotation = Quaternion.Euler(0.58f, 30.0001f, 0.58f);
            NetworkServer.Spawn(ret);

            /*NetworkServer.Spawn(ShopInstance);
            foreach (var identity in ShopInstance.GetComponentsInChildren<NetworkIdentity>().Where(x => x != ShopInstance.GetComponent<NetworkIdentity>()))
            {
                identity.Reset();
                identity.OnStartServer(false);
                identity.RebuildObservers(true);
            }*/
            /*/
            var scenePaths = BubbetsItemsPlugin.AssetBundle.GetAllScenePaths();
            SceneManager.LoadScene(scenePaths[0], LoadSceneMode.Additive);
            //*/
        }

        public static void EnableVoidCoins()
        {
            if (BubbetsItemsPlugin.Conf.VoidCoinBarrelDrop.Value)
            {
                _voidCoinTable = Addressables
                    .LoadAssetAsync<ExplicitPickupDropTable>("RoR2/DLC1/Common/DropTables/dtVoidCoin.asset")
                    .WaitForCompletion();
                _voidBarrelSpawncard = Addressables
                    .LoadAssetAsync<InteractableSpawnCard>("RoR2/DLC1/VoidCoinBarrel/iscVoidCoinBarrel.asset")
                    .WaitForCompletion();
                _voidBarrelSpawncard.prefab.GetComponent<ModelLocator>().gameObject.AddComponent<ChestBehavior>()
                    .dropTable = _voidCoinTable;
                _voidBarrelSpawncard.directorCreditCost = 7;
            }

            Run.onRunStartGlobal += _ => { voidCoinChances.Clear(); };
            GlobalEventManager.onCharacterDeathGlobal += delegate(DamageReport damageReport)
            {
                if (!damageReport.victimBody.bodyFlags.HasFlag(CharacterBody.BodyFlags.Void) &&
                    !damageReport.victimBody.HasBuff(DLC1Content.Buffs.EliteVoid)) return;
                CharacterMaster characterMaster = damageReport.attackerMaster;
                if (!characterMaster) return;
                if (characterMaster.minionOwnership.ownerMaster)
                {
                    characterMaster = characterMaster.minionOwnership.ownerMaster;
                }

                PlayerCharacterMasterController component =
                    characterMaster.GetComponent<PlayerCharacterMasterController>();
                if (!component) return; // Not a player.
                var chance = BubbetsItemsPlugin.Conf.VoidCoinDropChanceStart.Value;

                if (voidCoinChances.TryGetValue(component, out var chanceg))
                    chance = chanceg;
                else
                    voidCoinChances.Add(component, chance);

                if (!component || !Util.CheckRoll(chance)) return;

                PickupDropletController.CreatePickupDroplet(
                    PickupCatalog.FindPickupIndex(DLC1Content.MiscPickups.VoidCoin.miscPickupIndex),
                    damageReport.victim.transform.position, Vector3.up * 10f);
                voidCoinChances[component] = chance * BubbetsItemsPlugin.Conf.VoidCoinDropChanceMult.Value;
            };
        }
    }
}