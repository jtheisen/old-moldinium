using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronStone.Moldinium
{
    public static partial class LiveList
    {
        public static ILiveList<TSource> CheckSanity<TSource>(this ILiveList<TSource> source)
        {
            return LiveList.Create<TSource>(onNext =>
            {
                var keys = new HashSet<Id>();

                return source.Subscribe((type, item, id, previousId, nextId) =>
                {
                    switch (type)
                    {
                        case ListEventType.Add:
                            if (!keys.Add(id))
                                throw new Exception("Known id provided at insertion in sanity check.");
                            if (previousId.HasValue && !keys.Contains(previousId.Value))
                                throw new Exception("Unkown previous id provided at insertion in sanity check.");
                            if (nextId.HasValue && !keys.Contains(nextId.Value))
                                throw new Exception("Unkown next id provided at insertion in sanity check.");
                            break;
                        case ListEventType.Remove:
                            if (!keys.Remove(id))
                                throw new Exception("Unknown id provided at removal in sanity check.");
                            if (previousId.HasValue && !keys.Contains(previousId.Value))
                                throw new Exception("Unkown previous id provided at removal in sanity check.");
                            if (nextId.HasValue && !keys.Contains(nextId.Value))
                                throw new Exception("Unkown previous id provided at removal in sanity check.");
                            break;
                        default:
                            throw new Exception("Unkown operation type provided in sanity check.");
                    }

                    onNext(type, item, id, previousId, nextId);
                });
            });
        }
    }
}
