using System;
using System.Runtime.CompilerServices;

namespace HotUpdatePacker.Runtime
{
    /// <summary>
    /// 自定义异步等待器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncAwaiter<T> : INotifyCompletion
    {
        private T result;
        private Action continuation;

        public bool IsCompleted { get; private set; }

        public AsyncAwaiter<T> GetAwaiter() => this;

        public void Reset()
        {
            result = default;
            IsCompleted = false;
            continuation = null;
        }

        public void SetResult(T value)
        {
            result = value;
            IsCompleted = true;
            continuation?.Invoke();
        }

        public T GetResult()
        {
            return result;
        }

        public void OnCompleted(Action continuation)
        {
            this.continuation = continuation;
        }
    }
}