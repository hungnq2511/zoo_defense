using System.Collections.Generic;
using UnityEngine;

namespace ZooDefense.Game
{
    public enum Sfx { Click, Buy, Merge, Attack, Kill, Leak, Wave, Boss, Send, Win, Lose, Chest, Upgrade, Reward, Error, AttackFire, AttackWater, AttackGrass, AttackLight }

    /// <summary>
    /// Music + sound effects. Real files override everything: put clips in Resources/ZooAudio/
    /// named after the Sfx value in lower case (click.wav, merge.ogg, ...) and music_lobby / music_battle.
    /// Without files, short placeholder blips are synthesized so the game still has feedback.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        static AudioService _instance;
        GameApp _app;
        AudioSource _music, _sfx;
        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly Dictionary<Sfx, float> _lastPlayed = new Dictionary<Sfx, float>();
        string _musicName;

        public void Init(GameApp app)
        {
            _instance = this;
            _app = app;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx)))
            {
                var clip = Resources.Load<AudioClip>("ZooAudio/" + FileName(s));
                _clips[s] = clip != null ? clip : Synth(s);
            }
            ApplyVolumes();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        public static void ApplyVolumes()
        {
            if (_instance == null || _instance._app == null || _instance._app.Profile == null) return;
            _instance._music.volume = _instance._app.Profile.musicVolume * 0.6f;
            _instance._sfx.volume = _instance._app.Profile.sfxVolume;
        }

        /// <summary>File name in Resources/ZooAudio (without extension).</summary>
        public static string FileName(Sfx s)
        {
            switch (s)
            {
                case Sfx.AttackFire: return "attack_fire";
                case Sfx.AttackWater: return "attack_water";
                case Sfx.AttackGrass: return "attack_grass";
                case Sfx.AttackLight: return "attack_light";
                default: return s.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Attack sound for an element (falls back to the generic attack blip).</summary>
        public static void PlayAttack(ZooDefense.Core.Element e, float volume)
        {
            switch (e)
            {
                case ZooDefense.Core.Element.Fire: Play(Sfx.AttackFire, volume); break;
                case ZooDefense.Core.Element.Water: Play(Sfx.AttackWater, volume); break;
                case ZooDefense.Core.Element.Grass: Play(Sfx.AttackGrass, volume); break;
                case ZooDefense.Core.Element.Light: Play(Sfx.AttackLight, volume); break;
                default: Play(Sfx.Attack, volume); break;
            }
        }

        public static void Play(Sfx s, float volume = 1f)
        {
            if (_instance == null) return;
            // Rapid-fire sounds (attacks, kills) are throttled so a big fight does not become noise.
            bool attack = s == Sfx.Attack || s == Sfx.AttackFire || s == Sfx.AttackWater || s == Sfx.AttackGrass || s == Sfx.AttackLight;
            float minGap = attack ? 0.09f : (s == Sfx.Kill ? 0.05f : 0f);
            float last;
            if (minGap > 0f && _instance._lastPlayed.TryGetValue(s, out last) && Time.unscaledTime - last < minGap) return;
            _instance._lastPlayed[s] = Time.unscaledTime;
            AudioClip clip;
            if (_instance._clips.TryGetValue(s, out clip) && clip != null) _instance._sfx.PlayOneShot(clip, volume);
        }

        /// <summary>Plays Resources/ZooAudio/{name} in a loop; silent when the file is missing.</summary>
        public static void PlayMusic(string name)
        {
            if (_instance == null || _instance._musicName == name) return;
            _instance._musicName = name;
            var clip = Resources.Load<AudioClip>("ZooAudio/" + name);
            _instance._music.Stop();
            if (clip == null) return;
            _instance._music.clip = clip;
            _instance._music.Play();
        }

        // ------------------------------------------------------------------ placeholder synthesis

        static AudioClip Synth(Sfx s)
        {
            switch (s)
            {
                case Sfx.Click: return Tones("click", 0.05f, 0.25f, 880f);
                case Sfx.Buy: return Tones("buy", 0.07f, 0.3f, 660f, 880f);
                case Sfx.Merge: return Tones("merge", 0.07f, 0.35f, 523f, 659f, 784f);
                case Sfx.Attack: case Sfx.AttackFire: case Sfx.AttackWater: case Sfx.AttackGrass: case Sfx.AttackLight:
                    return Tones("attack", 0.03f, 0.08f, 300f);
                case Sfx.Kill: return Tones("kill", 0.05f, 0.18f, 990f, 1320f);
                case Sfx.Leak: return Tones("leak", 0.18f, 0.35f, 150f, 110f);
                case Sfx.Wave: return Tones("wave", 0.12f, 0.3f, 784f, 1047f);
                case Sfx.Boss: return Tones("boss", 0.22f, 0.4f, 196f, 147f, 196f);
                case Sfx.Send: return Tones("send", 0.06f, 0.3f, 400f, 600f, 800f);
                case Sfx.Win: return Tones("win", 0.13f, 0.4f, 523f, 659f, 784f, 1047f);
                case Sfx.Lose: return Tones("lose", 0.18f, 0.4f, 392f, 330f, 262f);
                case Sfx.Chest: return Tones("chest", 0.08f, 0.35f, 784f, 988f, 1175f, 1568f);
                case Sfx.Upgrade: return Tones("upgrade", 0.08f, 0.35f, 523f, 784f, 1047f);
                case Sfx.Reward: return Tones("reward", 0.09f, 0.3f, 880f, 1175f);
                default: return Tones("error", 0.12f, 0.3f, 220f, 208f);
            }
        }

        /// <summary>A short sequence of soft square-ish notes with a quick decay.</summary>
        static AudioClip Tones(string name, float noteSeconds, float gain, params float[] freqs)
        {
            const int rate = 22050;
            int perNote = (int)(rate * noteSeconds);
            var data = new float[perNote * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
            {
                float phase = 0f;
                for (int i = 0; i < perNote; i++)
                {
                    float t = (float)i / perNote;
                    float env = Mathf.Min(1f, t * 20f) * (1f - t) * (1f - t);
                    phase += freqs[n] / rate;
                    float sine = Mathf.Sin(phase * 2f * Mathf.PI);
                    float wave = sine * 0.7f + Mathf.Sign(sine) * 0.15f;
                    data[n * perNote + i] = wave * env * gain;
                }
            }
            var clip = AudioClip.Create("synth_" + name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
