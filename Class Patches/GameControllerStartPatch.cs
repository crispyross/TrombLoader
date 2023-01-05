using HarmonyLib;
using SimpleJSON;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Permissions;
using TrombLoader.Data;
using TrombLoader.Helpers;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;
using UnityEngine.PostProcessing;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;

namespace TrombLoader.Class_Patches
{
    [HarmonyPatch(typeof(GameController))]
    [HarmonyPatch(nameof(GameController.Start))]
    class GameControllerStartPatch
    {
        // ORIGINAL (decompiled) CODE:
        /*
		string text = "/trackassets/";
		if (!this.freeplay)
		{
			text += GlobalVariables.chosen_track;
		}
		else if (this.freeplay)
		{
			text += "freeplay";
		}
		this.myLoadedAssetBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + text);


        /*
	    304	0426	ldstr	"/trackassets/"
		305	042B	stloc.1
		306	042C	ldarg.0
		307	042D	ldfld	bool GameController::freeplay
		308	0432	brtrue.s	314 (0442) ldarg.0 
		309	0434	ldloc.1
		310	0435	ldsfld	string GlobalVariables::chosen_track
		311	043A	call	string [netstandard]System.String::Concat(string, string)
		312	043F	stloc.1
		313	0440	br.s	321 (0456) ldarg.0 
		314	0442	ldarg.0
		315	0443	ldfld	bool GameController::freeplay
		316	0448	brfalse.s	321 (0456) ldarg.0 
		317	044A	ldloc.1
		318	044B	ldstr	"freeplay"
		319	0450	call	string [netstandard]System.String::Concat(string, string)
		320	0455	stloc.1
		321	0456	ldarg.0
		322	0457	call	string [UnityEngine.CoreModule]UnityEngine.Application::get_streamingAssetsPath()
                    ________^ inject here (since preceded by ldarg.0 anyways)

		323	045C	ldloc.1
		324	045D	call	string [netstandard]System.String::Concat(string, string)
		325	0462	call	class [UnityEngine.AssetBundleModule]UnityEngine.AssetBundle [UnityEngine.AssetBundleModule]UnityEngine.AssetBundle::LoadFromFile(string)
		326	0467	stfld	class [UnityEngine.AssetBundleModule]UnityEngine.AssetBundle GameController::myLoadedAssetBundle
		*/


        // Later:
        /* string[] array = new string[] { "default", "bass", "muted", "eightbit", "club", "fart" };
        423	05AD	ldc.i4.6
        424	05AE	newarr	[netstandard]System.String
        425	05B3	dup
        426	05B4	ldc.i4.0
        427	05B5	ldstr	"default"
        428	05BA	stelem.ref
        429	05BB	dup
        430	05BC	ldc.i4.1
        431	05BD	ldstr	"bass"
        432	05C2	stelem.ref
        433	05C3	dup
        434	05C4	ldc.i4.2
        435	05C5	ldstr	"muted"
        436	05CA	stelem.ref
        437	05CB	dup
        438	05CC	ldc.i4.3
        439	05CD	ldstr	"eightbit"
        440	05D2	stelem.ref
        441	05D3	dup
        442	05D4	ldc.i4.4
        443	05D5	ldstr	"club"
        444	05DA	stelem.ref
        445	05DB	dup
        446	05DC	ldc.i4.5
        447	05DD	ldstr	"fart"
        448	05E2	stelem.ref
        449	05E3	stloc.3
        */


        // Pass in GameController w/ ldarg.0
        delegate void CoolDelegate(GameController __instance);

