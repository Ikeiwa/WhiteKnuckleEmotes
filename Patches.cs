using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using WKLib.API.Input;

namespace WhiteKnuckleEmotes;

public static class Patches
{
    private static bool[] _playingEmote;
    private static Dictionary<string, Dictionary<string, AudioClip>> _emoteSounds = new();
    
    [Serializable]
    public class EmoteData
    {
        public string id;
        public List<HandEmote> emotes;

        [Serializable]
        public class HandEmote
        {
            public string id;
            public string sound;
        }
    }

    [HarmonyPatch(typeof(ENT_Player), "Awake")]
    class Patch_ENT_Player_Awake
    {
        static void Postfix(ENT_Player __instance)
        {
            _playingEmote = new bool[__instance.hands.Length];
        }
    }

    [HarmonyPatch(typeof(CL_CosmeticManager), "CreateHandCosmetics")]
    class Patch_CL_CosmeticManager_CreateHandCosmetics
    {
        static void Postfix(CL_CosmeticManager __instance, string subdir, List<string> jsonList,
            Dictionary<string, Cosmetic_HandItem> ___cosmeticHandDict)
        {
            foreach (string jsonFile in jsonList)
            {
                string json = File.ReadAllText(jsonFile);
                EmoteData cosmeticHandItemData = JsonConvert.DeserializeObject<EmoteData>(json);

                if (!___cosmeticHandDict.TryGetValue(cosmeticHandItemData.id, out Cosmetic_HandItem cosmeticHandItem))
                    continue;

                if (cosmeticHandItem.cosmeticData.emotes == null || cosmeticHandItem.cosmeticData.emotes.Count == 0)
                    continue;

                bool loadLinear = cosmeticHandItem.cosmeticData.palettes is { Count: > 0 };

                for (int emoteIndex = 0; emoteIndex < cosmeticHandItem.cosmeticData.emotes.Count; emoteIndex++)
                {
                    var emote = cosmeticHandItem.cosmeticData.emotes[emoteIndex];
                    var customEmote = cosmeticHandItemData.emotes[emoteIndex];
                    
                    if (string.IsNullOrEmpty(emote.spriteName))
                        continue;

                    var emoteSprite = RuntimeSpriteImporter.LoadSpriteFromFile(
                        Path.Combine(subdir, "Sprites", emote.spriteName + ".png"), linear: loadLinear);
                    emoteSprite.name = emote.spriteName;

                    emote.sprite = emoteSprite;

                    if (!string.IsNullOrEmpty(customEmote.sound))
                    {
                        Plugin.Instance.StartCoroutine(LoadAudioClip(
                            Path.Combine(subdir, "Sounds", customEmote.sound + ".wav"), cosmeticHandItemData.id,
                            customEmote.id));
                    }
                }
            }
        }

        private static IEnumerator LoadAudioClip(string path, string cosmeticId, string emoteId)
        {
            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogError(uwr.error);
                    yield break;
                }

                AudioClip content = DownloadHandlerAudioClip.GetContent(uwr);
                content.name = Path.GetFileNameWithoutExtension(path);
                Plugin.Log.LogInfo("Loaded Clip: " + content.name);

                if (!_emoteSounds.ContainsKey(cosmeticId))
                    _emoteSounds.Add(cosmeticId, new Dictionary<string, AudioClip>());

                if (!_emoteSounds[cosmeticId].ContainsKey(emoteId))
                    _emoteSounds[cosmeticId].Add(emoteId, content);
            }
        }
    }

    [HarmonyPatch(typeof(ENT_Player), "HandAnimation")]
    class Patch_ENT_Player_HandAnimation
    {
        static void Postfix(ENT_Player __instance, ENT_Player.Hand curhand, bool interacting, bool canInteract)
        {
            if (interacting || !canInteract || !curhand.IsFree())
            {
                if (_playingEmote[curhand.id])
                {
                    curhand.GetViewSway().targetOffset = Vector3.zero;
                    _playingEmote[curhand.id] = false;
                }

                return;
            }

            if (curhand.currentCosmetics == null || curhand.currentCosmetics.Count == 0)
                return;

            bool isLeft = curhand.id == 0;
            var keyBinds = isLeft ? Plugin.EmoteKeysLeft : Plugin.EmoteKeysRight;

            foreach (var cosmetic in curhand.currentCosmetics)
            {
                if (cosmetic.cosmeticData.emotes == null || cosmetic.cosmeticData.emotes.Count == 0)
                    continue;

                bool playingEmote = false;
                for (int i = 0; i < Mathf.Min(cosmetic.cosmeticData.emotes.Count, Plugin.MaxEmotes); i++)
                {
                    if (keyBinds[i].Value == KeyCode.None) continue;
                    if (!InputUtility.GetKeyDown(keyBinds[i].Value)) continue;

                    var emote = cosmetic.cosmeticData.emotes[i];

                    curhand.SetSprite(emote.sprite);
                    curhand.GetViewSway().targetOffset =
                        Vector3.Scale(emote.position, curhand.handSprite.transform.localScale);

                    if (!_playingEmote[curhand.id] && _emoteSounds.ContainsKey(cosmetic.cosmeticData.id) &&
                        _emoteSounds[cosmetic.cosmeticData.id].ContainsKey(emote.id))
                    {
                        var clip = _emoteSounds[cosmetic.cosmeticData.id][emote.id];
                        if (clip)
                            AudioManager.PlaySound(clip, curhand.handModel);
                    }

                    _playingEmote[curhand.id] = true;
                    playingEmote = true;
                    break;
                }

                if (!playingEmote)
                {
                    if (_playingEmote[curhand.id])
                    {
                        curhand.GetViewSway().targetOffset = Vector3.zero;
                        _playingEmote[curhand.id] = false;
                    }
                }
            }
        }
    }
}