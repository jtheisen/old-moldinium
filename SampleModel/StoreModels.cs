using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IronStone.Moldinium;
using System.Windows.Input;

namespace SampleModel
{
    // TODO: caching ILiveLookup

    public static class Ctx
    {
        public static WorldVm World { get; set; }
        public static ModelFactory Factory { get; set; }
    }

    public class WorldVm
    {
        public LiveList<OrderVm> Orders { get; set; } = new LiveList<OrderVm>();
        public LiveList<PositionVm> Positions { get; set; } = new LiveList<PositionVm>();

        //public ILiveLookup<OrderVm, PositionVm> PositionsByOrder { get { return from p in Positions.AsLiveList() group p by p.Order; } }
    }

    public abstract class OrderVm
    {
        public String OrderNo { get; set; }

        // Problem: this is inefficient in the presence of many orders
        public ILiveList<PositionVm> Positions { get { return from p in Ctx.World.Positions.AsLiveList() orderby p.Index descending where p.Order == this select p; } }

        //public virtual Decimal Sum { get { return Positions.Select(p => p.Total).Sum(); } }
    }

    public abstract class PositionVm
    {
        public OrderVm Order { get; set; }

        public abstract Int32 Index { get; set; }

        public abstract Decimal Total { get; set; }

        public ICommand MoveUpCommand { get { return new ConcreteCommand(MoveUp); } }

        Boolean MoveUp(Boolean simulate)
        {
            var positions = Order.Positions.ToEnumerable().ToList();

            var index = positions.IndexOf(this);

            if (index == 0) return false;

            if (simulate) return true;

            var temp = positions[index - 1].Index;
            positions[index - 1].Index = positions[index].Index;
            positions[index].Index = temp;

            return true;
        }
    }

    public static class SampleDataFactory
    {
        public static void Populate()
        {
            Ctx.Factory = new ModelFactory();
            Ctx.World = new WorldVm();

            var order = Ctx.Factory.Create<OrderVm>();

            Ctx.World.Orders.Add(order);

            for (int i = 0; i < 5; ++i)
            {
                var position = Ctx.Factory.Create<PositionVm>(p => { p.Index = i; p.Order = order; p.Total = 10m + i; });

                Ctx.World.Positions.Add(position);
            }
        }
    }
}
