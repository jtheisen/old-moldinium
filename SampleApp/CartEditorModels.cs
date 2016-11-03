namespace SampleApp
{
    using IronStone.Moldinium;
    using Newtonsoft.Json;
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Input;
    using static Global;

    public class ProductModel
    {
        [JsonProperty("name")]
        public String Name { get; set; }

        [JsonProperty("price")]
        public Decimal Price { get; set; }
    }

    public class CategoryModel
    {
        [JsonProperty("name")]
        public String Name { get; set; }

        [JsonProperty("products")]
        public ProductModel[] Products { get; set; }
    }

    public abstract class CartLineViewModel : IModel
    {
        public CartViewModel Parent { get; set; }

        public abstract CategoryModel Category { get; set; }

        public abstract ProductModel Product { get; set; }

        public abstract Int32 Quantity { get; set; }

        public virtual Decimal? SubTotal
            => Product?.Price * Quantity;

        public ICommand RemoveLine
            => new Command(() => Parent.Lines.Remove(this));
    }

    public abstract class CartViewModel : IModel
    {
        public CartViewModel()
        {
            Lines.Add(Create<CartLineViewModel>(m => m.Parent = this));
            Categories = App.GetResourceObject<CategoryModel[]>("sampleProductCategories.js");
        }

        public CategoryModel[] Categories { get; }

        public WatchableList<CartLineViewModel> Lines { get; }
            = new WatchableList<CartLineViewModel>();

        public virtual Decimal GrandTotal
            => Lines.Sum(l => l.SubTotal ?? 0m);

        public ICommand AddLine
            => new Command(() => Lines.Add(Create<CartLineViewModel>(m => m.Parent = this)));
    }
}
