using System;
using System.Threading;

namespace GzipStreamExtensions.GZipTest.Threads
{
    public class ThreadState<T>
    {
        private volatile bool isBusy;

        public Thread Thread { get; private set; }
        public bool IsBusy
        {
            get { return isBusy; }
            private set { isBusy = value; }
        }
        public T State { get; private set; }
        public Action<T> Action { get; private set; }

        public ThreadState(Action<T> action, T state)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (state == null)
                throw new ArgumentNullException(nameof(state));

            State = state;
            Action = action;
            Thread = new Thread(Handle);
        }

        public void Start()
        {
            if (IsBusy)
                throw new InvalidOperationException($"The thread {Thread.ManagedThreadId} is not in a valid state.");

            Thread.Start();
        }

        private void Handle()
        {
            if (IsBusy)
                throw new InvalidOperationException($"The thread {Thread.ManagedThreadId} is not in a valid state.");

            IsBusy = true;

            try
            {
                Action(State);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
