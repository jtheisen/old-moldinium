using System;

namespace IronStone.Moldinium
{
    public static partial class LiveListSubscription
    {
        public static ILiveListSubscription Create(Action<Id> handleRefreshRequest, params IDisposable[] disposables)
        {
            return new ActionCompositeLiveListSubscription(handleRefreshRequest, disposables);
        }

        public static ILiveListSubscription Create(ILiveListSubscription nestedSubscription, params IDisposable[] disposables)
        {
            return new SubscriptionCompositeLiveListSubscription(nestedSubscription, disposables);
        }
    }

    internal abstract class AbstractLiveListSubscription : ILiveListSubscription
    {
        public abstract void Dispose();

        public abstract void Refresh(Id id);
    }

    internal abstract class CompositeLiveListSubscription : AbstractLiveListSubscription
    {
        IDisposable[] disposables;

        public CompositeLiveListSubscription(params IDisposable[] disposables)
        {
            this.disposables = disposables;
        }

        public override void Dispose()
        {
            for (int i = 0; i < disposables.Length; ++i)
                InternalExtensions.DisposeProperly(ref disposables[i]);
        }
    }

    internal class ActionCompositeLiveListSubscription : CompositeLiveListSubscription
    {
        Action<Id> handleRefreshRequest;

        public ActionCompositeLiveListSubscription(Action<Id> handleRefreshRequest, params IDisposable[] disposables)
            : base(disposables)
        {
            this.handleRefreshRequest = handleRefreshRequest;
        }

        public override void Refresh(Id id)
        {
            handleRefreshRequest(id);
        }
    }

    internal class SubscriptionCompositeLiveListSubscription : CompositeLiveListSubscription
    {
        ILiveListSubscription nestedSubscription;

        public SubscriptionCompositeLiveListSubscription(ILiveListSubscription nestedSubscription, params IDisposable[] disposables)
            : base(disposables)
        {
            this.nestedSubscription = nestedSubscription;
        }

        public override void Dispose()
        {
            InternalExtensions.DisposeProperly(ref nestedSubscription);
            base.Dispose();
        }

        public override void Refresh(Id id)
        {
            nestedSubscription.Refresh(id);
        }
    }
}
