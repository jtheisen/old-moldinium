using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows.Input;

namespace IronStone.Moldinium
{
    public delegate Boolean DCommandImplementation(Boolean simulate);

    public class ConcreteCommand : ICommand, IDisposable
    {
        DCommandImplementation action;

        SerialDisposable subscriptions = null;

        public ConcreteCommand(DCommandImplementation action)
        {
            this.action = action;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(Object parameter)
        {
            return Repository.Instance.EvaluateAndSubscribe(ref subscriptions, Evaluate, NotifyChange);
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
