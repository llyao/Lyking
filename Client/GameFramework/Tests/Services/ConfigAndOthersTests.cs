using Lyking.GameFramework.Client.Services.Audio;
using Lyking.GameFramework.Client.Services.Config;
using Lyking.GameFramework.Client.Services.Localization;
using Lyking.GameFramework.Client.Services.Scene;
using Lyking.GameFramework.Client.Services.Settings;
using Xunit;

namespace Lyking.GameFramework.Client.Tests.Services;

public sealed class ConfigStoreTests
{
    [Fact]
    public void 原始值读取与解析()
    {
        var store = new ConfigStore(new Dictionary<string, string>
        {
            ["game.maxLevel"] = "60",
            ["game.title"] = "黎明",
        });

        Assert.Equal(60, store.Get("game.maxLevel", int.Parse));
        Assert.Equal("黎明", store.GetRaw("game.title"));
        Assert.True(store.TryGet("game.maxLevel", int.Parse, out int level));
        Assert.Equal(60, level);
    }

    [Fact]
    public void 缺失配置抛出且TryGet返回false()
    {
        var store = new ConfigStore();

        Assert.Throws<KeyNotFoundException>(() => store.GetRaw("missing"));
        Assert.False(store.TryGet("missing", int.Parse, out _));
    }

    [Fact]
    public void 热更写入覆盖()
    {
        var store = new ConfigStore(new Dictionary<string, string> { ["a"] = "1" });
        store.SetRaw("a", "2");

        Assert.Equal("2", store.GetRaw("a"));
    }
}

public sealed class LocalizationServiceTests
{
    [Fact]
    public void 当前语言查找与格式化()
    {
        var service = new LocalizationService();
        service.AddTable("zh-Hans", new Dictionary<string, string>
        {
            ["login.welcome"] = "欢迎，{0}！",
        });

        Assert.Equal("欢迎，勇者！", service.Get("login.welcome", "勇者"));
    }

    [Fact]
    public void 回退到默认语言()
    {
        var service = new LocalizationService(defaultLanguage: "zh-Hans");
        service.AddTable("zh-Hans", new Dictionary<string, string> { ["ui.ok"] = "确定" });
        service.AddTable("en", new Dictionary<string, string> { ["ui.cancel"] = "Cancel" });
        service.SetLanguage("en");

        Assert.Equal("确定", service.Get("ui.ok"));
    }

    [Fact]
    public void 全部缺失时返回key并触发事件()
    {
        var service = new LocalizationService();
        service.AddTable("zh-Hans", new Dictionary<string, string>());
        var missing = new List<(string Key, string Language)>();
        service.MissingTranslation += (key, language) => missing.Add((key, language));

        Assert.Equal("no.such.key", service.Get("no.such.key"));
        Assert.Equal(("no.such.key", "zh-Hans"), missing.Single());
    }

    [Fact]
    public void 切换未知语言抛出()
    {
        var service = new LocalizationService();
        Assert.Throws<ArgumentException>(() => service.SetLanguage("fr"));
    }

    [Fact]
    public void 语言切换触发事件且同语言切换不触发()
    {
        var service = new LocalizationService();
        service.AddTable("zh-Hans", new Dictionary<string, string>());
        service.AddTable("en", new Dictionary<string, string>());
        var languages = new List<string>();
        service.LanguageChanged += languages.Add;

        service.SetLanguage("en");
        service.SetLanguage("en");

        Assert.Equal(new[] { "en" }, languages);
        Assert.Equal("en", service.CurrentLanguage);
    }

    [Fact]
    public void 词条表合并覆盖()
    {
        var service = new LocalizationService();
        service.AddTable("zh-Hans", new Dictionary<string, string> { ["a"] = "旧" });
        service.AddTable("zh-Hans", new Dictionary<string, string> { ["a"] = "新", ["b"] = "增" });

        Assert.Equal("新", service.Get("a"));
        Assert.Equal("增", service.Get("b"));
    }
}

public sealed class SceneServiceTests
{
    [Fact]
    public async Task 加载场景触发事件并更新当前场景()
    {
        var service = new SimulationSceneService();
        var events = new List<string>();
        service.SceneLoading += name => events.Add("loading:" + name);
        service.SceneLoaded += name => events.Add("loaded:" + name);

        await service.LoadAsync("main-city");

        Assert.Equal("main-city", service.CurrentScene);
        Assert.Equal(new[] { "loading:main-city", "loaded:main-city" }, events);
    }

    [Fact]
    public async Task 同名场景重复加载幂等()
    {
        var service = new SimulationSceneService();
        await service.LoadAsync("main-city");
        int loaded = 0;
        service.SceneLoaded += _ => loaded++;

        await service.LoadAsync("main-city");

        Assert.Equal(0, loaded);
    }

    [Fact]
    public async Task 空场景名抛出()
    {
        var service = new SimulationSceneService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.LoadAsync(" ").AsTask());
    }
}

public sealed class AudioServiceTests
{
    [Fact]
    public void 播放事件携带通道与合成音量()
    {
        var service = new SimulationAudioService();
        service.SetVolume(AudioChannel.Sfx, 0.5f);
        var played = new List<(AudioChannel Channel, string Clip, float Volume)>();
        service.ClipPlayed += (channel, clip, volume) => played.Add((channel, clip, volume));

        service.Play(AudioChannel.Sfx, "explosion", 0.8f);

        Assert.Equal((AudioChannel.Sfx, "explosion", 0.4f), played.Single());
    }

    [Fact]
    public void 音量越界夹取()
    {
        var service = new SimulationAudioService();
        service.SetVolume(AudioChannel.Music, 2f);
        Assert.Equal(1f, service.GetVolume(AudioChannel.Music));
        service.SetVolume(AudioChannel.Music, -1f);
        Assert.Equal(0f, service.GetVolume(AudioChannel.Music));
    }

    [Fact]
    public void 停止通道与全部停止()
    {
        var service = new SimulationAudioService();
        var stopped = new List<AudioChannel>();
        service.Stopped += stopped.Add;

        service.Stop(AudioChannel.Music);
        Assert.Equal(new[] { AudioChannel.Music }, stopped);

        service.StopAll();
        Assert.Equal(4, stopped.Count);
    }

    [Fact]
    public void 空片段key抛出()
    {
        var service = new SimulationAudioService();
        Assert.Throws<ArgumentException>(() => service.Play(AudioChannel.Music, " "));
    }
}

public sealed class SettingsStoreTests
{
    [Fact]
    public void 读写与类型判断()
    {
        var store = new MemorySettingsStore();
        store.Set("music.volume", 0.7f);

        Assert.True(store.TryGet("music.volume", out float volume));
        Assert.Equal(0.7f, volume);
        Assert.False(store.TryGet("music.volume", out string _));
        Assert.False(store.TryGet("missing", out int _));
    }

    [Fact]
    public void 变更触发事件移除返回是否存在()
    {
        var store = new MemorySettingsStore();
        var changed = new List<string>();
        store.Changed += changed.Add;

        store.Set("a", 1);
        Assert.True(store.Remove("a"));
        Assert.False(store.Remove("a"));

        Assert.Equal(new[] { "a" }, changed);
    }

    [Fact]
    public async Task 保存计数与null值拒绝()
    {
        var store = new MemorySettingsStore();
        store.Set("a", 1);
        store.Set("b", 2);

        await store.SaveAsync();

        Assert.Equal(1, store.SaveCount);
        Assert.Throws<ArgumentNullException>(() => store.Set<string?>("c", null));
    }
}
