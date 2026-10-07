namespace Lyking.Wordle.Core
{
    /// <summary>单格判定结果：绿=位置正确，黄=字母存在但位置不对，灰=字母不存在。</summary>
    public enum LetterHint
    {
        /// <summary>字母不在答案中（灰）。</summary>
        Miss,

        /// <summary>字母存在但位置不对（黄）。</summary>
        Present,

        /// <summary>字母与位置均正确（绿）。</summary>
        Correct,
    }
}
