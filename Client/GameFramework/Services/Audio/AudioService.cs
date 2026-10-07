using System;

namespace Lyking.GameFramework.Client.Services.Audio
{
    /// <summary>音频通道。</summary>
    public enum AudioChannel
    {
        Music = 0,
        Sfx = 1,
        Voice = 2,
    }

    /// <summary>
    /// 音频播放抽象：按通道播放/停止与音量控制。
    /// 模拟实现只维护音量与播放记录；真实播放（AudioSource、混音器分组）由 Unity 适配层实现。
    /// </summary>
    public interface IAudioService
    {
        /// <summary>播放一段音频；音量 = 通道音量 × 片段音量。</summary>
        void Play(AudioChannel channel, string clipKey, float volume = 1f);

        /// <summary>停止通道当前播放（Sfx 通道停止全部音效）。</summary>
        void Stop(AudioChannel channel);

        void StopAll();

        /// <summary>设置通道音量，范围 [0,1]（越界夹取）。</summary>
        void SetVolume(AudioChannel channel, float volume);

        float GetVolume(AudioChannel channel);

        /// <summary>片段开始播放事件：(通道, 片段 key, 实际音量)。</summary>
        event Action<AudioChannel, string, float>? ClipPlayed;

        /// <summary>通道停止事件。</summary>
        event Action<AudioChannel>? Stopped;
    }

    /// <summary>IAudioService 模拟实现：记录音量与播放事件，不发声。</summary>
    public sealed class SimulationAudioService : IAudioService
    {
        private readonly float[] _volumes = { 1f, 1f, 1f };

        public event Action<AudioChannel, string, float>? ClipPlayed;

        public event Action<AudioChannel>? Stopped;

        public void Play(AudioChannel channel, string clipKey, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                throw new ArgumentException("片段 key 不能为空。", nameof(clipKey));
            }

            ClipPlayed?.Invoke(channel, clipKey, _volumes[(int)channel] * volume);
        }

        public void Stop(AudioChannel channel)
        {
            Stopped?.Invoke(channel);
        }

        public void StopAll()
        {
            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                Stop(channel);
            }
        }

        public void SetVolume(AudioChannel channel, float volume)
        {
            _volumes[(int)channel] = Math.Clamp(volume, 0f, 1f);
        }

        public float GetVolume(AudioChannel channel) => _volumes[(int)channel];
    }
}
