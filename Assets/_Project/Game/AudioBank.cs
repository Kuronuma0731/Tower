using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Tower.Game
{
    /// <summary>
    /// 音效庫。與 SpriteMap 同樣的設計：**遊戲只講事件名，不碰檔名**——換音效改這裡即可。
    /// 檔案在 StreamingAssets/audio/（不進版控，來源見 docs/art-assets.md）。
    /// 非同步載入：載入前呼叫 Play 只是靜音，不會炸也不會卡住遊戲。
    /// </summary>
    public sealed class AudioBank : MonoBehaviour
    {
        public const string Attack = "sfx_attack";
        public const string Crit = "sfx_crit";
        public const string Gold = "sfx_gold";
        public const string Item = "sfx_item";
        public const string Door = "sfx_door";
        public const string Blocked = "sfx_blocked";
        public const string Stairs = "sfx_stairs";
        public const string Talk = "sfx_talk";
        public const string BgmFloor = "bgm_floor";

        private static readonly string[] Sfx =
        {
            Attack, Crit, Gold, Item, Door, Blocked, Stairs, Talk,
        };

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _sfxSource;
        private AudioSource _bgmSource;

        public static AudioBank Create(Transform parent)
        {
            var go = new GameObject("audio");
            go.transform.SetParent(parent);
            var bank = go.AddComponent<AudioBank>();
            bank._sfxSource = go.AddComponent<AudioSource>();
            bank._sfxSource.playOnAwake = false;
            bank._bgmSource = go.AddComponent<AudioSource>();
            bank._bgmSource.playOnAwake = false;
            bank._bgmSource.loop = true;
            bank._bgmSource.volume = 0.35f; // BGM 退到背景，音效才聽得見
            bank.StartCoroutine(bank.LoadAll());
            return bank;
        }

        private IEnumerator LoadAll()
        {
            foreach (var name in Sfx)
                yield return Load(name, AudioType.MPEG);

            yield return Load(BgmFloor, AudioType.MPEG);
            if (_clips.TryGetValue(BgmFloor, out var bgm))
            {
                _bgmSource.clip = bgm;
                _bgmSource.Play();
            }
        }

        private IEnumerator Load(string name, AudioType type)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "audio", name + ".mp3");
            if (!File.Exists(path)) yield break;

            using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Audio] 載入失敗 {name}: {req.error}");
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip != null) _clips[name] = clip;
        }

        /// <summary>播一次音效。未載入完成則靜默略過。</summary>
        public void Play(string id, float volume = 1f)
        {
            if (_sfxSource != null && _clips.TryGetValue(id, out var clip))
                _sfxSource.PlayOneShot(clip, volume);
        }
    }
}
