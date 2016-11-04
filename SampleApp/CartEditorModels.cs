/* The Knockout Cart Editor Sample from http://knockoutjs.com/examples/cartEditor.html,
 *  - rewritten for C# with Moldinium.
 */

namespace SampleApp
{
    using IronStone.Moldinium;
    using Newtonsoft.Json;
    using System;
    using System.Linq;
    using System.Windows.Input;
    using static Global;

    /*
     * Two plain C# objects we create from a static
     * json file we stole from the Knockout sample.
     */

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

    /*
     * Two Moldinium models that will have watchable properties.
     */

    public abstract class CartLineViewModel : IModel
    {
        // This property never changes, so it doesn't need to be watchable.
        public CartViewModel Parent { get; set; }

        // This property is two-way-bound and does change.
        public abstract CategoryModel Category { get; set; }

        // So is this.
        public abstract ProductModel Product { get; set; }

        // And this.
        public abstract Int32 Quantity { get; set; }

        // The subtotal is calculated and automatically depends on Product.Price and Quantity.
        public virtual Decimal? SubTotal
            => Product?.Price * Quantity;

        // A simple command which is constantly enabled.
        public ICommand RemoveLine
            => new Command(() => Parent.Lines.Remove(this));
    }

    public abstract class CartViewModel : IModel
    {
        public CartViewModel()
        {
            // Courtesy of Knockout.
            Categories = App.GetResourceObject<CategoryModel[]>("sampleProductCategories.js");

            // We want one line from the start.
            AddLine.Execute(null);
        }

        // This property is loaded from json and never changes.
        public CategoryModel[] Categories { get; }

        // Since the Lines property itself doesn't change, it doesn't need to be watchable. It's
        // the value in the property that changes and needs to be watchable.
        public WatchableList<CartLineViewModel> Lines { get; }
            = new WatchableList<CartLineViewModel>();

        // Thanks to WatchableList<>, this is even correct after a new line gets added. It depends not only
        // on all the subtotals and the property Lines, but also on the contents of the WatchableList
        // that is held in the Lines property. This last thing would not be the case with an ordinary
        // List<> or even an ObservableCollection<>, both of which are not watchable in the Moldinium sense.
        public virtual Decimal GrandTotal
            => Lines.Sum(l => l.SubTotal ?? 0m);

        // Since the actual model types are not the abstract bases defined in this file,
        // but generated dynamically at runtime, we need to construct them in this unusual way.
        public ICommand AddLine
            => new Command(() => Lines.Add(Create<CartLineViewModel>(m => m.Parent = this)));

        // This command's status depends on lines being present.
        public ICommand ClearLines
            => new Command(justCheck => {
                if (justCheck) return Lines.Count > 0; else Lines.Clear(); return true;
            });
    }
}
