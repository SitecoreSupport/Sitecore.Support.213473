using Sitecore.Web.UI.Sheer;
using System.Linq;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Diagnostics;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Configuration;
using Sitecore.Globalization;
using Sitecore.Web.UI.HtmlControls.Data;

namespace Sitecore.Support.Shell.Applications.ContentEditor
{
    public class TreeList : Sitecore.Shell.Applications.ContentEditor.TreeList
    {
        private string _itemID;
        private Listbox _listBox;
        private bool _readOnly;
        private string _source;

        public TreeList() : base()
        {

        }

        protected override string GetHeaderValue(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string str = string.IsNullOrEmpty(this.DisplayFieldName) ? item.DisplayName : item[this.DisplayFieldName];
            return " " + (string.IsNullOrEmpty(str) ? item.DisplayName : str); // Sitecore.Support.213473: return with additional space in front will prevent this item from getting translated by Core DB Dictionary
        }

        private string FormTemplateFilterForDisplay()
        {
            if ((string.IsNullOrEmpty(this.IncludeTemplatesForDisplay) && string.IsNullOrEmpty(this.ExcludeTemplatesForDisplay)) && (string.IsNullOrEmpty(this.IncludeItemsForDisplay) && string.IsNullOrEmpty(this.ExcludeItemsForDisplay)))
            {
                return string.Empty;
            }
            string str = string.Empty;
            string str2 = ("," + this.IncludeTemplatesForDisplay + ",").ToLowerInvariant();
            string str3 = ("," + this.ExcludeTemplatesForDisplay + ",").ToLowerInvariant();
            string str4 = "," + this.IncludeItemsForDisplay + ",";
            string str5 = "," + this.ExcludeItemsForDisplay + ",";
            if (!string.IsNullOrEmpty(this.IncludeTemplatesForDisplay))
            {
                if (!string.IsNullOrEmpty(str))
                {
                    str = str + " and ";
                }
                str = str + string.Format("(contains('{0}', ',' + @@templateid + ',') or contains('{0}', ',' + @@templatekey + ','))", str2);
            }
            if (!string.IsNullOrEmpty(this.ExcludeTemplatesForDisplay))
            {
                if (!string.IsNullOrEmpty(str))
                {
                    str = str + " and ";
                }
                str = str + string.Format("not (contains('{0}', ',' + @@templateid + ',') or contains('{0}', ',' + @@templatekey + ','))", str3);
            }
            if (!string.IsNullOrEmpty(this.IncludeItemsForDisplay))
            {
                if (!string.IsNullOrEmpty(str))
                {
                    str = str + " and ";
                }
                str = str + string.Format("(contains('{0}', ',' + @@id + ',') or contains('{0}', ',' + @@key + ','))", str4);
            }
            if (string.IsNullOrEmpty(this.ExcludeItemsForDisplay))
            {
                return str;
            }
            if (!string.IsNullOrEmpty(str))
            {
                str = str + " and ";
            }
            return (str + string.Format("not (contains('{0}', ',' + @@id + ',') or contains('{0}', ',' + @@key + ','))", str5));
        }

        private bool HasExcludeTemplateForSelection(Item item) =>
            ((bool)((item == null) || HasItemTemplate(item, this.ExcludeTemplatesForSelection)));

        private bool HasIncludeTemplateForSelection(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            return (bool)((this.IncludeTemplatesForSelection.Length == 0) || HasItemTemplate(item, this.IncludeTemplatesForSelection));
        }

        private static bool HasItemTemplate(Item item, string templateList)
        {
            Assert.ArgumentNotNull(templateList, "templateList");
            if (item == null)
            {
                return false;
            }
            if (templateList.Length == 0)
            {
                return false;
            }
            string[] strArray = templateList.Split((char[])new char[] { ',' });
            System.Collections.ArrayList list = new System.Collections.ArrayList((int)strArray.Length);
            for (int i = 0; i < strArray.Length; i = (int)(i + 1))
            {
                list.Add(strArray[i].Trim().ToLowerInvariant());
            }
            return list.Contains(item.TemplateName.Trim().ToLowerInvariant());
        }

