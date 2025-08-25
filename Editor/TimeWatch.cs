using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace HotUpdatePacker.Editor
{
    public static class TimeWatch
    {
        private static Stopwatch _stopwatch = Stopwatch.StartNew();
        private static long timeCost = 0;

        public static string ConvertToHMSWithUnits(long milliseconds)
        {
            // 计算各时间单位
            var ts = TimeSpan.FromMilliseconds(milliseconds);
            var hours = ts.Hours + (ts.Days * 24);
            var minutes = ts.Minutes;
            var seconds = ts.Seconds;

            // 带单位格式化
            return hours > 0
                ? $"{hours}小时{minutes}分{seconds}秒"
                : minutes > 0
                    ? $"{minutes}分{seconds}秒"
                    : $"{seconds}秒";
        }

        public static void TimeStart()
        {
            _stopwatch.Reset();
            _stopwatch.Start();
            timeCost = 0;
        }

        public static void TimeStamp(string info)
        {
            var mileSencond = _stopwatch.ElapsedMilliseconds;
            timeCost += mileSencond;
            var time = ConvertToHMSWithUnits(mileSencond);
            var total = ConvertToHMSWithUnits(timeCost);
            Debug.LogWarning($"Time Cost:{info}->{time} Total Cost:{total}");
            _stopwatch.Reset();
            _stopwatch.Start();
        }

        public static void TimeEnd(string info)
        {
            _stopwatch.Stop();
            Debug.LogWarning($"{info}->Total Cost:{timeCost}");
        }
    }
}