        static void PatchedLoadAssetBundle(GameController game)
        {
            string baseGameChartPath = "/trackassets/";
            string trackReference = GlobalVariables.chosen_track;
            string customTrackReference = trackReference;
            bool isCustomTrack = false;
            if (!game.freeplay)
            {
                baseGameChartPath += trackReference;
            }
            else if (game.freeplay)
            {
                baseGameChartPath += "freeplay";
            }
            if (!File.Exists(Application.streamingAssetsPath + baseGameChartPath))
            {
                Plugin.LogDebug("Nyx: Cant load asset bundle, must be a custom song, hijacking Ball game!");
                baseGameChartPath = "/trackassets/ballgame";
                trackReference = "ballgame";
                isCustomTrack = true;
            }
            game.myLoadedAssetBundle = AssetBundle.LoadFromFile(Application.streamingAssetsPath + baseGameChartPath);
            if (game.myLoadedAssetBundle == null)
            {
                Plugin.LogDebug("Failed to load AssetBundle!");
                return;
            }
            Plugin.LogDebug("LOADED ASSETBUNDLE: " + Application.streamingAssetsPath + baseGameChartPath);
            if (!game.freeplay)
            {
                AudioSource component = game.myLoadedAssetBundle.LoadAsset<GameObject>("music_" + trackReference).GetComponent<AudioSource>();
                game.musictrack.clip = component.clip;
                game.musictrack.volume = component.volume;
                if (isCustomTrack)
                {
                    Plugin.LogDebug("Nyx: Trying to load ogg from file!");

                    var songPath = Path.Combine(Globals.ChartFolders[customTrackReference], "song.ogg");
                    IEnumerator e = Plugin.Instance.GetAudioClipSync(songPath);

                    //Worst piece of code I have ever seen, but it does the job, I guess
                    //Unity has forced my hand once again
                    //Forces a coroutine to be held manually, basically removing the point of it being a coroutine
                    while (e.MoveNext())
                    {
                        if (e.Current != null)
                        {
                            if (e.Current is string)
                            {
                                Plugin.LogError("Couldnt Load OGG FILE!!");
                            }
                            else
                            {
                                game.musictrack.clip = e.Current as AudioClip;
                            }
                        }
                    }

                    //AudioClip clip = WavUtility.ToAudioClip(Globals.GetCustomSongsPath() + customTrackReference + "/song.wav");
                    //Debug.Log(__instance.musictrack.clip == null);

                }
            }
            game.StartCoroutine(game.loadAssetBundleResources());
            game.bgcontroller.songname = customTrackReference;
            GameObject bgCam;

            // force kill all old puppets
            Globals.Tromboners.Clear();

            if (!game.freeplay)
            {
                bgCam = game.myLoadedAssetBundle.LoadAsset<GameObject>("BGCam_" + trackReference);

                if (isCustomTrack)
                {
                    game.bgcontroller.tickontempo = false;

                    var songPath = Globals.ChartFolders[customTrackReference];
                    if (File.Exists(Path.Combine(songPath, "bg.trombackground")))
                    {
                        var bgCamOld = bgCam;
                        bgCam = AssetBundleHelper.LoadObjectFromAssetBundlePath<GameObject>(Path.Combine(songPath, "bg.trombackground"));
                        UnityEngine.Object.DontDestroyOnLoad(bgCam);

                        var managers = bgCam.GetComponentsInChildren<TromboneEventManager>();
                        foreach (var manager in managers) manager.DeserializeAllGenericEvents();

                        var invoker = bgCam.AddComponent<TromboneEventInvoker>();
                        invoker.InitializeInvoker(game, managers);
                        UnityEngine.Object.DontDestroyOnLoad(invoker);

                        foreach (var videoPlayer in bgCam.GetComponentsInChildren<VideoPlayer>())
                        {
                            if (videoPlayer.url != null && videoPlayer.url.Contains("SERIALIZED_OUTSIDE_BUNDLE"))
                            {
                                var videoName = videoPlayer.url.Replace("SERIALIZED_OUTSIDE_BUNDLE/", "");
                                var clipURL = Path.Combine(songPath, videoName);
                                videoPlayer.url = clipURL;
                            }
                        }

                        // puppet handling
                        foreach (var trombonePlaceholder in bgCam.GetComponentsInChildren<TrombonerPlaceholder>())
                        {
                            int trombonerIndex = trombonePlaceholder.TrombonerType == TrombonerType.DoNotOverride ? game.puppetnum : (int)trombonePlaceholder.TrombonerType;
                            int tromboneSkinIndex = trombonePlaceholder.TromboneSkin == TromboneSkin.DoNotOverride ? game.textureindex : (int)trombonePlaceholder.TromboneSkin;
                            // this specific thing could cause problems later but it's fine for now.
                            trombonePlaceholder.transform.SetParent(bgCam.transform.GetChild(0));

                            foreach (Transform child in trombonePlaceholder.transform)
                            {
                                if (child != null) child.gameObject.SetActive(false);
                            }

                            var sub = new GameObject();
                            sub.transform.SetParent(trombonePlaceholder.transform);
                            sub.transform.SetSiblingIndex(0);
                            sub.transform.localPosition = new Vector3(-0.7f, 0.45f, -1.25f);
                            sub.transform.localEulerAngles = new Vector3(0, 0f, 0f);
                            trombonePlaceholder.transform.Rotate(new Vector3(0f, 19f, 0f));
                            sub.transform.localScale = Vector3.one;

                            //handle male tromboners being slightly shorter
                            if (trombonerIndex > 3 && trombonerIndex != 8) sub.transform.localPosition = new Vector3(-0.7f, 0.35f, -1.25f);

                            var placeHolder2 = new GameObject("TrombonePlaceHolder");
                            placeHolder2.transform.position = trombonePlaceholder.transform.position;
                            placeHolder2.transform.eulerAngles = trombonePlaceholder.transform.eulerAngles;
                            placeHolder2.transform.localScale = trombonePlaceholder.transform.localScale;

                            var tromboneRefs = new GameObject("TromboneTextureRefs");
                            tromboneRefs.transform.SetParent(sub.transform);
                            tromboneRefs.transform.SetSiblingIndex(0);

                            var textureRefs = tromboneRefs.AddComponent<TromboneTextureRefs>();
                            textureRefs.trombmaterials = game.modelparent.transform.GetChild(0).GetComponent<TromboneTextureRefs>().trombmaterials; // a bit of getchild action to mirror game behaviour

                            var trombonerGameObject = Object.Instantiate<GameObject>(game.playermodels[trombonerIndex]);

                            trombonerGameObject.transform.SetParent(placeHolder2.transform);
                            trombonerGameObject.transform.localScale = Vector3.one;

                            var reparent = trombonerGameObject.AddComponent<Reparent>();
                            reparent.instanceID = trombonePlaceholder.InstanceID;

                            Tromboner tromboner = new(trombonerGameObject, trombonePlaceholder);

                            Globals.Tromboners.Add(tromboner);

                            //LeanTween.scaleY(tromboner.gameObject, 0.01f, 0.01f); 
                            tromboner.controller.setTromboneTex(trombonePlaceholder.TromboneSkin == TromboneSkin.DoNotOverride ? game.textureindex : (int)trombonePlaceholder.TromboneSkin);

                            if (GlobalVariables.localsave.cardcollectionstatus[36] > 9)
                            {
                                tromboner.controller.show_rainbow = true;
                            }
                        }

                        // very scuffed and temporary, this could probably be completely done on export

                        // handle foreground objects
                        while (bgCam.transform.GetChild(1).childCount < 8)
                        {
                            var fillerObject = new GameObject("Filler");
                            fillerObject.transform.SetParent(bgCam.transform.GetChild(1));
                        }

                        // handle two background images
                        while (bgCam.transform.GetChild(0).GetComponentsInChildren<SpriteRenderer>().Length < 2)
                        {
                            var fillerObject = new GameObject("Filler");
                            fillerObject.AddComponent<SpriteRenderer>();
                            fillerObject.transform.SetParent(bgCam.transform.GetChild(0));
                        }

                        // move confetti
                        bgCamOld.transform.GetChild(2).SetParent(bgCam.transform);

                        // layering
                        var breathCanvas = game.bottombreath?.transform.parent?.parent?.GetComponent<Canvas>();
                        if (breathCanvas != null) breathCanvas.planeDistance = 2;

                        var champCanvas = game.champcontroller.letters[0]?.transform?.parent?.parent?.parent?.GetComponent<Canvas>();
                        if (champCanvas != null) champCanvas.planeDistance = 2;

                        var gameplayCam = GameObject.Find("GameplayCam")?.GetComponent<Camera>();
                        if (gameplayCam != null) gameplayCam.depth = 99;

                        var removeDefaultLights = bgCam.transform.Find("RemoveDefaultLights");
                        if (removeDefaultLights)
                        {
                            foreach (var light in GameObject.FindObjectsOfType<Light>()) light.enabled = false;
                            removeDefaultLights.gameObject.AddComponent<SceneLightingHelper>();
                        }

                        var addShadows = bgCam.transform.Find("AddShadows");
                        if (addShadows)
                        {
                            QualitySettings.shadows = ShadowQuality.All;
                            QualitySettings.shadowDistance = 100;
                        }
                    }
                }
            }
            else
            {
                bgCam = game.myLoadedAssetBundle.LoadAsset<GameObject>("BGCam_freeplay");
            }

            var copy = Object.Instantiate(bgCam, Vector3.zero, Quaternion.identity, game.bgholder.transform);
            copy.transform.localPosition = Vector3.zero;
            game.bgcontroller.fullbgobject = copy;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var myLabel = generator.DefineLabel();
            var codes = new List<CodeInstruction>(instructions);

            int i = 0;
            int removalStart = 0, removalEnd = 0;

            // Look for first call to get_streamingAssetsPath after /trackassets/
            while (codes[i].opcode != OpCodes.Ldstr || (string)codes[i].operand != "/trackassets/")
                ++i;
            while (codes[i].opcode != OpCodes.Call || (codes[i].operand as MethodInfo)?.Name != "get_streamingAssetsPath")
                ++i;
            // Replace call
            codes[i].operand = ((CoolDelegate)PatchedLoadAssetBundle).Method;
            // Then break to the label
            codes[i + 1] = new CodeInstruction(OpCodes.Br, myLabel);
            removalStart = i + 2;

            // Look for place to put label
            // Look for "newarr" instruction, put it one before that
            while (codes[i].opcode != OpCodes.Newarr)
                ++i;
            codes[i - 1].labels.Add(myLabel);
            removalEnd = i - 2;

            codes.RemoveRange(removalStart, removalEnd - removalStart + 1);
            return codes.AsEnumerable();

        }
    }

