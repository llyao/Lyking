namespace Lyking.GameFramework.Client.UI
{
    /// <summary>
    /// 窗口层级：数值越大越靠近玩家（渲染与焦点优先级越高）。
    /// 新增层级用显式数值插入（如 15），不要连续重排既有值。
    /// </summary>
    public enum UILayer
    {
        Background = 0,
        Main = 10,
        Popup = 20,
        Guide = 30,
        Loading = 40,
        Toast = 50,
    }
}
