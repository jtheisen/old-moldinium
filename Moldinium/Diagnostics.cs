using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    public static partial class LiveList
    {
        public static ILiveList<T> CheckSanity<T>(this ILiveList<T> source)
        {
            return LiveList.Create<T>((onNext, downwardsRefreshRequests) =>
            {
                var keys = new HashSet<Key>();

                return source.Subscribe((type, item, key, previousKey) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!keys.Add(key))
                                throw new Exception("Known key provided at insertion in sanity check.");
                            if (previousKey.HasValue && !keys.Contains(previousKey.Value))
                                throw new Exception("Unkown previous key provided at insertion in sanity check.");
                            break;
                        case ListEventType.Remove:
                            if (!keys.Remove(key))
                                throw new Exception("Unknown key provided at removal in sanity check.");
                            if (previousKey.HasValue && !keys.Contains(previousKey.Value))
                                throw new Exception("Unkown previous key provided at removal in sanity check.");
                            break;
                        default:
                            throw new Exception("Unkown operation type provided in sanity check.");
                    }

                    onNext(type, item, key, previousKey);

                }, downwardsRefreshRequests);
            });
        }
    }
}
