using DotVVM.Framework.Binding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;
using System.Collections;
using DotVVM.Framework.Runtime.Compilation.JavascriptCompilation;

namespace DotVVM.Framework.Controls
{
    /// <summary>
    /// A multi-purpose grid control with advanced binding and templating options and sorting support.
    /// </summary>
    public class GridView : ItemsControl
    {

        public GridView() : base("table")
        {
            SetValue(Internal.IsNamingContainerProperty, true);

            Columns = new List<GridViewColumn>();
            RowDecorators = new List<Decorator>();
        }

        /// <summary>
        /// Gets or sets a collection of columns that will be placed inside the grid.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        [ControlPropertyBindingDataContextChange("DataSource")]
        [CollectionElementDataContextChange(1)]
        public List<GridViewColumn> Columns
        {
            get { return (List<GridViewColumn>)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }
        public static readonly DotvvmProperty ColumnsProperty =
            DotvvmProperty.Register<List<GridViewColumn>, GridView>(c => c.Columns);

        /// <summary>
        /// Gets or sets a list of decorators that will be applied on each row.
        /// </summary>
        [MarkupOptions(AllowBinding = false, MappingMode = MappingMode.InnerElement)]
        [ControlPropertyBindingDataContextChange("DataSource")]
        [CollectionElementDataContextChange(1)]
        public List<Decorator> RowDecorators
        {
            get { return (List<Decorator>)GetValue(RowDecoratorsProperty); }
            set { SetValue(RowDecoratorsProperty, value); }
        }
        public static readonly DotvvmProperty RowDecoratorsProperty =
            DotvvmProperty.Register<List<Decorator>, GridView>(c => c.RowDecorators);


        /// <summary>
        /// Gets or sets the command that will be triggered when the user changed the sort order.
        /// </summary>
        [MarkupOptions(AllowHardCodedValue = false)]
        public Action<string> SortChanged
        {
            get { return (Action<string>)GetValue(SortChangedProperty); }
            set { SetValue(SortChangedProperty, value); }
        }
        public static readonly DotvvmProperty SortChangedProperty =
            DotvvmProperty.Register<Action<string>, GridView>(c => c.SortChanged, null);


        protected internal override void OnLoad(IDotvvmRequestContext context)
        {
            EnsureControlHasId();

            DataBind(context);
            base.OnLoad(context);
        }

        protected internal override void OnPreRender(IDotvvmRequestContext context)
        {
            DataBind(context);     // TODO: support for observable collection
            base.OnPreRender(context);
        }


        private void DataBind(IDotvvmRequestContext context)
        {
            Children.Clear();

            var dataSourceBinding = GetDataSourceBinding();
            var dataSource = DataSource;

            Action<string> sortCommand = null;
            if (dataSource is IGridViewDataSet)
            {
                sortCommand = ((IGridViewDataSet)dataSource).SetSortExpression;
            }
            else
            {
                sortCommand = SortChanged;
            }

            var index = 0;
            if (dataSource != null)
            {
                // create header row
                CreateHeaderRow(context, sortCommand);
                var items = GetIEnumerableFromDataSource(dataSource);
                var javascriptDataSourceExpression = dataSourceBinding.GetKnockoutBindingExpression();

                foreach (var item in items)
                {
                    // create row
                    var placeholder = new DataItemContainer { DataItemIndex = index };
                    placeholder.SetBinding(DataContextProperty, GetItemBinding((IList)items, javascriptDataSourceExpression, index));
                    placeholder.SetValue(Internal.PathFragmentProperty, JavascriptCompilationHelper.AddIndexerToViewModel(GetPathFragmentExpression(), index));
                    placeholder.ID = "i" + index;
                    Children.Add(placeholder);

                    CreateRow(context, placeholder);

                    index++;
                }
            }
        }

        private void CreateHeaderRow(IDotvvmRequestContext context, Action<string> sortCommand)
        {
            var head = new HtmlGenericControl("thead");
            Children.Add(head);

            var headerRow = new HtmlGenericControl("tr");
            // workaroud: header template must have to be one level nested, because it is in the Columns property which nests the dataContext to the item type
            // on server we need null, to be Convertible to Item type and on client the best is empty object, because with will hide the inner content when it is null
            headerRow.SetBinding(DataContextProperty, new ValueBindingExpression(h => null, "{}"));
            head.Children.Add(headerRow);
            foreach (var column in Columns)
            {
                var cell = new HtmlGenericControl("th");
                SetCellAttributes(column, cell, true);
                headerRow.Children.Add(cell);

                column.CreateHeaderControls(context, this, sortCommand, cell);
            }
        }

        private static void SetCellAttributes(GridViewColumn column, HtmlGenericControl cell, bool isHeaderCell)
        {
            if (!string.IsNullOrEmpty(column.Width))
            {
                cell.Attributes["style"] = "width: " + column.Width;
            }

            if (!isHeaderCell)
            {
                var cssClassBinding = column.GetValueBinding(GridViewColumn.CssClassProperty);
                if (cssClassBinding != null)
                {
                    cell.Attributes["class"] = cssClassBinding;
                }
                else if (!string.IsNullOrWhiteSpace(column.CssClass))
                {
                    cell.Attributes["class"] = column.CssClass;
                }
            }
            else
            {
                var cssClassBinding = column.GetValueBinding(GridViewColumn.HeaderCssClassProperty);
                if (cssClassBinding != null)
                {
                    cell.Attributes["class"] = cssClassBinding;
                }
                else if (!string.IsNullOrWhiteSpace(column.HeaderCssClass))
                {
                    cell.Attributes["class"] = column.HeaderCssClass;
                }
            }
        }

        private void CreateRow(IDotvvmRequestContext context, DataItemContainer placeholder)
        {
            var row = new HtmlGenericControl("tr");

            DotvvmControl container = row;
            foreach (var decorator in RowDecorators)
            {
                var decoratorInstance = decorator.Clone();
                decoratorInstance.Children.Add(container);
                container = decoratorInstance;
            }
            placeholder.Children.Add(container);

            // create cells
            foreach (var column in Columns)
            {
                var cell = new HtmlGenericControl("td");
                SetCellAttributes(column, cell, false);
                row.Children.Add(cell);
                column.CreateControls(context, cell);
            }
        }

        protected override void RenderContents(IHtmlWriter writer, RenderContext context)
        {
            if (Children.Count == 0) return;

            // render the header
            Children[0].Render(writer, context);

            // render body
            if (!RenderOnServer)
            {
                writer.AddKnockoutForeachDataBind(GetForeachDataBindJavascriptExpression());
            }
            writer.RenderBeginTag("tbody");

            // render contents
            if (RenderOnServer)
            {
                // render on server
                var index = 0;
                foreach (var child in Children.Skip(1))
                {
                    child.Render(writer, context);
                    index++;
                }
            }
            else
            {
                // render on client
                var placeholder = new DataItemContainer { DataContext = null };
                placeholder.SetValue(Internal.PathFragmentProperty, JavascriptCompilationHelper.AddIndexerToViewModel(GetPathFragmentExpression(), "$index"));
                placeholder.SetValue(Internal.ClientIDFragmentProperty, "'i' + $index()");
                Children.Add(placeholder);

                CreateRow(context.RequestContext, placeholder);

                placeholder.Render(writer, context);
            }

            writer.RenderEndTag();
        }
    }
}