	[HarmonyPatch(typeof(GameController))]
	[HarmonyPatch("tryToLoadLevel")] // if possible use nameof() here
	class GameControllerTryToLoadLevelPatch
	{
		//rewrite of the original
		static bool Prefix(GameController __instance, ref string filename, ref bool customtrack)
		{
			string baseChartName;
			if (filename == "EDITOR")
			{
				baseChartName = Application.streamingAssetsPath + "/leveldata/" + __instance.levelnamefield.text + ".tmb";
			}
			else
			{
				baseChartName = Application.streamingAssetsPath + "/leveldata/" + filename + ".tmb";
			}
			if (!File.Exists(baseChartName))
			{
				Plugin.LogError("File doesnt exist!! Try to load custom song, hijacking Ballgame!!!!!");
				baseChartName = Application.streamingAssetsPath + "/leveldata/ballgame.tmb";
				Plugin.LogDebug("Loading Chart:" + baseChartName);
				Plugin.LogDebug("NYX: HERE WE HOOK OUR CUSTOM CHART!!!!!!!!!!!");
				customtrack = true;
			}
			if (File.Exists(baseChartName))
			{
				Plugin.LogDebug("found level");

				BinaryFormatter binaryFormatter = new BinaryFormatter();
				FileStream fileStream = File.Open(baseChartName, FileMode.Open);
				SavedLevel savedLevel = (SavedLevel)binaryFormatter.Deserialize(fileStream);
				fileStream.Close();
				if (!customtrack)
				{
					Plugin.LogDebug("NYX: Printing Ingame Chart!!!!");
					//Plugin.LogDebug(savedLevel.Serialize().ToString());
				}

				CustomSavedLevel customLevel = new CustomSavedLevel(savedLevel);
				if (customtrack)
				{
					string customChartPath = Path.Combine(Globals.ChartFolders[filename], "song.tmb");
					Plugin.LogDebug("Loading Chart from:" + customChartPath); 

					string jsonString = File.ReadAllText(customChartPath);
					var jsonObject = JSON.Parse(jsonString);
					customLevel.Deserialize(jsonObject);
				}
				__instance.bgdata.Clear();
				__instance.bgdata = customLevel.bgdata;
				__instance.leveldata.Clear();
				__instance.leveldata = customLevel.savedleveldata;
				__instance.lyricdata_pos = customLevel.lyricspos;
				__instance.lyricdata_txt = customLevel.lyricstxt;

				//Debug.Log("Nyx: Serialize Custom level to get lyrics");
				//File.WriteAllText(customChartPath+".withlyrics", customLevel.Serialize().ToString());

				if (customLevel.note_color_start == null)
				{
					Plugin.LogDebug("no color data :-(");
				}
				else
				{
					__instance.note_c_start = customLevel.note_color_start;
					__instance.note_c_end = customLevel.note_color_end;
					if (__instance.leveleditor)
					{
						__instance.col_r_1.text = __instance.note_c_start[0].ToString();
						__instance.col_g_1.text = __instance.note_c_start[1].ToString();
						__instance.col_b_1.text = __instance.note_c_start[2].ToString();
						__instance.col_r_2.text = __instance.note_c_end[0].ToString();
						__instance.col_g_2.text = __instance.note_c_end[1].ToString();
						__instance.col_b_2.text = __instance.note_c_end[2].ToString();
						Plugin.LogDebug(__instance.col_r_1.text + __instance.col_g_1.text + __instance.col_b_1.text);
					}
				}
				__instance.levelendpoint = customLevel.endpoint;
				__instance.editorendpostext.text = "end: " + __instance.levelendpoint;
				__instance.tempo = customLevel.tempo;
				__instance.defaultnotelength = customLevel.savednotespacing;
				__instance.defaultnotelength = Mathf.FloorToInt((float)__instance.defaultnotelength * GlobalVariables.gamescrollspeed);
				__instance.beatspermeasure = customLevel.timesig;
				if (__instance.leveleditor)
				{
					__instance.buildAllBGNodes();
				}
				__instance.buildNotes();
				__instance.buildAllLyrics();
				__instance.changeEditorTempo(0);
				__instance.moveTimeline(0);
				__instance.changeTimeSig(0);
				__instance.levelendtime = 60f / __instance.tempo * __instance.levelendpoint;
				
				BGControllerPatch.BGEffect = customLevel.backgroundMovement;

				var modelCam = GameObject.Find("3dModelCamera")?.GetComponent<Camera>();
				if (modelCam != null) modelCam.clearFlags = CameraClearFlags.Depth;

				Plugin.LogDebug("Level end TIME: " + __instance.levelendtime);
				Plugin.LogDebug("Level Loaded!!");

				return false;
			}
			Plugin.LogDebug("No file exists at that filename!");
			return false;
		}
	}
}