        private bool IsDeniedMultipleSelection(Item item, Listbox listbox)
        {
            Assert.ArgumentNotNull(listbox, "listbox");
            if (item == null)
            {
                return true;
            }
            if (!this.AllowMultipleSelection)
            {
                foreach (ListItem item2 in listbox.Controls)
                {
                    string[] strArray = item2.Value.Split((char[])new char[] { '|' });
                    if ((strArray.Length >= 2) && (strArray[1] == item.ID.ToString()))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        private void RestoreState()
        {
            string[] strArray = this.Value.Split((char[])new char[] { '|' });
            if (strArray.Length > 0)
            {
                Database contentDatabase = Sitecore.Context.ContentDatabase;
                if (!string.IsNullOrEmpty(this.DatabaseName))
                {
                    contentDatabase = Factory.GetDatabase(this.DatabaseName);
                }
                for (int i = 0; i < strArray.Length; i = (int)(i + 1))
                {
                    string path = strArray[i];
                    if (!string.IsNullOrEmpty(path))
                    {
                        ListItem item = new ListItem();
                        item.ID = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("I");
                        this._listBox.Controls.Add(item);
                        item.Value = item.ID + "|" + path;
                        Item item2 = contentDatabase.GetItem(path, Language.Parse(this.ItemLanguage));

                        if (item2 != null)
                        {
                            item.Header = this.GetHeaderValue(item2);
                        }
                        else
                        {
                            item.Header = path + ' ' + Translate.Text("[Item not found]");
                        }
                    }
                }
                SheerResponse.Refresh(this._listBox);
            }
        }

        private void SetProperties()
        {
            string source = StringUtil.GetString((string[])new string[] { this.Source });
            if (source.StartsWith("query:"))
            {
                if ((Sitecore.Context.ContentDatabase != null) && (this.ItemID != null))
                {
                    Item current = Sitecore.Context.ContentDatabase.GetItem(this.ItemID);
                    if (current != null)
                    {
                        Item item2 = null;
                        try
                        {
                            item2 = LookupSources.GetItems(current, source).FirstOrDefault<Item>();
                        }
                        catch (System.Exception exception)
                        {
                            Log.Error("Treelist field failed to execute query.", exception, this);
                        }
                        if (item2 != null)
                        {
                            this.DataSource = item2.Paths.FullPath;
                        }
                    }
                }
            }
            else if (Sitecore.Data.ID.IsID(source))
            {
                this.DataSource = this.Source;
            }
            else if ((this.Source != null) && !source.Trim().StartsWith("/", System.StringComparison.OrdinalIgnoreCase))
            {
                this.ExcludeTemplatesForSelection = StringUtil.ExtractParameter("ExcludeTemplatesForSelection", this.Source).Trim();
                this.IncludeTemplatesForSelection = StringUtil.ExtractParameter("IncludeTemplatesForSelection", this.Source).Trim();
                this.IncludeTemplatesForDisplay = StringUtil.ExtractParameter("IncludeTemplatesForDisplay", this.Source).Trim();
                this.ExcludeTemplatesForDisplay = StringUtil.ExtractParameter("ExcludeTemplatesForDisplay", this.Source).Trim();
                this.ExcludeItemsForDisplay = StringUtil.ExtractParameter("ExcludeItemsForDisplay", this.Source).Trim();
                this.IncludeItemsForDisplay = StringUtil.ExtractParameter("IncludeItemsForDisplay", this.Source).Trim();
                string str2 = StringUtil.ExtractParameter("AllowMultipleSelection", this.Source).Trim().ToLowerInvariant();
                this.AllowMultipleSelection = (bool)(string.Compare(str2, "yes", System.StringComparison.InvariantCultureIgnoreCase) == 0);
                this.DataSource = StringUtil.ExtractParameter("DataSource", this.Source).Trim().ToLowerInvariant();
                this.DatabaseName = StringUtil.ExtractParameter("databasename", this.Source).Trim().ToLowerInvariant();
            }
            else
            {
                this.DataSource = this.Source;
            }
        }
    }
}