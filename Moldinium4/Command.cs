using System;
using System.Windows.Input;

namespace IronStone.Moldinium
{
    public delegate Boolean DCommandImplementation(Boolean simulate);

    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="System.Windows.Input.ICommand" />
    /// <seealso cref="System.IDisposable" />
    public class Command : ICommand, IDisposable
    {
        DCommandImplementation action;

        SerialWatchSubscription subscriptions = null;

        public Command(DCommandImplementation action)
        {
            this.action = action;
        }

        public Command(Action action)
        {
            this.action = simulate => { if (simulate) return true; action(); return true; };
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(Object parameter)
        {
            return Repository.Instance.EvaluateAndSubscribe("command", ref subscriptions, Evaluate, NotifyChange);
        }

        public void Execute(Object parameter)
        {
            action(false);
        }

        public void Dispose() => subscriptions.Dispose();

        void NotifyChange() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        Boolean Evaluate() => action(true);
    }
}
