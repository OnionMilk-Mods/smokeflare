using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using LethalLib.Modules;

namespace OnionMilk_smokeflare
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		private static Plugin instance;
		private readonly Harmony _harmony = new Harmony("OnionMilk.Smokeflare");

		public static ConfigEntry<bool> cfgEnabled;
		public static ConfigEntry<float> cfgDuration;
		public static ConfigEntry<float> cfgRange;

		public static GameObject[] prefabs;

		public static void Log(string msg)
		{
			instance.Logger.LogInfo($"[{PluginInfo.PLUGIN_GUID}] {msg}");
		}
		private void Awake()
		{
			instance = this;

			cfgRange = Config.Bind(
				"Settings",
				"range",
				4f,
				"Range of flare influence (bees and mines)"
			);
			cfgDuration = Config.Bind(
				"Settings",
				"duration",
				10f,
				"Duration of flare flaming"
			);
			
			cfgEnabled = Config.Bind(
				"General",
				"enabled",
				true,
				"Is mod enabled?"
			);

			if(!cfgEnabled.Value)
				return;

			string assetPath = Path.Combine(GetPath, "smokeflare");
			var bundle = AssetBundle.LoadFromFile(assetPath);
			var itm = bundle.LoadAsset<Item>("Assets/SMOKE_FLARE/SmokeFlareItem.asset");
			var prefab = itm.spawnPrefab;

			prefabs = new GameObject[6];
			for(int i = 0; i < prefabs.Length; ++i)
			{
				prefabs[i] = bundle.LoadAsset<GameObject>($"Assets/SMOKE_FLARE/Smoke{i}.prefab");
				if(prefabs[i] != null)
				{

					var p = prefabs[i].GetComponent<ParticleSystem>();
					var main = p.main;
					main.duration = cfgDuration.Value;

					prefabs[i].AddComponent<SmokeLightFade>();
					NetworkPrefabs.RegisterNetworkPrefab(prefabs[i]);
				}
			}
			
			NetworkPrefabs.RegisterNetworkPrefab(prefab);
			Utilities.FixMixerGroups(prefab);
			Items.RegisterScrap(itm, 10, Levels.LevelTypes.MarchLevel | Levels.LevelTypes.OffenseLevel);
			Items.RegisterScrap(itm, 30, Levels.LevelTypes.VowLevel | Levels.LevelTypes.ExperimentationLevel | Levels.LevelTypes.AssuranceLevel);
			Items.RegisterScrap(itm, 20, Levels.LevelTypes.TitanLevel | Levels.LevelTypes.RendLevel | Levels.LevelTypes.DineLevel);

			TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
			node.clearPreviousText = true;
			node.displayText = "Smoke Flare";
			//Items.RegisterShopItem(itm, null, null, node, 1);
			
			_harmony.PatchAll();
			Log($"Mod is loaded!");
		}

		public static string GetPath
		{
			get
			{
				if(getPath == null)
				{
					var cd = Assembly.GetExecutingAssembly().CodeBase;
					UriBuilder uri = new UriBuilder(cd);
					string path = Uri.UnescapeDataString(uri.Path);
					getPath = Path.GetDirectoryName(path);
				}
				return getPath;
			}
		}
		private static string getPath = null;
	}
}

namespace HealthMetrics.Patches
{
	using System.Collections;
	using System.Linq;
	using GameNetcodeStuff;
	using OnionMilk_smokeflare;
	using UnityEngine;

	[HarmonyPatch(typeof(GrabbableObject))]
	internal class GrabbableObjectPatches
	{
		private static Color[] colors = new Color[]
		{
			Color.red,
			Color.green,
			Color.blue,
			Color.magenta,
			Color.yellow,
			Color.cyan
		};

		[HarmonyPatch("Start")]
		[HarmonyPostfix]
		private static void Start(ref GrabbableObject __instance)
		{
			if(__instance is StunGrenadeItem stun)
			{
				var renderer = __instance.transform.Find("model/RetopoGroup1").GetComponent<MeshRenderer>();
				Material newMat = new(renderer.sharedMaterial);

				int idx = UnityEngine.Random.Range(0, Plugin.prefabs.Length);
				Color clr = colors[idx];
				newMat.SetColor("_BaseColor", clr);

				if(Plugin.prefabs[idx] != null)
					stun.stunGrenadeExplosion = Plugin.prefabs[idx];

				renderer.sharedMaterial = newMat;
			}
		}
	}

	[HarmonyPatch(typeof(StunGrenadeItem))]
	internal class StunGrenadeItemExplosionPatches
	{
		private static FieldInfo thrownByFld = null;

		[HarmonyPatch("ExplodeStunGrenade")]
		[HarmonyPrefix]
		private static void ExplodeStunGrenade(ref StunGrenadeItem __instance)
		{
			if(__instance is StunGrenadeItem stun)
			{
				if(!__instance.hasExploded)
				{
					__instance.hasExploded = true;
					__instance.itemAudio.PlayOneShot(__instance.explodeSFX);
					WalkieTalkie.TransmitOneShotAudio(__instance.itemAudio, __instance.explodeSFX);
					var smoke = GameObject.Instantiate(
						parent: (!__instance.isInElevator) ? RoundManager.Instance.mapPropsContainer.transform : StartOfRound.Instance.elevatorTransform,
						original: __instance.stunGrenadeExplosion,
						position: __instance.transform.position,
						rotation: Quaternion.identity
					);
					smoke.transform.SetParent(__instance.transform);
					smoke.transform.forward = __instance.transform.right;

					if(thrownByFld == null)
						thrownByFld = typeof(StunGrenadeItem)
							.GetField(
								"playerThrownBy",
								BindingFlags.Instance | BindingFlags.NonPublic
							);
					
					PlayerControllerB thrownBy = thrownByFld.GetValue(__instance) as PlayerControllerB;
					if(__instance.DestroyGrenade)
					{
						__instance.DestroyObjectInHand(thrownBy);
					}

					if(!__instance.IsServer)
						return;

					var colliders = Physics.OverlapSphere(
						__instance.transform.position,
						Plugin.cfgRange.Value,
						LayerMask.GetMask("Enemies")
					);
					var bees = colliders.Where(c =>
						c is CapsuleCollider
					).Select(c => c.GetComponentInParent<RedLocustBees>());
					if(bees.Any())
					{
						var exploPos = __instance.transform.position;
						foreach(RedLocustBees bee in bees)
						{
							if(bee == null)
								continue;

							__instance.StartCoroutine(BeeStartle(bee, exploPos));
						}
					}
				}
			}
		}

		private static IEnumerator BeeStartle(
			RedLocustBees bee, Vector3 exploPos
		)
		{
			Plugin.Log("Bee found and startled!");
			var dir = (bee.transform.position - exploPos).normalized;
			bee.SwitchToBehaviourState(2137);
			bee.moveTowardsDestination = true;

			if(Physics.Raycast(
				bee.transform.position + (4 * dir) + (3 * Vector3.up),
				Vector3.down,
				out RaycastHit hit,
				10f
			))
			{
				bee.SetDestinationToPosition(hit.point);
			}
			else
			{
				bee.SetDestinationToPosition(bee.transform.position + (4 * dir));
			}
			
			yield return null;
			float time = Plugin.cfgDuration.Value * 0.6f;
			while(time > 0f)
			{
				yield return null;
				time -= Time.deltaTime;
			}
			bee.SwitchToBehaviourState(0);
			Plugin.Log("Bee returning!");

		}
	}
}